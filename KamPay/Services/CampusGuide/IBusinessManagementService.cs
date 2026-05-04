using KamPay.Models;

namespace KamPay.Services.CampusGuide;

public interface IBusinessManagementService
{
    Task<ServiceResult<MicroBusiness>> GetMyBusinessAsync();
    Task<ServiceResult<MicroBusiness>> UpdateMyBusinessAsync(MicroBusiness business);
    Task<ServiceResult<List<MicroBusiness>>> GetPendingBusinessesAsync();
    Task<ServiceResult<bool>> VerifyBusinessAsync(string businessId);
    Task<ServiceResult<bool>> RejectBusinessAsync(string businessId, string reason);
    Task<ServiceResult<bool>> SuspendBusinessAsync(string businessId, string reason);
}
