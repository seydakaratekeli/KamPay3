using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Models;
using KamPay.Helpers;
using System;
using System.Threading.Tasks;
using KamPay.Services.Messaging;

namespace KamPay.Services.ServiceSharing
{
    public class ServiceRequestNegotiationService
    {
        private readonly FirebaseClient _firebaseClient;
        private readonly INotificationService _notificationService;
        private readonly IMessagingService _messagingService;

        public ServiceRequestNegotiationService(
            FirebaseClient firebaseClient,
            INotificationService notificationService,
            IMessagingService messagingService)
        {
            _firebaseClient = firebaseClient ?? throw new ArgumentNullException(nameof(firebaseClient));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _messagingService = messagingService ?? throw new ArgumentNullException(nameof(messagingService));
        }

        public async Task<ServiceResult<bool>> ProposePrice(string requestId, decimal proposedPrice, string currentUserId)
        {
            try
            {
                var requestNode = _firebaseClient.Child(Constants.ServiceRequestsCollection).Child(requestId);
                var request = await requestNode.OnceSingleAsync<ServiceRequest>();

                if (request == null)
                    return ServiceResult<bool>.FailureResult("Talep bulunamadı.");

                if (request.RequesterId != currentUserId)
                    return ServiceResult<bool>.FailureResult("Sadece talep eden kişi fiyat teklif edebilir.");

                if (proposedPrice <= 0)
                    return ServiceResult<bool>.FailureResult("Geçerli bir fiyat giriniz.");

                request.ProposedPriceByRequester = proposedPrice;
                request.IsNegotiating = true;
                request.LastNegotiationDate = DateTime.UtcNow;

                if (!request.NegotiationStartedAt.HasValue)
                    request.NegotiationStartedAt = DateTime.UtcNow;

                request.NegotiationRoundCount++;

                await requestNode.PutAsync(request);

                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = request.ProviderId,
                    Title = "Yeni Fiyat Teklifi",
                    Message = $"{request.RequesterName}, '{request.ServiceTitle}' hizmeti için {proposedPrice} ₺ teklif etti. (Orijinal fiyat: {request.Price} ₺)"
                });

                if (!string.IsNullOrEmpty(request.ConversationId))
                {
                    var messageContent = $"💬 [{request.ServiceTitle} - Hizmet]\n💰 Fiyat Teklifi: {proposedPrice:N2} ₺\n(Orijinal fiyat: {request.Price:N2} ₺)";
                    var currentUserObj = await GetUserAsync(currentUserId);
                    if (currentUserObj != null)
                    {
                        await _messagingService.SendMessageAsync(new SendMessageRequest
                        {
                            ReceiverId = request.ProviderId,
                            Content = messageContent,
                            Type = MessageType.System
                        }, currentUserObj);
                    }
                }

