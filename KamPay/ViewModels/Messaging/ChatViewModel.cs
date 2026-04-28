using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using KamPay.Services;
using KamPay.Models;
using KamPay.Views;
using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Helpers;
using System.Reactive.Linq;
using KamPay.Services.Auth;
using KamPay.Services.Messaging;
using KamPay.Services.Transactions;

namespace KamPay.ViewModels
{
    [QueryProperty(nameof(ConversationId), "conversationId")]
    [QueryProperty(nameof(OtherUserPhoto), "otherUserPhoto")]
    [QueryProperty(nameof(OtherUserName), "otherUserName")]
    public partial class ChatViewModel : ObservableObject, IDisposable
    {
        private readonly IMessagingService _messagingService;
        private readonly IAuthenticationService _authService;
        private readonly IUserStateService _userStateService;
        private readonly IStorageService _storageService;
        private readonly IUserProfileService _userProfileService;
        private readonly ITransactionService _transactionService; // Ã¢Å“â€¦ EKLENEN
        private readonly FirebaseClient _firebaseClient;
        private User? _currentUser;
        private static LocalizationResourceManager Res => LocalizationResourceManager.Instance;
        
        private static readonly Dictionary<string, ConversationState> _conversationCache = new();
        private System.Timers.Timer? _cacheCleanupTimer;
        private const int MaxCacheAgeMinutes = 15;
        private const int MaxCachedConversations = 10;

        private IDisposable? _messagesSubscription;
        private bool _isListenerActive = false;
        private string? _activeConversationId;
        private bool _initialLoadComplete = false;
        private IDisposable? _typingSubscription;
        private System.Timers.Timer? _typingDebounceTimer;
        private const string NullTransactionFilter = "__ALL__";

        [ObservableProperty]
        private string conversationId = string.Empty;

        [ObservableProperty]
        private string messageText = string.Empty;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private bool isSending;

        [ObservableProperty]
        private bool isRefreshing;

        // FotoÃ„Å¸raf yÃƒÂ¼kleme ÃƒÂ¶zellikleri
        [ObservableProperty]
        private bool isUploadingImage;

        [ObservableProperty]
        private string? selectedImagePath;

        // Ã¢Å“â€¦ Observable property olarak deÃ„Å¸iÃ…Å¸tirildi
        [ObservableProperty]
        private string otherUserPhoto = string.Empty;

        // Ã¢Å“â€¦ Observable property olarak deÃ„Å¸iÃ…Å¸tirildi
        [ObservableProperty]
        private string otherUserName = string.Empty;

        public ObservableRangeCollection<Message> Messages { get; set; } = new();
        public Conversation? Conversation { get; set; }

        [ObservableProperty]
        private Transaction? activeTransaction;

        [ObservableProperty]
        private bool hasActiveTransaction;

        [ObservableProperty]
        private string selectedTransactionId = string.Empty;

        // âœ… BUG-3/4 FIX: Ã‡oklu transaction desteÄŸi
        // AynÄ± conversation'da birden fazla Ã¼rÃ¼n iÃ§in aktif pazarlÄ±k tutulabilir
        public ObservableRangeCollection<Transaction> ActiveTransactions { get; } = new();
        [ObservableProperty]
        private bool hasActiveTransactions;

        private string _selectedFilterTransactionId = NullTransactionFilter;
        public string SelectedFilterTransactionId
        {
            get => _selectedFilterTransactionId;
            set
            {
                if (SetProperty(ref _selectedFilterTransactionId, value))
                {
                    OnPropertyChanged(nameof(FilteredMessages));
                }
            }
        }

        public IEnumerable<Message> FilteredMessages =>
            SelectedFilterTransactionId == NullTransactionFilter
                ? Messages
                : Messages.Where(m =>
                    m.RelatedTransactionId == SelectedFilterTransactionId ||
                    m.Type == MessageType.System ||
                    m.Type == MessageType.Negotiation);

        private bool _isOtherUserTyping;
        public bool IsOtherUserTyping
        {
            get => _isOtherUserTyping;
            set => SetProperty(ref _isOtherUserTyping, value);
        }

        private bool _showAllChipSelected = true;
        public bool ShowAllChipSelected
        {
            get => _showAllChipSelected;
            set => SetProperty(ref _showAllChipSelected, value);
        }


