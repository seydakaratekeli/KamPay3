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

namespace KamPay.Services
{
    public class FirebaseMessagingService : IMessagingService
    {
        private readonly FirebaseClient _firebaseClient;
        private readonly INotificationService _notificationService;

        public FirebaseMessagingService(INotificationService notificationService)
        {
            _firebaseClient = new FirebaseClient(Constants.FirebaseRealtimeDbUrl);
            _notificationService = notificationService;
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
                if (request == null || sender == null || string.IsNullOrEmpty(request.ReceiverId) || string.IsNullOrEmpty(request.Content))
                {
                    return ServiceResult<Message>.FailureResult("Geçersiz istek: Gönderen, alıcı veya mesaj içeriği boş olamaz.");
                }

                var conversationResult = await GetOrCreateConversationAsync(sender.UserId, request.ReceiverId, request.ProductId);
                if (!conversationResult.Success || conversationResult.Data == null)
                {
                    return ServiceResult<Message>.FailureResult("Konuşma oluşturulamadı veya bulunamadı.");
                }
                var conversation = conversationResult.Data;

                var receiver = await _firebaseClient
                    .Child(Constants.UsersCollection)
                    .Child(request.ReceiverId)
                    .OnceSingleAsync<User>();

                if (receiver == null)
                {
                    return ServiceResult<Message>.FailureResult("Alıcı kullanıcı bulunamadı.");
                }

                var message = new Message
                {
                    ConversationId = conversation.ConversationId,
                    SenderId = sender.UserId,
                    SenderName = sender.FullName,
                    ReceiverId = request.ReceiverId,
                    Content = request.Content,
                    Type = request.Type,
                    ProductId = request.ProductId,
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

                // 🔥 OPTIMIZE: Paralel yazma işlemleri
                var messageTask = _firebaseClient
                    .Child(Constants.MessagesCollection)
                    .Child(conversation.ConversationId)
                    .Child(message.MessageId)
                    .PutAsync(message);

                // Conversation güncelleme
                conversation.LastMessage = message.Type == MessageType.Text ? message.Content : "📷 Medya";
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

                // 🔥 İki işlemi paralel bekle
                await Task.WhenAll(messageTask, conversationTask);

                return ServiceResult<Message>.SuccessResult(message, "Mesaj başarıyla gönderildi.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SendMessageAsync Hata: {ex.Message}");
                return ServiceResult<Message>.FailureResult("Mesaj gönderilemedi. Bir hata oluştu.", ex.Message);
            }
        }

        // 🔥 OPTIMIZE: Limit ve sıralama ekle
        public async Task<ServiceResult<List<Message>>> GetConversationMessagesAsync(string conversationId, int limit = 50)
        {
            try
            {
                var messagesRef = await _firebaseClient
                    .Child(Constants.MessagesCollection)
                    .Child(conversationId)
                    .OrderByKey()
                    .LimitToLast(limit) // 🔥 Firebase'den sadece son N mesajı çek
                    .OnceAsync<Message>();

                var messages = messagesRef
                    .Select(m =>
                    {
                        var msg = m.Object;
                        msg.MessageId = m.Key;
                        return msg;
                    })
                    .Where(m => !m.IsDeleted)
                    .OrderBy(m => m.SentAt) // Zaten limit'li geldi, sıralama hafif
                    .ToList();

                return ServiceResult<List<Message>>.SuccessResult(messages);
            }
            catch (Exception ex)
            {
                return ServiceResult<List<Message>>.FailureResult("Mesajlar yüklenemedi", ex.Message);
            }
        }

        // 🔥 OPTIMIZE: Client-side filtering (Firebase.Database.net limitasyonu nedeniyle)
        public async Task<ServiceResult<List<Conversation>>> GetUserConversationsAsync(string userId)
        {
            try
            {
                // Firebase.Database.net kütüphanesi çoklu index sorgusunu desteklemiyor
                // Tüm konuşmaları çek, sonra client-side filtrele
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
                return ServiceResult<List<Conversation>>.FailureResult("Konuşmalar yüklenemedi", ex.Message);
            }
        }

        public async Task<ServiceResult<Conversation>> GetOrCreateConversationAsync(string user1Id, string user2Id, string productId = null)
        {
            try
            {
                // 🔥 OPTIMIZE: Önce cache'den kontrol et (isteğe bağlı)
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

                // 🔥 Paralel kullanıcı sorguları
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
                    return ServiceResult<Conversation>.FailureResult("Kullanıcı bulunamadı");
                }

                var conversation = new Conversation
                {
                    User1Id = user1Id,
                    User1Name = user1.FullName,
                    User1PhotoUrl = user1.ProfileImageUrl,
                    User2Id = user2Id,
                    User2Name = user2.FullName,
                    User2PhotoUrl = user2.ProfileImageUrl,
                    LastMessage = "Konuşma başladı",
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
                return ServiceResult<Conversation>.FailureResult("Konuşma oluşturulamadı", ex.Message);
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
                    return ServiceResult<bool>.FailureResult("Konuşma bulunamadı.");

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

                // 🔥 Sadece değişiklik varsa Firebase'e yaz
                if (needsUpdate)
                {
                    await _firebaseClient
                        .Child(Constants.ConversationsCollection)
                        .Child(conversationId)
                        .PutAsync(conversation);

                    // Okunmamış sayısını güncelle
                    await CheckAndBroadcastUnreadMessageStatus(readerUserId);
                }

                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Mesajlar okundu olarak işaretlenemedi.", ex.Message);
            }
        }

