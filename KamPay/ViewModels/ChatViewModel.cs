using System;
using System.Collections.Generic;
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

namespace KamPay.ViewModels
{
    [QueryProperty(nameof(ConversationId), "conversationId")]
    public partial class ChatViewModel : ObservableObject, IDisposable
    {
        private readonly IMessagingService _messagingService;
        private readonly IAuthenticationService _authService;
        private readonly IUserStateService _userStateService;
        private readonly IStorageService _storageService;
        private readonly FirebaseClient _firebaseClient = new(Constants.FirebaseRealtimeDbUrl);

        // 🔥 CACHE: Her konuşma için ayrı state
        private static readonly Dictionary<string, ConversationState> _conversationCache = new();

        // 🔥 CACHE: Otomatik temizleme için timer
        private static System.Timers.Timer _cacheCleanupTimer;
        private const int MaxCacheAgeMinutes = 15; // 15 dakikadan eski cache'leri temizle
        private const int MaxCachedConversations = 10; // Maksimum 10 konuşma cache'le

        private IDisposable _messagesSubscription;
        private bool _isListenerActive = false;
        private string _activeConversationId;
        private bool _initialLoadComplete = false;

        [ObservableProperty]
        private string conversationId;

        [ObservableProperty]
        private Conversation conversation;

        [ObservableProperty]
        private string otherUserName;

        [ObservableProperty]
        private string otherUserPhoto;

        [ObservableProperty]
        private string messageText;

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
        private string selectedImagePath;

        public ObservableCollection<Message> Messages { get; } = new();

        private User _currentUser;

        public ChatViewModel(IMessagingService messagingService, IAuthenticationService authService, IUserStateService userStateService, IStorageService storageService)
        {
            _messagingService = messagingService;
            _authService = authService;
            _userStateService = userStateService;
            _storageService = storageService;

            // Kullanıcı profil değişikliklerini dinle
            _userStateService.UserProfileChanged += OnUserProfileChanged;

            // 🔥 Static timer başlat (sadece bir kez)
            if (_cacheCleanupTimer == null)
            {
                _cacheCleanupTimer = new System.Timers.Timer(TimeSpan.FromMinutes(5).TotalMilliseconds);
                _cacheCleanupTimer.Elapsed += (s, e) => CleanupOldCache();
                _cacheCleanupTimer.Start();
            }
        }

