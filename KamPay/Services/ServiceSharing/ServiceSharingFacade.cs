using KamPay.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using KamPay.Services;

namespace KamPay.Services.ServiceSharing
{
    public class ServiceSharingFacade : IServiceSharingService
    {
        private readonly ServiceOfferService _offerService;
        private readonly ServiceRequestCrudService _crudService;
        private readonly ServiceRequestNegotiationService _negotiationService;
        private readonly ServiceRequestCompletionService _completionService;
        private readonly ICustomerRequestManager _customerRequestManager;
        private readonly IProviderProposalManager _providerProposalManager;

        public ServiceSharingFacade(
            ServiceOfferService offerService,
            ServiceRequestCrudService crudService,
            ServiceRequestNegotiationService negotiationService,
            ServiceRequestCompletionService completionService,
            ICustomerRequestManager customerRequestManager,
            IProviderProposalManager providerProposalManager)
        {
            _offerService = offerService;
            _crudService = crudService;
            _negotiationService = negotiationService;
            _completionService = completionService;
            _customerRequestManager = customerRequestManager;
            _providerProposalManager = providerProposalManager;
        }

        public Task<ServiceResult<ServiceOffer>> CreateServiceOfferAsync(ServiceOffer offer) => _offerService.CreateServiceOfferAsync(offer);
        public Task<ServiceResult<List<ServiceOffer>>> GetServiceOffersAsync(ServiceCategory? category = null) => _offerService.GetServiceOffersAsync(category);
        public Task<ServiceResult<ServiceOffer>> GetServiceOfferByIdAsync(string offerId) => _offerService.GetServiceOfferByIdAsync(offerId);
        public Task<ServiceResult<List<ServiceOffer>>> GetServiceOffersPagedAsync(int pageSize = 20, string? lastKey = null, ServiceCategory? category = null) => _offerService.GetServiceOffersPagedAsync(pageSize, lastKey, category);
        public Task<ServiceResult<bool>> UpdateServiceOfferAsync(ServiceOffer offer) => _offerService.UpdateServiceOfferAsync(offer);
        public Task<ServiceResult<bool>> DeleteServiceOfferAsync(string offerId) => _offerService.DeleteServiceOfferAsync(offerId);
        public Task<ServiceResult<bool>> ToggleAvailabilityAsync(string offerId, bool isAvailable) => _offerService.ToggleAvailabilityAsync(offerId, isAvailable);
        public Task<ServiceResult<bool>> UpdateUserInfoInServicesAsync(string userId, string? newName, string? newPhotoUrl) => _offerService.UpdateUserInfoInServicesAsync(userId, newName, newPhotoUrl);

        public Task<ServiceResult<ServiceRequest>> RequestServiceAsync(ServiceOffer offer, User requester, string message) => _crudService.RequestServiceAsync(offer, requester, message);
        public Task<ServiceResult<(List<ServiceRequest> Incoming, List<ServiceRequest> Outgoing)>> GetMyServiceRequestsAsync(string userId) => _crudService.GetMyServiceRequestsAsync(userId);
        public Task<ServiceResult<bool>> RespondToRequestAsync(string requestId, bool accept) => _crudService.RespondToRequestAsync(requestId, accept);
        public Task<ServiceResult<string>> StartConversationForRequestAsync(string requestId, string currentUserId) => _crudService.StartConversationForRequestAsync(requestId, currentUserId);
        
        public Task<ServiceResult<bool>> CompleteRequestAsync(string requestId, string currentUserId) => _completionService.CompleteRequestAsync(requestId, currentUserId);
        public Task<ServiceResult<bool>> CompleteServiceRequestAsync(string transactionId, string providerId) => _completionService.CompleteServiceRequestAsync(transactionId, providerId);
        public Task<ServiceResult<bool>> ProviderFinishServiceAsync(string requestId, string providerId) => _completionService.ProviderFinishServiceAsync(requestId, providerId);
        public Task<ServiceResult<bool>> RequesterConfirmServiceAsync(string requestId, string requesterId) => _completionService.RequesterConfirmServiceAsync(requestId, requesterId);
        
        public Task<ServiceResult<bool>> ProposePrice(string requestId, decimal proposedPrice, string currentUserId) => _negotiationService.ProposePrice(requestId, proposedPrice, currentUserId);
        public Task<ServiceResult<bool>> SendCounterOfferAsync(string requestId, decimal counterOffer, string currentUserId) => _negotiationService.SendCounterOfferAsync(requestId, counterOffer, currentUserId);
        public Task<ServiceResult<bool>> AcceptNegotiatedPriceAsync(string requestId, string currentUserId) => _negotiationService.AcceptNegotiatedPriceAsync(requestId, currentUserId);

        public Task<ServiceResult<CustomerServiceRequest>> CreateCustomerRequestAsync(CustomerServiceRequest request) => _customerRequestManager.CreateCustomerRequestAsync(request);
        public Task<ServiceResult<List<CustomerServiceRequest>>> GetCustomerRequestsAsync(ServiceCategory? category = null, string? location = null) => _customerRequestManager.GetCustomerRequestsAsync(category, location);
        public Task<ServiceResult<List<CustomerServiceRequest>>> GetCustomerRequestsPagedAsync(int pageSize = 20, string? lastKey = null, ServiceCategory? category = null) => _customerRequestManager.GetCustomerRequestsPagedAsync(pageSize, lastKey, category);
        public Task<ServiceResult<CustomerServiceRequest>> GetCustomerRequestByIdAsync(string requestId) => _customerRequestManager.GetCustomerRequestByIdAsync(requestId);
        public Task<ServiceResult<List<CustomerServiceRequest>>> GetMyCustomerRequestsAsync(string customerId) => _customerRequestManager.GetMyCustomerRequestsAsync(customerId);
        public Task<ServiceResult<bool>> UpdateCustomerRequestAsync(CustomerServiceRequest request) => _customerRequestManager.UpdateCustomerRequestAsync(request);
        public Task<ServiceResult<bool>> CancelCustomerRequestAsync(string requestId, string customerId) => _customerRequestManager.CancelCustomerRequestAsync(requestId, customerId);
        
        public Task<ServiceResult<ProviderProposal>> SendProposalAsync(ProviderProposal proposal) => _providerProposalManager.SendProposalAsync(proposal);
        public Task<ServiceResult<List<ProviderProposal>>> GetProposalsForRequestAsync(string customerRequestId) => _providerProposalManager.GetProposalsForRequestAsync(customerRequestId);
        public Task<ServiceResult<List<ProviderProposal>>> GetMyProposalsAsync(string providerId) => _providerProposalManager.GetMyProposalsAsync(providerId);
        public Task<ServiceResult<bool>> AcceptProposalAsync(string proposalId, string customerId) => _providerProposalManager.AcceptProposalAsync(proposalId, customerId);
        public Task<ServiceResult<bool>> RejectProposalAsync(string proposalId, string customerId, string? reason = null) => _providerProposalManager.RejectProposalAsync(proposalId, customerId, reason);
        public Task<ServiceResult<bool>> WithdrawProposalAsync(string proposalId, string providerId) => _providerProposalManager.WithdrawProposalAsync(proposalId, providerId);
        public Task<ServiceResult<ServiceRequest>> CreateServiceContractFromProposalAsync(string proposalId) => _providerProposalManager.CreateServiceContractFromProposalAsync(proposalId);
    }
}
