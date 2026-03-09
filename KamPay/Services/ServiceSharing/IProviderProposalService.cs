using KamPay.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KamPay.Services.ServiceSharing
{
    /// <summary>
    /// ? ISP: Profesyonel Teklifleri (ProviderProposal) için Query Ýþlemleri
    /// ARMUT MODELÝ - Sadece teklif OKUMA iþlemlerini yapacak sýnýflar bu interface'i implement eder
    /// </summary>
    public interface IProviderProposalQueryService
    {
        Task<ServiceResult<List<ProviderProposal>>> GetProposalsForRequestAsync(string customerRequestId);
        Task<ServiceResult<List<ProviderProposal>>> GetMyProposalsAsync(string providerId);
    }

    /// <summary>
    /// ? ISP: Profesyonel Teklifleri için Command Ýþlemleri
    /// ARMUT MODELÝ - Sadece teklif YAZMA iþlemlerini yapacak sýnýflar bu interface'i implement eder
    /// </summary>
    public interface IProviderProposalCommandService
    {
        Task<ServiceResult<ProviderProposal>> SendProposalAsync(ProviderProposal proposal);
        Task<ServiceResult<bool>> AcceptProposalAsync(string proposalId, string customerId);
        Task<ServiceResult<bool>> RejectProposalAsync(string proposalId, string customerId, string? reason = null);
        Task<ServiceResult<bool>> WithdrawProposalAsync(string proposalId, string providerId);
        Task<ServiceResult<ServiceRequest>> CreateServiceContractFromProposalAsync(string proposalId);
    }
}
