using System;

namespace KamPay.Models
{
    public class UserStats
    {
        // bu sayfada kullanýcýlarýn oyunlaþtýrma ve istatistiksel verilerini tutacaðýz.

        public string UserId { get; set; }

       
        /// Kullanýcýnýn oyunlaþtýrma sistemiyle kazandýðý toplam puan.
       
        public int Points { get; set; }

       
        /// Baþarýyla tamamlanan takas veya satýþ sayýsý.
       
        public int CompletedTrades { get; set; }

       
        /// Kullanýcýnýn yaptýðý toplam baðýþ sayýsý. (Rozet için)
       
        public int DonatedProducts { get; set; }

       
        /// Kullanýcýnýn paylaþtýðý (ilan açtýðý) toplam ürün sayýsý. (Rozet için)
       
        public int TotalProducts { get; set; }

       
        /// Kullanýcýnýn satýn aldýðý veya takasla edindiði toplam ürün sayýsý. (Rozet için)
       
        public int PurchasedProducts { get; set; }

        
        public int ItemsShared { get; set; }
        public int DonationsMade { get; set; }

        public int TimeCredits { get; set; } = 0; // Baþlangýç deðeri 0

    }
}