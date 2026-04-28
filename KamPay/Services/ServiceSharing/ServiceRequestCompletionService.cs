using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Models;
using KamPay.Helpers;
using System;
using System.Threading.Tasks;

namespace KamPay.Services.ServiceSharing
{
    public class ServiceRequestCompletionService
    {
        private readonly FirebaseClient _firebaseClient;
        private readonly INotificationService _notificationService;
        private readonly IUserProfileService _userProfileService;

        public ServiceRequestCompletionService(
            FirebaseClient firebaseClient,
            INotificationService notificationService,
            IUserProfileService userProfileService)
        {
            _firebaseClient = firebaseClient ?? throw new ArgumentNullException(nameof(firebaseClient));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _userProfileService = userProfileService ?? throw new ArgumentNullException(nameof(userProfileService));
        }

        public async Task<ServiceResult<bool>> CompleteServiceRequestAsync(string transactionId, string providerId)
        {
            try
            {
                if (!NetworkHelper.HasInternetConnection())
                    return ServiceResult<bool>.FailureResult("İnternet bağlantısı yok.");

                var transactionNode = _firebaseClient.Child(Constants.TransactionsCollection).Child(transactionId);
                var transaction = await transactionNode.OnceSingleAsync<Transaction>();

                if (transaction == null || transaction.SellerId != providerId)
                    return ServiceResult<bool>.FailureResult("İşlem bulunamadı veya yetkiniz yok.");

                if (transaction.Price > 0 && transaction.PaymentStatus != PaymentStatus.Paid)
                    return ServiceResult<bool>.FailureResult("Hizmetin ödemesi henüz tamamlanmamış.");

                transaction.Status = TransactionStatus.Completed;
                transaction.UpdatedAt = DateTime.UtcNow;
                await transactionNode.PutAsync(transaction);

                await _userProfileService.AddPointsForAction(providerId, UserAction.ProvideService);

                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = transaction.BuyerId,
                    Title = "Hizmet Tamamlandı",
                    Message = $"{transaction.SellerName}, '{transaction.ProductTitle}' hizmetini tamamladığını bildirdi.",
                    Type = NotificationType.ServiceCompleted,
                    ActionUrl = nameof(Views.ServiceRequestsPage)
                });

