using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Helpers;
using KamPay.Models;

namespace KamPay.Services.CampusGuide;

public class FirebaseMicroBusinessService : IMicroBusinessService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);
    private readonly FirebaseClient _firebaseClient;

    public FirebaseMicroBusinessService(FirebaseClient firebaseClient)
    {
        _firebaseClient = firebaseClient ?? throw new ArgumentNullException(nameof(firebaseClient));
    }

    public async Task<ServiceResult<List<MicroBusiness>>> GetVerifiedBusinessesAsync(bool forceRefresh = false)
    {
        if (!forceRefresh && FirebaseCampusGuideCache.HasFreshBusinesses(CacheTtl))
        {
            return ServiceResult<List<MicroBusiness>>.SuccessResult(FirebaseCampusGuideCache.GetBusinesses());
        }

        try
        {
            var snapshot = await _firebaseClient
                .Child(Constants.MicroBusinessesCollection)
                .OnceAsync<MicroBusiness>();

            var businesses = snapshot
                .Where(item => item.Object != null)
                .Select(item =>
                {
                    var business = item.Object;
                    if (string.IsNullOrWhiteSpace(business.BusinessId))
                        business.BusinessId = item.Key;
                    return business;
                })
                .Where(IsPubliclyVisible)
                .OrderBy(b => b.DisplayOrder)
                .ThenBy(b => b.Name)
                .ToList();

            FirebaseCampusGuideCache.SetBusinesses(businesses);
            return ServiceResult<List<MicroBusiness>>.SuccessResult(businesses);
        }
        catch (Exception ex)
        {
            var cached = FirebaseCampusGuideCache.GetBusinesses();
            if (cached.Count > 0)
            {
                return ServiceResult<List<MicroBusiness>>.SuccessResult(
                    cached,
                    "Güncel kampüs rehberi alınamadı, kayıtlı veriler gösteriliyor.");
            }

            return ServiceResult<List<MicroBusiness>>.FailureResult(
                "Kampüs rehberi şu anda yüklenemiyor.",
                ex.Message);
        }
    }

    public async Task<ServiceResult<MicroBusiness>> GetBusinessByIdAsync(string businessId, bool forceRefresh = false)
    {
        if (string.IsNullOrWhiteSpace(businessId))
            return ServiceResult<MicroBusiness>.FailureResult("İşletme bilgisi bulunamadı.");

        if (!forceRefresh)
        {
            var cachedBusiness = FirebaseCampusGuideCache.GetBusiness(businessId);
            if (cachedBusiness != null)
                return ServiceResult<MicroBusiness>.SuccessResult(cachedBusiness);
        }

        try
        {
            var business = await _firebaseClient
                .Child(Constants.MicroBusinessesCollection)
                .Child(businessId)
                .OnceSingleAsync<MicroBusiness>();

            if (business == null || !IsPubliclyVisible(business))
                return ServiceResult<MicroBusiness>.FailureResult("İşletme yayında değil veya doğrulanmamış.");

            business.BusinessId = string.IsNullOrWhiteSpace(business.BusinessId) ? businessId : business.BusinessId;
            return ServiceResult<MicroBusiness>.SuccessResult(business);
        }
        catch (Exception ex)
        {
            var cachedBusiness = FirebaseCampusGuideCache.GetBusiness(businessId);
            if (cachedBusiness != null)
            {
                return ServiceResult<MicroBusiness>.SuccessResult(
                    cachedBusiness,
                    "Güncel işletme bilgisi alınamadı, kayıtlı veri gösteriliyor.");
            }

            return ServiceResult<MicroBusiness>.FailureResult("İşletme bilgisi yüklenemedi.", ex.Message);
        }
    }

    private static bool IsPubliclyVisible(MicroBusiness business) =>
        business.IsPubliclyVisible &&
        !string.IsNullOrWhiteSpace(business.Name) &&
        !string.IsNullOrWhiteSpace(business.BusinessId);
}
