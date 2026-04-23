using System.Globalization;
using KamPay.Models;

namespace KamPay.Converters
{
    /// <summary>
    /// ProductType'a göre fiyat alanında gösterilecek metni döndürür.
    /// ✅ FAZ 6 FIX: Tüm fiyat gösterimlerinde tr-TR locale ile N2 formatı kullanılıyor.
    /// Satis → "₺1.500,00"  |  Bagis → "Ücretsiz"  |  Takas → "Takas"
    /// Binding: {Binding ., Converter={StaticResource ProductPriceDisplayConverter}}
    /// </summary>
    public class ProductPriceDisplayConverter : IValueConverter
    {
        // ✅ FAZ 6: Türk locale — binlik ayraç nokta, ondalık ayraç virgül
        private static readonly CultureInfo TrCulture = new CultureInfo("tr-TR");

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Product product)
            {
                return product.Type switch
                {
                    ProductType.Satis => $"₺{product.Price.ToString("N2", TrCulture)}",
                    ProductType.Bagis => "Ücretsiz",
                    ProductType.Takas => "Takas",
                    _ => string.Empty
                };
            }
            return string.Empty;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// ProductType'a göre fiyat label'ının rengini döndürür.
    /// Binding: {Binding Type, Converter={StaticResource ProductPriceColorConverter}}
    /// </summary>
    public class ProductPriceColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is ProductType type)
            {
                return type switch
                {
                    ProductType.Satis => (Color)Application.Current.Resources["Primary"],
                    ProductType.Bagis => (Color)Application.Current.Resources["Success"],
                    ProductType.Takas => (Color)Application.Current.Resources["Warning"],
                    _ => Colors.Gray
                };
            }
            return Colors.Gray;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// ✅ FAZ 6 YENİ: decimal fiyat değerini tr-TR formatında "₺X.XXX,XX" olarak döndürür.
    /// Transaction ve teklif ekranlarında tutarlı fiyat gösterimi için kullanılır.
    /// Binding: {Binding Price, Converter={StaticResource PriceFormatConverter}}
    /// </summary>
    public class PriceFormatConverter : IValueConverter
    {
        private static readonly CultureInfo TrCulture = new CultureInfo("tr-TR");

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            decimal price = value switch
            {
                decimal d => d,
                double db => (decimal)db,
                float f => (decimal)f,
                int i => (decimal)i,
                _ => 0m
            };

            // parameter="NoSymbol" ise sembol olmadan döndür
            if (parameter?.ToString() == "NoSymbol")
                return price.ToString("N2", TrCulture);

            return $"₺{price.ToString("N2", TrCulture)}";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}