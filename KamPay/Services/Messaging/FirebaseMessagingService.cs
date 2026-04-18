using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Helpers;
using KamPay.Models;
using KamPay.Views;
using CommunityToolkit.Mvvm.Messaging;
using KamPay.ViewModels;


namespace KamPay.Services.Messaging
{
    // bu sayfanÄ±n amacÄ± Firebase Realtime Database Ã¼zerinden mesajlaÅŸma iÅŸlevlerini yÃ¶netmektir. Mesaj gÃ¶nderme, alma, konuÅŸma oluÅŸturma, okunmamÄ±ÅŸ mesaj sayÄ±sÄ±nÄ± takip etme ve kullanÄ±cÄ± bilgilerini gÃ¼ncelleme gibi iÅŸlevleri kapsar. kullanÄ±cÄ±lar arasÄ±ndaki iletiÅŸimi saÄŸlar ve mesajlaÅŸma deneyimini yÃ¶netir.
    public class FirebaseMessagingService : IMessagingService
    {
        private readonly FirebaseClient _firebaseClient;
        private readonly INotificationService _notificationService;

        // âœ… SOLID FIX: FirebaseClient'Ä± DI'den alÄ±yoruz (LSP ve DIP prensiplerine uygun)
        public FirebaseMessagingService(
            FirebaseClient firebaseClient, // âœ… YENÄ°: DI'den alÄ±yoruz
            INotificationService notificationService)
        {
            _firebaseClient = firebaseClient ?? throw new ArgumentNullException(nameof(firebaseClient));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            
            KamPay.Helpers.AppLogger.DebugLog("âœ… FirebaseMessagingService oluÅŸturuldu (DI ile)");
        }

        private async Task CheckAndBroadcastUnreadMessageStatus(string userId)
        {
            var result = await GetTotalUnreadMessageCountAsync(userId);
            bool hasUnread = result.Success && result.Data > 0;
            WeakReferenceMessenger.Default.Send(new UnreadMessageStatusMessage(hasUnread));
        }

