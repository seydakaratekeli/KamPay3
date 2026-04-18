using System;
using System.Threading.Tasks;
using KamPay.Models;
using ZXing.Net.Maui;
using ZXing;

namespace KamPay.Models
{
    // QR Kod Teslimat Modeli
    public class DeliveryQRCode
    {
        public string QRCodeId { get; set; } = "";
        public string ProductId { get; set; } = "";
        public string ProductTitle { get; set; } = "";
        public string SellerId { get; set; } = "";
        public string BuyerId { get; set; } = "";

        // QR kodu işleme başlamak için.
        public string TransactionId { get; set; } = "";

        public string QRCodeData { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsUsed { get; set; }
        public DateTime? UsedAt { get; set; }
        
        // ⚠️ Status: Eski alan, backward compatibility için korunuyor
        // Yeni kod DeliveryStatus'u kullanmalı, her iki alanı da güncellemeli
        public DeliveryStatus Status { get; set; }

        //  Süre Sınırı Özellikleri
        public int ValidityMinutes { get; set; } = 60;

        //  Konum Doğrulama Özellikleri
        public double? MeetingPointLatitude { get; set; }
        public double? MeetingPointLongitude { get; set; }
        public string? MeetingPointName { get; set; }
        public double MaxDistanceMeters { get; set; } = 100;
        public double? ActualDeliveryLatitude { get; set; }
        public double? ActualDeliveryLongitude { get; set; }
        public bool LocationVerified { get; set; }

        //  PIN Güvenliği
        public string? VerificationPin { get; set; }
        public int ScanAttempts { get; set; } = 0;
        public int MaxScanAttempts { get; set; } = 5;

        //  Durum Yönetimi - Yeni status alanı (yeni enum değerlerini destekler)
        public DeliveryStatus DeliveryStatus { get; set; } = DeliveryStatus.Pending;
        public string? CancellationReason { get; set; }
        public DateTime? CancelledAt { get; set; }
        public string? CancelledByUserId { get; set; }

        //  Zaman Takibi - DeliveryDuration hesaplanan özellik
        public TimeSpan? DeliveryDuration => UsedAt.HasValue ? UsedAt.Value - CreatedAt : null;

        // Süre uzatma kontrolü
        public bool HasBeenExtended { get; set; } = false;

        //  Fotoğraf Özellikleri 
        public string? DeliveryPhotoUrl { get; set; }
        public string? DeliveryPhotoThumbnailUrl { get; set; }
        public DateTime? PhotoUploadedAt { get; set; }
        public bool PhotoRequired { get; set; } = true;
        public long PhotoSizeBytes { get; set; }
        public string? PhotoUploadedByUserId { get; set; }
        public int PhotoWidth { get; set; }
        public int PhotoHeight { get; set; }
        public string PhotoFormat { get; set; } = "JPEG";

        public DeliveryQRCode()
        {
            QRCodeId = Guid.NewGuid().ToString();
            CreatedAt = DateTime.UtcNow;
            ExpiresAt = DateTime.UtcNow.AddHours(24); // 24 saat geçerli (varsayılan, güvenli QR'da ValidityMinutes'a göre ayarlanacak)
            IsUsed = false;
            Status = DeliveryStatus.Pending;
            DeliveryStatus = DeliveryStatus.Pending;
        }

        public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    }

    public enum DeliveryStatus
    {
        Pending = 0,      // Bekliyor
        InProgress = 1,   // Teslimatta 
        Completed = 2,    // Tamamlandı 
        Cancelled = 3,    // İptal edildi 
        Scheduled = 4,    // Planlandı 
        Disputed = 5,     // Anlaşmazlık 
        Expired = 6,      // Süresi doldu 
        WaitingForPhoto = 7  // Fotoğraf bekleniyor 
    }
}