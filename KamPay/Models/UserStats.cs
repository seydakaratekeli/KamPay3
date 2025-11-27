using System;

namespace KamPay.Models
{
    public class UserStats
    {
        public string UserId { get; set; }

        /// <summary>
        /// Kullanýcýnýn oyunlaþtýrma sistemiyle kazandýðý toplam puan.
        /// </summary>
        public int Points { get; set; }

        /// <summary>
        /// Baþarýyla tamamlanan takas veya satýþ sayýsý.
        /// </summary>
        public int CompletedTrades { get; set; }

        /// <summary>
        /// Kullanýcýnýn yaptýðý toplam baðýþ sayýsý. (Rozet için)
        /// </summary>
        public int DonatedProducts { get; set; }

        /// <summary>
        /// Kullanýcýnýn paylaþtýðý (ilan açtýðý) toplam ürün sayýsý. (Rozet için)
        /// </summary>
        public int TotalProducts { get; set; }

        /// <summary>
        /// Kullanýcýnýn satýn aldýðý veya takasla edindiði toplam ürün sayýsý. (Rozet için)
        /// </summary>
        public int PurchasedProducts { get; set; }

        // Bu alanlarý eski kodunuzda gördüm, projenizin ihtiyacýna göre kalabilirler.
        public int ItemsShared { get; set; }
        public int DonationsMade { get; set; }

        public int TimeCredits { get; set; } = 0; // Baþlangýç deðeri 0

    }
}