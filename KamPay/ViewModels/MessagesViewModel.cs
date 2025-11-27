using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using KamPay.Models;
using KamPay.Services;
using KamPay.Views;
using Firebase.Database;
using Firebase.Database.Query;
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
            IUserProfileService userProfileService) // <-- Parametre eklendi
        {
            _messagingService = messagingService;
            _authService = authService;
            _userProfileService = userProfileService; // <-- Atama yapıldı
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

                StartListeningForConversations();
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ InitializeAsync hatası: {ex.Message}");
                EmptyMessage = "Konuşmalar yüklenemedi.";
                IsLoading = false;
            }
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
            _conversationsSubscription?.Dispose();
            _conversationsSubscription = null;
            _conversationIds.Clear();
            _isInitialized = false;
        }
    }
}