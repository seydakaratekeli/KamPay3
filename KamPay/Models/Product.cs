using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;

namespace KamPay.Models
{
    // Ürün durumu enum
    public enum ProductCondition
    {
        YeniGibi = 0,      // Sıfır ayarında
        CokIyi = 1,        // Az kullanılmış
        Iyi = 2,           // Kullanılmış, iyi durumda
        Orta = 3,          // Kullanım izleri var
        Kullanilabilir = 4 // Çalışıyor ama eskimiş
    }

    // Ürün tipi enum
    public enum ProductType
    {
        Satis = 0,  // Satılık
        Bagis = 1,  // Bağış
        Takas = 2   // Takas
    }

    // Ürün modeli
    public partial class Product : ObservableObject
    {
        public string ProductId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string CategoryId { get; set; }
        public string CategoryName { get; set; }
        public bool HasPendingOffer { get; set; }

        // Ürün bilgileri
        public ProductCondition Condition { get; set; }
        public ProductType Type { get; set; }
        public decimal Price { get; set; }

        // Kullanıcı bilgileri
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string UserEmail { get; set; }
        public string UserPhotoUrl { get; set; }

        // Konum bilgileri
        public string Location { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        // Fotoğraflar
        public List<string> ImageUrls { get; set; }
        public string ThumbnailUrl { get; set; }

        // Durum bilgileri
        public bool IsActive { get; set; }
        public bool IsReserved { get; set; }
        public bool IsSold { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? SoldAt { get; set; }

        // 🟢 Yeni alanlar (ödeme simülasyonu için)
        public ServicePaymentStatus PaymentStatus { get; set; } = ServicePaymentStatus.None;
        public PaymentMethodType PaymentMethod { get; set; } = PaymentMethodType.None;
        public string BuyerId { get; set; }
      //  public bool IsSold { get; set; } = false;

        /// <summary>
        /// Ürünün durumunu belirler (Satış mı, Takas mı?)
        /// </summary>
        public string StatusText
        {
            get
            {
                // Önce TAMAMLANAN durumlar (IsSold)
                if (IsSold && Type == ProductType.Takas)
                    return "TAKAS YAPILDI ✓";

                if (IsSold && Type == ProductType.Satis)
                    return "SATILDI ✓";

                if (IsSold && Type == ProductType.Bagis) // YENİ EKLENDİ
                    return "BAĞIŞLANDI ✓";

                // Sonra BEKLEYEN durumlar (IsReserved)
                if (IsReserved && Type == ProductType.Takas)
                    return "TAKAS SÜRECİNDE";

                if (IsReserved && Type == ProductType.Satis)
                    return "SATIŞ SÜRECİNDE";

                if (IsReserved && Type == ProductType.Bagis) // YENİ EKLENDİ
                    return "BAĞIŞ SÜRECİNDE"; // Veya "BAĞIŞ İÇİN AYRILDI"

                return string.Empty;
            }
        }
        /// <summary>
        /// Etiket rengi
        /// </summary>
        public Color StatusColor
        {
            get
            {
                if (IsSold)
                    return Color.FromArgb("#4CAF50"); // Yeşil

                if (IsReserved)
                    return Color.FromArgb("#FF9800"); // Turuncu

                return Colors.Transparent;
            }
        }



        // İstatistikler
        [ObservableProperty]
        private int viewCount;

        [ObservableProperty]
        private int favoriteCount;

        // Takas için
        public string ExchangePreference { get; set; }

        public bool IsForSurpriseBox { get; set; } = false;


        public Product()
        {
            ProductId = Guid.NewGuid().ToString();
            CreatedAt = DateTime.UtcNow;
            IsActive = true;
            IsReserved = false;
            IsSold = false;
            ViewCount = 0;
            FavoriteCount = 0;
            ImageUrls = new List<string>();
        }

        // Yardımcı özellikler
        public string ConditionText => Condition switch
        {
            ProductCondition.YeniGibi => "Yeni Gibi",
            ProductCondition.CokIyi => "Çok İyi",
            ProductCondition.Iyi => "İyi",
            ProductCondition.Orta => "Orta",
            ProductCondition.Kullanilabilir => "Kullanılabilir",
            _ => "Belirtilmemiş"
        };

        public string TypeText => Type switch
        {
            ProductType.Satis => "Satılık",
            ProductType.Bagis => "Bağış",
            ProductType.Takas => "Takas",
            _ => "Belirtilmemiş"
        };

        public string PriceText => Type == ProductType.Satis
            ? $"{Price:N2} ₺"
            : Type == ProductType.Bagis
                ? "Ücretsiz"
                : "Takas";

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
                if (timeSpan.TotalDays < 30)
                    return $"{(int)(timeSpan.TotalDays / 7)} hafta önce";

                return CreatedAt.ToString("dd MMM yyyy");
            }
        }

        public bool HasImages => ImageUrls != null && ImageUrls.Count > 0;
    }

    // Ürün ekleme/güncelleme için DTO
    public class ProductRequest
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string CategoryId { get; set; }
        public string CategoryName { get; set; } 
        public ProductCondition Condition { get; set; }
        public ProductType Type { get; set; }
        public decimal Price { get; set; }
        public string Location { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public List<string> ImagePaths { get; set; } 
        public string ExchangePreference { get; set; }
        public bool IsForSurpriseBox { get; set; }

        public ProductRequest()
        {
            ImagePaths = new List<string>();
        }
    }

    // Filtreleme için model
    public class ProductFilter
    {
        public string SearchText { get; set; }
        public string CategoryId { get; set; }
        public ProductType? Type { get; set; }
        public ProductCondition? Condition { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public string Location { get; set; }
        public bool OnlyActive { get; set; } = true;
        public bool ExcludeSold { get; set; } = true;

        // Sıralama
        public ProductSortOption SortBy { get; set; } = ProductSortOption.Newest;
    }

    public enum ProductSortOption
    {
        Newest,        // En yeni
        Oldest,        // En eski
        PriceAsc,      // Fiyat artan
        PriceDesc,     // Fiyat azalan
        MostViewed,    // En çok görüntülenen
        MostFavorited  // En çok favorilenen
    }
}