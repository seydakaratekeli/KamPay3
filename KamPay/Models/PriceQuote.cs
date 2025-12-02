using System;

namespace KamPay.Models
{
    /// <summary>
    /// Fiyat teklifi modeli - Dolap tarzı pazarlık sistemi
    /// </summary>
    public class PriceQuote
    {
        public string QuoteId { get; set; }
        
        // Teklif türü (Ürün veya Hizmet)
        public PriceQuoteType QuoteType { get; set; }
        
        // İlişkili ürün veya hizmet
        public string ReferenceId { get; set; } // ProductId veya ServiceId
        public string ReferenceTitle { get; set; } // Ürün/Hizmet başlığı
        public string ReferenceThumbnailUrl { get; set; }
        
        // Taraflar
        public string SellerId { get; set; } // Ürün/hizmet sahibi
        public string SellerName { get; set; }
        public string SellerPhotoUrl { get; set; }
        
        public string BuyerId { get; set; } // Teklif veren
        public string BuyerName { get; set; }
        public string BuyerPhotoUrl { get; set; }
        
        // Fiyat bilgileri
        public decimal OriginalPrice { get; set; } // Orijinal fiyat
        public decimal QuotedPrice { get; set; } // Teklif edilen fiyat
        public decimal? CounterOfferPrice { get; set; } // Karşı teklif (varsa)
        public string Currency { get; set; } = "TRY";
        
        // Durum
        public PriceQuoteStatus Status { get; set; }
        
        // Mesaj ve notlar
        public string Message { get; set; } // Teklif ile birlikte gönderilen mesaj
        public string CounterOfferMessage { get; set; } // Karşı teklif mesajı
        public string RejectionReason { get; set; } // Red nedeni
        
        // Zaman bilgileri
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; } // Teklifin geçerlilik süresi
        public DateTime? AcceptedAt { get; set; }
        public DateTime? RejectedAt { get; set; }
        
        // İstatistikler ve meta
        public int CounterOfferCount { get; set; } // Kaç kez karşı teklif yapıldı
        public bool IsRead { get; set; } // Satıcı tarafından okundu mu?
        public bool IsFinal { get; set; } // Son teklif mi? (başka teklif yapılamaz)
        
        public PriceQuote()
        {
            QuoteId = Guid.NewGuid().ToString();
            CreatedAt = DateTime.UtcNow;
            Status = PriceQuoteStatus.Pending;
            IsRead = false;
            IsFinal = false;
            CounterOfferCount = 0;
            ExpiresAt = DateTime.UtcNow.AddDays(7); // 7 gün geçerli
        }
        
        // Yardımcı özellikler
        public string StatusText => Status switch
        {
            PriceQuoteStatus.Pending => "Onay Bekliyor",
            PriceQuoteStatus.CounterOffered => "Karşı Teklif Var",
            PriceQuoteStatus.Accepted => "Kabul Edildi",
            PriceQuoteStatus.Rejected => "Reddedildi",
            PriceQuoteStatus.Expired => "Süresi Doldu",
            PriceQuoteStatus.Cancelled => "İptal Edildi",
            _ => "Bilinmiyor"
        };
        
        public Color StatusColor => Status switch
        {
            PriceQuoteStatus.Pending => Color.FromArgb("#FF9800"), // Turuncu
            PriceQuoteStatus.CounterOffered => Color.FromArgb("#2196F3"), // Mavi
            PriceQuoteStatus.Accepted => Color.FromArgb("#4CAF50"), // Yeşil
            PriceQuoteStatus.Rejected => Color.FromArgb("#F44336"), // Kırmızı
            PriceQuoteStatus.Expired => Color.FromArgb("#9E9E9E"), // Gri
            PriceQuoteStatus.Cancelled => Color.FromArgb("#757575"), // Koyu gri
            _ => Colors.Transparent
        };
        
        public bool CanCounterOffer => 
            Status == PriceQuoteStatus.Pending && 
            !IsFinal && 
            CounterOfferCount < 3; // Maksimum 3 karşı teklif
        
        public bool IsExpired => 
            ExpiresAt.HasValue && 
            DateTime.UtcNow > ExpiresAt.Value && 
            Status == PriceQuoteStatus.Pending;
        
        public string TimeAgoText
        {
            get
            {
                var timeSpan = DateTime.UtcNow - CreatedAt;
                
                if (timeSpan.TotalMinutes < 1)
                    return "Az önce";
                if (timeSpan.TotalMinutes < 60)
                    return $"{(int)timeSpan.TotalMinutes} dakika önce";
                if (timeSpan.TotalHours < 24)
                    return $"{(int)timeSpan.TotalHours} saat önce";
                if (timeSpan.TotalDays < 7)
                    return $"{(int)timeSpan.TotalDays} gün önce";
                
                return CreatedAt.ToString("dd MMM yyyy");
            }
        }
        
        public decimal DiscountPercentage => 
            OriginalPrice > 0 
                ? ((OriginalPrice - QuotedPrice) / OriginalPrice) * 100 
                : 0;
        
        public string DiscountText => $"%{DiscountPercentage:F0} indirim";
    }
    
    /// <summary>
    /// Teklif türü
    /// </summary>
    public enum PriceQuoteType
    {
        Product = 0,  // Ürün için teklif
        Service = 1   // Hizmet için teklif
    }
    
    /// <summary>
    /// Teklif durumu
    /// </summary>
    public enum PriceQuoteStatus
    {
        Pending = 0,        // Beklemede - satıcının cevabı bekleniyor
        CounterOffered = 1, // Karşı teklif yapıldı - alıcının cevabı bekleniyor
        Accepted = 2,       // Kabul edildi - işlem yapılabilir
        Rejected = 3,       // Reddedildi
        Expired = 4,        // Süresi doldu
        Cancelled = 5       // İptal edildi
    }
    
    /// <summary>
    /// Teklif oluşturma isteği
    /// </summary>
    public class CreateQuoteRequest
    {
        public PriceQuoteType QuoteType { get; set; }
        public string ReferenceId { get; set; } // ProductId veya ServiceId
        public decimal QuotedPrice { get; set; }
        public string Message { get; set; }
        public bool IsFinalOffer { get; set; } = false; // Son teklif işareti
    }
    
    /// <summary>
    /// Karşı teklif isteği
    /// </summary>
    public class CounterOfferRequest
    {
        public string QuoteId { get; set; }
        public decimal CounterOfferPrice { get; set; }
        public string Message { get; set; }
        public bool IsFinalOffer { get; set; } = false;
    }
    
    /// <summary>
    /// Teklif filtreleme
    /// </summary>
    public class PriceQuoteFilter
    {
        public string UserId { get; set; } // Kullanıcı ID (alıcı veya satıcı)
        public bool? IsSeller { get; set; } // True: satıcı teklifleri, False: alıcı teklifleri
        public PriceQuoteType? QuoteType { get; set; }
        public PriceQuoteStatus? Status { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public bool ExcludeExpired { get; set; } = true;
    }
}
