using CommunityToolkit.Mvvm.ComponentModel;

namespace KamPay.Models;

public enum CampaignStatus
{
    Draft = 0,
    Active = 1,
    Expired = 2,
    Suspended = 3
}

public partial class Campaign : ObservableObject
{
    [ObservableProperty] private string campaignId = string.Empty;
    [ObservableProperty] private string businessId = string.Empty;
    [ObservableProperty] private string title = string.Empty;
    [ObservableProperty] private string description = string.Empty;
    [ObservableProperty] private string imageUrl = string.Empty;
    [ObservableProperty] private string badgeText = string.Empty;
    [ObservableProperty] private bool isActive;
    [ObservableProperty] private int displayOrder;
    [ObservableProperty] private string createdByUserId = string.Empty;
    [ObservableProperty] private CampaignStatus status = CampaignStatus.Active;

    public DateTime StartsAt { get; set; } = DateTime.UtcNow;
    public DateTime EndsAt { get; set; } = DateTime.UtcNow.AddDays(7);
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public bool IsCurrentlyValid(DateTime utcNow) =>
        IsActive && Status == CampaignStatus.Active && StartsAt <= utcNow && EndsAt >= utcNow;

    public string ValidUntilText => $"Son gün: {EndsAt.ToLocalTime():dd.MM.yyyy}";
    public bool HasImage => !string.IsNullOrWhiteSpace(ImageUrl);
    public bool HasBadge => !string.IsNullOrWhiteSpace(BadgeText);
}