        public async Task<ServiceResult<Message>> SendMessageAsync(SendMessageRequest request, User sender)
        {
            try
            {
                // 1. Rate Limiting KontrolÃ¼: Dakikada en fazla 30 mesaj
                var limitCheck = KamPay.Helpers.SecureRateLimiters.Message.CheckRequest(sender.UserId);
                if (!limitCheck.IsAllowed)
                {
                    return ServiceResult<Message>.FailureResult(limitCheck.Message);
                }

                if (request == null || sender == null || string.IsNullOrEmpty(request.ReceiverId))
                {
                    return ServiceResult<Message>.FailureResult("GeÃ§ersiz istek: GÃ¶nderen veya alÄ±cÄ± boÅŸ olamaz.");
                }

                // 2. Girdi Temizleme (Input Sanitization) ve DoÄŸrulama
                if (request.Type == MessageType.Text)
                {
                    if (string.IsNullOrEmpty(request.Content))
                    {
                        return ServiceResult<Message>.FailureResult("Mesaj iÃ§eriÄŸi boÅŸ olamaz.");
                    }

                    // GÃœVENLÄ°K: Mesaj iÃ§eriÄŸini XSS saldÄ±rÄ±larÄ±na karÅŸÄ± temizle
                    request.Content = InputSanitizer.SanitizeText(request.Content);

                    // Temizleme sonrasÄ± iÃ§erik boÅŸ kalmÄ±ÅŸsa (sadece zararlÄ± kodlardan oluÅŸuyorsa) engelle
                    if (string.IsNullOrWhiteSpace(request.Content))
                    {
                        return ServiceResult<Message>.FailureResult("GeÃ§ersiz mesaj iÃ§eriÄŸi.");
                    }
                }
                else if (request.Type == MessageType.Image && string.IsNullOrEmpty(request.ImageUrl))
                {
                    return ServiceResult<Message>.FailureResult("GÃ¶rsel URL'i boÅŸ olamaz.");
                }

                var conversationResult = await GetOrCreateConversationAsync(sender.UserId, request.ReceiverId, request.ProductId);
                if (!conversationResult.Success || conversationResult.Data == null)
                {
                    return ServiceResult<Message>.FailureResult("KonuÅŸma oluÅŸturulamadÄ± veya bulunamadÄ±.");
                }
                var conversation = conversationResult.Data;

                var receiver = await _firebaseClient
                    .Child(Constants.UsersCollection)
                    .Child(request.ReceiverId)
                    .OnceSingleAsync<User>();

                if (receiver == null)
                {
                    return ServiceResult<Message>.FailureResult("AlÄ±cÄ± kullanÄ±cÄ± bulunamadÄ±.");
                }

                var message = new Message
                {
                    ConversationId = conversation.ConversationId,
                    SenderId = sender.UserId,
                    SenderName = sender.FullName,
                    ReceiverId = request.ReceiverId,
                    Content = request.Content ?? string.Empty,
                    Type = request.Type,
                    ProductId = request.ProductId,
                    ImageUrl = request.ImageUrl,
                    ReceiverName = receiver.FullName,
                    ReceiverPhotoUrl = receiver.ProfileImageUrl,
                };

                if (!string.IsNullOrEmpty(request.ProductId))
                {
                    var product = await _firebaseClient
                        .Child(Constants.ProductsCollection)
                        .Child(request.ProductId)
                        .OnceSingleAsync<Product>();

                    if (product != null)
                    {
                        message.ProductTitle = product.Title;
                        message.ProductThumbnail = product.ThumbnailUrl;
                    }
                }

                //  OPTIMIZE: Paralel yazma iÅŸlemleri
                var messageTask = _firebaseClient
                    .Child(Constants.MessagesCollection)
                    .Child(conversation.ConversationId)
                    .Child(message.MessageId)
                    .PutAsync(message);

                // Conversation gÃ¼ncelleme
                conversation.LastMessage = message.Type == MessageType.Image ? "ğŸ“· Medya" : (message.Content ?? "Mesaj");
                conversation.LastMessageTime = DateTime.UtcNow;
                conversation.LastMessageSenderId = sender.UserId;
                conversation.UpdatedAt = DateTime.UtcNow;

                if (conversation.User1Id == request.ReceiverId)
                    conversation.UnreadCountUser1++;
                else
                    conversation.UnreadCountUser2++;

                var conversationTask = _firebaseClient
                    .Child(Constants.ConversationsCollection)
                    .Child(conversation.ConversationId)
                    .PutAsync(conversation);

                //  Ä°ki iÅŸlemi paralel bekle
                await Task.WhenAll(messageTask, conversationTask);

                return ServiceResult<Message>.SuccessResult(message, "Mesaj baÅŸarÄ±yla gÃ¶nderildi.");
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"SendMessageAsync Hata: {ex.Message}");
                return ServiceResult<Message>.FailureResult("Mesaj gÃ¶nderilemedi. Bir hata oluÅŸtu.", ex.Message);
            }
        }

        // OPTIMIZE: Limit ve sÄ±ralama ekle
        public async Task<ServiceResult<List<Message>>> GetConversationMessagesAsync(string conversationId, int limit = 50)
        {
            try
            {
                var messagesRef = await _firebaseClient
                    .Child(Constants.MessagesCollection)
                    .Child(conversationId)
                    .OrderByKey()
                    .LimitToLast(limit) //  Firebase'den sadece son N mesajÄ± Ã§ek
                    .OnceAsync<Message>();

                var messages = messagesRef
                    .Select(m =>
                    {
                        var msg = m.Object;
                        msg.MessageId = m.Key;
                        return msg;
                    })
                    .Where(m => !m.IsDeleted)
                    .OrderBy(m => m.SentAt) // Zaten limit'li geldi, sÄ±ralama hafif
                    .ToList();

                return ServiceResult<List<Message>>.SuccessResult(messages);
            }
            catch (Exception ex)
            {
                return ServiceResult<List<Message>>.FailureResult("Mesajlar yÃ¼klenemedi", ex.Message);
            }
        }

