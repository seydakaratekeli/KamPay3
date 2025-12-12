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

        // / Mesajlaşma ve Pazarlık Metodları
        
        /// <summary>
        /// Hizmet talebi için konuşma başlatır (veya mevcut konuşmayı döndürür)
        /// </summary>
        Task<ServiceResult<string>> StartConversationForRequestAsync(string requestId, string currentUserId);
        
        /// <summary>
        /// Talep eden kişinin fiyat teklifi göndermesi
        /// </summary>
        Task<ServiceResult<bool>> ProposePrice(string requestId, decimal proposedPrice, string currentUserId);
        
        /// <summary>
        /// Hizmet sağlayıcısının karşı teklif göndermesi
        /// </summary>
        Task<ServiceResult<bool>> SendCounterOfferAsync(string requestId, decimal counterOffer, string currentUserId);
        
        /// <summary>
        /// Teklifi kabul etme (hem talep eden hem de sağlayıcı kullanabilir)
        /// </summary>
        Task<ServiceResult<bool>> AcceptNegotiatedPriceAsync(string requestId, string currentUserId);
    }
}
