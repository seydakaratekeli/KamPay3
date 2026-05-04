using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Helpers;
using KamPay.Models;
using KamPay.Services.Auth;

namespace KamPay.Services.CampusGuide;

public class FirebaseBusinessManagementService : IBusinessManagementService
{
    private readonly FirebaseClient _firebaseClient;
    private readonly IAuthenticationService _authService;

    public FirebaseBusinessManagementService(FirebaseClient firebaseClient, IAuthenticationService authService)
    {
        _firebaseClient = firebaseClient ?? throw new ArgumentNullException(nameof(firebaseClient));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
    }

    public async Task<ServiceResult<MicroBusiness>> GetMyBusinessAsync()
    {
        var user = await _authService.GetCurrentUserAsync();
        if (user == null || user.Role != UserRole.BusinessOwner || string.IsNullOrWhiteSpace(user.BusinessId))
            return ServiceResult<MicroBusiness>.FailureResult("İşletme hesabı bulunamadı.");

        var business = await _firebaseClient
            .Child(Constants.MicroBusinessesCollection)
            .Child(user.BusinessId)
            .OnceSingleAsync<MicroBusiness>();

        if (business == null || business.OwnerUserId != user.UserId)
            return ServiceResult<MicroBusiness>.FailureResult("İşletme kaydı bulunamadı.");

        business.BusinessId = user.BusinessId;
        return ServiceResult<MicroBusiness>.SuccessResult(business);
    }

    public async Task<ServiceResult<MicroBusiness>> UpdateMyBusinessAsync(MicroBusiness business)
    {
        var current = await GetMyBusinessAsync();
        if (!current.Success || current.Data == null)
            return ServiceResult<MicroBusiness>.FailureResult(current.Message, current.Errors.ToArray());

        var existing = current.Data;
        existing.Name = business.Name.Trim();
        existing.Description = business.Description.Trim();
        existing.Category = business.Category;
        existing.CategoryName = business.CategoryName.Trim();
        existing.Location = business.Location.Trim();
        existing.PhoneNumber = business.PhoneNumber.Trim();
        existing.InstagramUrl = business.InstagramUrl.Trim();
        existing.WebsiteUrl = business.WebsiteUrl.Trim();
        existing.LogoUrl = business.LogoUrl.Trim();
        existing.CoverImageUrl = business.CoverImageUrl.Trim();
        existing.Tags = business.Tags;
        existing.UpdatedAt = DateTime.UtcNow;

        await _firebaseClient
            .Child(Constants.MicroBusinessesCollection)
            .Child(existing.BusinessId)
            .PutAsync(existing);

        return ServiceResult<MicroBusiness>.SuccessResult(existing, "İşletme bilgileri güncellendi.");
    }

    public async Task<ServiceResult<List<MicroBusiness>>> GetPendingBusinessesAsync()
    {
        var admin = await RequireAdminAsync();
        if (!admin.Success)
            return ServiceResult<List<MicroBusiness>>.FailureResult(admin.Message);

        var snapshot = await _firebaseClient
            .Child(Constants.MicroBusinessesCollection)
            .OrderBy("VerificationStatus")
            .EqualTo((int)BusinessVerificationStatus.PendingReview)
            .OnceAsync<MicroBusiness>();

        var businesses = snapshot
            .Where(x => x.Object != null)
            .Select(x =>
            {
                x.Object.BusinessId = string.IsNullOrWhiteSpace(x.Object.BusinessId) ? x.Key : x.Object.BusinessId;
                return x.Object;
            })
            .OrderBy(x => x.SubmittedAt ?? x.CreatedAt)
            .ToList();

        return ServiceResult<List<MicroBusiness>>.SuccessResult(businesses);
    }

    public async Task<ServiceResult<bool>> VerifyBusinessAsync(string businessId)
    {
        var admin = await RequireAdminAsync();
        if (!admin.Success)
            return ServiceResult<bool>.FailureResult(admin.Message);

        var business = await GetBusinessForAdminAsync(businessId);
        if (business == null)
            return ServiceResult<bool>.FailureResult("İşletme bulunamadı.");

        business.IsVerified = true;
        business.IsActive = true;
        business.VerificationStatus = BusinessVerificationStatus.Verified;
        business.VerifiedAt = DateTime.UtcNow;
        business.VerifiedByAdminId = admin.Data!.UserId;
        business.RejectionReason = string.Empty;
        business.UpdatedAt = DateTime.UtcNow;

        await SaveBusinessAsync(business);
        return ServiceResult<bool>.SuccessResult(true, "İşletme doğrulandı.");
    }

    public async Task<ServiceResult<bool>> RejectBusinessAsync(string businessId, string reason)
    {
        var admin = await RequireAdminAsync();
        if (!admin.Success)
            return ServiceResult<bool>.FailureResult(admin.Message);

        var business = await GetBusinessForAdminAsync(businessId);
        if (business == null)
            return ServiceResult<bool>.FailureResult("İşletme bulunamadı.");

        business.IsVerified = false;
        business.VerificationStatus = BusinessVerificationStatus.Rejected;
        business.RejectionReason = string.IsNullOrWhiteSpace(reason) ? "Başvuru reddedildi." : reason.Trim();
        business.UpdatedAt = DateTime.UtcNow;

        await SaveBusinessAsync(business);
        return ServiceResult<bool>.SuccessResult(true, "İşletme başvurusu reddedildi.");
    }

    public async Task<ServiceResult<bool>> SuspendBusinessAsync(string businessId, string reason)
    {
        var admin = await RequireAdminAsync();
        if (!admin.Success)
            return ServiceResult<bool>.FailureResult(admin.Message);

        var business = await GetBusinessForAdminAsync(businessId);
        if (business == null)
            return ServiceResult<bool>.FailureResult("İşletme bulunamadı.");

        business.IsActive = false;
        business.IsVerified = false;
        business.VerificationStatus = BusinessVerificationStatus.Suspended;
        business.AdminNote = reason.Trim();
        business.UpdatedAt = DateTime.UtcNow;

        await SaveBusinessAsync(business);
        return ServiceResult<bool>.SuccessResult(true, "İşletme askıya alındı.");
    }

    private async Task<MicroBusiness?> GetBusinessForAdminAsync(string businessId)
    {
        if (string.IsNullOrWhiteSpace(businessId))
            return null;

        var business = await _firebaseClient
            .Child(Constants.MicroBusinessesCollection)
            .Child(businessId)
            .OnceSingleAsync<MicroBusiness>();

        if (business != null)
            business.BusinessId = businessId;

        return business;
    }

    private Task SaveBusinessAsync(MicroBusiness business) =>
        _firebaseClient.Child(Constants.MicroBusinessesCollection).Child(business.BusinessId).PutAsync(business);

    private async Task<ServiceResult<User>> RequireAdminAsync()
    {
        var user = await _authService.GetCurrentUserAsync();
        return user?.Role == UserRole.Admin
            ? ServiceResult<User>.SuccessResult(user)
            : ServiceResult<User>.FailureResult("Bu işlem için admin yetkisi gerekir.");
    }
}
