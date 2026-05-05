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
using KamPay.Services.Auth;
using KamPay.Services.Messaging;

namespace KamPay.ViewModels
{
    public partial class MessagesViewModel : ObservableObject, IDisposable
    {
        private readonly IMessagingService _messagingService;
        private readonly IAuthenticationService _authService;

        //  : Profil servisi
        private readonly IUserProfileService _userProfileService;
        private readonly IUserStateService _userStateService;

        //  UltraFastLoad: Snapshot + Realtime loader
        private readonly IRealtimeSnapshotService<Conversation> _loader; // âœ… Interface
        private readonly FirebaseClient _firebaseClient;

        private IDisposable? _realtimeListener;
        private IDisposable? _conversationsSubscription;
        private User? _currentUser;
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
        private string emptyMessage = "HenÃ¼z mesajÄ±nÄ±z yok";
        [ObservableProperty]
        private string searchText = string.Empty;

        [ObservableProperty]
        private Conversation? selectedConversation;

        public ObservableRangeCollection<Conversation> Conversations { get; } = new();

        
        public MessagesViewModel(
            IMessagingService messagingService,
            IAuthenticationService authService,
            IUserProfileService userProfileService,
            IUserStateService userStateService,
            IRealtimeSnapshotService<Conversation> realtimeLoader, // âœ… DI ile inject
            FirebaseClient firebaseClient) // âœ… DI ile inject
        {
            _messagingService = messagingService;
            _authService = authService;
            _userProfileService = userProfileService;
            _userStateService = userStateService;
            _loader = realtimeLoader; // âœ… ArtÄ±k DI'den geliyor
            _firebaseClient = firebaseClient;

            // KullanÄ±cÄ± profil deÄŸiÅŸikliklerini dinle
            _userStateService.UserProfileChanged += OnUserProfileChanged;

            WeakReferenceMessenger.Default.Register<UserSessionChangedMessage>(this, (r, m) =>
            {
                if (!m.Value) // Logout
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        Conversations.Clear();
                        _conversationIds.Clear();
                        _isInitialized = false;
                        _currentUser = null;
                        EmptyMessage = "MesajlarÄ± gÃ¶rmek iÃ§in giriÅŸ yapmalÄ±sÄ±nÄ±z.";
                    });
                }
                else // Login
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        _isInitialized = false;
                        _ = InitializeAsync();
                    });
                }
            });

            // Constructor (MessagesViewModel metodu) içine ekleyin:
            Conversations.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(PersonalConversations));
                OnPropertyChanged(nameof(NegotiationConversations));
            };

        }

        private void OnUserProfileChanged(object? sender, User updatedUser)
        {
            if (updatedUser == null) return;

            //  Kritik: UI'da anlÄ±k gÃ¼ncelleme iÃ§in MainThread'de Ã§alÄ±ÅŸtÄ±rÄ±lmalÄ±dÄ±r.
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // KonuÅŸmalardaki kullanÄ±cÄ± bilgilerini gÃ¼ncelle
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

                    // OtherUser bilgilerini de gÃ¼ncelle
                    if (_currentUser != null)
                    {
                        var otherUserId = conversation.GetOtherUserId(_currentUser.UserId);
                        if (otherUserId == updatedUser.UserId)
                        {
                            conversation.OtherUserName = updatedUser.FullName ?? string.Empty;
                            conversation.OtherUserPhotoUrl = updatedUser.ProfileImageUrl ?? string.Empty;
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
                    EmptyMessage = "MesajlarÄ± gÃ¶rmek iÃ§in giriÅŸ yapmalÄ±sÄ±nÄ±z.";
                    IsLoading = false;
                    return;
                }

                //  UltraFastLoad pattern ile hÄ±zlÄ± yÃ¼kleme
                await UltraFastLoadAsync();
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âŒ InitializeAsync hatasÄ±: {ex.Message}");
                EmptyMessage = "KonuÅŸmalar yÃ¼klenemedi.";
                IsLoading = false;
            }
        }

        //  UltraFastLoad Pattern - Snapshot + Realtime
        public async Task UltraFastLoadAsync()
        {
            if (_currentUser == null) return;

            try
            {
                // 1ï¸âƒ£ SNAPSHOT: AnÄ±nda veri yÃ¼kle
                var snapshot = await _loader.LoadSnapshotAsync(Constants.ConversationsCollection);

                if (snapshot.Any())
                {
                    // KullanÄ±cÄ±ya ait konuÅŸmalarÄ± filtrele ve iÅŸle
                    var userConversations = snapshot
                        .Where(kvp => kvp.Value != null &&
                                      kvp.Value.IsActive &&
                                      (kvp.Value.User1Id == _currentUser.UserId || kvp.Value.User2Id == _currentUser.UserId))
                        .Select(kvp =>
                        {
                            var conversation = kvp.Value;
                            conversation.ConversationId = kvp.Key;
                            conversation.OtherUserName = conversation.GetOtherUserName(_currentUser.UserId) ?? string.Empty;
                            conversation.UnreadCount = conversation.GetUnreadCountDb(_currentUser.UserId);
                            // Ä°lk yÃ¼klemede placeholder resim koy
                            conversation.OtherUserPhotoUrl = conversation.GetOtherUserPhotoUrl(_currentUser.UserId) ?? "person_icon.svg";
                            return conversation;
                        })
                        .OrderByDescending(c => c.LastMessageTime)
                        .ToList();

                    // UI'a ekle
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        Conversations.ReplaceRange(userConversations);
                        _conversationIds.Clear();

                        foreach (var conversation in userConversations)
                        {
                            _conversationIds.Add(conversation.ConversationId);
                        }

                        //  Loading'i hemen kapat - veri gÃ¶sterildi
                        IsLoading = false;
                        UpdateUnreadCount();
                        EmptyMessage = Conversations.Any() ? string.Empty : "HenÃ¼z mesajÄ±nÄ±z yok.";
                    });

                    // 3ï¸âƒ£ ARKA PLAN: Profil resimlerini asenkron yÃ¼kle
                    _ = Task.Run(async () => await LoadProfileImagesInBackgroundAsync(userConversations));
                }
                else
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        IsLoading = false;
                        EmptyMessage = "HenÃ¼z mesajÄ±nÄ±z yok.";
                    });
                }

                // 4ï¸âƒ£ REALTIME: CanlÄ± gÃ¼ncellemeler iÃ§in listener baÅŸlat
                _realtimeListener = _loader.Listen(Constants.ConversationsCollection, evt =>
                {
                    MainThread.BeginInvokeOnMainThread(() => ApplyRealtimeEvent(evt));
                });
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âŒ UltraFastLoadAsync hatasÄ±: {ex.Message}");
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    IsLoading = false;
                    EmptyMessage = "KonuÅŸmalar yÃ¼klenemedi.";
                });
            }
        }

        //  Profil resimlerini arka planda yÃ¼kle (UI bloke etmez)
        private async Task LoadProfileImagesInBackgroundAsync(List<Conversation> conversations)
        {
            if (_currentUser == null) return;

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
                                // ✅ FIX: FullName kullan, Username değil
                                convo.OtherUserPhotoUrl = userProfile.Data.ProfileImageUrl ?? "person_icon.svg";
                                convo.OtherUserName = userProfile.Data.FullName ?? string.Empty;
                                
                                // ✅ FAZ 3: Online durumu
                                if (userProfile.Data.LastLoginAt.HasValue)
                                {
                                    // Son 15 dakika içinde giriş yaptıysa "Online" kabul edelim
                                    convo.IsOtherUserOnline = (DateTime.UtcNow - userProfile.Data.LastLoginAt.Value).TotalMinutes < 15;
                                }
                                else
                                {
                                    convo.IsOtherUserOnline = false;
                                }
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    KamPay.Helpers.AppLogger.DebugLog($"âš ï¸ Profil resmi yÃ¼klenemedi: {ex.Message}");
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
        }

        //  Realtime Event Handler
        private void ApplyRealtimeEvent(FirebaseEvent<Conversation> evt)
        {
            if (evt.Object == null) return;
            if (_currentUser == null) return;

            var conversation = evt.Object;
            conversation.ConversationId = evt.Key;

            // KullanÄ±cÄ±ya ait olmayan konuÅŸmalarÄ± atla
            if (conversation.User1Id != _currentUser.UserId && conversation.User2Id != _currentUser.UserId)
                return;

            // Aktif olmayan konuÅŸmalarÄ± atla
            if (!conversation.IsActive)
            {
                // Delete olarak iÅŸle
                var toRemove = Conversations.FirstOrDefault(c => c.ConversationId == conversation.ConversationId);
                if (toRemove != null)
                {
                    Conversations.Remove(toRemove);
                    _conversationIds.Remove(conversation.ConversationId);
                    UpdateUnreadCount();
                    EmptyMessage = Conversations.Any() ? string.Empty : "HenÃ¼z mesajÄ±nÄ±z yok.";
                }
                return;
            }

            conversation.OtherUserName = conversation.GetOtherUserName(_currentUser.UserId) ?? string.Empty;
            conversation.UnreadCount = conversation.GetUnreadCountDb(_currentUser.UserId);
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

                    // Arka planda profil resmini yÃ¼kle
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
            EmptyMessage = Conversations.Any() ? string.Empty : "HenÃ¼z mesajÄ±nÄ±z yok.";
        }

        private void StartListeningForConversations()
        {
            if (_currentUser == null)
            {
                KamPay.Helpers.AppLogger.DebugLog("âš ï¸ _currentUser null, listener baÅŸlatÄ±lamadÄ±!");
                return;
            }

            KamPay.Helpers.AppLogger.DebugLog(" Conversations listener baÅŸlatÄ±lÄ±yor...");

            _conversationsSubscription = _firebaseClient
                .Child(Constants.ConversationsCollection)
                .AsObservable<Conversation>()
                .Where(e => e.Object != null &&
                           e.Object.IsActive &&
                           (e.Object.User1Id == _currentUser.UserId || e.Object.User2Id == _currentUser.UserId))
                .Buffer(TimeSpan.FromMilliseconds(250))
                .Where(batch => batch.Any())
                .Subscribe(
                    async events => //  Async yapÄ±ldÄ±
                    {
                        // UI thread'e geÃ§meden Ã¶nce aÄŸÄ±r iÅŸleri yapalÄ±m mÄ±? 
                        // Burada MainThread iÃ§inde async Ã§aÄŸÄ±racaÄŸÄ±z.
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            try
                            {
                                await ProcessConversationBatchAsync(events);
                            }
                            catch (Exception ex)
                            {
                                KamPay.Helpers.AppLogger.DebugLog($"âŒ Conversation batch hatasÄ±: {ex.Message}");
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
                        KamPay.Helpers.AppLogger.DebugLog($"âŒ Firebase listener hatasÄ±: {error.Message}");
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            EmptyMessage = "KonuÅŸmalar yÃ¼klenirken hata oluÅŸtu.";
                            IsLoading = false;
                            IsRefreshing = false;
                        });
                    });
        }

        

        private async Task ProcessConversationBatchAsync(IList<Firebase.Database.Streaming.FirebaseEvent<Conversation>> events)
        {
            if (_currentUser == null) return;

            bool hasChanges = false;

            foreach (var e in events)
            {
                var conversation = e.Object;
                conversation.ConversationId = e.Key;

                // Temel bilgileri modelden al
                conversation.OtherUserName = conversation.GetOtherUserName(_currentUser.UserId) ?? string.Empty;
                conversation.UnreadCount = conversation.GetUnreadCountDb(_currentUser.UserId);

                //  : Profil FotoÄŸrafÄ±nÄ± Servisten Ã‡ek
                try
                {
                    var otherUserId = conversation.GetOtherUserId(_currentUser.UserId);
                    var userProfile = await _userProfileService.GetUserProfileAsync(otherUserId);

                    // EÄŸer profil varsa resmini al, yoksa varsayÄ±lan ikon
                    conversation.OtherUserPhotoUrl = userProfile?.Data?.ProfileImageUrl ?? "person_icon.svg";

                    // âœ… FIX: FullName kullan, Username deÄŸil
                    if (!string.IsNullOrEmpty(userProfile?.Data?.FullName))
                    {
                        conversation.OtherUserName = userProfile.Data.FullName;
                    }
                }
                catch (Exception ex)
                {
                    KamPay.Helpers.AppLogger.DebugLog($"âš ï¸ Profil yÃ¼klenemedi: {ex.Message}");
                    conversation.OtherUserPhotoUrl = "person_icon.svg"; // Hata olursa varsayÄ±lan
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

            //  Ä°LK VERÄ° GELDÄ°ÄÄ°NDE LOADING'Ä° KAPAT
            if (hasChanges && IsLoading)
            {
                IsLoading = false;
            }

            if (hasChanges)
            {
                SortConversationsInPlace();
                UpdateUnreadCount();
                EmptyMessage = Conversations.Any() ? string.Empty : "HenÃ¼z mesajÄ±nÄ±z yok.";
            }
        }

        private async Task UpdateConversationsFromRefreshAsync(List<Conversation> freshData)
        {
            if (_currentUser == null) return;

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
                freshConvo.OtherUserName = freshConvo.GetOtherUserName(_currentUser.UserId) ?? string.Empty;
                freshConvo.UnreadCount = freshConvo.GetUnreadCountDb(_currentUser.UserId);

                //  âœ… FIX: Profil Resmini ve FullName'i Ã‡ek
                try
                {
                    var otherUserId = freshConvo.GetOtherUserId(_currentUser.UserId);
                    var userProfile = await _userProfileService.GetUserProfileAsync(otherUserId);
                    
                    freshConvo.OtherUserPhotoUrl = userProfile?.Data?.ProfileImageUrl ?? "person_icon.svg";
                    
                    // FullName'i kullan
                    if (!string.IsNullOrEmpty(userProfile?.Data?.FullName))
                    {
                        freshConvo.OtherUserName = userProfile.Data.FullName;
                    }
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
        // Filtrelenmiş conversation listesi
        public IEnumerable<Conversation> PersonalConversations
        {
            get
            {
                var q = Conversations.Where(c => !c.IsNegotiationConversation);

                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    var t = SearchText.ToLower();
                    q = q.Where(c =>
                        (c.OtherUserName != null && c.OtherUserName.ToLower().Contains(t)) ||
                        (c.LastMessage != null && c.LastMessage.ToLower().Contains(t)) ||
                        (c.ProductTitle != null && c.ProductTitle.ToLower().Contains(t))
                    );
                }

                return q;
            }
        }

        public IEnumerable<Conversation> NegotiationConversations
        {
            get
            {
                var q = Conversations.Where(c => c.IsNegotiationConversation);

                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    var t = SearchText.ToLower();
                    q = q.Where(c =>
                        (c.OtherUserName != null && c.OtherUserName.ToLower().Contains(t)) ||
                        (c.LastMessage != null && c.LastMessage.ToLower().Contains(t)) ||
                        (c.ProductTitle != null && c.ProductTitle.ToLower().Contains(t))
                    );
                }

                return q;
            }
        }

        // ─── 2) Partial void override (sınıf gövdesine ekle) ─────────────────

        partial void OnSearchTextChanged(string value)
        {
            OnPropertyChanged(nameof(PersonalConversations));
            OnPropertyChanged(nameof(NegotiationConversations));
        }

        // ─── 3) YENİ KomutLAR (sınıf gövdesine ekle) ─────────────────────────

        [RelayCommand]
        private async Task RefreshConversationsAsync()
        {
            if (IsRefreshing || _currentUser == null) return;

            try
            {
                IsRefreshing = true;

                var result = await _messagingService.GetUserConversationsAsync(_currentUser.UserId);

                if (result.Success && result.Data != null)
                {
                    await UpdateConversationsFromRefreshAsync(result.Data); 

                    UpdateUnreadCount();
                    EmptyMessage = Conversations.Any() ? string.Empty : "HenÃ¼z mesajÄ±nÄ±z yok.";
                }
                else
                {
                    await Application.Current!.MainPage!.DisplayAlert("Hata",
                        result.Message ?? "KonuÅŸmalar yÃ¼klenemedi", "Tamam");
                }
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âŒ Refresh hatasÄ±: {ex.Message}");
                await Application.Current!.MainPage!.DisplayAlert("Hata",
                    "KonuÅŸmalar yenilenirken bir hata oluÅŸtu.", "Tamam");
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        [RelayCommand]
        private async Task ConversationTappedAsync(Conversation conversation)
        {
            if (conversation == null)
            {
                KamPay.Helpers.AppLogger.DebugLog("âš ï¸ ConversationTappedAsync: conversation null!");
                return;
            }
            
            KamPay.Helpers.AppLogger.DebugLog($" ConversationTappedAsync Ã§aÄŸrÄ±ldÄ±: {conversation.ConversationId}");
            KamPay.Helpers.AppLogger.DebugLog($"   OtherUser: {conversation.OtherUserName}");
            KamPay.Helpers.AppLogger.DebugLog($"   LastMessage: {conversation.LastMessage}");
            
            try
            {
                SelectedConversation = null;

                // Encode query parameters (photo and name) to pass to ChatPage
                var photo = Uri.EscapeDataString(conversation.OtherUserPhotoUrl ?? string.Empty);
                var name = Uri.EscapeDataString(conversation.OtherUserName ?? string.Empty);

                var pageName = conversation.IsNegotiationConversation
                    ? nameof(NegotiationChatPage)
                    : nameof(ChatPage);

                var navigationParameter = $"{pageName}?conversationId={conversation.ConversationId}&otherUserPhoto={photo}&otherUserName={name}";
                KamPay.Helpers.AppLogger.DebugLog($"ğŸš€ Navigation: {navigationParameter}");

                await Shell.Current.GoToAsync(navigationParameter);

                KamPay.Helpers.AppLogger.DebugLog("âœ… Navigation baÅŸarÄ±lÄ±!");
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âŒ Navigation hatasÄ±: {ex.Message}");
                KamPay.Helpers.AppLogger.DebugLog($"   StackTrace: {ex.StackTrace}");
                await Application.Current!.MainPage!.DisplayAlert("Hata", 
                    $"Sohbete giderken hata oluÅŸtu: {ex.Message}", "Tamam");
            }
        }

        [RelayCommand]
        private async Task DeleteConversationAsync(Conversation conversation)
        {
            if (conversation == null || _currentUser == null) return;

            var confirm = await Application.Current!.MainPage!.DisplayAlert("Onay", "Bu konuÅŸmayÄ± silmek istediÄŸinize emin misiniz?", "Evet", "HayÄ±r");
            if (!confirm) return;

            try
            {
                var result = await _messagingService.DeleteConversationAsync(conversation.ConversationId, _currentUser.UserId);
                if (!result.Success) 
                    await Application.Current!.MainPage!.DisplayAlert("Hata", result.Message, "Tamam");
            }
            catch (Exception ex) 
            { 
                await Application.Current!.MainPage!.DisplayAlert("Hata", ex.Message, "Tamam"); 
            }
        }

        // âœ… EKLE: OkunmamÄ±ÅŸ mesaj sayÄ±sÄ±nÄ± gÃ¼ncelle
        private void UpdateUnreadCount()
        {
            UnreadCount = Conversations.Sum(c => c.UnreadCount);
            WeakReferenceMessenger.Default.Send(new UnreadMessageStatusMessage(UnreadCount > 0));
        }

        // âœ… EKLE: KonuÅŸmalarÄ± tarihe gÃ¶re sÄ±rala (en yeni Ã¶nce)
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

        public void Dispose()
        {
            KamPay.Helpers.AppLogger.DebugLog("ğŸ§¹ MessagesViewModel dispose ediliyor...");
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
