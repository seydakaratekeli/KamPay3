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
        public string QRCodeId { get; set; }
        public string ProductId { get; set; }
        public string ProductTitle { get; set; }
        public string SellerId { get; set; }
        public string BuyerId { get; set; }

        // YENÝ EKLENEN ÖZELLÝK: QR kodu iþleme baðlamak için.
        public string TransactionId { get; set; }

        public string QRCodeData { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsUsed { get; set; }
        public DateTime? UsedAt { get; set; }
        public DeliveryStatus Status { get; set; }

        public DeliveryQRCode()
        {
            QRCodeId = Guid.NewGuid().ToString();
            CreatedAt = DateTime.UtcNow;
            ExpiresAt = DateTime.UtcNow.AddHours(24); // 24 saat geçerli
            IsUsed = false;
            Status = DeliveryStatus.Pending;
        }

        public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    }

    public enum DeliveryStatus
    {
        Pending = 0,      // Bekliyor
        InProgress = 1,   // Teslimatta
        Completed = 2,    // Tamamlandý
        Cancelled = 3     // Ýptal edildi
    }
}