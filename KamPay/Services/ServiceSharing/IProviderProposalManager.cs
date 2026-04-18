using KamPay.Models;

namespace KamPay.Services;

/// <summary>
/// ?? ARMUT MODELÝ: Profesyonel teklif yönetimi
/// Single Responsibility: Sadece profesyonellerin müþteri taleplerine gönderdiði tekliflerin yönetimi
/// </summary>
public interface IProviderProposalManager
{
    /// <summary>
    /// Profesyonel bir müþteri talebine teklif gönderir
    /// </summary>
    Task<ServiceResult<ProviderProposal>> SendProposalAsync(ProviderProposal proposal);

    /// <summary>
    /// Belirli bir talebe gönderilen tüm teklifleri getirir
    /// </summary>
    Task<ServiceResult<List<ProviderProposal>>> GetProposalsForRequestAsync(string customerRequestId);

    /// <summary>
    /// Profesyonelin gönderdiði tüm teklifleri getirir
    /// </summary>
    Task<ServiceResult<List<ProviderProposal>>> GetMyProposalsAsync(string providerId);

    /// <summary>
    /// Belirli bir teklifi ID ile getirir
    /// </summary>
    Task<ServiceResult<ProviderProposal>> GetProposalByIdAsync(string proposalId);

    /// <summary>
    /// Müþteri bir teklifi kabul eder
    /// </summary>
    Task<ServiceResult<bool>> AcceptProposalAsync(string proposalId, string customerId);

    /// <summary>
    /// Müþteri bir teklifi reddeder
    /// </summary>
    Task<ServiceResult<bool>> RejectProposalAsync(string proposalId, string customerId, string? reason = null);

    /// <summary>
    /// Profesyonel kendi teklifini geri çeker
    /// </summary>
    Task<ServiceResult<bool>> WithdrawProposalAsync(string proposalId, string providerId);

    /// <summary>
    /// Teklif kabul edildikten sonra iþ sözleþmesi oluþturur
    /// </summary>
    Task<ServiceResult<ServiceRequest>> CreateServiceContractFromProposalAsync(string proposalId);
}
