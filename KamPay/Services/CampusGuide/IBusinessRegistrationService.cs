using KamPay.Models;

namespace KamPay.Services.CampusGuide;

public interface IBusinessRegistrationService
{
    ValidationResult ValidateBusinessRegistration(BusinessRegistrationRequest request);
    Task<ServiceResult<MicroBusiness>> RegisterBusinessAsync(BusinessRegistrationRequest request);
}
