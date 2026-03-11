using CommunityToolkit.Mvvm.ComponentModel;

namespace KamPay.Models;

// ✅ ObservableObject — IsRead değişince CollectionView anında güncellenir
public partial class Notification : ObservableObject
{
    public string NotificationId { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = "";
    public NotificationType Type { get; set; }
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string IconUrl { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ✅ [ObservableProperty] — MarkAsRead anında UI'a yansır, OnPropertyChanged(nameof(Notifications)) artık gerekmez
    [ObservableProperty] private bool isRead;

    public DateTime? ReadAt { get; set; }
    public string RelatedEntityId { get; set; } = "";
    public string RelatedEntityType { get; set; } = "";
    public string ActionUrl { get; set; } = "";

    public Notification()
    {
        NotificationId = Guid.NewGuid().ToString();
        CreatedAt = DateTime.UtcNow;
        IsRead = false;
    }

    public string TimeAgoText
    {
        get
        {
            var diff = DateTime.UtcNow - CreatedAt;
            if (diff.TotalMinutes < 1) return "Az önce";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} dakika önce";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} saat önce";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays} gün önce";
            return CreatedAt.ToString("dd MMM yyyy");
        }
    }
}

public enum NotificationType
{
    SurpriseBoxWon,
    DonationClaimed,
    NewMessage = 0,
    ProductSold = 1,
    ProductViewed = 2,
    NewFavorite = 3,
    BadgeEarned = 4,
    PointsEarned = 5,
    DonationMade = 6,
    SystemNotice = 7,
    NewOffer = 8,
    OfferAccepted = 9,
    OfferRejected = 10,
    Quote = 11,
    ServiceCompleted = 12,
    TransactionUpdate = 13
}