        private void OnUserProfileChanged(object sender, User updatedUser)
        {
            if (updatedUser == null) return;

            // 🔥 Kritik: UI'da anlık güncelleme için MainThread'de çalıştırılmalıdır.
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Diğer kullanıcının bilgilerini güncelle
                if (Conversation != null && _currentUser != null)
                {
                    var otherUserId = Conversation.GetOtherUserId(_currentUser.UserId);
                    if (otherUserId == updatedUser.UserId)
                    {
                        OtherUserName = updatedUser.FullName;
                        OtherUserPhoto = updatedUser.ProfileImageUrl;
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
            // 🔥 CACHE: Aynı konuşma için tekrar yükleme yapma
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

                // 🔥 Eski konuşmadan geliyorsak kaydet
                if (_activeConversationId != null &&
                    _activeConversationId != ConversationId &&
                    _initialLoadComplete)
                {
                    SaveToCache(_activeConversationId);
                    CleanupCurrentConversation();
                }

                // Kullanıcı bilgisi (singleton pattern)
                if (_currentUser == null)
                {
                    _currentUser = await _authService.GetCurrentUserAsync();
                    if (_currentUser == null)
                    {
                        await Application.Current.MainPage.DisplayAlert("Hata", "Giriş yapmış kullanıcı bulunamadı.", "Tamam");
                        await Shell.Current.GoToAsync("..");
                        return;
                    }
                }

                // 🔥 CACHE: Cache'de varsa oradan yükle
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

                // 🔥 İlk kez yükleniyor
                Console.WriteLine($"🔥 İlk yükleme: {ConversationId}");

                // Konuşma bilgileri
                if (Conversation == null || Conversation.ConversationId != ConversationId)
                {
                    var conversations = await _messagingService.GetUserConversationsAsync(_currentUser.UserId);
                    Conversation = conversations.Data?.FirstOrDefault(c => c.ConversationId == ConversationId);

                    if (Conversation != null)
                    {
                        OtherUserName = Conversation.GetOtherUserName(_currentUser.UserId);
                        OtherUserPhoto = Conversation.GetOtherUserPhotoUrl(_currentUser.UserId);
                    }
                }

                // 🔥 UltraFastLoad: Snapshot ile anında mesajları yükle
                await LoadMessagesWithSnapshotAsync();

                _activeConversationId = ConversationId;

                await _messagingService.MarkMessagesAsReadAsync(ConversationId, _currentUser.UserId);
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Hata", ex.Message, "Tamam");
                Console.WriteLine($"❌ LoadChatAsync hatası: {ex.Message}");
                IsLoading = false;
            }
        }

        // 🔥 UltraFastLoad: Snapshot ile mesajları anında yükle
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
                    var loadedMessages = messagesSnapshot
                        .Where(m => m.Object != null && !m.Object.IsDeleted)
                        .Select(m =>
                        {
                            var message = m.Object;
                            message.MessageId = m.Key;
                            message.IsSentByMe = message.SenderId == _currentUser.UserId;
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

        // 🔥 OPTİMİZE: Cache kaydetme (LRU pattern)
        private void SaveToCache(string conversationId)
        {
            if (string.IsNullOrEmpty(conversationId)) return;

            // 🔥 LRU: Maksimum cache sayısını kontrol et
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

        // 🔥 Cache'den geri yükleme
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

        // 🔥 Mevcut konuşmayı temizle
        private void CleanupCurrentConversation()
        {
            _messagesSubscription?.Dispose();
            _messagesSubscription = null;
            _isListenerActive = false;
            _initialLoadComplete = false;
        }

        // 🔥 Pull-to-Refresh
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

        // 🔥 OPTİMİZE: 200ms buffer + batch processing
        private void StartListeningToMessages()
        {
            if (_isListenerActive)
            {
                Console.WriteLine("⚠️ Listener zaten aktif, yeniden başlatılmadı.");
                return;
            }

            Console.WriteLine($"🔥 Real-time listener başlatıldı: {ConversationId}");

            _messagesSubscription = _firebaseClient
                .Child(Constants.MessagesCollection)
                .Child(ConversationId)
                .AsObservable<Message>()
                .Where(e => e.Object != null && !e.Object.IsDeleted)
                .Buffer(TimeSpan.FromMilliseconds(200))
                .Where(batch => batch.Any())
                .Subscribe(
                    events =>
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            try
                            {
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

        // 🔥 Batch processing
        private void ProcessMessageBatch(IList<Firebase.Database.Streaming.FirebaseEvent<Message>> events)
        {
            bool shouldScroll = false;
            Message lastNewMessage = null;

            foreach (var e in events)
            {
                var message = e.Object;
                message.MessageId = e.Key;
                message.IsSentByMe = message.SenderId == _currentUser.UserId;

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
                            _ = _messagingService.MarkMessagesAsReadAsync(ConversationId, _currentUser.UserId);
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

        // 🔥 Binary search insert (optimize edilmiş)
        private void InsertMessageSorted(Message newMessage)
        {
            // Temp mesajı bul ve kaldır
            var tempMessage = Messages.FirstOrDefault(m => m.MessageId.StartsWith("temp_") &&
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

            var messageContent = MessageText.Trim();
            MessageText = string.Empty;

            var tempMessage = new Message
            {
                MessageId = $"temp_{Guid.NewGuid()}",
                ConversationId = ConversationId,
                SenderId = _currentUser.UserId,
                SenderName = _currentUser.FullName,
                SenderPhotoUrl = _currentUser.ProfileImageUrl,
                ReceiverId = Conversation.GetOtherUserId(_currentUser.UserId),
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

                var request = new SendMessageRequest
                {
                    ReceiverId = Conversation.GetOtherUserId(_currentUser.UserId),
                    Content = messageContent,
                    Type = MessageType.Text,
                    ProductId = Conversation.ProductId
                };

                if (string.IsNullOrEmpty(request.ReceiverId))
                {
                    Messages.Remove(tempMessage);
                    await Application.Current.MainPage.DisplayAlert("Hata", "Alıcı bilgisi bulunamadı.", "Tamam");
                    MessageText = messageContent;
                    return;
                }

                var result = await _messagingService.SendMessageAsync(request, _currentUser);

                if (!result.Success)
                {
                    Messages.Remove(tempMessage);
                    await Application.Current.MainPage.DisplayAlert("Hata", result.Message ?? "Mesaj gönderilemedi", "Tamam");
                    MessageText = messageContent;
                }
            }
            catch (Exception ex)
            {
                Messages.Remove(tempMessage);
                await Application.Current.MainPage.DisplayAlert("Hata", ex.Message, "Tamam");
                MessageText = messageContent;
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

        // 🔥 Otomatik cache temizleme
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

        public void Dispose()
        {
            Console.WriteLine("🧹 ChatViewModel dispose ediliyor...");

            // Event subscription'ı temizle
            _userStateService.UserProfileChanged -= OnUserProfileChanged;

            if (!string.IsNullOrEmpty(_activeConversationId))
            {
                SaveToCache(_activeConversationId);
            }

            _messagesSubscription?.Dispose();
            _messagesSubscription = null;
            _isListenerActive = false;
            _initialLoadComplete = false;
        }

        // Public helper metodlar
        public static void ClearCache()
        {
            _conversationCache.Clear();
            Console.WriteLine("🗑️ Tüm cache temizlendi");
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

            if (oldKeys.Any())
            {
                Console.WriteLine($"🗑️ {oldKeys.Count} eski cache temizlendi");
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
                await Application.Current.MainPage.DisplayAlert("Hata", "Bu cihazda fotoğraf seçme desteklenmiyor.", "Tamam");
            }
            catch (PermissionException)
            {
                await Application.Current.MainPage.DisplayAlert("Hata", "Galeri erişim izni gerekli.", "Tamam");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ PickImage hatası: {ex.Message}");
                await Application.Current.MainPage.DisplayAlert("Hata", "Fotoğraf seçilemedi.", "Tamam");
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
                    await Application.Current.MainPage.DisplayAlert("Hata", "Bu cihazda kamera desteklenmiyor.", "Tamam");
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
                await Application.Current.MainPage.DisplayAlert("Hata", "Bu cihazda kamera desteklenmiyor.", "Tamam");
            }
            catch (PermissionException)
            {
                await Application.Current.MainPage.DisplayAlert("Hata", "Kamera erişim izni gerekli.", "Tamam");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ TakePhoto hatası: {ex.Message}");
                await Application.Current.MainPage.DisplayAlert("Hata", "Fotoğraf çekilemedi.", "Tamam");
            }
        }

        private async Task SendImageMessageAsync(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath) || _currentUser == null || Conversation == null)
            {
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
                    ReceiverId = Conversation.GetOtherUserId(_currentUser.UserId),
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
                    await Application.Current.MainPage.DisplayAlert("Hata", uploadResult.Message ?? "Görsel yüklenemedi.", "Tamam");
                    return;
                }

                // 3️⃣ Mesaj olarak gönder
                var request = new SendMessageRequest
                {
                    ReceiverId = Conversation.GetOtherUserId(_currentUser.UserId),
                    Content = "📷 Fotoğraf",
                    Type = MessageType.Image,
                    ProductId = Conversation.ProductId,
                    ImageUrl = uploadResult.Data
                };

                var sendResult = await _messagingService.SendMessageAsync(request, _currentUser);

                if (!sendResult.Success)
                {
                    Messages.Remove(tempMessage);
                    await Application.Current.MainPage.DisplayAlert("Hata", sendResult.Message ?? "Mesaj gönderilemedi.", "Tamam");
                }
                // 4️⃣ Real-time listener mesajı güncelleyecek
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ SendImageMessage hatası: {ex.Message}");
                await Application.Current.MainPage.DisplayAlert("Hata", "Fotoğraf gönderilemedi.", "Tamam");
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
                await Shell.Current.GoToAsync($"{nameof(ImageViewerPage)}?imageUrl={Uri.EscapeDataString(imageUrl)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ViewImage hatası: {ex.Message}");
            }
        }
    }

    // 🔥 OPTİMİZE: Cache state modeli
    public class ConversationState
    {
        public List<Message> Messages { get; set; }
        public Conversation Conversation { get; set; }
        public string OtherUserName { get; set; }
        public string OtherUserPhoto { get; set; }
        public DateTime CachedAt { get; set; }
        public DateTime LastAccessedAt { get; set; } // 🔥 LRU için
    }
}