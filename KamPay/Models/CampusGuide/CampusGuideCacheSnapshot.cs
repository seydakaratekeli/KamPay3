namespace KamPay.Models;

public sealed class CampusGuideCacheSnapshot
{
    public List<MicroBusiness> Businesses { get; set; } = new();
    public Dictionary<string, List<Campaign>> CampaignsByBusinessId { get; set; } = new();
    public DateTime CachedAtUtc { get; set; } = DateTime.UtcNow;
}
