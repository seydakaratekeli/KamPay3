using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Helpers;
using KamPay.Models;
using KamPay.Services.Auth;

namespace KamPay.Services.CampusGuide;

public class FirebaseCampaignManagementService : ICampaignManagementService
{
    private readonly FirebaseClient _firebaseClient;
    private readonly IAuthenticationService _authService;
    private readonly IBusinessManagementService _businessManagementService;

    public FirebaseCampaignManagementService(
        FirebaseClient firebaseClient,
        IAuthenticationService authService,
        IBusinessManagementService businessManagementService)
    {
        _firebaseClient = firebaseClient ?? throw new ArgumentNullException(nameof(firebaseClient));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _businessManagementService = businessManagementService ?? throw new ArgumentNullException(nameof(businessManagementService));
    }

    public async Task<ServiceResult<List<Campaign>>> GetMyCampaignsAsync()
    {
        var businessResult = await RequireVerifiedBusinessAsync();
        if (!businessResult.Success || businessResult.Data == null)
            return ServiceResult<List<Campaign>>.FailureResult(businessResult.Message, businessResult.Errors.ToArray());

        var snapshot = await _firebaseClient
            .Child(Constants.CampaignsCollection)
            .OrderBy("BusinessId")
            .EqualTo(businessResult.Data.BusinessId)
            .OnceAsync<Campaign>();

        var campaigns = snapshot
            .Where(x => x.Object != null)
            .Select(x =>
            {
                x.Object.CampaignId = string.IsNullOrWhiteSpace(x.Object.CampaignId) ? x.Key : x.Object.CampaignId;
                return x.Object;
            })
            .OrderByDescending(x => x.CreatedAt)
            .ToList();

        return ServiceResult<List<Campaign>>.SuccessResult(campaigns);
    }

    public async Task<ServiceResult<Campaign>> CreateCampaignAsync(Campaign campaign)
    {
        var businessResult = await RequireVerifiedBusinessAsync();
        if (!businessResult.Success || businessResult.Data == null)
            return ServiceResult<Campaign>.FailureResult(businessResult.Message, businessResult.Errors.ToArray());

        var user = await _authService.GetCurrentUserAsync();
        var validation = ValidateCampaign(campaign);
        if (!validation.IsValid)
            return ServiceResult<Campaign>.FailureResult("Kampanya bilgileri geçersiz.", validation.Errors.ToArray());

        campaign.CampaignId = Guid.NewGuid().ToString();
        campaign.BusinessId = businessResult.Data.BusinessId;
        campaign.CreatedByUserId = user.UserId;
        campaign.IsActive = true;
        campaign.Status = CampaignStatus.Active;
        campaign.CreatedAt = DateTime.UtcNow;
        campaign.UpdatedAt = null;

        await _firebaseClient.Child(Constants.CampaignsCollection).Child(campaign.CampaignId).PutAsync(campaign);
        return ServiceResult<Campaign>.SuccessResult(campaign, "Kampanya yayınlandı.");
    }

    public async Task<ServiceResult<Campaign>> UpdateCampaignAsync(Campaign campaign)
    {
        var businessResult = await RequireVerifiedBusinessAsync();
        if (!businessResult.Success || businessResult.Data == null)
            return ServiceResult<Campaign>.FailureResult(businessResult.Message, businessResult.Errors.ToArray());

        if (string.IsNullOrWhiteSpace(campaign.CampaignId))
            return ServiceResult<Campaign>.FailureResult("Kampanya bulunamadı.");

        var existing = await _firebaseClient
            .Child(Constants.CampaignsCollection)
            .Child(campaign.CampaignId)
            .OnceSingleAsync<Campaign>();

        if (existing == null || existing.BusinessId != businessResult.Data.BusinessId)
            return ServiceResult<Campaign>.FailureResult("Bu kampanyayı düzenleme yetkiniz yok.");

        var validation = ValidateCampaign(campaign);
        if (!validation.IsValid)
            return ServiceResult<Campaign>.FailureResult("Kampanya bilgileri geçersiz.", validation.Errors.ToArray());

        existing.Title = campaign.Title.Trim();
        existing.Description = campaign.Description.Trim();
        existing.BadgeText = campaign.BadgeText.Trim();
        existing.ImageUrl = campaign.ImageUrl.Trim();
        existing.StartsAt = campaign.StartsAt;
        existing.EndsAt = campaign.EndsAt;
        existing.IsActive = campaign.IsActive;
        existing.Status = campaign.IsActive ? CampaignStatus.Active : CampaignStatus.Suspended;
        existing.DisplayOrder = campaign.DisplayOrder;
        existing.UpdatedAt = DateTime.UtcNow;

        await _firebaseClient.Child(Constants.CampaignsCollection).Child(existing.CampaignId).PutAsync(existing);
        return ServiceResult<Campaign>.SuccessResult(existing, "Kampanya güncellendi.");
    }

    public async Task<ServiceResult<bool>> DeleteCampaignAsync(string campaignId)
    {
        var businessResult = await RequireVerifiedBusinessAsync();
        if (!businessResult.Success || businessResult.Data == null)
            return ServiceResult<bool>.FailureResult(businessResult.Message, businessResult.Errors.ToArray());

        var existing = await _firebaseClient.Child(Constants.CampaignsCollection).Child(campaignId).OnceSingleAsync<Campaign>();
        if (existing == null || existing.BusinessId != businessResult.Data.BusinessId)
            return ServiceResult<bool>.FailureResult("Bu kampanyayı silme yetkiniz yok.");

        await _firebaseClient.Child(Constants.CampaignsCollection).Child(campaignId).DeleteAsync();
        return ServiceResult<bool>.SuccessResult(true, "Kampanya silindi.");
    }

    private async Task<ServiceResult<MicroBusiness>> RequireVerifiedBusinessAsync()
    {
        var user = await _authService.GetCurrentUserAsync();
        if (user == null || user.Role != UserRole.BusinessOwner)
            return ServiceResult<MicroBusiness>.FailureResult("Bu işlem için işletme hesabıyla giriş yapılmalı.");

        var businessResult = await _businessManagementService.GetMyBusinessAsync();
        if (!businessResult.Success || businessResult.Data == null)
            return businessResult;

        if (!businessResult.Data.IsPubliclyVisible)
            return ServiceResult<MicroBusiness>.FailureResult("Kampanya yönetimi için işletme admin tarafından doğrulanmış olmalı.");

        return businessResult;
    }

    private static ValidationResult ValidateCampaign(Campaign campaign)
    {
        var result = new ValidationResult();
        if (string.IsNullOrWhiteSpace(campaign.Title))
            result.AddError("Kampanya başlığı zorunludur.");
        if (string.IsNullOrWhiteSpace(campaign.Description))
            result.AddError("Kampanya açıklaması zorunludur.");
        if (campaign.EndsAt <= campaign.StartsAt)
            result.AddError("Bitiş tarihi başlangıç tarihinden sonra olmalıdır.");
        return result;
    }
}
