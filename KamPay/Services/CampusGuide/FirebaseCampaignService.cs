using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Helpers;
using KamPay.Models;

namespace KamPay.Services.CampusGuide;

public class FirebaseCampaignService : ICampaignService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);
    private readonly FirebaseClient _firebaseClient;

    public FirebaseCampaignService(FirebaseClient firebaseClient)
    {
        _firebaseClient = firebaseClient ?? throw new ArgumentNullException(nameof(firebaseClient));
    }

    public async Task<ServiceResult<List<Campaign>>> GetActiveCampaignsByBusinessIdAsync(string businessId, bool forceRefresh = false)
    {
        if (string.IsNullOrWhiteSpace(businessId))
            return ServiceResult<List<Campaign>>.FailureResult("İşletme bilgisi bulunamadı.");

        if (!forceRefresh && FirebaseCampusGuideCache.HasFreshCampaigns(businessId, CacheTtl))
        {
            return ServiceResult<List<Campaign>>.SuccessResult(FirebaseCampusGuideCache.GetCampaigns(businessId));
        }

        try
        {
            var snapshot = await _firebaseClient
                .Child(Constants.CampaignsCollection)
                .OrderBy("BusinessId")
                .EqualTo(businessId)
                .OnceAsync<Campaign>();

            var utcNow = DateTime.UtcNow;
            var campaigns = snapshot
                .Where(item => item.Object != null)
                .Select(item =>
                {
                    var campaign = item.Object;
                    if (string.IsNullOrWhiteSpace(campaign.CampaignId))
                        campaign.CampaignId = item.Key;
                    return campaign;
                })
                .Where(c => c.BusinessId == businessId && c.IsCurrentlyValid(utcNow))
                .OrderBy(c => c.DisplayOrder)
                .ThenBy(c => c.EndsAt)
                .ToList();

            FirebaseCampusGuideCache.SetCampaigns(businessId, campaigns);
            return ServiceResult<List<Campaign>>.SuccessResult(campaigns);
        }
        catch (Exception ex)
        {
            var cached = FirebaseCampusGuideCache.GetCampaigns(businessId)
                .Where(c => c.IsCurrentlyValid(DateTime.UtcNow))
                .ToList();

            if (cached.Count > 0)
            {
                return ServiceResult<List<Campaign>>.SuccessResult(
                    cached,
                    "Güncel kampanyalar alınamadı, kayıtlı veriler gösteriliyor.");
            }

            return ServiceResult<List<Campaign>>.FailureResult("Kampanyalar yüklenemedi.", ex.Message);
        }
    }
}
