using System;

namespace KamPay.Models
{
    /// <summary>
    /// Ödeme durumu - Hizmet ve ürün ödemeleri için kullanılır
    /// </summary>
    public enum ServicePaymentStatus
    {
        None = 0,        // Ödeme yok
        Initiated = 1,   // Ödeme başlatıldı
        Paid = 2,        // Ödeme tamamlandı
        Failed = 3       // Ödeme başarısız
    }

    /// <summary>
    /// Ödeme yöntemi türleri - Simülasyon amaçlı
    /// </summary>
    public enum PaymentMethodType
    {
        None = 0,              // Ödeme yöntemi seçilmedi
        CardSim = 1,           // Kredi Kartı (Simülasyon - OTP ile)
        BankTransferSim = 2,   // Havale/EFT (Simülasyon - Referans kodu ile)
        WalletSim = 3,         // Cüzdan (Gelecekte kullanılabilir)
        Cash = 4               // Elden (Nakit / Yüz Yüze) Ödeme
    }

    /// <summary>
    /// Ödeme simülasyonu için kullanılan veri transfer objesi (DTO)
    /// Gerçek ödeme sisteminde bu model gateway'den dönen yanıtı temsil eder
    /// </summary>
    public class PaymentDto
    {
        /// <summary>
        /// Benzersiz ödeme kimliği
        /// </summary>
        public string PaymentId { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>
        /// Ödenecek tutar
        /// </summary>
        public decimal Amount { get; set; }
        
        /// <summary>
        /// Para birimi (varsayılan: TRY - Türk Lirası)
        /// </summary>
        public string Currency { get; set; } = "TRY";
        
        /// <summary>
        /// Ödeme durumu
        /// </summary>
        public ServicePaymentStatus Status { get; set; } = ServicePaymentStatus.Initiated;
        
        /// <summary>
        /// Seçilen ödeme yöntemi
        /// </summary>
        public PaymentMethodType Method { get; set; } = PaymentMethodType.CardSim;
        
        /// <summary>
        /// Kredi kartı için maskelenmiş son 4 hane (örn: "****1234")
        /// Simülasyonda kullanılmaz ama gerçek sistemde önemlidir
        /// </summary>
        public string? MaskedCardLast4 { get; set; }
        
        /// <summary>
        /// Havale/EFT için banka adı
        /// </summary>
        public string? BankName { get; set; }
        
        /// <summary>
        /// Havale/EFT için referans kodu
        /// Kullanıcı bu kodu ödeme açıklamasına yazmalıdır
        /// </summary>
        public string? BankReference { get; set; }
        
        /// <summary>
        /// Ödeme oluşturulma zamanı (UTC)
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// OTP simülasyonu için geçici model
    /// </summary>
    public class TempOtpModel
    {
        public string Otp { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }
}