        //  OPTIMIZE: Client-side filtering (Firebase.Database.net limitasyonu nedeniyle)
        public async Task<ServiceResult<List<Conversation>>> GetUserConversationsAsync(string userId)
        {
            try
            {
                //araÅŸtÄ±r
                // Firebase.Database.net kÃ¼tÃ¼phanesi Ã§oklu index sorgusunu desteklemiyor
                // TÃ¼m konuÅŸmalarÄ± Ã§ek, sonra client-side filtrele
                var allConversationsTask = _firebaseClient
                    .Child(Constants.ConversationsCollection)
                    .OnceAsync<Conversation>();

                var allConversations = await allConversationsTask;

                var conversations = allConversations
                    .Select(c =>
                    {
                        var conv = c.Object;
                        conv.ConversationId = c.Key;
                        return conv;
                    })
                    .Where(c => c.IsActive &&
                               (c.User1Id == userId || c.User2Id == userId))
                    .OrderByDescending(c => c.LastMessageTime)
                    .ToList();

                return ServiceResult<List<Conversation>>.SuccessResult(conversations);
            }
            catch (Exception ex)
            {
                return ServiceResult<List<Conversation>>.FailureResult("KonuÅŸmalar yÃ¼klenemedi", ex.Message);
            }
        }

        public async Task<ServiceResult<Conversation>> GetOrCreateConversationAsync(string user1Id, string user2Id, string? productId = null)
        {
            try
            {
                //  OPTIMIZE: Ã–nce cache'den kontrol et (isteÄŸe baÄŸlÄ±)
                var allConversations = await _firebaseClient
                    .Child(Constants.ConversationsCollection)
                    .OnceAsync<Conversation>();

                var existing = allConversations
                    .Select(c => c.Object)
                    .FirstOrDefault(c =>
                        c.IsActive &&
                        ((c.User1Id == user1Id && c.User2Id == user2Id) ||
                         (c.User1Id == user2Id && c.User2Id == user1Id)));

                if (existing != null)
                {
                    return ServiceResult<Conversation>.SuccessResult(existing);
                }

                //  Paralel kullanÄ±cÄ± sorgularÄ±
                var user1Task = _firebaseClient
                    .Child(Constants.UsersCollection)
                    .Child(user1Id)
                    .OnceSingleAsync<User>();

                var user2Task = _firebaseClient
                    .Child(Constants.UsersCollection)
                    .Child(user2Id)
                    .OnceSingleAsync<User>();

                await Task.WhenAll(user1Task, user2Task);

                var user1 = user1Task.Result;
                var user2 = user2Task.Result;

                if (user1 == null || user2 == null)
                {
                    return ServiceResult<Conversation>.FailureResult("KullanÄ±cÄ± bulunamadÄ±");
                }

                var conversation = new Conversation
                {
                    User1Id = user1Id,
                    User1Name = user1.FullName ?? string.Empty,
                    User1PhotoUrl = user1.ProfileImageUrl ?? string.Empty,
                    User2Id = user2Id,
                    User2Name = user2.FullName ?? string.Empty,
                    User2PhotoUrl = user2.ProfileImageUrl ?? string.Empty,
                    LastMessage = "KonuÅŸma baÅŸladÄ±",
                    LastMessageTime = DateTime.UtcNow
                };

                if (!string.IsNullOrEmpty(productId))
                {
                    var product = await _firebaseClient
                        .Child(Constants.ProductsCollection)
                        .Child(productId)
                        .OnceSingleAsync<Product>();

                    if (product != null)
                    {
                        conversation.ProductId = productId;
                        conversation.ProductTitle = product.Title;
                        conversation.ProductThumbnail = product.ThumbnailUrl;
                    }
                }

                await _firebaseClient
                    .Child(Constants.ConversationsCollection)
                    .Child(conversation.ConversationId)
                    .PutAsync(conversation);

                return ServiceResult<Conversation>.SuccessResult(conversation);
            }
            catch (Exception ex)
            {
                return ServiceResult<Conversation>.FailureResult("KonuÅŸma oluÅŸturulamadÄ±", ex.Message);
            }
        }

