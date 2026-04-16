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
        // ✅ [ObservableProperty] ile tanımlandı — CollectionView otomatik güncellenir
        [ObservableProperty] private string productId = "";
        [ObservableProperty] private string title = "";
        [ObservableProperty] private string description = "";
        [ObservableProperty] private string categoryId = "";
        [ObservableProperty] private string categoryName = "";
        [ObservableProperty] private bool hasPendingOffer;

        [ObservableProperty] private ProductCondition condition;
        [ObservableProperty] private ProductType type;
        [ObservableProperty] private decimal price;

        // ✅ Kullanıcı bilgileri — OnUserProfileChanged ile güncelleniyor, artık UI'a yansır
        [ObservableProperty] private string userId = "";
        [ObservableProperty] private string userName = "";
        [ObservableProperty] private string userEmail = "";
        [ObservableProperty] private string userPhotoUrl = "";

        [ObservableProperty] private string location = "";
        [ObservableProperty] private double? latitude;
        [ObservableProperty] private double? longitude;

        // Fotoğraflar
        public List<string> ImageUrls { get; set; } = new();
        // ✅ PERFORMANS: ObservableProperty olarak değiştirildi — CachedImage binding'i doğru güncellenir
        [ObservableProperty] private string thumbnailUrl = "";

        // ✅ Durum bayrakları — IsSold/IsReserved değişince StatusText/StatusColor güncellenir
        [ObservableProperty]
        private bool isActive;

        [ObservableProperty]
        private bool isReserved;

        [ObservableProperty]
        private bool isSold;

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? SoldAt { get; set; }

        //  (ödeme simülasyonu için) ödeme simulasyonu şu an yok
        public ServicePaymentStatus PaymentStatus { get; set; } = ServicePaymentStatus.None;
        public PaymentMethodType PaymentMethod { get; set; } = PaymentMethodType.None;
        public string? BuyerId { get; set; }

        // İstatistikler
        [ObservableProperty]
        private int viewCount;

        [ObservableProperty]
        private int favoriteCount;

        // Takas için
        public string ExchangePreference { get; set; } = "";

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
        }

        public bool HasImages => ImageUrls != null && ImageUrls.Count > 0;
    }

    // Ürün ekleme/güncelleme için DTO
    public class ProductRequest
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string CategoryId { get; set; } = "";
        public string CategoryName { get; set; } = "";
        public ProductCondition Condition { get; set; }
        public ProductType Type { get; set; }
        public decimal Price { get; set; }
        public string Location { get; set; } = "";
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public List<string> ImagePaths { get; set; } = new();
        public string ExchangePreference { get; set; } = "";
        public bool IsForSurpriseBox { get; set; }
    }

    // Filtreleme için model
    public class ProductFilter
    {
        public string SearchText { get; set; } = "";
        public string CategoryId { get; set; } = "";
        public ProductType? Type { get; set; }
        public ProductCondition? Condition { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public string Location { get; set; } = "";
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