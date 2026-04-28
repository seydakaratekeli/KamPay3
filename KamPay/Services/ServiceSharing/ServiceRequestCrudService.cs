using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Models;
using KamPay.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KamPay.Services.Messaging;

namespace KamPay.Services.ServiceSharing
{
    public class ServiceRequestCrudService
    {
        private readonly FirebaseClient _firebaseClient;
        private readonly INotificationService _notificationService;
        private readonly IUserProfileService _userProfileService;
        private readonly IMessagingService _messagingService;

        public ServiceRequestCrudService(
            FirebaseClient firebaseClient,
            INotificationService notificationService,
            IUserProfileService userProfileService,
            IMessagingService messagingService)
        {
            _firebaseClient = firebaseClient ?? throw new ArgumentNullException(nameof(firebaseClient));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _userProfileService = userProfileService ?? throw new ArgumentNullException(nameof(userProfileService));
            _messagingService = messagingService ?? throw new ArgumentNullException(nameof(messagingService));
        }

        public async Task<ServiceResult<ServiceRequest>> RequestServiceAsync(ServiceOffer offer, User requester, string message)
        {
            try
            {
                if (offer == null || requester == null)
                    return ServiceResult<ServiceRequest>.FailureResult("Hizmet veya kullanıcı bilgisi eksik.");

                var request = new ServiceRequest
                {
                    RequestId = Guid.NewGuid().ToString(),
                    ServiceId = offer.ServiceId,
                    ServiceTitle = offer.Title,
                    ProviderId = offer.ProviderId,
                    ProviderName = offer.ProviderName,
                    RequesterId = requester.UserId,
                    RequesterName = requester.FullName,
                    Message = message,
                    Status = ServiceRequestStatus.Pending,
                    RequestedAt = DateTime.UtcNow,
                
                    QuotedPrice = offer.Price,
                    Price = offer.Price,
                    TimeCreditValue = offer.TimeCredits,
                    PaymentStatus = ServicePaymentStatus.None,
                    PaymentMethod = PaymentMethodType.None,
                    Currency = "TRY"
                };

                await _firebaseClient
                    .Child(Constants.ServiceRequestsCollection)
                    .Child(request.RequestId)
                    .PutAsync(request);

                return ServiceResult<ServiceRequest>.SuccessResult(request, "Hizmet talebiniz başarıyla oluşturuldu.");
            }
            catch (Exception ex)
            {
                return ServiceResult<ServiceRequest>.FailureResult($"Talep oluşturulamadı: {ex.Message}");
            }
        }

        public async Task<ServiceResult<(List<ServiceRequest> Incoming, List<ServiceRequest> Outgoing)>> GetMyServiceRequestsAsync(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    return ServiceResult<(List<ServiceRequest> Incoming, List<ServiceRequest> Outgoing)>.FailureResult("Kullanıcı ID'si bulunamadı.");
                }

                var incomingRequestsTask = _firebaseClient
                    .Child(Constants.ServiceRequestsCollection)
                    .OrderBy("ProviderId")
                    .EqualTo(userId)
                    .OnceAsync<ServiceRequest>();

                var outgoingRequestsTask = _firebaseClient
                    .Child(Constants.ServiceRequestsCollection)
                    .OrderBy("RequesterId")
                    .EqualTo(userId)
                    .OnceAsync<ServiceRequest>();

                await Task.WhenAll(incomingRequestsTask, outgoingRequestsTask);

                var incoming = incomingRequestsTask.Result
                    .Select(item => { item.Object.RequestId = item.Key; return item.Object; })
                    .OrderByDescending(r => r.RequestedAt)
                    .ToList();

                var outgoing = outgoingRequestsTask.Result
                    .Select(item => { item.Object.RequestId = item.Key; return item.Object; })
                    .OrderByDescending(r => r.RequestedAt)
                    .ToList();

