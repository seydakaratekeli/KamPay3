// IServiceSharingService.cs

using KamPay.Models;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace KamPay.Services
{
    public interface IServiceSharingService
    {
        Task<ServiceResult<ServiceOffer>> CreateServiceOfferAsync(ServiceOffer offer);
        Task<ServiceResult<List<ServiceOffer>>> GetServiceOffersAsync(ServiceCategory? category = null);

        Task<ServiceResult<ServiceRequest>> RequestServiceAsync(ServiceOffer offer, User requester, string message);
        Task<ServiceResult<(List<ServiceRequest> Incoming, List<ServiceRequest> Outgoing)>> GetMyServiceRequestsAsync(string userId);
        Task<ServiceResult<bool>> RespondToRequestAsync(string requestId, bool accept);

        Task<ServiceResult<bool>> CompleteRequestAsync(string requestId, string currentUserId); // mevcut (kredi)

        // --- YEN�: �cretli (sim�lasyon) ak��� ---
        Task<ServiceResult<PaymentDto>> CreatePaymentSimulationAsync(string requestId, string method /* "CardSim" | "BankTransferSim" | "WalletSim" */);
        Task<ServiceResult<bool>> ConfirmPaymentSimulationAsync(string requestId, string paymentId, string? otp = null);
        Task<ServiceResult<bool>> SimulatePaymentAndCompleteAsync(string requestId, string currentUserId, PaymentMethodType method = PaymentMethodType.CardSim, string? maskedCardLast4 = null);

        /// <summary>
        /// Kullanıcının tüm hizmetlerindeki isim ve profil fotoğrafı bilgilerini günceller
        /// </summary>
        Task<ServiceResult<bool>> UpdateUserInfoInServicesAsync(string userId, string newName, string newPhotoUrl);
    }
}
