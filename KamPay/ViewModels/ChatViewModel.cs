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
        private readonly ITransactionService _transactionService; // ✅ EKLENEN
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

        // Fotoğraf yükleme özellikleri
        [ObservableProperty]
        private bool isUploadingImage;

        [ObservableProperty]
        private string? selectedImagePath;

        // ✅ Observable property olarak değiştirildi
        [ObservableProperty]
        private string otherUserPhoto = string.Empty;

        // ✅ Observable property olarak değiştirildi
        [ObservableProperty]
        private string otherUserName = string.Empty;

        public ObservableCollection<Message> Messages { get; set; } = new();
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
            ITransactionService transactionService, // ✅ EKLENEN
            FirebaseClient firebaseClient)
        {
            _messagingService = messagingService;
            _authService = authService;
            _userStateService = userStateService;
            _storageService = storageService;
            _userProfileService = userProfileService;
            _transactionService = transactionService; // ✅ EKLENEN
            _firebaseClient = firebaseClient;

            // Kullanıcı profil değişikliklerini dinle
            _userStateService.UserProfileChanged += OnUserProfileChanged;

            //  Static timer başlat (sadece bir kez)
            _cacheCleanupTimer = new System.Timers.Timer(TimeSpan.FromMinutes(5).TotalMilliseconds);
            _cacheCleanupTimer.Elapsed += (s, e) => CleanupOldCache();
            _cacheCleanupTimer.Start();
        }

        private void OnUserProfileChanged(object? sender, User updatedUser)
        {
            if (updatedUser == null) return;

            //  Kritik: UI'da anlık güncelleme için MainThread'de çalıştırılmalıdır.
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Diğer kullanıcının bilgilerini güncelle
                if (Conversation != null && _currentUser != null)
                {
                    var otherUserId = Conversation.GetOtherUserId(_currentUser.UserId);
                    if (otherUserId == updatedUser.UserId)
                    {
                        OtherUserName = updatedUser.FullName ?? string.Empty;
                        OtherUserPhoto = updatedUser.ProfileImageUrl ?? string.Empty;
                    }
                }

                // Mesajlardaki kullanıcı bilgilerini güncelle
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
            //  CACHE: Aynı konuşma için tekrar yükleme yapma
            if (_activeConversationId == ConversationId && _initialLoadComplete)
            {
                Console.WriteLine($"⚡ Cache'den yükleniyor: {ConversationId}");

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

                //  Eski konuşmadan geliyorsak kaydet
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

                //  CACHE: Cache'de varsa oradan yükle
                if (_conversationCache.TryGetValue(ConversationId, out var cachedState))
                {
                    Console.WriteLine($"📦 Cache'den yüklendi: {ConversationId}");
                    RestoreFromCache(cachedState);
                    _activeConversationId = ConversationId;
                    _initialLoadComplete = true;
                    IsLoading = false;

                    await _messagingService.MarkMessagesAsReadAsync(ConversationId, _currentUser.UserId);
                    return;
                }

                //  İlk kez yükleniyor
                Console.WriteLine($" İlk yükleme: {ConversationId}");

                // Konuşma bilgileri
                if (Conversation == null || Conversation.ConversationId != ConversationId)
                {
                    var conversations = await _messagingService.GetUserConversationsAsync(_currentUser.UserId);
                    Conversation = conversations.Data?.FirstOrDefault(c => c.ConversationId == ConversationId);

                    if (Conversation != null)
                    {
                        // Navigation parametresinden URL-decoded değeri al
                        var photoFromNav = System.Net.WebUtility.UrlDecode(OtherUserPhoto ?? string.Empty);
                        
                        OtherUserName = Conversation.GetOtherUserName(_currentUser.UserId) ?? string.Empty;
                        
                        // Navigation'dan gelen fotoğraf varsa ve geçerliyse onu kullan
                        if (!string.IsNullOrEmpty(photoFromNav) && photoFromNav != "person_icon.svg")
                        {
                            OtherUserPhoto = photoFromNav;
                        }
                        else
                        {
                            // Konuşmadan fotoğraf al
                            OtherUserPhoto = Conversation.GetOtherUserPhotoUrl(_currentUser.UserId) ?? "person_icon.svg";
                        }

                        Console.WriteLine($"👤 OtherUserName: {OtherUserName}");
                        Console.WriteLine($"📷 OtherUserPhoto: {OtherUserPhoto}");

                        // / Online durumunu sorgula - Users koleksiyonundan LastLoginAt kontrolü
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
                                    OnlineStatusText = IsOtherUserOnline ? "Çevrimiçi" : $"Son görülme: {last.ToLocalTime():g}";

                                    // Profil fotoğrafı boşsa Firebase'den al
                                    if ((string.IsNullOrEmpty(OtherUserPhoto) || OtherUserPhoto == "person_icon.svg") && 
                                        !string.IsNullOrEmpty(otherUser.ProfileImageUrl))
                                    {
                                        OtherUserPhoto = otherUser.ProfileImageUrl;
                                        Console.WriteLine($"📷 Firebase'den fotoğraf alındı: {OtherUserPhoto}");
                                    }
                                }
                                else
                                {
                                    IsOtherUserOnline = false;
                                    OnlineStatusText = "Çevrimdışı";
                                }
                            }
                            else
                            {
                                IsOtherUserOnline = false;
                                OnlineStatusText = "Çevrimdışı";
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"⚠️ Online durumu alınamadı: {ex.Message}");
                            IsOtherUserOnline = false;
                            OnlineStatusText = "Çevrimdışı";
                        }

                        // ✅ Fallback: Kullanıcı profil servisi ile fotoğraf yükle
                        await EnsureOtherUserPhotoAsync();

                        // ✅ LOAD ACTIVE TRANSACTION (Daha güvenli yöntem)
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
                                    Console.WriteLine($"✅ ActiveTransaction loaded: {match.TransactionId}");
                                }
                                else
                                {
                                    Console.WriteLine($"⚠️ Bu konuşmaya bağlı işlem bulunamadı: {ConversationId}");
                                    // Belki henüz conversationId set edilmemiş bir transaction var? ProductId filtresiyle de bakılabilir
                                    if (Conversation != null && !string.IsNullOrEmpty(Conversation.ProductId))
                                    {
                                        var fallbackMatch = myOffersResult.Data.FirstOrDefault(t => t.ProductId == Conversation.ProductId && (t.Status == TransactionStatus.Pending || t.IsNegotiating));
                                        if (fallbackMatch != null)
                                        {
                                            ActiveTransaction = fallbackMatch;
                                            HasActiveTransaction = true;
                                            Console.WriteLine($"✅ ActiveTransaction loaded via Fallback ProductId: {fallbackMatch.TransactionId}");
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"⚠️ Could not load active transaction: {ex.Message}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"⚠️ Conversation bulunamadı: {ConversationId}");
                    }
                }

                //  UltraFastLoad: Snapshot ile anında mesajları yükle
                await LoadMessagesWithSnapshotAsync();

                _activeConversationId = ConversationId;

                await _messagingService.MarkMessagesAsReadAsync(ConversationId, _currentUser.UserId);
            }
            catch (Exception ex)
            {
                await Application.Current!.MainPage!.DisplayAlert("Hata", ex.Message, "Tamam");
                Console.WriteLine($"❌ LoadChatAsync hatası: {ex.Message}");
                IsLoading = false;
            }
        }

        //  UltraFastLoad: Snapshot ile mesajları anında yükle
        private async Task LoadMessagesWithSnapshotAsync()
        {
            try
            {
                Console.WriteLine($"📸 Snapshot ile mesaj yükleme başlıyor: {ConversationId}");

                // 1️⃣ SNAPSHOT: Tüm mesajları anında çek
                var messagesSnapshot = await _firebaseClient
                    .Child(Constants.MessagesCollection)
                    .Child(ConversationId)
                    .OnceAsync<Message>();

                if (messagesSnapshot.Any())
                {
                    //  : Sistem mesajlarını da dahil et
                    var loadedMessages = messagesSnapshot
                        .Where(m => m.Object != null && !m.Object.IsDeleted) // Sadece silinen mesajları filtrele
                        .Select(m =>
                        {
                            var message = m.Object;
                            message.MessageId = m.Key;
                            message.IsSentByMe = message.SenderId == _currentUser!.UserId;
                            
                            //  DEBUG: Sistem mesajı kontrolü
                            if (message.Type == MessageType.System || message.IsSystemMessage)
                            {
                                Console.WriteLine($"📋 Sistem mesajı yüklendi: {message.Content}");
                            }
                            
                            return message;
                        })
                        .OrderBy(m => m.SentAt)
                        .ToList();

                    // UI'a ekle
                    Messages.Clear();
                    foreach (var message in loadedMessages)
                    {
                        Messages.Add(message);
                    }

                    Console.WriteLine($"✅ Snapshot ile {loadedMessages.Count} mesaj yüklendi");
                }

                // 2️⃣ Loading'i kapat - veri gösterildi
                _initialLoadComplete = true;
                IsLoading = false;

                // 3️⃣ Scroll to bottom
                WeakReferenceMessenger.Default.Send(new ScrollToChatMessage(null));

                // 4️⃣ REALTIME: Listener başlat (yeni mesajlar için)
                StartListeningToMessages();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ LoadMessagesWithSnapshotAsync hatası: {ex.Message}");
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

            //  LRU: Maksimum cache sayısını kontrol et
            if (_conversationCache.Count >= MaxCachedConversations)
            {
                var oldestKey = _conversationCache
                    .OrderBy(kvp => kvp.Value.LastAccessedAt)
                    .First().Key;

                _conversationCache.Remove(oldestKey);
                Console.WriteLine($"🗑️ LRU: En eski cache temizlendi: {oldestKey}");
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
            Console.WriteLine($"💾 Cache'e kaydedildi: {conversationId} ({state.Messages.Count} mesaj)");
        }

        //  Cache'den geri yükleme
        private void RestoreFromCache(ConversationState state)
        {
            // Cache yaşını kontrol et
            if ((DateTime.UtcNow - state.CachedAt).TotalMinutes > MaxCacheAgeMinutes)
            {
                Console.WriteLine("⚠️ Cache eski, yeniden yükleniyor...");
                _conversationCache.Remove(ConversationId);
                _initialLoadComplete = false;
                _ = Task.Run(() => LoadChatAsync());
                return;
            }

            // Last accessed time güncelle (LRU için)
            state.LastAccessedAt = DateTime.UtcNow;

            Messages.Clear();
            foreach (var msg in state.Messages)
            {
                Messages.Add(msg);
            }

            Conversation = state.Conversation;
            OtherUserName = state.OtherUserName;
            OtherUserPhoto = state.OtherUserPhoto;

            // Listener'ı yeniden başlat
            StartListeningToMessages();

            Console.WriteLine($"✅ Cache'den geri yüklendi: {Messages.Count} mesaj");
        }

        //  Mevcut konuşmayı temizle
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

                // Listener'ı yeniden başlat
                CleanupCurrentConversation();
                Messages.Clear();

                _initialLoadComplete = false;
                StartListeningToMessages();

                await Task.Delay(500);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Refresh hatası: {ex.Message}");
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
                Console.WriteLine("⚠️ Listener zaten aktif, yeniden başlatılmadı.");
                return;
            }

            Console.WriteLine($" Real-time listener başlatıldı: {ConversationId}");

            //  : Sistem mesajlarını da dahil et
            _messagesSubscription = _firebaseClient
                .Child(Constants.MessagesCollection)
                .Child(ConversationId)
                .AsObservable<Message>()
                .Where(e => e.Object != null && !e.Object.IsDeleted) // Sadece silinen mesajları filtrele
                .Buffer(TimeSpan.FromMilliseconds(200))
                .Where(batch => batch.Any())
                .Subscribe(
                    events =>
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            try
                            {
                                //  DEBUG: Sistem mesajı kontrolü
                                foreach (var e in events)
                                {
                                    if (e.Object != null && (e.Object.Type == MessageType.System || e.Object.IsSystemMessage))
                                    {
                                        Console.WriteLine($"📋 Realtime sistem mesajı: {e.Object.Content}");
                                    }
                                }
                                
                                ProcessMessageBatch(events);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"❌ Message batch hatası: {ex.Message}");
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
                        Console.WriteLine($"❌ Firebase message listener hatası: {error.Message}");
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

        //  Binary search insert (optimize edilmiş)
        private void InsertMessageSorted(Message newMessage)
        {
            // Temp mesajı bul ve kaldır
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

            // GÜVENLİK: Mesaj içeriğini XSS saldırılarına karşı temizle
            var messageContent = InputSanitizer.SanitizeText(MessageText.Trim());

            // Eğer temizleme sonrası mesaj tamamen boşaldıysa işlemi iptal et
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
                    await Application.Current!.MainPage!.DisplayAlert("Hata", "Alıcı bilgisi bulunamadı.", "Tamam");
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
                    // ✅ Başarılı gönderim sonrası input temizle
                    MessageText = string.Empty;
                }
                else
                {
                    Messages.Remove(tempMessage);
                    await Application.Current!.MainPage!.DisplayAlert("Hata", result.Message ?? "Mesaj gönderilemedi", "Tamam");
                }
            }
            catch (Exception ex)
            {
                Messages.Remove(tempMessage);
                await Application.Current!.MainPage!.DisplayAlert("Hata", ex.Message, "Tamam");
                Console.WriteLine($"❌ SendMessage hatası: {ex.Message}");
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
                Console.WriteLine($"🗑️ {oldKeys.Count} eski cache otomatik temizlendi");
            }
        }

        // ✅ IN-CHAT NEGOTIATION COMMANDS

        [RelayCommand]
        private async Task ProposeOfferAsync(Message message)
        {
            // Kullanılacak özel transaction (Balon üzerinden geliyorsa kendi transaction'ı, alt butondan geliyorsa genel ActiveTransaction)
            Transaction targetTransaction = ActiveTransaction;

            if (message != null && !string.IsNullOrEmpty(message.RelatedTransactionId))
            {
                // Eğer buton, bir teklif balonundan tetiklendiyse ve balonda işlem ID'si varsa onu kullan
                // Mevcut listedeki işlemler arasından bulmayı deneriz
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
                await Application.Current!.MainPage!.DisplayAlert("Hata", "Aktif işlem (transaction) yüklenemedi. Lütfen sayfayı yenileyin veya tekrar girin.", "Tamam");
                return;
            }
            if (_currentUser == null) return;

            try
            {
                var result = await Application.Current!.MainPage!.DisplayPromptAsync(
                    $"💰 Fiyat Teklifi ({targetTransaction.ProductTitle})",
                    "Teklif etmek istediğiniz tutarı girin (₺):",
                    Res["SendButton"] ?? "Gönder",
                    Res["Cancel"] ?? "İptal",
                    "Örn: 500",
                    keyboard: Keyboard.Numeric
                );

                if (string.IsNullOrWhiteSpace(result)) return;

                if (!decimal.TryParse(result, out var proposedPrice) || proposedPrice <= 0)
                {
                    await Application.Current.MainPage.DisplayAlert("Hata", "Geçerli bir tutar giriniz.", "Tamam");
                    return;
                }

                IsLoading = true;

                // Satıcı mı Alıcı mı?
                if (targetTransaction.SellerId == _currentUser.UserId)
                {
                    // Satıcı ise CounterOffer gönder
                    var response = await _transactionService.SendCounterOfferForSaleAsync(targetTransaction.TransactionId, proposedPrice, _currentUser.UserId);
                    if (!response.Success)
                    {
                        await Application.Current.MainPage.DisplayAlert("Hata", response.Message, "Tamam");
                    }
                }
                else
                {
                    // Alıcı ise ProposePrice gönder
                    var response = await _transactionService.ProposePriceForSaleAsync(targetTransaction.TransactionId, proposedPrice, _currentUser.UserId);
                    if (!response.Success)
                    {
                        await Application.Current.MainPage.DisplayAlert("Hata", response.Message, "Tamam");
                    }
                }

                // Tekrar transaction'ı yükleyelim ki UI güncellensin
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
            
            // Özel transaction bul (karışıklığı önlemek için mesaj bilgisini süz)
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
                await Application.Current!.MainPage!.DisplayAlert("Hata", "Aktif işlem yüklenemedi. Lütfen sayfayı yenile.", "Tamam");
                return;
            }
            if (_currentUser == null) return;
            
            // Eğer teklif zaten kabul edildiyse veya reddedildiyse işlem yapma
            if (targetTransaction.Status != TransactionStatus.Pending || !targetTransaction.IsNegotiating)
            {
                 await Application.Current!.MainPage!.DisplayAlert("Uyarı", "Bu pazarlık zaten sonuçlanmış.", "Tamam");
                 return;
            }

            try
            {
                var confirm = await Application.Current!.MainPage!.DisplayAlert(
                    $"Onay ({targetTransaction.ProductTitle})",
                    $"{message.ProposedPrice:N2}₺ teklifi kabul etmek istediğinize emin misiniz?",
                    "Kabul Et",
                    "İptal"
                );

                if (!confirm) return;

                IsLoading = true;

                var result = await _transactionService.AcceptNegotiatedPriceAsync(targetTransaction.TransactionId, _currentUser.UserId);

                if (result.Success)
                {
                    await LoadChatAsync(); // State'i güncelle
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
                Console.WriteLine("🧹 ChatViewModel dispose ediliyor...");

                // Event subscription'ı temizle
                _userStateService.UserProfileChanged -= OnUserProfileChanged;

                if (!string.IsNullOrEmpty(_activeConversationId))
                {
                    SaveToCache(_activeConversationId);
                }

                // ✅ EKLEME: Listener temizliği
                _messagesSubscription?.Dispose();
                _messagesSubscription = null;
                _isListenerActive = false;
                _initialLoadComplete = false;

                // ✅ EKLEME: Timer temizliği
                _cacheCleanupTimer?.Stop();
                _cacheCleanupTimer?.Dispose();
                _cacheCleanupTimer = null;
                
                Console.WriteLine("✅ ChatViewModel resources disposed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ ChatViewModel dispose hatası: {ex.Message}");
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
            Console.WriteLine("✅ Tüm chat cache temizlendi");
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

            // ✅ EKLEME: Boyut limiti kontrolü
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
                Console.WriteLine($"✅ Cache temizlendi: {oldKeys.Count} eski, {removedOldestCount} fazla öğe silindi");
            }
        }

        // 📷 Fotoğraf Gönderme Komutları

        [RelayCommand]
        private async Task PickImageAsync()
        {
            if (IsUploadingImage) return;

            try
            {
                var result = await MediaPicker.PickPhotoAsync(new MediaPickerOptions
                {
                    Title = "Fotoğraf Seçin"
                });

                if (result != null)
                {
                    SelectedImagePath = result.FullPath;
                    await SendImageMessageAsync(result.FullPath);
                }
            }
            catch (FeatureNotSupportedException)
            {
                await Application.Current!.MainPage!.DisplayAlert("Hata", "Bu cihazda fotoğraf seçme desteklenmiyor.", "Tamam");
            }
            catch (PermissionException)
            {
                await Application.Current!.MainPage!.DisplayAlert("Hata", "Galeri erişim izni gerekli.", "Tamam");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ PickImage hatası: {ex.Message}");
                await Application.Current!.MainPage!.DisplayAlert("Hata", "Fotoğraf seçilemedi.", "Tamam");
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
                    Title = "Fotoğraf Çekin"
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
                await Application.Current!.MainPage!.DisplayAlert("Hata", "Kamera erişim izni gerekli.", "Tamam");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ TakePhoto hatası: {ex.Message}");
                await Application.Current!.MainPage!.DisplayAlert("Hata", "Fotoğraf çekilemedi.", "Tamam");
            }
        }

        private async Task SendImageMessageAsync(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath) || _currentUser == null || Conversation == null)
            {
                return;
            }

            // Hız Sınırı Kontrolü
            var limitCheck = RateLimiters.ImageUpload.CheckLimit(_currentUser.UserId);
            if (!limitCheck.IsAllowed)
            {
                await Application.Current!.MainPage!.DisplayAlert(Res["Error"], limitCheck.Message, Res["Ok"]);
                return;
            }
            try
            {
                IsUploadingImage = true;

                // 1️⃣ Temp mesaj oluştur (loading state ile)
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
                    Content = "📷 Fotoğraf",
                    Type = MessageType.Image,
                    ImageUrl = imagePath, // Geçici olarak local path göster
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

                // 2️⃣ Firebase Storage'a yükle
                var uploadResult = await _storageService.UploadMessageImageAsync(imagePath, ConversationId);

                if (!uploadResult.Success)
                {
                    Messages.Remove(tempMessage);
                    await Application.Current!.MainPage!.DisplayAlert("Hata", uploadResult.Message ?? "Görsel yüklenemedi.", "Tamam");
                    return;
                }

                // 3️⃣ Mesaj olarak gönder
                var receiverId = Conversation.GetOtherUserId(_currentUser.UserId);
                if (string.IsNullOrEmpty(receiverId))
                {
                    Messages.Remove(tempMessage);
                    await Application.Current!.MainPage!.DisplayAlert("Hata", "Alıcı bilgisi bulunamadı.", "Tamam");
                    return;
                }

                var request = new SendMessageRequest
                {
                    ReceiverId = receiverId,
                    Content = "📷 Fotoğraf",
                    Type = MessageType.Image,
                    ProductId = Conversation.ProductId,
                    ImageUrl = uploadResult.Data
                };

                var sendResult = await _messagingService.SendMessageAsync(request, _currentUser);

                if (!sendResult.Success)
                {
                    Messages.Remove(tempMessage);
                    await Application.Current!.MainPage!.DisplayAlert("Hata", sendResult.Message ?? "Mesaj gönderilemedi.", "Tamam");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ SendImageMessage hatası: {ex.Message}");
                await Application.Current!.MainPage!.DisplayAlert("Hata", "Fotoğraf gönderilemedi.", "Tamam");
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
                Console.WriteLine($"❌ ViewImage hatası: {ex.Message}");
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
                    Console.WriteLine($"✅ Profil servisi fotoğrafını yükledi: {OtherUserPhoto}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ EnsureOtherUserPhotoAsync hata: {ex.Message}");
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
        public DateTime LastAccessedAt { get; set; } //  LRU için
    }
}