                return ServiceResult<(List<ServiceRequest> Incoming, List<ServiceRequest> Outgoing)>.SuccessResult((incoming, outgoing));
            }
            catch (Exception ex)
            {
                return ServiceResult<(List<ServiceRequest> Incoming, List<ServiceRequest> Outgoing)>.FailureResult("Talepler getirilirken bir hata oluştu.", ex.Message);
            }
        }

        public async Task<ServiceResult<bool>> RespondToRequestAsync(string requestId, bool accept)
        {
            try
            {
                var requestNode = _firebaseClient.Child(Constants.ServiceRequestsCollection).Child(requestId);
                var request = await requestNode.OnceSingleAsync<ServiceRequest>();

                if (request == null)
                {
                    return ServiceResult<bool>.FailureResult("Talep bulunamadı.");
                }

                request.Status = accept ? ServiceRequestStatus.Accepted : ServiceRequestStatus.Declined;
                await requestNode.PutAsync(request);

                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = request.RequesterId,
                    Type = accept ? NotificationType.OfferAccepted : NotificationType.OfferRejected,
                    Title = accept ? "Hizmet Talebin Onaylandı!" : "Hizmet Talebin Reddedildi",
                    Message = $"'{request.ServiceTitle}' hizmeti için talebin {(accept ? "kabul edildi." : "reddedildi.")}",
                    ActionUrl = "///ServiceSharingPage"
                });

                return ServiceResult<bool>.SuccessResult(true, "Talep yanıtlandı.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("İşlem sırasında hata oluştu.", ex.Message);
            }
        }

        public async Task<ServiceResult<string>> StartConversationForRequestAsync(string requestId, string currentUserId)
        {
            try
            {
                var requestNode = _firebaseClient.Child(Constants.ServiceRequestsCollection).Child(requestId);
                var request = await requestNode.OnceSingleAsync<ServiceRequest>();

                if (request == null)
                {
                    return ServiceResult<string>.FailureResult("Talep bulunamadı.");
                }

                if (request.RequesterId != currentUserId && request.ProviderId != currentUserId)
                {
                    return ServiceResult<string>.FailureResult("Bu talebe erişim yetkiniz yok.");
                }

                if (!string.IsNullOrEmpty(request.ConversationId))
                {
                    try
                    {
                        var existingConversation = await _firebaseClient
                            .Child(Constants.ConversationsCollection)
                            .Child(request.ConversationId)
                            .OnceSingleAsync<Conversation>();

                        if (existingConversation != null && existingConversation.IsActive)
                        {
                            return ServiceResult<string>.SuccessResult(request.ConversationId, "Mevcut konuşma bulundu.");
                        }
                    }
                    catch { }
                }

                var otherUserId = request.RequesterId == currentUserId ? request.ProviderId : request.RequesterId;

                var existingConversations = await _firebaseClient
                    .Child(Constants.ConversationsCollection)
                    .OrderBy("User1Id")
                    .EqualTo(currentUserId)
                    .OnceAsync<Conversation>();

                var existingWithOtherUser = existingConversations
                    .FirstOrDefault(c => c.Object != null &&
                                        c.Object.IsActive &&
                                        c.Object.ConversationType == "Negotiation" &&
                                        (c.Object.User2Id == otherUserId || c.Object.User1Id == otherUserId));

                var currentUserObj = await GetUserAsync(currentUserId);

                if (existingWithOtherUser != null)
                {
                    request.ConversationId = existingWithOtherUser.Key;
                    request.HasActiveConversation = true;
                    await requestNode.PutAsync(request);

                    // ✅ FAZ 2: Hizmet Kartını Gönder
                    if (currentUserObj != null)
                    {
                        await _messagingService.SendMessageAsync(new SendMessageRequest
                        {
                            ReceiverId = otherUserId,
                            Content = request.Message ?? "Hizmet talebi gönderildi",
                            Type = MessageType.ServiceCard,
                            ServiceOfferId = request.ServiceId,
                            ProductTitle = request.ServiceTitle,
                            ProductPrice = request.Price,
                            ConversationType = "Negotiation"
                        }, currentUserObj);
                    }

                    return ServiceResult<string>.SuccessResult(existingWithOtherUser.Key, "Mevcut konuşma bulundu.");
                }

                var conversationResult = await _messagingService.GetOrCreateConversationAsync(currentUserId, otherUserId, null, "Negotiation");

                if (!conversationResult.Success || conversationResult.Data == null)
                {
                    return ServiceResult<string>.FailureResult("Konuşma oluşturulamadı.", conversationResult.Message);
                }

                request.ConversationId = conversationResult.Data.ConversationId;
                request.HasActiveConversation = true;
                await requestNode.PutAsync(request);

                var systemMessageContent = $"💬 [{request.ServiceTitle} - Hizmet]\n📢 Konuşma başlatıldı\nFiyat: {request.Price:N2} ₺";

                if (currentUserObj != null)
                {
                    await _messagingService.SendMessageAsync(new SendMessageRequest
                    {
                        ReceiverId = otherUserId,
                        Content = systemMessageContent,
                        Type = MessageType.System,
                        ConversationType = "Negotiation"
                    }, currentUserObj);

                    // ✅ FAZ 2: Hizmet Kartını Gönder
                    await _messagingService.SendMessageAsync(new SendMessageRequest
                    {
                        ReceiverId = otherUserId,
                        Content = request.Message ?? "Hizmet talebi gönderildi",
                        Type = MessageType.ServiceCard,
                        ServiceOfferId = request.ServiceId,
                        ProductTitle = request.ServiceTitle,
                        ProductPrice = request.Price,
                        ConversationType = "Negotiation"
                    }, currentUserObj);
                }

                return ServiceResult<string>.SuccessResult(conversationResult.Data.ConversationId, "Konuşma başlatıldı.");
            }
            catch (Exception ex)
            {
                return ServiceResult<string>.FailureResult("Konuşma başlatılırken hata oluştu.", ex.Message);
            }
        }

        public async Task<User?> GetUserAsync(string userId)
        {
            try
            {
                var user = await _firebaseClient
                    .Child(Constants.UsersCollection)
                    .Child(userId)
                    .OnceSingleAsync<User>();
                return user;
            }
            catch
            {
                return null;
            }
        }
    }
}
