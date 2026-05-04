using KamPay.Models;

namespace KamPay.Services.CampusGuide;

public interface ICampaignManagementService
{
    Task<ServiceResult<List<Campaign>>> GetMyCampaignsAsync();
    Task<ServiceResult<Campaign>> CreateCampaignAsync(Campaign campaign);
    Task<ServiceResult<Campaign>> UpdateCampaignAsync(Campaign campaign);
    Task<ServiceResult<bool>> DeleteCampaignAsync(string campaignId);
}