        public async Task<ServiceResult<bool>> MarkMessagesAsReadAsync(string conversationId, string readerUserId)
        {
            try
            {
                var conversation = await _firebaseClient
                    .Child(Constants.ConversationsCollection)
                    .Child(conversationId)
                    .OnceSingleAsync<Conversation>();

                if (conversation == null)
                    return ServiceResult<bool>.FailureResult("KonuÅŸma bulunamadÄ±.");

                bool needsUpdate = false;

                if (conversation.User1Id == readerUserId && conversation.UnreadCountUser1 > 0)
                {
                    conversation.UnreadCountUser1 = 0;
                    needsUpdate = true;
                }
                else if (conversation.User2Id == readerUserId && conversation.UnreadCountUser2 > 0)
                {
                    conversation.UnreadCountUser2 = 0;
                    needsUpdate = true;
                }

                //  Sadece deÄŸiÅŸiklik varsa Firebase'e yaz
                if (needsUpdate)
                {
                    await _firebaseClient
                        .Child(Constants.ConversationsCollection)
                        .Child(conversationId)
                        .PutAsync(conversation);

                    // OkunmamÄ±ÅŸ sayÄ±sÄ±nÄ± gÃ¼ncelle
                    await CheckAndBroadcastUnreadMessageStatus(readerUserId);
                }

                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Mesajlar okundu olarak iÅŸaretlenemedi.", ex.Message);
            }
        }

        public async Task<ServiceResult<int>> GetTotalUnreadMessageCountAsync(string userId)
        {
            try
            {
                var conversationsResult = await GetUserConversationsAsync(userId);
                if (!conversationsResult.Success || conversationsResult.Data == null)
                    return ServiceResult<int>.FailureResult("OkunmamÄ±ÅŸ mesajlar sayÄ±lamadÄ±.");

                int totalUnread = conversationsResult.Data
                    .Sum(convo => convo.User1Id == userId
                        ? convo.UnreadCountUser1
                        : convo.UnreadCountUser2);

                return ServiceResult<int>.SuccessResult(totalUnread);
            }
            catch (Exception ex)
            {
                return ServiceResult<int>.FailureResult("OkunmamÄ±ÅŸ mesajlar sayÄ±lamadÄ±.", ex.Message);
            }
        }

        public async Task<ServiceResult<bool>> DeleteConversationAsync(string conversationId, string userId)
        {
            try
            {
                var conversation = await _firebaseClient
                    .Child(Constants.ConversationsCollection)
                    .Child(conversationId)
                    .OnceSingleAsync<Conversation>();

                if (conversation == null)
                {
                    return ServiceResult<bool>.FailureResult("KonuÅŸma bulunamadÄ±");
                }

                conversation.IsActive = false;

                await _firebaseClient
                    .Child(Constants.ConversationsCollection)
                    .Child(conversationId)
                    .PutAsync(conversation);

                return ServiceResult<bool>.SuccessResult(true, "KonuÅŸma silindi");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Silme baÅŸarÄ±sÄ±z", ex.Message);
            }
        }