                return ServiceResult<bool>.SuccessResult(true, "Fiyat teklifiniz gönderildi.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Fiyat teklifi gönderilemedi.", ex.Message);
            }
        }

        public async Task<ServiceResult<bool>> SendCounterOfferAsync(string requestId, decimal counterOffer, string currentUserId)
        {
            try
            {
                var requestNode = _firebaseClient.Child(Constants.ServiceRequestsCollection).Child(requestId);
                var request = await requestNode.OnceSingleAsync<ServiceRequest>();

                if (request == null)
                    return ServiceResult<bool>.FailureResult("Talep bulunamadı.");

                if (request.ProviderId != currentUserId)
                    return ServiceResult<bool>.FailureResult("Sadece hizmet sağlayıcı karşı teklif verebilir.");

                if (counterOffer <= 0)
                    return ServiceResult<bool>.FailureResult("Geçerli bir fiyat giriniz.");

                request.CounterOfferByProvider = counterOffer;
                request.IsNegotiating = true;
                request.LastNegotiationDate = DateTime.UtcNow;

                if (!request.NegotiationStartedAt.HasValue)
                    request.NegotiationStartedAt = DateTime.UtcNow;

                request.NegotiationRoundCount++;

                await requestNode.PutAsync(request);

                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = request.RequesterId,
                    Title = "Karşı Teklif Alındı",
                    Message = $"'{request.ServiceTitle}' hizmeti için karşı teklif: {counterOffer} ₺"
                });

                if (!string.IsNullOrEmpty(request.ConversationId))
                {
                    var requesterOffer = request.ProposedPriceByRequester.HasValue
                        ? $"\n(Talep edenin teklifi: {request.ProposedPriceByRequester:N2} ₺)"
                        : "";

                    var messageContent = $"💬 [{request.ServiceTitle} - Hizmet]\n💰 Karşı Teklif: {counterOffer:N2} ₺{requesterOffer}";
                    
                    var currentUserObj = await GetUserAsync(currentUserId);
                    if (currentUserObj != null)
                    {
                        await _messagingService.SendMessageAsync(new SendMessageRequest
                        {
                            ReceiverId = request.RequesterId,
                            Content = messageContent,
                            Type = MessageType.System
                        }, currentUserObj);
                    }
                }

                return ServiceResult<bool>.SuccessResult(true, "Karşı teklifiniz gönderildi.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Karşı teklif gönderilemedi.", ex.Message);
            }
        }

        public async Task<ServiceResult<bool>> AcceptNegotiatedPriceAsync(string requestId, string currentUserId)
        {
            try
            {
                var requestNode = _firebaseClient.Child(Constants.ServiceRequestsCollection).Child(requestId);
                var request = await requestNode.OnceSingleAsync<ServiceRequest>();

                if (request == null)
                    return ServiceResult<bool>.FailureResult("Talep bulunamadı.");

                if (request.RequesterId != currentUserId && request.ProviderId != currentUserId)
                    return ServiceResult<bool>.FailureResult("Bu talebe erişim yetkiniz yok.");

                if (!request.IsNegotiating)
                    return ServiceResult<bool>.FailureResult("Aktif bir pazarlık bulunmuyor.");

                decimal agreedPrice = request.CounterOfferByProvider ?? request.ProposedPriceByRequester ?? 0;

                if (agreedPrice <= 0)
                    return ServiceResult<bool>.FailureResult("Kabul edilecek bir teklif bulunamadı.");

                request.QuotedPrice = agreedPrice;
                request.Price = agreedPrice;
                request.IsNegotiating = false;

                var acceptedBy = request.RequesterId == currentUserId ? "Talep Eden" : "Sağlayıcı";
                var negotiationDuration = request.NegotiationStartedAt.HasValue
                    ? (DateTime.UtcNow - request.NegotiationStartedAt.Value).TotalMinutes
                    : 0;

                var negotiationSummary = $"🤝 Anlaşma Sağlandı\n" +
                    $"Fiyat: {agreedPrice:N2}₺\n" +
                    $"Kabul Eden: {acceptedBy}\n" +
                    $"Pazarlık Turu: {request.NegotiationRoundCount}\n" +
                    $"Süre: {negotiationDuration:N0} dakika\n" +
                    $"Tarih: {DateTime.UtcNow:dd.MM.yyyy HH:mm}";

                request.NegotiationNotes += (string.IsNullOrEmpty(request.NegotiationNotes) ? "" : "\n\n") + negotiationSummary;

                await requestNode.PutAsync(request);

                var otherUserId = request.RequesterId == currentUserId ? request.ProviderId : request.RequesterId;

                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = otherUserId,
                    Title = "Fiyat Anlaşması",
                    Message = $"'{request.ServiceTitle}' hizmeti için {agreedPrice} ₺ fiyat üzerinde anlaşıldı."
                });

                if (!string.IsNullOrEmpty(request.ConversationId))
                {
                    var messageContent = $"💬 [{request.ServiceTitle} - Hizmet]\n✅ Anlaşma Sağlandı: {agreedPrice:N2} ₺";
                    var currentUserObj = await GetUserAsync(currentUserId);
                    if (currentUserObj != null)
                    {
                        await _messagingService.SendMessageAsync(new SendMessageRequest
                        {
                            ReceiverId = otherUserId,
                            Content = messageContent,
                            Type = MessageType.System
                        }, currentUserObj);
                    }
                }

                return ServiceResult<bool>.SuccessResult(true, $"Fiyat {agreedPrice} ₺ olarak kabul edildi.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Fiyat kabulü sırasında hata oluştu.", ex.Message);
            }
        }

        private async Task<User?> GetUserAsync(string userId)
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
