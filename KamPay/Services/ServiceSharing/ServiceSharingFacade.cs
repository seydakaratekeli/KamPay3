using KamPay.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using KamPay.Services;

namespace KamPay.Services.ServiceSharing
{
    public class ServiceSharingFacade : IServiceSharingService
    {
        private readonly ServiceOfferService _offerService;
        private readonly ServiceRequestService _requestService;

        public ServiceSharingFacade(ServiceOfferService offerService, ServiceRequestService requestService)
        {
            _offerService = offerService;
            _requestService = requestService;
        }

        public Task<ServiceResult<ServiceOffer>> CreateServiceOfferAsync(ServiceOffer offer) => _offerService.CreateServiceOfferAsync(offer);
        public Task<ServiceResult<List<ServiceOffer>>> GetServiceOffersAsync(ServiceCategory? category = null) => _offerService.GetServiceOffersAsync(category);
        public Task<ServiceResult<List<ServiceOffer>>> GetServiceOffersPagedAsync(int pageSize = 20, string? lastKey = null, ServiceCategory? category = null) => _offerService.GetServiceOffersPagedAsync(pageSize, lastKey, category);
        public Task<ServiceResult<bool>> UpdateUserInfoInServicesAsync(string userId, string? newName, string? newPhotoUrl) => _offerService.UpdateUserInfoInServicesAsync(userId, newName, newPhotoUrl);

        public Task<ServiceResult<ServiceRequest>> RequestServiceAsync(ServiceOffer offer, User requester, string message) => _requestService.RequestServiceAsync(offer, requester, message);
        public Task<ServiceResult<(List<ServiceRequest> Incoming, List<ServiceRequest> Outgoing)>> GetMyServiceRequestsAsync(string userId) => _requestService.GetMyServiceRequestsAsync(userId);
        public Task<ServiceResult<bool>> RespondToRequestAsync(string requestId, bool accept) => _requestService.RespondToRequestAsync(requestId, accept);
        public Task<ServiceResult<bool>> CompleteRequestAsync(string requestId, string currentUserId) => _requestService.CompleteRequestAsync(requestId, currentUserId);
        public Task<ServiceResult<bool>> CompleteServiceRequestAsync(string transactionId, string providerId) => _requestService.CompleteServiceRequestAsync(transactionId, providerId);
        public Task<ServiceResult<bool>> ProviderFinishServiceAsync(string requestId, string providerId) => _requestService.ProviderFinishServiceAsync(requestId, providerId);
        public Task<ServiceResult<bool>> RequesterConfirmServiceAsync(string requestId, string requesterId) => _requestService.RequesterConfirmServiceAsync(requestId, requesterId);
        public Task<ServiceResult<string>> StartConversationForRequestAsync(string requestId, string currentUserId) => _requestService.StartConversationForRequestAsync(requestId, currentUserId);
        public Task<ServiceResult<bool>> ProposePrice(string requestId, decimal proposedPrice, string currentUserId) => _requestService.ProposePrice(requestId, proposedPrice, currentUserId);
        public Task<ServiceResult<bool>> SendCounterOfferAsync(string requestId, decimal counterOffer, string currentUserId) => _requestService.SendCounterOfferAsync(requestId, counterOffer, currentUserId);
        public Task<ServiceResult<bool>> AcceptNegotiatedPriceAsync(string requestId, string currentUserId) => _requestService.AcceptNegotiatedPriceAsync(requestId, currentUserId);

        public Task<ServiceResult<CustomerServiceRequest>> CreateCustomerRequestAsync(CustomerServiceRequest request) => _requestService.CreateCustomerRequestAsync(request);
        public Task<ServiceResult<List<CustomerServiceRequest>>> GetCustomerRequestsAsync(ServiceCategory? category = null, string? location = null) => _requestService.GetCustomerRequestsAsync(category, location);
        public Task<ServiceResult<List<CustomerServiceRequest>>> GetCustomerRequestsPagedAsync(int pageSize = 20, string? lastKey = null, ServiceCategory? category = null) => _requestService.GetCustomerRequestsPagedAsync(pageSize, lastKey, category);
        public Task<ServiceResult<CustomerServiceRequest>> GetCustomerRequestByIdAsync(string requestId) => _requestService.GetCustomerRequestByIdAsync(requestId);
        public Task<ServiceResult<List<CustomerServiceRequest>>> GetMyCustomerRequestsAsync(string customerId) => _requestService.GetMyCustomerRequestsAsync(customerId);
        public Task<ServiceResult<bool>> UpdateCustomerRequestAsync(CustomerServiceRequest request) => _requestService.UpdateCustomerRequestAsync(request);
        public Task<ServiceResult<bool>> CancelCustomerRequestAsync(string requestId, string customerId) => _requestService.CancelCustomerRequestAsync(requestId, customerId);
        public Task<ServiceResult<ProviderProposal>> SendProposalAsync(ProviderProposal proposal) => _requestService.SendProposalAsync(proposal);
        public Task<ServiceResult<List<ProviderProposal>>> GetProposalsForRequestAsync(string customerRequestId) => _requestService.GetProposalsForRequestAsync(customerRequestId);
        public Task<ServiceResult<List<ProviderProposal>>> GetMyProposalsAsync(string providerId) => _requestService.GetMyProposalsAsync(providerId);
        public Task<ServiceResult<bool>> AcceptProposalAsync(string proposalId, string customerId) => _requestService.AcceptProposalAsync(proposalId, customerId);
        public Task<ServiceResult<bool>> RejectProposalAsync(string proposalId, string customerId, string? reason = null) => _requestService.RejectProposalAsync(proposalId, customerId, reason);
        public Task<ServiceResult<bool>> WithdrawProposalAsync(string proposalId, string providerId) => _requestService.WithdrawProposalAsync(proposalId, providerId);
        public Task<ServiceResult<ServiceRequest>> CreateServiceContractFromProposalAsync(string proposalId) => _requestService.CreateServiceContractFromProposalAsync(proposalId);
    }
}
