using System.Globalization;
using KamPay.Models;

namespace KamPay.Converters
{
    public class ProductSortOptionToTextConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is ProductSortOption option)
            {
                return option switch
                {
                    ProductSortOption.Newest => "En Yeni",
                    ProductSortOption.Oldest => "En Eski",
                    ProductSortOption.PriceAsc => "Fiyat (Düþükten Yükseðe)",
                    ProductSortOption.PriceDesc => "Fiyat (Yüksekten Düþüðe)",
                    ProductSortOption.MostViewed => "En Çok Ýzlenen",
                    ProductSortOption.MostFavorited => "En Çok Favori",
                    _ => "Bilinmiyor"
                };
            }

            return value?.ToString() ?? string.Empty;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string text)
            {
                return text switch
                {
                    "En Yeni" => ProductSortOption.Newest,
                    "En Eski" => ProductSortOption.Oldest,
                    "Fiyat (Düþükten Yükseðe)" => ProductSortOption.PriceAsc,
                    "Fiyat (Yüksekten Düþüðe)" => ProductSortOption.PriceDesc,
                    "En Çok Ýzlenen" => ProductSortOption.MostViewed,
                    "En Çok Favori" => ProductSortOption.MostFavorited,
                    _ => ProductSortOption.Newest
                };
            }

            return ProductSortOption.Newest;
        }
    }
}