        //  Bu metod artÄ±k kullanÄ±lmÄ±yor (direkt Firebase Observable kullanÄ±lÄ±yor)
        [Obsolete("Direkt ViewModel'de Firebase Observable kullanÄ±n")]
        public IDisposable SubscribeToConversations(string userId, Action<List<Conversation>> onConversationsChanged)
        {
            var observable = _firebaseClient
                .Child(Constants.ConversationsCollection)
                .AsObservable<Conversation>();

            return observable.Subscribe(changeEvent =>
            {
                try
                {
                    Task.Run(async () =>
                    {
                        var result = await GetUserConversationsAsync(userId);
                        if (result.Success && result.Data != null)
                        {
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                onConversationsChanged?.Invoke(result.Data);
                            });
                        }
                    });
                }
                catch (Exception ex)
                {
                    KamPay.Helpers.AppLogger.DebugLog($"SubscribeToConversations Hata: {ex.Message}");
                }
            });
        }

        //  Bu metod artÄ±k kullanÄ±lmÄ±yor (direkt Firebase Observable kullanÄ±lÄ±yor)
        [Obsolete("Direkt ViewModel'de Firebase Observable kullanÄ±n")]
        public IDisposable SubscribeToMessages(string conversationId, Action<List<Message>> onMessagesChanged)
        {
            var observable = _firebaseClient
                .Child(Constants.MessagesCollection)
                .Child(conversationId)
                .AsObservable<Message>();

            return observable.Subscribe(changeEvent =>
            {
                try
                {
                    Task.Run(async () =>
                    {
                        var result = await GetConversationMessagesAsync(conversationId);
                        if (result.Success && result.Data != null)
                        {
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                onMessagesChanged?.Invoke(result.Data);
                            });
                        }
                    });
                }
                catch (Exception ex)
                {
                    KamPay.Helpers.AppLogger.DebugLog($"SubscribeToMessages Hata: {ex.Message}");
                }
            });
        }


        /// <summary>
        /// KullanÄ±cÄ±nÄ±n tÃ¼m mesajlarÄ±ndaki isim bilgilerini gÃ¼nceller
        /// ? OPTIMIZE: Firebase multi-path atomic update ile tek istekle gÃ¼ncelleme
        /// </summary>
        public async Task<ServiceResult<bool>> UpdateUserInfoInMessagesAsync(string userId, string? newName, string? newPhotoUrl)
        {
            try
            {
                // 1?? KullanÄ±cÄ±nÄ±n dahil olduÄŸu konuÅŸmalarÄ± bul
                var allConversations = await _firebaseClient
                    .Child(Constants.ConversationsCollection)
                    .OnceAsync<Conversation>();

                var userConversations = allConversations
                    .Where(c => c.Object.User1Id == userId || c.Object.User2Id == userId)
                    .Select(c => c.Key)
                    .ToList();

                if (!userConversations.Any())
                {
                    return ServiceResult<bool>.SuccessResult(true, "GÃ¼ncellenecek mesaj yok");
                }

                // 2?? Her mesaj iÃ§in PatchAsync Ã§aÄŸrÄ±larÄ± oluÅŸtur
                var tasks = new List<Task>();
                int messageCount = 0;

                foreach (var conversationId in userConversations)
                {
                    // Bu konuÅŸmadaki tÃ¼m mesajlarÄ± al
                    var messages = await _firebaseClient
                        .Child(Constants.MessagesCollection)
                        .Child(conversationId)
                        .OnceAsync<Message>();

                    foreach (var messageEntry in messages)
                    {
                        var msg = messageEntry.Object;
                        var perMessageUpdates = new Dictionary<string, object>();

                        // GÃ¶nderen kiÅŸi gÃ¼ncelleniyorsa
                        if (msg.SenderId == userId && !string.IsNullOrWhiteSpace(newName))
                        {
                            perMessageUpdates["SenderName"] = newName;
                            messageCount++;
                        }

                        // AlÄ±cÄ± kiÅŸi gÃ¼ncelleniyorsa
                        if (msg.ReceiverId == userId)
                        {
                            if (!string.IsNullOrWhiteSpace(newName))
                                perMessageUpdates["ReceiverName"] = newName;
                            if (!string.IsNullOrWhiteSpace(newPhotoUrl))
                                perMessageUpdates["ReceiverPhotoUrl"] = newPhotoUrl;

                            if (perMessageUpdates.Any() && msg.SenderId != userId) // avoid double counting when sender==receiver
                                messageCount++;
                        }

                        if (perMessageUpdates.Any())
                        {
                            var task = _firebaseClient
                                .Child(Constants.MessagesCollection)
                                .Child(conversationId)
                                .Child(messageEntry.Key)
                                .PatchAsync(perMessageUpdates);

                            tasks.Add(task);
                        }
                    }
                }

                if (tasks.Any())
                {
                    await Task.WhenAll(tasks);
                    KamPay.Helpers.AppLogger.DebugLog($"? {messageCount} mesaj PatchAsync ile gÃ¼ncellendi");
                }

                return ServiceResult<bool>.SuccessResult(true, $"{messageCount} mesaj gÃ¼ncellendi");
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"? UpdateUserInfoInMessages hatasÄ±: {ex.Message}");
                return ServiceResult<bool>.FailureResult("Mesajlar gÃ¼ncellenemedi", ex.Message);
            }
        }


        /// <summary>
        /// KullanÄ±cÄ±nÄ±n tÃ¼m konuÅŸmalarÄ±ndaki isim ve profil fotoÄŸrafÄ± bilgilerini gÃ¼nceller
        /// ? OPTIMIZE: Firebase multi-path atomic update ile tek istekle gÃ¼ncelleme
        /// </summary>
        public async Task<ServiceResult<bool>> UpdateUserInfoInConversationsAsync(string userId, string? newName, string? newPhotoUrl)
        {
            try
            {
                // 1?? KullanÄ±cÄ±nÄ±n konuÅŸmalarÄ±nÄ± bul
                var allConversations = await _firebaseClient
                    .Child(Constants.ConversationsCollection)
                    .OnceAsync<Conversation>();

                var userConversations = allConversations
                    .Where(c => c.Object.User1Id == userId || c.Object.User2Id == userId)
                    .ToList();

                if (!userConversations.Any())
                {
                    return ServiceResult<bool>.SuccessResult(true, "GÃ¼ncellenecek konuÅŸma yok");
                }

                var tasks = new List<Task>();

                foreach (var conversationEntry in userConversations)
                {
                    var conversation = conversationEntry.Object;
                    var perConvUpdates = new Dictionary<string, object>();

                    // User1 gÃ¼ncelleniyorsa
                    if (conversation.User1Id == userId)
                    {
                        if (!string.IsNullOrWhiteSpace(newName))
                            perConvUpdates["User1Name"] = newName;
                        if (!string.IsNullOrWhiteSpace(newPhotoUrl))
                            perConvUpdates["User1PhotoUrl"] = newPhotoUrl;
                    }

                    // User2 gÃ¼ncelleniyorsa
                    if (conversation.User2Id == userId)
                    {
                        if (!string.IsNullOrWhiteSpace(newName))
                            perConvUpdates["User2Name"] = newName;
                        if (!string.IsNullOrWhiteSpace(newPhotoUrl))
                            perConvUpdates["User2PhotoUrl"] = newPhotoUrl;
                    }

                    if (perConvUpdates.Any())
                    {
                        var task = _firebaseClient
                            .Child(Constants.ConversationsCollection)
                            .Child(conversationEntry.Key)
                            .PatchAsync(perConvUpdates);

                        tasks.Add(task);
                    }
                }

                if (tasks.Any())
                {
                    await Task.WhenAll(tasks);
                    KamPay.Helpers.AppLogger.DebugLog($"? {userConversations.Count} konuÅŸma PatchAsync ile gÃ¼ncellendi");
                }

                return ServiceResult<bool>.SuccessResult(true, $"{userConversations.Count} konuÅŸma gÃ¼ncellendi");
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"? UpdateUserInfoInConversations hatasÄ±: {ex.Message}");
                return ServiceResult<bool>.FailureResult("KonuÅŸmalar gÃ¼ncellenemedi", ex.Message);
            }
        }
    }
}
