using System.Globalization;
using KamPay.Models;

namespace KamPay.Converters
{
    /// <summary>
    /// ProductType'a göre fiyat alanında gösterilecek metni döndürür.
    /// Satis → "₺{fiyat:N0}"  |  Bagis → "Ücretsiz"  |  Takas → "Takas"
    /// Binding: {Binding ., Converter={StaticResource ProductPriceDisplayConverter}}
    /// </summary>
    public class ProductPriceDisplayConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Product product)
            {
                return product.Type switch
                {
                    ProductType.Satis => $"₺{product.Price:N0}",
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
}
