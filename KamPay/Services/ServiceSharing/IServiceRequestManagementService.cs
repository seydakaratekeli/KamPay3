using KamPay.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KamPay.Services.ServiceSharing
{
    /// <summary>
    /// ? ISP: Hizmet Talepleri (ServiceRequest) için iþlemler
    /// ESKÝ SÝSTEM - Profesyonelin hizmet paylaþtýðý sistem
    /// </summary>
    public interface IServiceRequestManagementService
    {
        Task<ServiceResult<ServiceRequest>> RequestServiceAsync(ServiceOffer offer, User requester, string message);
        Task<ServiceResult<(List<ServiceRequest> Incoming, List<ServiceRequest> Outgoing)>> GetMyServiceRequestsAsync(string userId);
        Task<ServiceResult<bool>> RespondToRequestAsync(string requestId, bool accept);
        Task<ServiceResult<bool>> CompleteRequestAsync(string requestId, string currentUserId);
        Task<ServiceResult<bool>> CompleteServiceRequestAsync(string transactionId, string providerId);
        Task<ServiceResult<bool>> ProviderFinishServiceAsync(string requestId, string providerId);
        Task<ServiceResult<bool>> RequesterConfirmServiceAsync(string requestId, string requesterId);
        Task<ServiceResult<string>> StartConversationForRequestAsync(string requestId, string currentUserId);
    }
}