        private bool _isOtherUserOnline;
        public bool IsOtherUserOnline
        {
            get => _isOtherUserOnline;
            set => SetProperty(ref _isOtherUserOnline, value);
        }

        [ObservableProperty]
        private bool isNegotiationChat;

        private string _onlineStatusText = string.Empty;
        public string OnlineStatusText
        {
            get => _onlineStatusText;
            set => SetProperty(ref _onlineStatusText, value);
        }

        public ChatViewModel(
            IMessagingService messagingService,
            IAuthenticationService authService,
            IUserStateService userStateService,
            IStorageService storageService,
            IUserProfileService userProfileService,
            ITransactionService transactionService, // Ã¢Å“â€¦ EKLENEN
            FirebaseClient firebaseClient)
        {
            _messagingService = messagingService;
            _authService = authService;
            _userStateService = userStateService;
            _storageService = storageService;
            _userProfileService = userProfileService;
            _transactionService = transactionService; // Ã¢Å“â€¦ EKLENEN
            _firebaseClient = firebaseClient;

            // KullanÃ„Â±cÃ„Â± profil deÃ„Å¸iÃ…Å¸ikliklerini dinle
            _userStateService.UserProfileChanged += OnUserProfileChanged;

            //  Static timer baÃ…Å¸lat (sadece bir kez)
            _cacheCleanupTimer = new System.Timers.Timer(TimeSpan.FromMinutes(5).TotalMilliseconds);
            _cacheCleanupTimer.Elapsed += (s, e) => CleanupOldCache();
            _cacheCleanupTimer.Start();
            Messages.CollectionChanged += (_, _) =>
    OnPropertyChanged(nameof(FilteredMessages));
        }

