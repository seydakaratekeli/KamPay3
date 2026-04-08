using System;
using System.Collections.Generic;

namespace KamPay.API.Models
{
    public enum ProductCondition
    {
        YeniGibi = 0,
        CokIyi = 1,
        Iyi = 2,
        Orta = 3,
        Kullanilabilir = 4
    }

    public enum ProductType
    {
        Satis = 0,
        Bagis = 1,
        Takas = 2
    }

    public class Product
    {
        public string ProductId { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string CategoryId { get; set; } = "";
        public string CategoryName { get; set; } = "";
        public bool HasPendingOffer { get; set; }

        public ProductCondition Condition { get; set; }
        public ProductType Type { get; set; }
        public decimal Price { get; set; }

        public string UserId { get; set; } = "";
        public string UserName { get; set; } = "";
        public string UserEmail { get; set; } = "";
        public string UserPhotoUrl { get; set; } = "";

        public string Location { get; set; } = "";
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        public List<string> ImageUrls { get; set; } = new();
        public string ThumbnailUrl { get; set; } = "";

        public bool IsActive { get; set; } = true;
        public bool IsReserved { get; set; }
        public bool IsSold { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? SoldAt { get; set; }

        public string? BuyerId { get; set; }

        public int ViewCount { get; set; }
        public int FavoriteCount { get; set; }

        public string ExchangePreference { get; set; } = "";
        public bool IsForSurpriseBox { get; set; }
    }
}
