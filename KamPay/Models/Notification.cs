namespace KamPay.Models;

// Bildirim modeli
public class Notification
{
    public string NotificationId { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; }
    public string Message { get; set; }
    public string IconUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }

 

    // İlgili veri (ürün, mesaj vb.)
    public string RelatedEntityId { get; set; }
    public string RelatedEntityType { get; set; } // "Product", "Message", "Badge" vb.

    // Aksiyon URL'i
    public string ActionUrl { get; set; }

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

            if (diff.TotalMinutes < 1)
                return "Az önce";
            if (diff.TotalMinutes < 60)
                return $"{(int)diff.TotalMinutes} dakika önce";
            if (diff.TotalHours < 24)
                return $"{(int)diff.TotalHours} saat önce";
            if (diff.TotalDays < 7)
                return $"{(int)diff.TotalDays} gün önce";

            return CreatedAt.ToString("dd MMM yyyy");
        }
    }
}

public enum NotificationType
{
    SurpriseBoxWon,      // 🔥 YENİ: Sürpriz kutu kazanıldı
    DonationClaimed,     // 🔥 YENİ: Bağış değerlendirildi
    NewMessage = 0,      // Yeni mesaj
    ProductSold = 1,     // Ürün satıldı
    ProductViewed = 2,   // Ürününüz görüntülendi
    NewFavorite = 3,     // Ürününüz favorilere eklendi
    BadgeEarned = 4,     // Rozet kazandınız
    PointsEarned = 5,    // Puan kazandınız
    DonationMade = 6,    // Bağış yapıldı
    SystemNotice = 7,    // Sistem bildirimi
    NewOffer = 8,        // Yeni teklif/istek geldi
    OfferAccepted = 9,   // Teklifin kabul edildi
    OfferRejected = 10,  // Teklifin reddedildi
    ServiceRequest = 11, // Hizmet talebi
    Payment = 12,        // Ödeme bildirimi
    Dispute = 13,        // Anlaşmazlık
    Rating = 14,         // Değerlendirme
    System = 15          // Sistem (admin işlemleri)
}