        private void OnUserProfileChanged(object? sender, User updatedUser)
        {
            if (updatedUser == null) return;

            //  Kritik: UI'da anlÃ„Â±k gÃƒÂ¼ncelleme iÃƒÂ§in MainThread'de ÃƒÂ§alÃ„Â±Ã…Å¸tÃ„Â±rÃ„Â±lmalÃ„Â±dÃ„Â±r.
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // DiÃ„Å¸er kullanÃ„Â±cÃ„Â±nÃ„Â±n bilgilerini gÃƒÂ¼ncelle
                if (Conversation != null && _currentUser != null)
                {
                    var otherUserId = Conversation.GetOtherUserId(_currentUser.UserId);
                    if (otherUserId == updatedUser.UserId)
                    {
                        OtherUserName = updatedUser.FullName ?? string.Empty;
                        OtherUserPhoto = updatedUser.ProfileImageUrl ?? string.Empty;
                    }
                }

                // Mesajlardaki kullanÃ„Â±cÃ„Â± bilgilerini gÃƒÂ¼ncelle
                foreach (var message in Messages.Where(m => m.SenderId == updatedUser.UserId || m.ReceiverId == updatedUser.UserId))
                {
                    if (message.SenderId == updatedUser.UserId)
                    {
                        message.SenderName = updatedUser.FullName;
                    }
                    if (message.ReceiverId == updatedUser.UserId)
                    {
                        message.ReceiverName = updatedUser.FullName;
                        message.ReceiverPhotoUrl = updatedUser.ProfileImageUrl;
                    }
                }
            });
        }

        partial void OnConversationIdChanged(string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                _ = LoadChatAsync();
            }
        }

        [RelayCommand]
        private async Task LoadChatAsync()
        {
            //  CACHE: AynÃ„Â± konuÃ…Å¸ma iÃƒÂ§in tekrar yÃƒÂ¼kleme yapma
            if (_activeConversationId == ConversationId && _initialLoadComplete)
            {
                KamPay.Helpers.AppLogger.DebugLog($"Ã¢Å¡Â¡ Cache'den yÃƒÂ¼kleniyor: {ConversationId}");

                if (_conversationCache.TryGetValue(ConversationId, out var cachedState))
                {
                    RestoreFromCache(cachedState);
                }

                if (_currentUser != null)
                {
                    await _messagingService.MarkMessagesAsReadAsync(ConversationId, _currentUser.UserId);
                }
                return;
            }

            try
            {
                IsLoading = true;

                //  Eski konuÃ…Å¸madan geliyorsak kaydet
                if (_activeConversationId != null &&
                    _activeConversationId != ConversationId &&
                    _initialLoadComplete)
                {
                    SaveToCache(_activeConversationId);
                    CleanupCurrentConversation();
                }

                //  HER DURUMDA yeni kullanc al (oturum deYiYikliYini yakalamak iin)
                _currentUser = await _authService.GetCurrentUserAsync();
                if (_currentUser == null)
                {
                    await Application.Current!.MainPage!.DisplayAlert("Hata", "GiriY yapmY kullanc bulunamad.", "Tamam");
                    await Shell.Current.GoToAsync("..");
                    return;
                }

                //  CACHE: Cache'de varsa oradan yÃƒÂ¼kle
                if (_conversationCache.TryGetValue(ConversationId, out var cachedState))
                {
                    KamPay.Helpers.AppLogger.DebugLog($"ÄŸÅ¸â€œÂ¦ Cache'den yÃƒÂ¼klendi: {ConversationId}");
                    RestoreFromCache(cachedState);
                    _activeConversationId = ConversationId;
                    _initialLoadComplete = true;
                    IsLoading = false;

                    await _messagingService.MarkMessagesAsReadAsync(ConversationId, _currentUser.UserId);
                    await LoadActiveTransactionsAsync(_currentUser.UserId);
                    return;
                }

                //  Ã„Â°lk kez yÃƒÂ¼kleniyor
                KamPay.Helpers.AppLogger.DebugLog($" Ã„Â°lk yÃƒÂ¼kleme: {ConversationId}");

                // KonuÃ…Å¸ma bilgileri
                if (Conversation == null || Conversation.ConversationId != ConversationId)
                {
                    var conversations = await _messagingService.GetUserConversationsAsync(_currentUser.UserId);
                    Conversation = conversations.Data?.FirstOrDefault(c => c.ConversationId == ConversationId);

                    if (Conversation != null)
                    {
                        // Navigation parametresinden URL-decoded deÃ„Å¸eri al
                        var photoFromNav = System.Net.WebUtility.UrlDecode(OtherUserPhoto ?? string.Empty);
                        
                        OtherUserName = Conversation.GetOtherUserName(_currentUser.UserId) ?? string.Empty;
                        
                        // Navigation'dan gelen fotoÃ„Å¸raf varsa ve geÃƒÂ§erliyse onu kullan
                        if (!string.IsNullOrEmpty(photoFromNav) && photoFromNav != "person_icon.svg")
                        {
                            OtherUserPhoto = photoFromNav;
                        }
                        else
                        {
                            // KonuÃ…Å¸madan fotoÃ„Å¸raf al
                            OtherUserPhoto = Conversation.GetOtherUserPhotoUrl(_currentUser.UserId) ?? "person_icon.svg";
                        }

                        KamPay.Helpers.AppLogger.DebugLog($"ÄŸÅ¸â€˜Â¤ OtherUserName: {OtherUserName}");
                        KamPay.Helpers.AppLogger.DebugLog($"ÄŸÅ¸â€œÂ· OtherUserPhoto: {OtherUserPhoto}");

                        // / Online durumunu sorgula - Users koleksiyonundan LastLoginAt kontrolÃƒÂ¼
                        try
                        {
                            var otherUserId = Conversation.GetOtherUserId(_currentUser.UserId);
                            if (!string.IsNullOrEmpty(otherUserId))
                            {
                                var otherUser = await _firebaseClient
                                    .Child(Constants.UsersCollection)
                                    .Child(otherUserId)
                                    .OnceSingleAsync<User>();

                                if (otherUser != null && otherUser.LastLoginAt.HasValue)
                                {
                                    var last = otherUser.LastLoginAt.Value.ToUniversalTime();
                                    var diff = DateTime.UtcNow - last;
                                    IsOtherUserOnline = diff.TotalMinutes <= 2;
                                    OnlineStatusText = IsOtherUserOnline ? "Ãƒâ€¡evrimiÃƒÂ§i" : $"Son gÃƒÂ¶rÃƒÂ¼lme: {last.ToLocalTime():g}";

                                    // Profil fotoÃ„Å¸rafÃ„Â± boÃ…Å¸sa Firebase'den al
                                    if ((string.IsNullOrEmpty(OtherUserPhoto) || OtherUserPhoto == "person_icon.svg") && 
                                        !string.IsNullOrEmpty(otherUser.ProfileImageUrl))
                                    {
                                        OtherUserPhoto = otherUser.ProfileImageUrl;
                                        KamPay.Helpers.AppLogger.DebugLog($"ÄŸÅ¸â€œÂ· Firebase'den fotoÃ„Å¸raf alÃ„Â±ndÃ„Â±: {OtherUserPhoto}");
                                    }
                                }
                                else
                                {
                                    IsOtherUserOnline = false;
                                    OnlineStatusText = "Ãƒâ€¡evrimdÃ„Â±Ã…Å¸Ã„Â±";
                                }
                            }
                            else
                            {
                                IsOtherUserOnline = false;
                                OnlineStatusText = "Ãƒâ€¡evrimdÃ„Â±Ã…Å¸Ã„Â±";
                            }
                        }
                        catch (Exception ex)
                        {
                            KamPay.Helpers.AppLogger.DebugLog($"Ã¢Å¡Â Ã¯Â¸Â Online durumu alÃ„Â±namadÃ„Â±: {ex.Message}");
                            IsOtherUserOnline = false;
                            OnlineStatusText = "Ãƒâ€¡evrimdÃ„Â±Ã…Å¸Ã„Â±";
                        }

                        // Ã¢Å“â€¦ Fallback: KullanÃ„Â±cÃ„Â± profil servisi ile fotoÃ„Å¸raf yÃƒÂ¼kle
                        await EnsureOtherUserPhotoAsync();

                        // Ã¢Å“â€¦ LOAD ACTIVE TRANSACTION (Daha gÃƒÂ¼venli yÃƒÂ¶ntem)
                        // âœ… Aktif transaction'larÄ± tek yerden yÃ¼kle
                        await LoadActiveTransactionsAsync(_currentUser.UserId);
                    }
                    else
                    {
                        KamPay.Helpers.AppLogger.DebugLog($"âš ï¸ Conversation bulunamadÄ±: {ConversationId}");
                    }
                }
                // 3Ã¯Â¸ÂÃ¢Æ’Â£ Scroll to bottom
                WeakReferenceMessenger.Default.Send(new ScrollToChatMessage(null));

                // 4Ã¯Â¸ÂÃ¢Æ’Â£ REALTIME: Listener baÃ…Å¸lat (yeni mesajlar iÃƒÂ§in)
                StartListeningToMessages();
                StartListeningToTyping();
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"Ã¢ÂÅ’ LoadMessagesWithSnapshotAsync hatasÃ„Â±: {ex.Message}");
                // Hata durumunda loading'i kapat ve real-time listener ile devam et
                _initialLoadComplete = false;
                IsLoading = false;
                StartListeningToMessages();
                StartListeningToTyping();
            }
        }




        //  Pull-to-Refresh
        [RelayCommand]
        private async Task RefreshMessagesAsync()
        {
            if (IsRefreshing) return;

            try
            {
                IsRefreshing = true;

                // Cache'i temizle
                _conversationCache.Remove(ConversationId);

                // Listener'Ã„Â± yeniden baÃ…Å¸lat
                CleanupCurrentConversation();
                Messages.Clear();

                _initialLoadComplete = false;
                StartListeningToMessages();
                StartListeningToTyping();

                await Task.Delay(500);
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"Ã¢ÂÅ’ Refresh hatasÃ„Â±: {ex.Message}");
            }
            finally
            {
                IsRefreshing = false;
            }
        }







        //  Binary search insert (optimize edilmiÃ…Å¸)
        private void InsertMessageSorted(Message newMessage)
        {
            // Temp mesajÃ„Â± bul ve kaldÃ„Â±r
            var tempMessage = Messages.FirstOrDefault(m => m.MessageId != null && m.MessageId.StartsWith("temp_") &&
                                                            m.Content == newMessage.Content &&
                                                            Math.Abs((m.SentAt - newMessage.SentAt).TotalSeconds) < 10);
            if (tempMessage != null)
            {
                Messages.Remove(tempMessage);
            }

            if (Messages.Count == 0)
            {
                Messages.Add(newMessage);
                return;
            }

            if (Messages[Messages.Count - 1].SentAt <= newMessage.SentAt)
            {
                Messages.Add(newMessage);
                return;
            }

            if (Messages[0].SentAt >= newMessage.SentAt)
            {
                Messages.Insert(0, newMessage);
                return;
            }

            // Binary search
            int left = 0;
            int right = Messages.Count - 1;

            while (left <= right)
            {
                int mid = (left + right) / 2;

                if (Messages[mid].SentAt == newMessage.SentAt)
                {
                    Messages.Insert(mid + 1, newMessage);
                    return;
                }
                else if (Messages[mid].SentAt < newMessage.SentAt)
                {
                    left = mid + 1;
                }
                else
                {
                    right = mid - 1;
                }
            }

            Messages.Insert(left, newMessage);
        }





        [RelayCommand]
        private async Task GoBackAsync()
        {
            SaveToCache(ConversationId);
            CleanupCurrentConversation();
            await Shell.Current.GoToAsync("..");
        }


        // Ã¢Å“â€¦ IN-CHAT NEGOTIATION COMMANDS


        private bool _disposed = false;
        
        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                KamPay.Helpers.AppLogger.DebugLog("ÄŸÅ¸Â§Â¹ ChatViewModel dispose ediliyor...");

                // Event subscription'Ã„Â± temizle
                _userStateService.UserProfileChanged -= OnUserProfileChanged;

                if (!string.IsNullOrEmpty(_activeConversationId))
                {
                    SaveToCache(_activeConversationId);
                }

                // Ã¢Å“â€¦ EKLEME: Listener temizliÃ„Å¸i
                _messagesSubscription?.Dispose();
                _messagesSubscription = null;
                _isListenerActive = false;
                _initialLoadComplete = false;

                _typingSubscription?.Dispose();
                _typingSubscription = null;
                _typingDebounceTimer?.Stop();
                _typingDebounceTimer?.Dispose();
                _typingDebounceTimer = null;

                if (_currentUser != null && !string.IsNullOrEmpty(ConversationId))
                {
                    _ = _firebaseClient
                          .Child("typing")
                          .Child(ConversationId)
                          .Child(_currentUser.UserId)
                          .PutAsync(false);
                }
                // Ã¢Å“â€¦ EKLEME: Timer temizliÃ„Å¸i
                _cacheCleanupTimer?.Stop();
                _cacheCleanupTimer?.Dispose();
                _cacheCleanupTimer = null;
                
                KamPay.Helpers.AppLogger.DebugLog("Ã¢Å“â€¦ ChatViewModel resources disposed");
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"Ã¢Å¡Â Ã¯Â¸Â ChatViewModel dispose hatasÃ„Â±: {ex.Message}");
            }
            finally
            {
                _disposed = true;
            }
        }



        // ÄŸÅ¸â€œÂ· FotoÃ„Å¸raf GÃƒÂ¶nderme KomutlarÃ„Â±





        // After setting OtherUserPhoto from conversation or Firebase, try loading cached profile from IUserProfileService if still empty.
        // Add this helper method in ChatViewModel
        private async Task EnsureOtherUserPhotoAsync()
        {
            if (!string.IsNullOrEmpty(OtherUserPhoto) && OtherUserPhoto != "person_icon.svg")
                return;

            try
            {
                if (Conversation == null || _currentUser == null) return;
                var otherUserId = Conversation.GetOtherUserId(_currentUser.UserId);
                if (string.IsNullOrEmpty(otherUserId)) return;

                var profileResult = await _userProfileService.GetUserProfileAsync(otherUserId);
                if (profileResult.Success && profileResult.Data != null && !string.IsNullOrEmpty(profileResult.Data.ProfileImageUrl))
                {
                    OtherUserPhoto = profileResult.Data.ProfileImageUrl;
                    KamPay.Helpers.AppLogger.DebugLog($"Ã¢Å“â€¦ Profil servisi fotoÃ„Å¸rafÃ„Â±nÃ„Â± yÃƒÂ¼kledi: {OtherUserPhoto}");
                }
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"Ã¢Å¡Â Ã¯Â¸Â EnsureOtherUserPhotoAsync hata: {ex.Message}");
            }
        }
    }

}


