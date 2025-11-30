using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using KamPay.Models;
using KamPay.Services;
using KamPay.Views;
using Firebase.Database;
using Firebase.Database.Query;
using Firebase.Database.Streaming;
using KamPay.Helpers;
using System.Reactive.Linq;
using System.Collections.Generic;

namespace KamPay.ViewModels
{
    public partial class MessagesViewModel : ObservableObject, IDisposable
    {
        private readonly IMessagingService _messagingService;
        private readonly IAuthenticationService _authService;

        // 🔥 EKLENEN: Profil servisi
        private readonly IUserProfileService _userProfileService;
        private readonly IUserStateService _userStateService;

        // 🔥 UltraFastLoad: Snapshot + Realtime loader
        private readonly RealtimeSnapshotService<Conversation> _loader;
        private IDisposable _realtimeListener;

        private IDisposable _conversationsSubscription;
        private readonly FirebaseClient _firebaseClient = new(Constants.FirebaseRealtimeDbUrl);
        private User _currentUser;
        private bool _isInitialized = false;

        // Cache: Conversation ID tracker
        private readonly HashSet<string> _conversationIds = new();

        [ObservableProperty]
        private bool isLoading = true;

        [ObservableProperty]
        private bool isRefreshing = false;

        [ObservableProperty]
        private int unreadCount;

        [ObservableProperty]
        private string emptyMessage = "Henüz mesajınız yok";

        [ObservableProperty]
        private Conversation selectedConversation;

        public ObservableCollection<Conversation> Conversations { get; } = new();

        // 🔥 Constructor Güncellendi
        public MessagesViewModel(
            IMessagingService messagingService,
            IAuthenticationService authService,
            IUserProfileService userProfileService,
            IUserStateService userStateService)
        {
            _messagingService = messagingService;
            _authService = authService;
            _userProfileService = userProfileService;
            _userStateService = userStateService;

            // 🔥 UltraFastLoad: RealtimeSnapshotService başlat
            _loader = new RealtimeSnapshotService<Conversation>(Constants.FirebaseRealtimeDbUrl);

            // Kullanıcı profil değişikliklerini dinle
            _userStateService.UserProfileChanged += OnUserProfileChanged;
        }

