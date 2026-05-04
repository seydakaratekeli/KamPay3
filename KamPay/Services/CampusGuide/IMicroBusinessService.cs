using KamPay.Models;

namespace KamPay.Services.CampusGuide;

public interface IMicroBusinessService
{
    Task<ServiceResult<List<MicroBusiness>>> GetVerifiedBusinessesAsync(bool forceRefresh = false);
    Task<ServiceResult<MicroBusiness>> GetBusinessByIdAsync(string businessId, bool forceRefresh = false);
}
