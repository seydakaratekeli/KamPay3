// IServiceSharingService.cs

using KamPay.Models;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace KamPay.Services
{
    public interface IServiceSharingService
    {
        // === MEVCUT METODLAR (Eski sistem - Profesyonel hizmet paylaşımı) ===
        Task<ServiceResult<ServiceOffer>> CreateServiceOfferAsync(ServiceOffer offer);
        Task<ServiceResult<List<ServiceOffer>>> GetServiceOffersAsync(ServiceCategory? category = null);
        Task<ServiceResult<List<ServiceOffer>>> GetServiceOffersPagedAsync(int pageSize = 20, string? lastKey = null, ServiceCategory? category = null);
        Task<ServiceResult<ServiceRequest>> RequestServiceAsync(ServiceOffer offer, User requester, string message);
        Task<ServiceResult<(List<ServiceRequest> Incoming, List<ServiceRequest> Outgoing)>> GetMyServiceRequestsAsync(string userId);
        Task<ServiceResult<bool>> RespondToRequestAsync(string requestId, bool accept);
        Task<ServiceResult<bool>> CompleteRequestAsync(string requestId, string currentUserId);
        Task<ServiceResult<bool>> CompleteServiceRequestAsync(string transactionId, string providerId);
        Task<ServiceResult<bool>> ProviderFinishServiceAsync(string requestId, string providerId);
        Task<ServiceResult<bool>> RequesterConfirmServiceAsync(string requestId, string requesterId);
        Task<ServiceResult<PaymentDto>> CreatePaymentSimulationAsync(string requestId, string method);
        Task<ServiceResult<bool>> ConfirmPaymentSimulationAsync(string requestId, string paymentId, string? otp = null);
        Task<ServiceResult<bool>> SimulatePaymentAndCompleteAsync(string requestId);
        Task<ServiceResult<bool>> UpdateUserInfoInServicesAsync(string userId, string? newName, string? newPhotoUrl);
        Task<ServiceResult<string>> StartConversationForRequestAsync(string requestId, string currentUserId);
        Task<ServiceResult<bool>> ProposePrice(string requestId, decimal proposedPrice, string currentUserId);
        Task<ServiceResult<bool>> SendCounterOfferAsync(string requestId, decimal counterOffer, string currentUserId);
        Task<ServiceResult<bool>> AcceptNegotiatedPriceAsync(string requestId, string currentUserId);

        // === 🎯 YENİ METODLAR (Armut Modeli - Müşteri talep oluşturur) ===
        
        /// <summary>
        /// Müşteri yeni bir hizmet talebi oluşturur (Armut modeli)
        /// </summary>
        Task<ServiceResult<CustomerServiceRequest>> CreateCustomerRequestAsync(CustomerServiceRequest request);

        /// <summary>
        /// Tüm aktif müşteri taleplerini getirir (Profesyonellerin göreceği liste)
        /// </summary>
        Task<ServiceResult<List<CustomerServiceRequest>>> GetCustomerRequestsAsync(ServiceCategory? category = null, string? location = null);

        /// <summary>
        /// Sayfalama ile müşteri taleplerini getirir
        /// </summary>
        Task<ServiceResult<List<CustomerServiceRequest>>> GetCustomerRequestsPagedAsync(int pageSize = 20, string? lastKey = null, ServiceCategory? category = null);

        /// <summary>
        /// Belirli bir müşteri talebini ID ile getirir
        /// </summary>
        Task<ServiceResult<CustomerServiceRequest>> GetCustomerRequestByIdAsync(string requestId);

        /// <summary>
        /// Müşterinin kendi oluşturduğu talepleri getirir
        /// </summary>
        Task<ServiceResult<List<CustomerServiceRequest>>> GetMyCustomerRequestsAsync(string customerId);

        /// <summary>
        /// Müşteri talebini günceller
        /// </summary>
        Task<ServiceResult<bool>> UpdateCustomerRequestAsync(CustomerServiceRequest request);

        /// <summary>
        /// Müşteri talebini iptal eder
        /// </summary>
        Task<ServiceResult<bool>> CancelCustomerRequestAsync(string requestId, string customerId);

        /// <summary>
        /// Profesyonel bir müşteri talebine teklif gönderir
        /// </summary>
        Task<ServiceResult<ProviderProposal>> SendProposalAsync(ProviderProposal proposal);

        /// <summary>
        /// Belirli bir talebe gönderilen tüm teklifleri getirir
        /// </summary>
        Task<ServiceResult<List<ProviderProposal>>> GetProposalsForRequestAsync(string customerRequestId);

        /// <summary>
        /// Profesyonelin gönderdiği tüm teklifleri getirir
        /// </summary>
        Task<ServiceResult<List<ProviderProposal>>> GetMyProposalsAsync(string providerId);

        /// <summary>
        /// Müşteri bir teklifi kabul eder
        /// </summary>
        Task<ServiceResult<bool>> AcceptProposalAsync(string proposalId, string customerId);

        /// <summary>
        /// Müşteri bir teklifi reddeder
        /// </summary>
        Task<ServiceResult<bool>> RejectProposalAsync(string proposalId, string customerId, string? reason = null);

        /// <summary>
        /// Profesyonel kendi teklifini geri çeker
        /// </summary>
        Task<ServiceResult<bool>> WithdrawProposalAsync(string proposalId, string providerId);

        /// <summary>
        /// Teklif kabul edildikten sonra iş sözleşmesi oluşturur
        /// </summary>
        Task<ServiceResult<ServiceRequest>> CreateServiceContractFromProposalAsync(string proposalId);
    }
}