        public async Task<ServiceResult<int>> GetTotalUnreadMessageCountAsync(string userId)
        {
            try
            {
                var conversationsResult = await GetUserConversationsAsync(userId);
                if (!conversationsResult.Success)
                    return ServiceResult<int>.FailureResult("Okunmamış mesajlar sayılamadı.");

                int totalUnread = conversationsResult.Data
                    .Sum(convo => convo.User1Id == userId
                        ? convo.UnreadCountUser1
                        : convo.UnreadCountUser2);

                return ServiceResult<int>.SuccessResult(totalUnread);
            }
            catch (Exception ex)
            {
                return ServiceResult<int>.FailureResult("Okunmamış mesajlar sayılamadı.", ex.Message);
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
                    return ServiceResult<bool>.FailureResult("Konuşma bulunamadı");
                }

                conversation.IsActive = false;

                await _firebaseClient
                    .Child(Constants.ConversationsCollection)
                    .Child(conversationId)
                    .PutAsync(conversation);

                return ServiceResult<bool>.SuccessResult(true, "Konuşma silindi");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Silme başarısız", ex.Message);
            }
        }

        // 🔥 UYARI: Bu metod artık kullanılmıyor (direkt Firebase Observable kullanılıyor)
        [Obsolete("Direkt ViewModel'de Firebase Observable kullanın")]
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
                        if (result.Success)
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
                    Console.WriteLine($"SubscribeToConversations Hata: {ex.Message}");
                }
            });
        }

        // 🔥 UYARI: Bu metod artık kullanılmıyor (direkt ViewModel'de Firebase Observable kullanılıyor)
        [Obsolete("Direkt ViewModel'de Firebase Observable kullanın")]
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
                        if (result.Success)
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
                    Console.WriteLine($"SubscribeToMessages Hata: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Kullanıcının tüm mesajlarındaki isim bilgilerini günceller
        /// </summary>
        public async Task<ServiceResult<bool>> UpdateUserInfoInMessagesAsync(string userId, string newName, string newPhotoUrl)
        {
            try
            {
                // Not: Firebase.Database.net kütüphanesi çoklu index sorgusunu desteklemiyor.
                // İdeal senaryoda User1Id veya User2Id üzerinde ayrı sorgular yapılmalı,
                // ancak mevcut veri yapısı ve diğer metotlarla tutarlılık için client-side filtreleme kullanılıyor.
                // Bu yaklaşım GetUserConversationsAsync ile aynı pattern'i takip eder.
                var allConversations = await _firebaseClient
                    .Child(Constants.ConversationsCollection)
                    .OnceAsync<Conversation>();

                // Kullanıcının dahil olduğu konuşmaları filtrele
                var userConversations = allConversations
                    .Where(c => c.Object.User1Id == userId || c.Object.User2Id == userId)
                    .ToList();

                int updatedMessageCount = 0;

                foreach (var conversationEntry in userConversations)
                {
                    var conversationId = conversationEntry.Key;

                    // Bu konuşmadaki tüm mesajları al
                    var messages = await _firebaseClient
                        .Child(Constants.MessagesCollection)
                        .Child(conversationId)
                        .OnceAsync<Message>();

                    foreach (var messageEntry in messages)
                    {
                        var message = messageEntry.Object;
                        bool needsUpdate = false;

                        // Gönderen kişi güncelleniyorsa
                        if (message.SenderId == userId)
                        {
                            message.SenderName = newName;
                            needsUpdate = true;
                        }

                        // Alıcı kişi güncelleniyorsa
                        if (message.ReceiverId == userId)
                        {
                            message.ReceiverName = newName;
                            message.ReceiverPhotoUrl = newPhotoUrl;
                            needsUpdate = true;
                        }

                        if (needsUpdate)
                        {
                            await _firebaseClient
                                .Child(Constants.MessagesCollection)
                                .Child(conversationId)
                                .Child(messageEntry.Key)
                                .PutAsync(message);
                            updatedMessageCount++;
                        }
                    }
                }

                return ServiceResult<bool>.SuccessResult(true, $"{updatedMessageCount} mesaj güncellendi");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Mesajlar güncellenemedi", ex.Message);
            }
        }

        /// <summary>
        /// Kullanıcının tüm konuşmalarındaki isim ve profil fotoğrafı bilgilerini günceller
        /// </summary>
        public async Task<ServiceResult<bool>> UpdateUserInfoInConversationsAsync(string userId, string newName, string newPhotoUrl)
        {
            try
            {
                // Not: Firebase.Database.net kütüphanesi çoklu index sorgusunu desteklemiyor.
                // Mevcut veri yapısı ve diğer metotlarla tutarlılık için client-side filtreleme kullanılıyor.
                var allConversations = await _firebaseClient
                    .Child(Constants.ConversationsCollection)
                    .OnceAsync<Conversation>();

                int updatedCount = 0;

                foreach (var conversationEntry in allConversations)
                {
                    var conversation = conversationEntry.Object;
                    conversation.ConversationId = conversationEntry.Key;
                    bool needsUpdate = false;

                    // User1 güncelleniyorsa
                    if (conversation.User1Id == userId)
                    {
                        conversation.User1Name = newName;
                        conversation.User1PhotoUrl = newPhotoUrl;
                        needsUpdate = true;
                    }

                    // User2 güncelleniyorsa
                    if (conversation.User2Id == userId)
                    {
                        conversation.User2Name = newName;
                        conversation.User2PhotoUrl = newPhotoUrl;
                        needsUpdate = true;
                    }

                    if (needsUpdate)
                    {
                        await _firebaseClient
                            .Child(Constants.ConversationsCollection)
                            .Child(conversationEntry.Key)
                            .PutAsync(conversation);
                        updatedCount++;
                    }
                }

                return ServiceResult<bool>.SuccessResult(true, $"{updatedCount} konuşma güncellendi");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Konuşmalar güncellenemedi", ex.Message);
            }
        }
    }
}