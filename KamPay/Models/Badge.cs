using System;
using System.Collections.Generic;

namespace KamPay.Models;

// Rozet (Badge) modeli - Oyunlaştırma için
public class Badge
{
    public string BadgeId { get; set; } = "";
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string IconName { get; set; }
    public BadgeCategory Category { get; set; }
    public int RequiredPoints { get; set; }
    public int RequiredCount { get; set; } // Örn: 10 ürün sat
    public required string Color { get; set; }

   
    public DateTime CreatedAt { get; set; }

    public Badge()
    {
        BadgeId = Guid.NewGuid().ToString();
        CreatedAt = DateTime.UtcNow; 
    }

    // Varsayılan rozetler
    public static List<Badge> GetDefaultBadges()
    {
        return new List<Badge>
        {
            new Badge
            {
                BadgeId = "first_step",
                Name = "İlk Adım",
                Description = "İlk ürününü ekledin!",
                IconName = "badge_first.png",
                Category = BadgeCategory.Seller,
                RequiredCount = 1,
                Color = "#4CAF50"
            },
            new Badge
            {
                BadgeId = "sharing_hero",
                Name = "Paylaşım Kahramanı",
                Description = "5 ürün paylaştın",
                IconName = "badge_hero.png",
                Category = BadgeCategory.Seller,
                RequiredCount = 5,
                Color = "#2196F3"
            },
            new Badge
            {
                BadgeId = "donation_angel",
                Name = "Bağış Meleği",
                Description = "3 ürün bağışladın",
                IconName = "badge_angel.png",
                Category = BadgeCategory.Donation,
                RequiredCount = 3,
                Color = "#FF9800"
            },
            new Badge
            {
                BadgeId = "active_buyer",
                Name = "Aktif Alıcı",
                Description = "5 ürün aldın",
                IconName = "badge_buyer.png",
                Category = BadgeCategory.Buyer,
                RequiredCount = 5,
                Color = "#9C27B0"
            },
            new Badge
            {
                BadgeId = "super_seller",
                Name = "Süper Satıcı",
                Description = "10 ürün sattın",
                IconName = "badge_super_seller.png",
                Category = BadgeCategory.Seller,
                RequiredCount = 10,
                Color = "#FF5722"
            },
            new Badge
            {
                BadgeId = "campus_star",
                Name = "Kampüs Yıldızı",
                Description = "100 puana ulaştın",
                IconName = "badge_star.png",
                Category = BadgeCategory.Points,
                RequiredPoints = 100,
                Color = "#FFC107"
            }
        };
    }
}

public enum BadgeCategory
{
    Seller = 0,    // Satıcı rozetleri
    Buyer = 1,     // Alıcı rozetleri
    Donation = 2,  // Bağış rozetleri
    Points = 3,    // Puan rozetleri
    Special = 4    // Özel rozetler
}

// Kullanıcı rozeti (User'ın kazandığı rozetler)
public class UserBadge
{
    public string UserBadgeId { get; set; } = "";
    public required string UserId { get; set; }
    public required string BadgeId { get; set; }
    public DateTime EarnedAt { get; set; }

    // Badge bilgileri (cache için)
    public required string BadgeName { get; set; }
    public required string BadgeIcon { get; set; }
    public required string BadgeColor { get; set; }

    public UserBadge()
    {
        UserBadgeId = Guid.NewGuid().ToString();
        EarnedAt = DateTime.UtcNow;
    }
}