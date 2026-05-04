using System.Text.Json;
using KamPay.Models;

namespace KamPay.Services.CampusGuide;

internal static class FirebaseCampusGuideCache
{
    private const string PreferencesKey = "campus_guide_cache_v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static CampusGuideCacheSnapshot? _snapshot;

    public static bool HasFreshBusinesses(TimeSpan ttl) =>
        _snapshot?.Businesses.Count > 0 && DateTime.UtcNow - _snapshot.CachedAtUtc <= ttl;

    public static bool HasFreshCampaigns(string businessId, TimeSpan ttl) =>
        _snapshot?.CampaignsByBusinessId.ContainsKey(businessId) == true &&
        DateTime.UtcNow - _snapshot.CachedAtUtc <= ttl;

    public static List<MicroBusiness> GetBusinesses()
    {
        EnsureLoaded();
        return _snapshot?.Businesses.ToList() ?? new List<MicroBusiness>();
    }

    public static MicroBusiness? GetBusiness(string businessId)
    {
        EnsureLoaded();
        return _snapshot?.Businesses.FirstOrDefault(b => b.BusinessId == businessId);
    }

    public static List<Campaign> GetCampaigns(string businessId)
    {
        EnsureLoaded();
        return _snapshot?.CampaignsByBusinessId.TryGetValue(businessId, out var campaigns) == true
            ? campaigns.ToList()
            : new List<Campaign>();
    }

    public static void SetBusinesses(IEnumerable<MicroBusiness> businesses)
    {
        EnsureLoaded();
        _snapshot ??= new CampusGuideCacheSnapshot();
        _snapshot.Businesses = businesses.ToList();
        _snapshot.CachedAtUtc = DateTime.UtcNow;
        Persist();
    }

    public static void SetCampaigns(string businessId, IEnumerable<Campaign> campaigns)
    {
        EnsureLoaded();
        _snapshot ??= new CampusGuideCacheSnapshot();
        _snapshot.CampaignsByBusinessId[businessId] = campaigns.ToList();
        _snapshot.CachedAtUtc = DateTime.UtcNow;
        Persist();
    }

    private static void EnsureLoaded()
    {
        if (_snapshot != null)
            return;

        try
        {
            var json = Preferences.Get(PreferencesKey, string.Empty);
            _snapshot = string.IsNullOrWhiteSpace(json)
                ? new CampusGuideCacheSnapshot()
                : JsonSerializer.Deserialize<CampusGuideCacheSnapshot>(json, JsonOptions) ?? new CampusGuideCacheSnapshot();
        }
        catch
        {
            _snapshot = new CampusGuideCacheSnapshot();
        }
    }

    private static void Persist()
    {
        try
        {
            Preferences.Set(PreferencesKey, JsonSerializer.Serialize(_snapshot, JsonOptions));
        }
        catch
        {
            // Cache persistence is best-effort; the online path remains authoritative.
        }
    }
}
