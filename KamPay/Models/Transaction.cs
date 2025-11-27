// KamPay/Models/Transaction.cs
using KamPay.Models;
using System;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Linq;

namespace KamPay.Models
{
    // 🔹 Bir ürün satışı, takası veya bağışı sürecini takip eden ana model
    public class Transaction
    {
        public string TransactionId { get; set; } = Guid.NewGuid().ToString();

        // 🔹 İlgili Ana Ürün Bilgileri
        public string ProductId { get; set; }
        public string ProductTitle { get; set; }
        public string ProductThumbnailUrl { get; set; }
        public ProductType Type { get; set; } // Satış, Takas, Bağış

        // 🔹 Taraflar
        public string SellerId { get; set; } // Ürünü sunan kişi
        public string SellerName { get; set; }
        public string BuyerId { get; set; }  // Teklifi yapan/isteği gönderen kişi
        public string BuyerName { get; set; }

        // 🔹 Ödeme Durumu
        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

        // 🆕 Yeni eklenen ödeme bilgileri (simülasyon desteği için)
        public PaymentMethodType PaymentMethod { get; set; } = PaymentMethodType.None; // CardSim, BankTransferSim vb.
        public string? PaymentSimulationId { get; set; } // OTP ile eşleşecek ID

        // *** HATA DÜZELTMESİ: EKSİK ALAN BURAYA EKLENDİ ***
        public decimal Price { get; set; } // Ürünün orijinal liste fiyatı

        public decimal QuotedPrice { get; set; } // Satış anındaki kilitli fiyat (pazarlık vb.)
        public string Currency { get; set; } = "TRY"; // Para birimi
        public DateTime? PaymentCompletedAt { get; set; } // Ödeme tamamlanma zamanı

        // 🔥 DÜZELTME BURADA: Eksik olan 'Message' özelliği eklendi.
        // Teklif gönderilirken yazılan notu tutar.
        public string? Message { get; set; }

        // 🔹 Durum ve Zaman Bilgileri
        public TransactionStatus Status { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // 🔹 QR Kod Teslimat Takibi
        [JsonIgnore]
        public List<DeliveryQRCode> DeliveryQRCodes { get; set; } = new();

        // 🔹 Görsel durum metni
        public string StatusText
        {
            get
            {
                // NOT: Bu StatusText mantığı, yeni 'Completed' durumunu (PaymentStatus.Paid) 
                // henüz yansıtmıyor olabilir. Şimdilik hatayı çözmeye odaklanalım.
                if (Status == TransactionStatus.Accepted && DeliveryQRCodes.Any() && DeliveryQRCodes.All(qr => qr.IsUsed))
                    return "Tamamlandı";

                return Status switch
                {
                    TransactionStatus.Pending => "Onay Bekliyor",
                    TransactionStatus.Accepted => "Kabul Edildi",
                    TransactionStatus.Rejected => "Reddedildi",
                    TransactionStatus.Completed => "Tamamlandı",
                    TransactionStatus.Cancelled => "İptal Edildi",
                    _ => "Bilinmiyor"
                };
            }
        }

        public bool CanManageDelivery =>
            Type == ProductType.Takas &&
            Status == TransactionStatus.Accepted &&
            !(DeliveryQRCodes.Any() && DeliveryQRCodes.All(qr => qr.IsUsed));

        public bool IsDeliveryCompleted =>
            Status == TransactionStatus.Completed ||
            (Status == TransactionStatus.Accepted && DeliveryQRCodes.Any() && DeliveryQRCodes.All(qr => qr.IsUsed));

        // 🔹 Takas'a özel alanlar
        public string? OfferedProductId { get; set; }
        public string? OfferedProductTitle { get; set; }
        public string? OfferMessage { get; set; }
    }

    // 🔸 İşlem Durumu
    public enum TransactionStatus
    {
        Pending,     // Teklif yapıldı, satıcının onayı bekliyor
        Accepted,    // Teklif kabul edildi, teslimat/ödeme süreci bekleniyor
        Rejected,    // Teklif reddedildi
        Completed,   // İşlem (ödeme/teslimat) tamamlandı ve kapandı
        Cancelled    // Taraflardan biri iptal etti
    }

    // 🔸 Ödeme Durumu
    public enum PaymentStatus
    {
        Pending, // Ödeme bekleniyor
        Paid,    // Ödendi (simülasyon)
        Failed   // Başarısız
    }

}