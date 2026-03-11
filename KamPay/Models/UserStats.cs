using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace KamPay.Models
{
    // ? ObservableObject — ProfileViewModel UserStats.TotalProducts'ý set edince UI anýnda yansýr
    public partial class UserStats : ObservableObject
    {
        public string UserId { get; set; } = "";

        // ? [ObservableProperty] — puan/sayý deðiþimleri ProfilePage'e anýnda yansýr
        [ObservableProperty] private int points;
        [ObservableProperty] private int completedTrades;
        [ObservableProperty] private int donatedProducts;
        [ObservableProperty] private int totalProducts;
        [ObservableProperty] private int purchasedProducts;
        [ObservableProperty] private int itemsShared;
        [ObservableProperty] private int donationsMade;
        [ObservableProperty] private int timeCredits;
    }
}