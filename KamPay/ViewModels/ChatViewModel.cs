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
        private readonly ITransactionService _transactionService; // âœ… EKLENEN
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

        // FotoÄŸraf yÃ¼kleme Ã¶zellikleri
        [ObservableProperty]
        private bool isUploadingImage;

        [ObservableProperty]
        private string? selectedImagePath;

        // âœ… Observable property olarak deÄŸiÅŸtirildi
        [ObservableProperty]
        private string otherUserPhoto = string.Empty;

        // âœ… Observable property olarak deÄŸiÅŸtirildi
        [ObservableProperty]
        private string otherUserName = string.Empty;

        public ObservableRangeCollection<Message> Messages { get; set; } = new();
        public Conversation? Conversation { get; set; }

        [ObservableProperty]
        private Transaction? activeTransaction;

        [ObservableProperty]
        private bool hasActiveTransaction;

        private bool _isOtherUserOnline;
        public bool IsOtherUserOnline
        {
            get => _isOtherUserOnline;
            set => SetProperty(ref _isOtherUserOnline, value);
        }

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
            ITransactionService transactionService, // âœ… EKLENEN
            FirebaseClient firebaseClient)
        {
            _messagingService = messagingService;
            _authService = authService;
            _userStateService = userStateService;
            _storageService = storageService;
            _userProfileService = userProfileService;
            _transactionService = transactionService; // âœ… EKLENEN
            _firebaseClient = firebaseClient;

            // KullanÄ±cÄ± profil deÄŸiÅŸikliklerini dinle
            _userStateService.UserProfileChanged += OnUserProfileChanged;

            //  Static timer baÅŸlat (sadece bir kez)
            _cacheCleanupTimer = new System.Timers.Timer(TimeSpan.FromMinutes(5).TotalMilliseconds);
            _cacheCleanupTimer.Elapsed += (s, e) => CleanupOldCache();
            _cacheCleanupTimer.Start();
        }

        private void OnUserProfileChanged(object? sender, User updatedUser)
        {
            if (updatedUser == null) return;

            //  Kritik: UI'da anlÄ±k gÃ¼ncelleme iÃ§in MainThread'de Ã§alÄ±ÅŸtÄ±rÄ±lmalÄ±dÄ±r.
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // DiÄŸer kullanÄ±cÄ±nÄ±n bilgilerini gÃ¼ncelle
                if (Conversation != null && _currentUser != null)
                {
                    var otherUserId = Conversation.GetOtherUserId(_currentUser.UserId);
                    if (otherUserId == updatedUser.UserId)
                    {
                        OtherUserName = updatedUser.FullName ?? string.Empty;
                        OtherUserPhoto = updatedUser.ProfileImageUrl ?? string.Empty;
                    }
                }

                // Mesajlardaki kullanÄ±cÄ± bilgilerini gÃ¼ncelle
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
            //  CACHE: AynÄ± konuÅŸma iÃ§in tekrar yÃ¼kleme yapma
            if (_activeConversationId == ConversationId && _initialLoadComplete)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âš¡ Cache'den yÃ¼kleniyor: {ConversationId}");

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

                //  Eski konuÅŸmadan geliyorsak kaydet
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

                //  CACHE: Cache'de varsa oradan yÃ¼kle
                if (_conversationCache.TryGetValue(ConversationId, out var cachedState))
                {
                    KamPay.Helpers.AppLogger.DebugLog($"ğŸ“¦ Cache'den yÃ¼klendi: {ConversationId}");
                    RestoreFromCache(cachedState);
                    _activeConversationId = ConversationId;
                    _initialLoadComplete = true;
                    IsLoading = false;

                    await _messagingService.MarkMessagesAsReadAsync(ConversationId, _currentUser.UserId);
                    return;
                }

                //  Ä°lk kez yÃ¼kleniyor
                KamPay.Helpers.AppLogger.DebugLog($" Ä°lk yÃ¼kleme: {ConversationId}");

                // KonuÅŸma bilgileri
                if (Conversation == null || Conversation.ConversationId != ConversationId)
                {
                    var conversations = await _messagingService.GetUserConversationsAsync(_currentUser.UserId);
                    Conversation = conversations.Data?.FirstOrDefault(c => c.ConversationId == ConversationId);

                    if (Conversation != null)
                    {
                        // Navigation parametresinden URL-decoded deÄŸeri al
                        var photoFromNav = System.Net.WebUtility.UrlDecode(OtherUserPhoto ?? string.Empty);
                        
                        OtherUserName = Conversation.GetOtherUserName(_currentUser.UserId) ?? string.Empty;
                        
                        // Navigation'dan gelen fotoÄŸraf varsa ve geÃ§erliyse onu kullan
                        if (!string.IsNullOrEmpty(photoFromNav) && photoFromNav != "person_icon.svg")
                        {
                            OtherUserPhoto = photoFromNav;
                        }
                        else
                        {
                            // KonuÅŸmadan fotoÄŸraf al
                            OtherUserPhoto = Conversation.GetOtherUserPhotoUrl(_currentUser.UserId) ?? "person_icon.svg";
                        }

                        KamPay.Helpers.AppLogger.DebugLog($"ğŸ‘¤ OtherUserName: {OtherUserName}");
                        KamPay.Helpers.AppLogger.DebugLog($"ğŸ“· OtherUserPhoto: {OtherUserPhoto}");

                        // / Online durumunu sorgula - Users koleksiyonundan LastLoginAt kontrolÃ¼
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
                                    OnlineStatusText = IsOtherUserOnline ? "Ã‡evrimiÃ§i" : $"Son gÃ¶rÃ¼lme: {last.ToLocalTime():g}";

                                    // Profil fotoÄŸrafÄ± boÅŸsa Firebase'den al
                                    if ((string.IsNullOrEmpty(OtherUserPhoto) || OtherUserPhoto == "person_icon.svg") && 
                                        !string.IsNullOrEmpty(otherUser.ProfileImageUrl))
                                    {
                                        OtherUserPhoto = otherUser.ProfileImageUrl;
                                        KamPay.Helpers.AppLogger.DebugLog($"ğŸ“· Firebase'den fotoÄŸraf alÄ±ndÄ±: {OtherUserPhoto}");
                                    }
                                }
                                else
                                {
                                    IsOtherUserOnline = false;
                                    OnlineStatusText = "Ã‡evrimdÄ±ÅŸÄ±";
                                }
                            }
                            else
                            {
                                IsOtherUserOnline = false;
                                OnlineStatusText = "Ã‡evrimdÄ±ÅŸÄ±";
                            }
                        }
                        catch (Exception ex)
                        {
                            KamPay.Helpers.AppLogger.DebugLog($"âš ï¸ Online durumu alÄ±namadÄ±: {ex.Message}");
                            IsOtherUserOnline = false;
                            OnlineStatusText = "Ã‡evrimdÄ±ÅŸÄ±";
                        }

                        // âœ… Fallback: KullanÄ±cÄ± profil servisi ile fotoÄŸraf yÃ¼kle
                        await EnsureOtherUserPhotoAsync();

                        // âœ… LOAD ACTIVE TRANSACTION (Daha gÃ¼venli yÃ¶ntem)
                        try
                        {
                            var myOffersResult = await _transactionService.GetMyOffersAsync(_currentUser.UserId);
                            if (myOffersResult.Success && myOffersResult.Data != null)
                            {
                                var match = myOffersResult.Data.FirstOrDefault(t => t.ConversationId == ConversationId);
                                if (match != null)
                                {
                                    ActiveTransaction = match;
                                    HasActiveTransaction = true;
                                    KamPay.Helpers.AppLogger.DebugLog($"âœ… ActiveTransaction loaded: {match.TransactionId}");
                                }
                                else
                                {
                                    KamPay.Helpers.AppLogger.DebugLog($"âš ï¸ Bu konuÅŸmaya baÄŸlÄ± iÅŸlem bulunamadÄ±: {ConversationId}");
                                    // Belki henÃ¼z conversationId set edilmemiÅŸ bir transaction var? ProductId filtresiyle de bakÄ±labilir
                                    if (Conversation != null && !string.IsNullOrEmpty(Conversation.ProductId))
                                    {
                                        var fallbackMatch = myOffersResult.Data.FirstOrDefault(t => t.ProductId == Conversation.ProductId && (t.Status == TransactionStatus.Pending || t.IsNegotiating));
                                        if (fallbackMatch != null)
                                        {
                                            ActiveTransaction = fallbackMatch;
                                            HasActiveTransaction = true;
                                            KamPay.Helpers.AppLogger.DebugLog($"âœ… ActiveTransaction loaded via Fallback ProductId: {fallbackMatch.TransactionId}");
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            KamPay.Helpers.AppLogger.DebugLog($"âš ï¸ Could not load active transaction: {ex.Message}");
                        }
                    }
                    else
                    {
                        KamPay.Helpers.AppLogger.DebugLog($"âš ï¸ Conversation bulunamadÄ±: {ConversationId}");
                    }
                }

                //  UltraFastLoad: Snapshot ile anÄ±nda mesajlarÄ± yÃ¼kle
                await LoadMessagesWithSnapshotAsync();

                _activeConversationId = ConversationId;

                await _messagingService.MarkMessagesAsReadAsync(ConversationId, _currentUser.UserId);
            }
            catch (Exception ex)
            {
                await Application.Current!.MainPage!.DisplayAlert("Hata", ex.Message, "Tamam");
                KamPay.Helpers.AppLogger.DebugLog($"âŒ LoadChatAsync hatasÄ±: {ex.Message}");
                IsLoading = false;
            }
        }

        //  UltraFastLoad: Snapshot ile mesajlarÄ± anÄ±nda yÃ¼kle
        private async Task LoadMessagesWithSnapshotAsync()
        {
            try
            {
                KamPay.Helpers.AppLogger.DebugLog($"ğŸ“¸ Snapshot ile mesaj yÃ¼kleme baÅŸlÄ±yor: {ConversationId}");

                // 1ï¸âƒ£ SNAPSHOT: TÃ¼m mesajlarÄ± anÄ±nda Ã§ek
                var messagesSnapshot = await _firebaseClient
                    .Child(Constants.MessagesCollection)
                    .Child(ConversationId)
                    .OnceAsync<Message>();

                if (messagesSnapshot.Any())
                {
                    //  : Sistem mesajlarÄ±nÄ± da dahil et
                    var loadedMessages = messagesSnapshot
                        .Where(m => m.Object != null && !m.Object.IsDeleted) // Sadece silinen mesajlarÄ± filtrele
                        .Select(m =>
                        {
                            var message = m.Object;
                            message.MessageId = m.Key;
                            message.IsSentByMe = message.SenderId == _currentUser!.UserId;
                            
                            //  DEBUG: Sistem mesajÄ± kontrolÃ¼
                            if (message.Type == MessageType.System || message.IsSystemMessage)
                            {
                                KamPay.Helpers.AppLogger.DebugLog($"ğŸ“‹ Sistem mesajÄ± yÃ¼klendi: {message.Content}");
                            }
                            
                            return message;
                        })
                        .OrderBy(m => m.SentAt)
                        .ToList();

                    // UI'a optimize ÅŸekilde ekle
                    if (loadedMessages.Count > 50)
                    {
                        Messages.ReplaceRange(loadedMessages.Take(50));
                        var remaining = loadedMessages.Skip(50).ToList();

                        _ = Task.Run(async () =>
                        {
                            for (int i = 0; i < remaining.Count; i += 50)
                            {
                                var chunk = remaining.Skip(i).Take(50).ToList();
                                await MainThread.InvokeOnMainThreadAsync(() => Messages.AddRange(chunk));
                                await Task.Delay(50); // Nefes alma (UI Thread)
                            }
                        });
                    }
                    else
                    {
                        Messages.ReplaceRange(loadedMessages);
                    }

                    KamPay.Helpers.AppLogger.DebugLog($"âœ… Snapshot ile {loadedMessages.Count} mesaj yÃ¼klendi");
                }

                // 2ï¸âƒ£ Loading'i kapat - veri gÃ¶sterildi
                _initialLoadComplete = true;
                IsLoading = false;

                // 3ï¸âƒ£ Scroll to bottom
                WeakReferenceMessenger.Default.Send(new ScrollToChatMessage(null));

                // 4ï¸âƒ£ REALTIME: Listener baÅŸlat (yeni mesajlar iÃ§in)
                StartListeningToMessages();
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âŒ LoadMessagesWithSnapshotAsync hatasÄ±: {ex.Message}");
                // Hata durumunda loading'i kapat ve real-time listener ile devam et
                _initialLoadComplete = false;
                IsLoading = false;
                StartListeningToMessages();
            }
        }

        //  : Cache kaydetme (LRU pattern)
        private void SaveToCache(string conversationId)
        {
            if (string.IsNullOrEmpty(conversationId)) return;

            //  LRU: Maksimum cache sayÄ±sÄ±nÄ± kontrol et
            if (_conversationCache.Count >= MaxCachedConversations)
            {
                var oldestKey = _conversationCache
                    .OrderBy(kvp => kvp.Value.LastAccessedAt)
                    .First().Key;

                _conversationCache.Remove(oldestKey);
                KamPay.Helpers.AppLogger.DebugLog($"ğŸ—‘ï¸ LRU: En eski cache temizlendi: {oldestKey}");
            }

            var state = new ConversationState
            {
                Messages = Messages.ToList(),
                Conversation = Conversation,
                OtherUserName = OtherUserName,
                OtherUserPhoto = OtherUserPhoto,
                CachedAt = DateTime.UtcNow,
                LastAccessedAt = DateTime.UtcNow
            };

            _conversationCache[conversationId] = state;
            KamPay.Helpers.AppLogger.DebugLog($"ğŸ’¾ Cache'e kaydedildi: {conversationId} ({state.Messages.Count} mesaj)");
        }

        //  Cache'den geri yÃ¼kleme
        private void RestoreFromCache(ConversationState state)
        {
            // Cache yaÅŸÄ±nÄ± kontrol et
            if ((DateTime.UtcNow - state.CachedAt).TotalMinutes > MaxCacheAgeMinutes)
            {
                KamPay.Helpers.AppLogger.DebugLog("âš ï¸ Cache eski, yeniden yÃ¼kleniyor...");
                _conversationCache.Remove(ConversationId);
                _initialLoadComplete = false;
                _ = Task.Run(() => LoadChatAsync());
                return;
            }

            // Last accessed time gÃ¼ncelle (LRU iÃ§in)
            state.LastAccessedAt = DateTime.UtcNow;

            Messages.Clear();
            foreach (var msg in state.Messages)
            {
                Messages.Add(msg);
            }

            Conversation = state.Conversation;
            OtherUserName = state.OtherUserName;
            OtherUserPhoto = state.OtherUserPhoto;

            // Listener'Ä± yeniden baÅŸlat
            StartListeningToMessages();

            KamPay.Helpers.AppLogger.DebugLog($"âœ… Cache'den geri yÃ¼klendi: {Messages.Count} mesaj");
        }

        //  Mevcut konuÅŸmayÄ± temizle
        private void CleanupCurrentConversation()
        {
            _messagesSubscription?.Dispose();
            _messagesSubscription = null;
            _isListenerActive = false;
            _initialLoadComplete = false;
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

                // Listener'Ä± yeniden baÅŸlat
                CleanupCurrentConversation();
                Messages.Clear();

                _initialLoadComplete = false;
                StartListeningToMessages();

                await Task.Delay(500);
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âŒ Refresh hatasÄ±: {ex.Message}");
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        //  : 200ms buffer + batch processing
        private void StartListeningToMessages()
        {
            if (_isListenerActive)
            {
                KamPay.Helpers.AppLogger.DebugLog("âš ï¸ Listener zaten aktif, yeniden baÅŸlatÄ±lmadÄ±.");
                return;
            }

            KamPay.Helpers.AppLogger.DebugLog($" Real-time listener baÅŸlatÄ±ldÄ±: {ConversationId}");

            //  : Sistem mesajlarÄ±nÄ± da dahil et
            _messagesSubscription = _firebaseClient
                .Child(Constants.MessagesCollection)
                .Child(ConversationId)
                .AsObservable<Message>()
                .Where(e => e.Object != null && !e.Object.IsDeleted) // Sadece silinen mesajlarÄ± filtrele
                .Buffer(TimeSpan.FromMilliseconds(200))
                .Where(batch => batch.Any())
                .Subscribe(
                    events =>
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            try
                            {
                                //  DEBUG: Sistem mesajÄ± kontrolÃ¼
                                foreach (var e in events)
                                {
                                    if (e.Object != null && (e.Object.Type == MessageType.System || e.Object.IsSystemMessage))
                                    {
                                        KamPay.Helpers.AppLogger.DebugLog($"ğŸ“‹ Realtime sistem mesajÄ±: {e.Object.Content}");
                                    }
                                }
                                
                                ProcessMessageBatch(events);
                            }
                            catch (Exception ex)
                            {
                                KamPay.Helpers.AppLogger.DebugLog($"âŒ Message batch hatasÄ±: {ex.Message}");
                            }
                            finally
                            {
                                if (!_initialLoadComplete)
                                {
                                    _initialLoadComplete = true;
                                    IsLoading = false;

                                    WeakReferenceMessenger.Default.Send(new ScrollToChatMessage(null));
                                }
                            }
                        });
                    },
                    error =>
                    {
                        KamPay.Helpers.AppLogger.DebugLog($"âŒ Firebase message listener hatasÄ±: {error.Message}");
                        MainThread.BeginInvokeOnMainThread(() => IsLoading = false);
                    });

            _isListenerActive = true;
        }

        //  Batch processing
        private void ProcessMessageBatch(IList<Firebase.Database.Streaming.FirebaseEvent<Message>> events)
        {
            bool shouldScroll = false;
            Message? lastNewMessage = null;

            foreach (var e in events)
            {
                var message = e.Object;
                if (message == null) continue;

                message.MessageId = e.Key;
                message.IsSentByMe = message.SenderId == _currentUser!.UserId;

                var existingMessage = Messages.FirstOrDefault(m => m.MessageId == message.MessageId);

                if (e.EventType == Firebase.Database.Streaming.FirebaseEventType.InsertOrUpdate)
                {
                    if (existingMessage != null)
                    {
                        var index = Messages.IndexOf(existingMessage);
                        Messages[index] = message;
                    }
                    else
                    {
                        InsertMessageSorted(message);

                        if (!message.IsSentByMe)
                        {
                            _ = _messagingService.MarkMessagesAsReadAsync(ConversationId, _currentUser!.UserId);
                        }

                        shouldScroll = true;
                        lastNewMessage = message;
                    }
                }
                else if (e.EventType == Firebase.Database.Streaming.FirebaseEventType.Delete)
                {
                    if (existingMessage != null)
                    {
                        Messages.Remove(existingMessage);
                    }
                }
            }

            if (shouldScroll && lastNewMessage != null && _initialLoadComplete)
            {
                WeakReferenceMessenger.Default.Send(new ScrollToChatMessage(lastNewMessage));
            }
        }

        //  Binary search insert (optimize edilmiÅŸ)
        private void InsertMessageSorted(Message newMessage)
        {
            // Temp mesajÄ± bul ve kaldÄ±r
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
        private async Task SendMessageAsync()
        {
            if (IsSending || string.IsNullOrWhiteSpace(MessageText) || _currentUser == null || Conversation == null)
            {
                return;
            }

            // GÃœVENLÄ°K: Mesaj iÃ§eriÄŸini XSS saldÄ±rÄ±larÄ±na karÅŸÄ± temizle
            var messageContent = InputSanitizer.SanitizeText(MessageText.Trim());

            // EÄŸer temizleme sonrasÄ± mesaj tamamen boÅŸaldÄ±ysa iÅŸlemi iptal et
            if (string.IsNullOrEmpty(messageContent)) return;

            var tempMessage = new Message
            {
                MessageId = $"temp_{Guid.NewGuid()}",
                ConversationId = ConversationId,
                SenderId = _currentUser.UserId,
                SenderName = _currentUser.FullName,
                SenderPhotoUrl = _currentUser.ProfileImageUrl,
                ReceiverId = Conversation.GetOtherUserId(_currentUser.UserId) ?? string.Empty,
                ReceiverName = Conversation.GetOtherUserName(_currentUser.UserId),
                ReceiverPhotoUrl = Conversation.GetOtherUserPhotoUrl(_currentUser.UserId),
                Content = messageContent,
                Type = MessageType.Text,
                ProductId = Conversation.ProductId,
                ProductTitle = Conversation.ProductTitle,
                ProductThumbnail = Conversation.ProductThumbnail,
                IsSentByMe = true,
                SentAt = DateTime.UtcNow,
                IsDelivered = false,
                IsRead = false
            };

            InsertMessageSorted(tempMessage);
            WeakReferenceMessenger.Default.Send(new ScrollToChatMessage(tempMessage));

            try
            {
                IsSending = true;

                var receiverId = Conversation.GetOtherUserId(_currentUser.UserId);
                if (string.IsNullOrEmpty(receiverId))
                {
                    Messages.Remove(tempMessage);
                    await Application.Current!.MainPage!.DisplayAlert("Hata", "AlÄ±cÄ± bilgisi bulunamadÄ±.", "Tamam");
                    return;
                }

                var request = new SendMessageRequest
                {
                    ReceiverId = receiverId,
                    Content = messageContent,
                    Type = MessageType.Text,
                    ProductId = Conversation.ProductId
                };

                var result = await _messagingService.SendMessageAsync(request, _currentUser);

                if (result.Success)
                {
                    // âœ… BaÅŸarÄ±lÄ± gÃ¶nderim sonrasÄ± input temizle
                    MessageText = string.Empty;
                }
                else
                {
                    Messages.Remove(tempMessage);
                    await Application.Current!.MainPage!.DisplayAlert("Hata", result.Message ?? "Mesaj gÃ¶nderilemedi", "Tamam");
                }
            }
            catch (Exception ex)
            {
                Messages.Remove(tempMessage);
                await Application.Current!.MainPage!.DisplayAlert("Hata", ex.Message, "Tamam");
                KamPay.Helpers.AppLogger.DebugLog($"âŒ SendMessage hatasÄ±: {ex.Message}");
            }
            finally
            {
                IsSending = false;
            }
        }

        [RelayCommand]
        private async Task GoBackAsync()
        {
            SaveToCache(ConversationId);
            CleanupCurrentConversation();
            await Shell.Current.GoToAsync("..");
        }

        //  Otomatik cache temizleme
        private static void CleanupOldCache()
        {
            var now = DateTime.UtcNow;
            var oldKeys = _conversationCache
                .Where(kvp => (now - kvp.Value.CachedAt).TotalMinutes > MaxCacheAgeMinutes)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in oldKeys)
            {
                _conversationCache.Remove(key);
            }

            if (oldKeys.Any())
            {
                KamPay.Helpers.AppLogger.DebugLog($"ğŸ—‘ï¸ {oldKeys.Count} eski cache otomatik temizlendi");
            }
        }

        // âœ… IN-CHAT NEGOTIATION COMMANDS

        [RelayCommand]
        private async Task ProposeOfferAsync(Message message)
        {
            // KullanÄ±lacak Ã¶zel transaction (Balon Ã¼zerinden geliyorsa kendi transaction'Ä±, alt butondan geliyorsa genel ActiveTransaction)
            Transaction targetTransaction = ActiveTransaction;

            if (message != null && !string.IsNullOrEmpty(message.RelatedTransactionId))
            {
                // EÄŸer buton, bir teklif balonundan tetiklendiyse ve balonda iÅŸlem ID'si varsa onu kullan
                // Mevcut listedeki iÅŸlemler arasÄ±ndan bulmayÄ± deneriz
                var myOffersResult = await _transactionService.GetMyOffersAsync(_currentUser.UserId);
                if (myOffersResult.Success && myOffersResult.Data != null)
                {
                    var specificTx = myOffersResult.Data.FirstOrDefault(t => t.TransactionId == message.RelatedTransactionId);
                    if (specificTx != null)
                    {
                        targetTransaction = specificTx;
                    }
                }
            }

            if (targetTransaction == null) 
            {
                await Application.Current!.MainPage!.DisplayAlert("Hata", "Aktif iÅŸlem (transaction) yÃ¼klenemedi. LÃ¼tfen sayfayÄ± yenileyin veya tekrar girin.", "Tamam");
                return;
            }
            if (_currentUser == null) return;

            try
            {
                var result = await Application.Current!.MainPage!.DisplayPromptAsync(
                    $"ğŸ’° Fiyat Teklifi ({targetTransaction.ProductTitle})",
                    "Teklif etmek istediÄŸiniz tutarÄ± girin (â‚º):",
                    Res["SendButton"] ?? "GÃ¶nder",
                    Res["Cancel"] ?? "Ä°ptal",
                    "Ã–rn: 500",
                    keyboard: Keyboard.Numeric
                );

                if (string.IsNullOrWhiteSpace(result)) return;

                if (!decimal.TryParse(result, out var proposedPrice) || proposedPrice <= 0)
                {
                    await Application.Current.MainPage.DisplayAlert("Hata", "GeÃ§erli bir tutar giriniz.", "Tamam");
                    return;
                }

                IsLoading = true;

                // SatÄ±cÄ± mÄ± AlÄ±cÄ± mÄ±?
                if (targetTransaction.SellerId == _currentUser.UserId)
                {
                    // SatÄ±cÄ± ise CounterOffer gÃ¶nder
                    var response = await _transactionService.SendCounterOfferForSaleAsync(targetTransaction.TransactionId, proposedPrice, _currentUser.UserId);
                    if (!response.Success)
                    {
                        await Application.Current.MainPage.DisplayAlert("Hata", response.Message, "Tamam");
                    }
                }
                else
                {
                    // AlÄ±cÄ± ise ProposePrice gÃ¶nder
                    var response = await _transactionService.ProposePriceForSaleAsync(targetTransaction.TransactionId, proposedPrice, _currentUser.UserId);
                    if (!response.Success)
                    {
                        await Application.Current.MainPage.DisplayAlert("Hata", response.Message, "Tamam");
                    }
                }

                // Tekrar transaction'Ä± yÃ¼kleyelim ki UI gÃ¼ncellensin
                await LoadChatAsync();
            }
            catch (Exception ex)
            {
                await Application.Current!.MainPage!.DisplayAlert("Hata", ex.Message, "Tamam");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task AcceptOfferAsync(Message message)
        {
            if (message == null) return;
            
            // Ã–zel transaction bul (karÄ±ÅŸÄ±klÄ±ÄŸÄ± Ã¶nlemek iÃ§in mesaj bilgisini sÃ¼z)
            Transaction targetTransaction = ActiveTransaction;
            if (!string.IsNullOrEmpty(message.RelatedTransactionId))
            {
                var myOffersResult = await _transactionService.GetMyOffersAsync(_currentUser?.UserId ?? "");
                if (myOffersResult.Success && myOffersResult.Data != null)
                {
                    var specificTx = myOffersResult.Data.FirstOrDefault(t => t.TransactionId == message.RelatedTransactionId);
                    if (specificTx != null)
                    {
                        targetTransaction = specificTx;
                    }
                }
            }

            if (targetTransaction == null) 
            {
                await Application.Current!.MainPage!.DisplayAlert("Hata", "Aktif iÅŸlem yÃ¼klenemedi. LÃ¼tfen sayfayÄ± yenile.", "Tamam");
                return;
            }
            if (_currentUser == null) return;
            
            // EÄŸer teklif zaten kabul edildiyse veya reddedildiyse iÅŸlem yapma
            if (targetTransaction.Status != TransactionStatus.Pending || !targetTransaction.IsNegotiating)
            {
                 await Application.Current!.MainPage!.DisplayAlert("UyarÄ±", "Bu pazarlÄ±k zaten sonuÃ§lanmÄ±ÅŸ.", "Tamam");
                 return;
            }

            try
            {
                var confirm = await Application.Current!.MainPage!.DisplayAlert(
                    $"Onay ({targetTransaction.ProductTitle})",
                    $"{message.ProposedPrice:N2}â‚º teklifi kabul etmek istediÄŸinize emin misiniz?",
                    "Kabul Et",
                    "Ä°ptal"
                );

                if (!confirm) return;

                IsLoading = true;

                var result = await _transactionService.AcceptNegotiatedPriceAsync(targetTransaction.TransactionId, _currentUser.UserId);

                if (result.Success)
                {
                    await LoadChatAsync(); // State'i gÃ¼ncelle
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert("Hata", result.Message, "Tamam");
                }
            }
            catch (Exception ex)
            {
                await Application.Current!.MainPage!.DisplayAlert("Hata", ex.Message, "Tamam");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private bool _disposed = false;
        
        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                KamPay.Helpers.AppLogger.DebugLog("ğŸ§¹ ChatViewModel dispose ediliyor...");

                // Event subscription'Ä± temizle
                _userStateService.UserProfileChanged -= OnUserProfileChanged;

                if (!string.IsNullOrEmpty(_activeConversationId))
                {
                    SaveToCache(_activeConversationId);
                }

                // âœ… EKLEME: Listener temizliÄŸi
                _messagesSubscription?.Dispose();
                _messagesSubscription = null;
                _isListenerActive = false;
                _initialLoadComplete = false;

                // âœ… EKLEME: Timer temizliÄŸi
                _cacheCleanupTimer?.Stop();
                _cacheCleanupTimer?.Dispose();
                _cacheCleanupTimer = null;
                
                KamPay.Helpers.AppLogger.DebugLog("âœ… ChatViewModel resources disposed");
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âš ï¸ ChatViewModel dispose hatasÄ±: {ex.Message}");
            }
            finally
            {
                _disposed = true;
            }
        }

        // Public helper metodlar
        public static void ClearCache()
        {
            _conversationCache.Clear();
            KamPay.Helpers.AppLogger.DebugLog("âœ… TÃ¼m chat cache temizlendi");
        }

        public static void ClearOldCache(int maxAgeMinutes = 30)
        {
            var now = DateTime.UtcNow;
            var oldKeys = _conversationCache
                .Where(kvp => (now - kvp.Value.CachedAt).TotalMinutes > maxAgeMinutes)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in oldKeys)
            {
                _conversationCache.Remove(key);
            }

            // âœ… EKLEME: Boyut limiti kontrolÃ¼
            int removedOldestCount = 0;
            if (_conversationCache.Count > MaxCachedConversations)
            {
                var oldestItems = _conversationCache
                    .OrderBy(kvp => kvp.Value.CachedAt)
                    .Take(_conversationCache.Count - MaxCachedConversations)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in oldestItems)
                {
                    _conversationCache.Remove(key);
                }

                removedOldestCount = oldestItems.Count;
            }

            if (oldKeys.Any() || removedOldestCount > 0)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âœ… Cache temizlendi: {oldKeys.Count} eski, {removedOldestCount} fazla Ã¶ÄŸe silindi");
            }
        }

        // ğŸ“· FotoÄŸraf GÃ¶nderme KomutlarÄ±

        [RelayCommand]
        private async Task PickImageAsync()
        {
            if (IsUploadingImage) return;

            try
            {
                var result = await MediaPicker.PickPhotoAsync(new MediaPickerOptions
                {
                    Title = "FotoÄŸraf SeÃ§in"
                });

                if (result != null)
                {
                    SelectedImagePath = result.FullPath;
                    await SendImageMessageAsync(result.FullPath);
                }
            }
            catch (FeatureNotSupportedException)
            {
                await Application.Current!.MainPage!.DisplayAlert("Hata", "Bu cihazda fotoÄŸraf seÃ§me desteklenmiyor.", "Tamam");
            }
            catch (PermissionException)
            {
                await Application.Current!.MainPage!.DisplayAlert("Hata", "Galeri eriÅŸim izni gerekli.", "Tamam");
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âŒ PickImage hatasÄ±: {ex.Message}");
                await Application.Current!.MainPage!.DisplayAlert("Hata", "FotoÄŸraf seÃ§ilemedi.", "Tamam");
            }
        }

        [RelayCommand]
        private async Task TakePhotoAsync()
        {
            if (IsUploadingImage) return;

            try
            {
                if (!MediaPicker.IsCaptureSupported)
                {
                    await Application.Current!.MainPage!.DisplayAlert("Hata", "Bu cihazda kamera desteklenmiyor.", "Tamam");
                    return;
                }

                var result = await MediaPicker.CapturePhotoAsync(new MediaPickerOptions
                {
                    Title = "FotoÄŸraf Ã‡ekin"
                });

                if (result != null)
                {
                    SelectedImagePath = result.FullPath;
                    await SendImageMessageAsync(result.FullPath);
                }
            }
            catch (FeatureNotSupportedException)
            {
                await Application.Current!.MainPage!.DisplayAlert("Hata", "Bu cihazda kamera desteklenmiyor.", "Tamam");
            }
            catch (PermissionException)
            {
                await Application.Current!.MainPage!.DisplayAlert("Hata", "Kamera eriÅŸim izni gerekli.", "Tamam");
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âŒ TakePhoto hatasÄ±: {ex.Message}");
                await Application.Current!.MainPage!.DisplayAlert("Hata", "FotoÄŸraf Ã§ekilemedi.", "Tamam");
            }
        }

        private async Task SendImageMessageAsync(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath) || _currentUser == null || Conversation == null)
            {
                return;
            }

            // HÄ±z SÄ±nÄ±rÄ± KontrolÃ¼
            var limitCheck = KamPay.Helpers.SecureRateLimiters.ImageUpload.CheckRequest(_currentUser.UserId);
            if (!limitCheck.IsAllowed)
            {
                await Application.Current!.MainPage!.DisplayAlert(Res["Error"], limitCheck.Message, Res["Ok"]);
                return;
            }
            try
            {
                IsUploadingImage = true;

                // 1ï¸âƒ£ Temp mesaj oluÅŸtur (loading state ile)
                var tempMessage = new Message
                {
                    MessageId = $"temp_{Guid.NewGuid()}",
                    ConversationId = ConversationId,
                    SenderId = _currentUser.UserId,
                    SenderName = _currentUser.FullName,
                    SenderPhotoUrl = _currentUser.ProfileImageUrl,
                    ReceiverId = Conversation.GetOtherUserId(_currentUser.UserId) ?? string.Empty,
                    ReceiverName = Conversation.GetOtherUserName(_currentUser.UserId),
                    ReceiverPhotoUrl = Conversation.GetOtherUserPhotoUrl(_currentUser.UserId),
                    Content = "ğŸ“· FotoÄŸraf",
                    Type = MessageType.Image,
                    ImageUrl = imagePath, // GeÃ§ici olarak local path gÃ¶ster
                    IsImageLoading = true,
                    ProductId = Conversation.ProductId,
                    ProductTitle = Conversation.ProductTitle,
                    ProductThumbnail = Conversation.ProductThumbnail,
                    IsSentByMe = true,
                    SentAt = DateTime.UtcNow,
                    IsDelivered = false,
                    IsRead = false
                };

                InsertMessageSorted(tempMessage);
                WeakReferenceMessenger.Default.Send(new ScrollToChatMessage(tempMessage));

                // 2ï¸âƒ£ Firebase Storage'a yÃ¼kle
                var uploadResult = await _storageService.UploadMessageImageAsync(imagePath, ConversationId);

                if (!uploadResult.Success)
                {
                    Messages.Remove(tempMessage);
                    await Application.Current!.MainPage!.DisplayAlert("Hata", uploadResult.Message ?? "GÃ¶rsel yÃ¼klenemedi.", "Tamam");
                    return;
                }

                // 3ï¸âƒ£ Mesaj olarak gÃ¶nder
                var receiverId = Conversation.GetOtherUserId(_currentUser.UserId);
                if (string.IsNullOrEmpty(receiverId))
                {
                    Messages.Remove(tempMessage);
                    await Application.Current!.MainPage!.DisplayAlert("Hata", "AlÄ±cÄ± bilgisi bulunamadÄ±.", "Tamam");
                    return;
                }

                var request = new SendMessageRequest
                {
                    ReceiverId = receiverId,
                    Content = "ğŸ“· FotoÄŸraf",
                    Type = MessageType.Image,
                    ProductId = Conversation.ProductId,
                    ImageUrl = uploadResult.Data
                };

                var sendResult = await _messagingService.SendMessageAsync(request, _currentUser);

                if (!sendResult.Success)
                {
                    Messages.Remove(tempMessage);
                    await Application.Current!.MainPage!.DisplayAlert("Hata", sendResult.Message ?? "Mesaj gÃ¶nderilemedi.", "Tamam");
                }
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âŒ SendImageMessage hatasÄ±: {ex.Message}");
                await Application.Current!.MainPage!.DisplayAlert("Hata", "FotoÄŸraf gÃ¶nderilemedi.", "Tamam");
            }
            finally
            {
                IsUploadingImage = false;
                SelectedImagePath = null;
            }
        }

        [RelayCommand]
        private async Task ViewImageAsync(string imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl)) return;

            try
            {
                await Shell.Current.GoToAsync($"{nameof(ImageViewerPage)}?photoUrl={Uri.EscapeDataString(imageUrl)}");
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âŒ ViewImage hatasÄ±: {ex.Message}");
            }
        }

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
                    KamPay.Helpers.AppLogger.DebugLog($"âœ… Profil servisi fotoÄŸrafÄ±nÄ± yÃ¼kledi: {OtherUserPhoto}");
                }
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âš ï¸ EnsureOtherUserPhotoAsync hata: {ex.Message}");
            }
        }
    }

    //  : Cache state modeli
    public class ConversationState
    {
        public required List<Message> Messages { get; set; }
        public Conversation? Conversation { get; set; }
        public required string OtherUserName { get; set; }
        public required string OtherUserPhoto { get; set; }
        public DateTime CachedAt { get; set; }
        public DateTime LastAccessedAt { get; set; } //  LRU iÃ§in
    }
}

