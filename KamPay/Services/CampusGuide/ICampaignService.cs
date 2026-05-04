using KamPay.Models;

namespace KamPay.Services.CampusGuide;

public interface ICampaignService
{
    Task<ServiceResult<List<Campaign>>> GetActiveCampaignsByBusinessIdAsync(string businessId, bool forceRefresh = false);
}