                return ServiceResult<bool>.SuccessResult(true, "Hizmet başarıyla tamamlandı.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Hata oluştu.", NetworkHelper.GetUserFriendlyErrorMessage(ex));
            }
        }

        public async Task<ServiceResult<bool>> CompleteRequestAsync(string requestId, string currentUserId)
        {
            try
            {
                var requestNode = _firebaseClient.Child(Constants.ServiceRequestsCollection).Child(requestId);
                var request = await requestNode.OnceSingleAsync<ServiceRequest>();

                if (request == null) return ServiceResult<bool>.FailureResult("Talep bulunamadı.");

                if (request.RequesterId != currentUserId)
                    return ServiceResult<bool>.FailureResult("Bu işlemi yapmaya yetkiniz yok.");

                if (request.Status == ServiceRequestStatus.Completed)
                    return ServiceResult<bool>.FailureResult("Bu talep zaten tamamlanmış.");

                if (request.TimeCreditValue > 0 && !request.CreditsTransferred)
                {
                    var transferResult = await _userProfileService.TransferTimeCreditsAsync(
                        request.RequesterId,
                        request.ProviderId,
                        request.TimeCreditValue,
                        $"Hizmet tamamlandı: {request.ServiceTitle}"
                    );

                    if (!transferResult.Success)
                    {
                        return ServiceResult<bool>.FailureResult($"Kredi transferi başarısız: {transferResult.Message}");
                    }
                    request.CreditsTransferred = true;
                }

                request.Status = ServiceRequestStatus.Completed;
                request.CompletedAt = DateTime.UtcNow;
                await requestNode.PutAsync(request);

                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = request.ProviderId,
                    Title = "Hizmet Tamamlandı ve Kredi Kazandın!",
                    Message = $"{request.RequesterName}, '{request.ServiceTitle}' hizmetini tamamlandı olarak işaretledi. Hesabına {request.TimeCreditValue} saat kredi eklendi."
                });

                return ServiceResult<bool>.SuccessResult(true, "Hizmet başarıyla tamamlandı.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("İşlem sırasında hata oluştu.", ex.Message);
            }
        }

        public async Task<ServiceResult<bool>> ProviderFinishServiceAsync(string requestId, string providerId)
        {
            try
            {
                if (!NetworkHelper.HasInternetConnection())
                    return ServiceResult<bool>.FailureResult("İnternet bağlantısı yok.");

                var requestNode = _firebaseClient.Child(Constants.ServiceRequestsCollection).Child(requestId);
                var request = await requestNode.OnceSingleAsync<ServiceRequest>();

                if (request == null || request.ProviderId != providerId)
                    return ServiceResult<bool>.FailureResult("Yetkisiz işlem veya talep bulunamadı.");

                if (request.Status != ServiceRequestStatus.Accepted)
                    return ServiceResult<bool>.FailureResult("Bu hizmetin durumu 'Tamamlandı' olarak işaretlenmeye uygun değil.");

                if (request.Price > 0 && request.PaymentStatus != ServicePaymentStatus.Paid)
                    return ServiceResult<bool>.FailureResult("Hizmet bedeli henüz ödenmemiş.");

                request.Status = ServiceRequestStatus.AwaitingConfirmation;
                request.UpdatedAt = DateTime.UtcNow;
                await requestNode.PutAsync(request);

                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = request.RequesterId,
                    Title = "Hizmet Tamamlandı mı?",
                    Message = $"{request.ProviderName}, '{request.ServiceTitle}' hizmetini tamamladığını bildirdi. Lütfen onaylayın.",
                    Type = NotificationType.ServiceCompleted,
                    ActionUrl = nameof(Views.ServiceRequestsPage)
                });

                return ServiceResult<bool>.SuccessResult(true, "Hizmet tamamlandı bildirimi gönderildi.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Hata oluştu.", NetworkHelper.GetUserFriendlyErrorMessage(ex));
            }
        }

        public async Task<ServiceResult<bool>> RequesterConfirmServiceAsync(string requestId, string requesterId)
        {
            try
            {
                var requestNode = _firebaseClient.Child(Constants.ServiceRequestsCollection).Child(requestId);
                var request = await requestNode.OnceSingleAsync<ServiceRequest>();

                if (request == null || request.RequesterId != requesterId)
                    return ServiceResult<bool>.FailureResult("Yetkisiz işlem.");

                if (request.Status == ServiceRequestStatus.Completed)
                    return ServiceResult<bool>.FailureResult("Hizmet zaten tamamlanmış.");

                if (request.Status != ServiceRequestStatus.AwaitingConfirmation && request.Status != ServiceRequestStatus.Accepted)
                    return ServiceResult<bool>.FailureResult("Hizmet şu anda onaylanacak durumda değil.");

                if (request.TimeCreditValue > 0 && !request.CreditsTransferred)
                {
                    var transfer = await _userProfileService.TransferTimeCreditsAsync(
                        request.RequesterId, request.ProviderId, request.TimeCreditValue, $"Hizmet Onayı: {request.ServiceTitle}");

                    if (!transfer.Success) return ServiceResult<bool>.FailureResult("Kredi transferi başarısız: " + transfer.Message);
                    
                    request.CreditsTransferred = true;
                }

                request.Status = ServiceRequestStatus.Completed;
                request.CompletedAt = DateTime.UtcNow;
                request.UpdatedAt = DateTime.UtcNow;
                await requestNode.PutAsync(request);

                await _userProfileService.AddPointsForAction(request.ProviderId, UserAction.ProvideService);
                await _userProfileService.AddPointsForAction(request.RequesterId, UserAction.ReceiveService);

                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = request.ProviderId,
                    Title = "İşlem Başarıyla Kapatıldı",
                    Message = $"{request.RequesterName} hizmeti onayladı. Puan ve krediler hesabınıza eklendi.",
                    Type = NotificationType.ServiceCompleted
                });

                return ServiceResult<bool>.SuccessResult(true, "Hizmet başarıyla onaylandı ve tamamlandı.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Hata.", NetworkHelper.GetUserFriendlyErrorMessage(ex));
            }
        }
    }
}
