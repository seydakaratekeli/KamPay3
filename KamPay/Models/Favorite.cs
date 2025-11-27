namespace KamPay.Models;

// Favori modeli
public class Favorite
{
    public string FavoriteId { get; set; }
    public string UserId { get; set; }
    public string ProductId { get; set; }
    public DateTime CreatedAt { get; set; }

    // Product bilgileri (cache için)
    public string ProductTitle { get; set; }
    public string ProductThumbnail { get; set; }
    public decimal ProductPrice { get; set; }
    public ProductType ProductType { get; set; }

  
    public string PriceText => ProductType == ProductType.Satis
        ? $"{ProductPrice:N2} ₺"
        : ProductType == ProductType.Bagis
            ? "Ücretsiz"
            : "Takas";

    public string TimeAgoText
    {
        get
        {
            var diff = DateTime.UtcNow - CreatedAt;
            if (diff.TotalMinutes < 1) return "Az önce eklendi";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}dk önce eklendi";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} saat önce eklendi";
            return CreatedAt.ToString("dd MMM yyyy");
        }
    }

    public Favorite()
    {
        FavoriteId = Guid.NewGuid().ToString();
        CreatedAt = DateTime.UtcNow;
    }
}