        private void OnUserProfileChanged(object sender, User updatedUser)
        {
            if (updatedUser == null) return;

            // 🔥 Kritik: UI'da anlık güncelleme için MainThread'de çalıştırılmalıdır.
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Konuşmalardaki kullanıcı bilgilerini güncelle
                foreach (var conversation in Conversations.Where(c => 
                    c.User1Id == updatedUser.UserId || c.User2Id == updatedUser.UserId))
                {
                    if (conversation.User1Id == updatedUser.UserId)
                    {
                        conversation.User1Name = updatedUser.FullName;
                        conversation.User1PhotoUrl = updatedUser.ProfileImageUrl;
                    }
                    if (conversation.User2Id == updatedUser.UserId)
                    {
                        conversation.User2Name = updatedUser.FullName;
                        conversation.User2PhotoUrl = updatedUser.ProfileImageUrl;
                    }

                    // OtherUser bilgilerini de güncelle
                    if (_currentUser != null)
                    {
                        var otherUserId = conversation.GetOtherUserId(_currentUser.UserId);
                        if (otherUserId == updatedUser.UserId)
                        {
                            conversation.OtherUserName = updatedUser.FullName;
                            conversation.OtherUserPhotoUrl = updatedUser.ProfileImageUrl;
                        }
                    }
                }
            });
        }

        public async Task InitializeAsync()
        {
            if (_isInitialized) return;

            IsLoading = true;
            try
            {
                _currentUser = await _authService.GetCurrentUserAsync();

                if (_currentUser == null)
                {
                    EmptyMessage = "Mesajları görmek için giriş yapmalısınız.";
                    IsLoading = false;
                    return;
                }

                // 🔥 UltraFastLoad pattern ile hızlı yükleme
                await UltraFastLoadAsync();
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ InitializeAsync hatası: {ex.Message}");
                EmptyMessage = "Konuşmalar yüklenemedi.";
                IsLoading = false;
            }
        }

        // 🔥 UltraFastLoad Pattern - Snapshot + Realtime
        public async Task UltraFastLoadAsync()
        {
            try
            {
                // 1️⃣ SNAPSHOT: Anında veri yükle
                var snapshot = await _loader.LoadSnapshotAsync(Constants.ConversationsCollection);

                if (snapshot.Any())
                {
                    // Kullanıcıya ait konuşmaları filtrele ve işle
                    var userConversations = snapshot
                        .Where(kvp => kvp.Value != null &&
                                      kvp.Value.IsActive &&
                                      (kvp.Value.User1Id == _currentUser.UserId || kvp.Value.User2Id == _currentUser.UserId))
                        .Select(kvp =>
                        {
                            var conversation = kvp.Value;
                            conversation.ConversationId = kvp.Key;
                            conversation.OtherUserName = conversation.GetOtherUserName(_currentUser.UserId);
                            conversation.UnreadCount = conversation.GetUnreadCount(_currentUser.UserId);
                            // İlk yüklemede placeholder resim koy
                            conversation.OtherUserPhotoUrl = conversation.GetOtherUserPhotoUrl(_currentUser.UserId) ?? "person_icon.svg";
                            return conversation;
                        })
                        .OrderByDescending(c => c.LastMessageTime)
                        .ToList();

                    // UI'a ekle
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        Conversations.Clear();
                        _conversationIds.Clear();

                        foreach (var conversation in userConversations)
                        {
                            Conversations.Add(conversation);
                            _conversationIds.Add(conversation.ConversationId);
                        }

                        // 🔥 Loading'i hemen kapat - veri gösterildi
                        IsLoading = false;
                        UpdateUnreadCount();
                        EmptyMessage = Conversations.Any() ? string.Empty : "Henüz mesajınız yok.";
                    });

                    // 3️⃣ ARKA PLAN: Profil resimlerini asenkron yükle
                    _ = Task.Run(async () => await LoadProfileImagesInBackgroundAsync(userConversations));
                }
                else
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        IsLoading = false;
                        EmptyMessage = "Henüz mesajınız yok.";
                    });
                }

                // 4️⃣ REALTIME: Canlı güncellemeler için listener başlat
                _realtimeListener = _loader.Listen(Constants.ConversationsCollection, evt =>
                {
                    MainThread.BeginInvokeOnMainThread(() => ApplyRealtimeEvent(evt));
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ UltraFastLoadAsync hatası: {ex.Message}");
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    IsLoading = false;
                    EmptyMessage = "Konuşmalar yüklenemedi.";
                });
            }
        }

        // 🔥 Profil resimlerini arka planda yükle (UI bloke etmez)
        private async Task LoadProfileImagesInBackgroundAsync(List<Conversation> conversations)
        {
            // Use SemaphoreSlim to limit concurrent API calls
            using var semaphore = new SemaphoreSlim(3, 3); // Max 3 concurrent requests
            
            var tasks = conversations.Select(async conversation =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var otherUserId = conversation.GetOtherUserId(_currentUser.UserId);

                    var userProfile = await _userProfileService.GetUserProfileAsync(otherUserId);
                    if (userProfile?.Data != null)
                    {
                        await MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            var convo = Conversations.FirstOrDefault(c => c.ConversationId == conversation.ConversationId);
                            if (convo != null)
                            {
                                convo.OtherUserPhotoUrl = userProfile.Data.ProfileImageUrl ?? "person_icon.svg";
                                if (!string.IsNullOrEmpty(userProfile.Data.Username))
                                {
                                    convo.OtherUserName = userProfile.Data.Username;
                                }
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Profil resmi yüklenemedi: {ex.Message}");
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
        }

        // 🔥 Realtime Event Handler
        private void ApplyRealtimeEvent(FirebaseEvent<Conversation> evt)
        {
            if (evt.Object == null) return;
            if (_currentUser == null) return;

            var conversation = evt.Object;
            conversation.ConversationId = evt.Key;

            // Kullanıcıya ait olmayan konuşmaları atla
            if (conversation.User1Id != _currentUser.UserId && conversation.User2Id != _currentUser.UserId)
                return;

            // Aktif olmayan konuşmaları atla
            if (!conversation.IsActive)
            {
                // Delete olarak işle
                var toRemove = Conversations.FirstOrDefault(c => c.ConversationId == conversation.ConversationId);
                if (toRemove != null)
                {
                    Conversations.Remove(toRemove);
                    _conversationIds.Remove(conversation.ConversationId);
                    UpdateUnreadCount();
                    EmptyMessage = Conversations.Any() ? string.Empty : "Henüz mesajınız yok.";
                }
                return;
            }

            conversation.OtherUserName = conversation.GetOtherUserName(_currentUser.UserId);
            conversation.UnreadCount = conversation.GetUnreadCount(_currentUser.UserId);
            conversation.OtherUserPhotoUrl = conversation.GetOtherUserPhotoUrl(_currentUser.UserId) ?? "person_icon.svg";

            var existingConvo = Conversations.FirstOrDefault(c => c.ConversationId == conversation.ConversationId);

            switch (evt.EventType)
            {
                case FirebaseEventType.InsertOrUpdate:
                    if (existingConvo != null)
                    {
                        var index = Conversations.IndexOf(existingConvo);
                        Conversations[index] = conversation;
                    }
                    else
                    {
                        if (!_conversationIds.Contains(conversation.ConversationId))
                        {
                            Conversations.Add(conversation);
                            _conversationIds.Add(conversation.ConversationId);
                        }
                    }

                    // Arka planda profil resmini yükle
                    _ = Task.Run(async () => await LoadProfileImagesInBackgroundAsync(new List<Conversation> { conversation }));
                    break;

                case FirebaseEventType.Delete:
                    if (existingConvo != null)
                    {
                        Conversations.Remove(existingConvo);
                        _conversationIds.Remove(conversation.ConversationId);
                    }
                    break;
            }

            SortConversationsInPlace();
            UpdateUnreadCount();
            EmptyMessage = Conversations.Any() ? string.Empty : "Henüz mesajınız yok.";
        }

        private void StartListeningForConversations()
        {
            if (_currentUser == null)
            {
                Console.WriteLine("⚠️ _currentUser null, listener başlatılamadı!");
                return;
            }

            Console.WriteLine("🔥 Conversations listener başlatılıyor...");

            _conversationsSubscription = _firebaseClient
                .Child(Constants.ConversationsCollection)
                .AsObservable<Conversation>()
                .Where(e => e.Object != null &&
                           e.Object.IsActive &&
                           (e.Object.User1Id == _currentUser.UserId || e.Object.User2Id == _currentUser.UserId))
                .Buffer(TimeSpan.FromMilliseconds(250))
                .Where(batch => batch.Any())
                .Subscribe(
                    async events => // 🔥 Async yapıldı
                    {
                        // UI thread'e geçmeden önce ağır işleri yapalım mı? 
                        // Burada MainThread içinde async çağıracağız.
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            try
                            {
                                await ProcessConversationBatchAsync(events);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"❌ Conversation batch hatası: {ex.Message}");
                            }
                            finally
                            {
                                IsLoading = false;
                                IsRefreshing = false;
                            }
                        });
                    },
                    error =>
                    {
                        Console.WriteLine($"❌ Firebase listener hatası: {error.Message}");
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            EmptyMessage = "Konuşmalar yüklenirken hata oluştu.";
                            IsLoading = false;
                            IsRefreshing = false;
                        });
                    });
        }

        // ProcessConversationBatchAsync metodunu bulun (satır 143 civarı) ve güncelleyin:

        private async Task ProcessConversationBatchAsync(IList<Firebase.Database.Streaming.FirebaseEvent<Conversation>> events)
        {
            bool hasChanges = false;

            foreach (var e in events)
            {
                var conversation = e.Object;
                conversation.ConversationId = e.Key;

                // Temel bilgileri modelden al
                conversation.OtherUserName = conversation.GetOtherUserName(_currentUser.UserId);
                conversation.UnreadCount = conversation.GetUnreadCount(_currentUser.UserId);

                // 🔥 KRİTİK DÜZELTME: Profil Fotoğrafını Servisten Çek
                try
                {
                    var otherUserId = conversation.GetOtherUserId(_currentUser.UserId);
                    var userProfile = await _userProfileService.GetUserProfileAsync(otherUserId);

                    // Eğer profil varsa resmini al, yoksa varsayılan ikon
                    conversation.OtherUserPhotoUrl = userProfile?.Data?.ProfileImageUrl ?? "person_icon.svg";

                    // İsim güncel değilse onu da güncelle (Opsiyonel)
                    if (!string.IsNullOrEmpty(userProfile?.Data?.Username))
                    {
                        conversation.OtherUserName = userProfile.Data.Username;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Profil yüklenemedi: {ex.Message}");
                    conversation.OtherUserPhotoUrl = "person_icon.svg"; // Hata olursa varsayılan
                }

                var existingConvo = Conversations.FirstOrDefault(c => c.ConversationId == conversation.ConversationId);

                switch (e.EventType)
                {
                    case Firebase.Database.Streaming.FirebaseEventType.InsertOrUpdate:
                        if (existingConvo != null)
                        {
                            var index = Conversations.IndexOf(existingConvo);
                            Conversations[index] = conversation;
                        }
                        else
                        {
                            if (!_conversationIds.Contains(conversation.ConversationId))
                            {
                                Conversations.Add(conversation);
                                _conversationIds.Add(conversation.ConversationId);
                            }
                        }
                        hasChanges = true;
                        break;

                    case Firebase.Database.Streaming.FirebaseEventType.Delete:
                        if (existingConvo != null)
                        {
                            Conversations.Remove(existingConvo);
                            _conversationIds.Remove(conversation.ConversationId);
                            hasChanges = true;
                        }
                        break;
                }
            }

            // 🔥 İLK VERİ GELDİĞİNDE LOADING'İ KAPAT
            if (hasChanges && IsLoading)
            {
                IsLoading = false;
            }

            if (hasChanges)
            {
                SortConversationsInPlace();
                UpdateUnreadCount();
                EmptyMessage = Conversations.Any() ? string.Empty : "Henüz mesajınız yok.";
            }
        }

        private void SortConversationsInPlace()
        {
            var sorted = Conversations.OrderByDescending(c => c.LastMessageTime).ToList();

            for (int i = 0; i < sorted.Count; i++)
            {
                var currentIndex = Conversations.IndexOf(sorted[i]);
                if (currentIndex != i && currentIndex >= 0)
                {
                    Conversations.Move(currentIndex, i);
                }
            }
        }

        private void UpdateUnreadCount()
        {
            UnreadCount = Conversations.Sum(c => c.UnreadCount);
            WeakReferenceMessenger.Default.Send(new UnreadMessageStatusMessage(UnreadCount > 0));
        }

        [RelayCommand]
        private async Task RefreshConversationsAsync()
        {
            if (IsRefreshing) return;

            try
            {
                IsRefreshing = true;

                var result = await _messagingService.GetUserConversationsAsync(_currentUser.UserId);

                if (result.Success && result.Data != null)
                {
                    await UpdateConversationsFromRefreshAsync(result.Data); // Async çağrı

                    UpdateUnreadCount();
                    EmptyMessage = Conversations.Any() ? string.Empty : "Henüz mesajınız yok.";
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert("Hata",
                        result.Message ?? "Konuşmalar yüklenemedi", "Tamam");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Refresh hatası: {ex.Message}");
                await Application.Current.MainPage.DisplayAlert("Hata",
                    "Konuşmalar yenilenirken bir hata oluştu.", "Tamam");
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        // 🔥 Refresh metodu da Async yapıldı ve resim çekme eklendi
        private async Task UpdateConversationsFromRefreshAsync(List<Conversation> freshData)
        {
            for (int i = Conversations.Count - 1; i >= 0; i--)
            {
                if (!freshData.Any(c => c.ConversationId == Conversations[i].ConversationId))
                {
                    _conversationIds.Remove(Conversations[i].ConversationId);
                    Conversations.RemoveAt(i);
                }
            }

            foreach (var freshConvo in freshData)
            {
                freshConvo.OtherUserName = freshConvo.GetOtherUserName(_currentUser.UserId);
                freshConvo.UnreadCount = freshConvo.GetUnreadCount(_currentUser.UserId);

                // 🔥 Profil Resmini Çek
                try
                {
                    var otherUserId = freshConvo.GetOtherUserId(_currentUser.UserId);
                    var userProfile = await _userProfileService.GetUserProfileAsync(otherUserId);
                    freshConvo.OtherUserPhotoUrl = userProfile?.Data?.ProfileImageUrl ?? "person_icon.svg";
                }
                catch
                {
                    freshConvo.OtherUserPhotoUrl = "person_icon.svg";
                }

                var existingIndex = -1;
                for (int i = 0; i < Conversations.Count; i++)
                {
                    if (Conversations[i].ConversationId == freshConvo.ConversationId)
                    {
                        existingIndex = i;
                        break;
                    }
                }

                if (existingIndex >= 0)
                {
                    Conversations[existingIndex] = freshConvo;
                }
                else
                {
                    Conversations.Add(freshConvo);
                    _conversationIds.Add(freshConvo.ConversationId);
                }
            }

            SortConversationsInPlace();
        }

        [RelayCommand]
        private async Task ConversationTappedAsync(Conversation conversation)
        {
            if (conversation == null) return;
            SelectedConversation = null;
            await Shell.Current.GoToAsync($"{nameof(ChatPage)}?conversationId={conversation.ConversationId}");
        }

        [RelayCommand]
        private async Task DeleteConversationAsync(Conversation conversation)
        {
            if (conversation == null) return;

            var confirm = await Application.Current.MainPage.DisplayAlert("Onay", "Bu konuşmayı silmek istediğinize emin misiniz?", "Evet", "Hayır");
            if (!confirm) return;

            try
            {
                var result = await _messagingService.DeleteConversationAsync(conversation.ConversationId, _currentUser.UserId);
                if (!result.Success) await Application.Current.MainPage.DisplayAlert("Hata", result.Message, "Tamam");
            }
            catch (Exception ex) { await Application.Current.MainPage.DisplayAlert("Hata", ex.Message, "Tamam"); }
        }

        public void Dispose()
        {
            Console.WriteLine("🧹 MessagesViewModel dispose ediliyor...");
            _userStateService.UserProfileChanged -= OnUserProfileChanged;
            _conversationsSubscription?.Dispose();
            _conversationsSubscription = null;
            _realtimeListener?.Dispose();
            _realtimeListener = null;
            _conversationIds.Clear();
            _isInitialized = false;
        }
    }
}