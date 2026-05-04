using CommunityToolkit.Mvvm.ComponentModel;

namespace KamPay.Models;

public enum MicroBusinessCategory
{
    FoodAndDrink = 0,
    Stationery = 1,
    Printing = 2,
    Technology = 3,
    Health = 4,
    Sports = 5,
    Other = 99
}

public enum BusinessVerificationStatus
{
    Draft = 0,
    PendingReview = 1,
    Verified = 2,
    Rejected = 3,
    Suspended = 4
}

public partial class MicroBusiness : ObservableObject
{
    [ObservableProperty] private string businessId = string.Empty;
    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string description = string.Empty;
    [ObservableProperty] private MicroBusinessCategory category = MicroBusinessCategory.Other;
    [ObservableProperty] private string categoryName = string.Empty;
    [ObservableProperty] private string location = string.Empty;
    [ObservableProperty] private string phoneNumber = string.Empty;
    [ObservableProperty] private string instagramUrl = string.Empty;
    [ObservableProperty] private string websiteUrl = string.Empty;
    [ObservableProperty] private string logoUrl = string.Empty;
    [ObservableProperty] private string coverImageUrl = string.Empty;
    [ObservableProperty] private double? latitude;
    [ObservableProperty] private double? longitude;
    [ObservableProperty] private bool isVerified;
    [ObservableProperty] private bool isActive;
    [ObservableProperty] private int displayOrder;
    [ObservableProperty] private string ownerUserId = string.Empty;
    [ObservableProperty] private string ownerEmail = string.Empty;
    [ObservableProperty] private string ownerFullName = string.Empty;
    [ObservableProperty] private string ownerPhoneNumber = string.Empty;
    [ObservableProperty] private BusinessVerificationStatus verificationStatus = BusinessVerificationStatus.Draft;
    [ObservableProperty] private string rejectionReason = string.Empty;
    [ObservableProperty] private string adminNote = string.Empty;
    [ObservableProperty] private string verifiedByAdminId = string.Empty;
    [ObservableProperty] private bool autoApproveCampaigns = true;

    public Dictionary<string, string> WorkingHours { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public List<string> DocumentUrls { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? VerifiedAt { get; set; }

    public bool HasLogo => !string.IsNullOrWhiteSpace(LogoUrl);
    public bool HasCoverImage => !string.IsNullOrWhiteSpace(CoverImageUrl);
    public bool HasContactInfo =>
        !string.IsNullOrWhiteSpace(PhoneNumber) ||
        !string.IsNullOrWhiteSpace(InstagramUrl) ||
        !string.IsNullOrWhiteSpace(WebsiteUrl);

    public string DisplayCategoryName => !string.IsNullOrWhiteSpace(CategoryName)
        ? CategoryName
        : Category switch
        {
            MicroBusinessCategory.FoodAndDrink => "Yeme İçme",
            MicroBusinessCategory.Stationery => "Kırtasiye",
            MicroBusinessCategory.Printing => "Fotokopi & Baskı",
            MicroBusinessCategory.Technology => "Teknoloji",
            MicroBusinessCategory.Health => "Sağlık",
            MicroBusinessCategory.Sports => "Spor",
            _ => "Diğer"
        };

    public string TagsText => Tags.Count == 0 ? string.Empty : string.Join(" • ", Tags);
    public bool HasTags => Tags.Count > 0;
    public bool IsPubliclyVisible => IsActive && IsVerified && VerificationStatus == BusinessVerificationStatus.Verified;
}
