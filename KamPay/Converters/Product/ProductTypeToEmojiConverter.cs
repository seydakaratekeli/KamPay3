using System.Globalization;
using KamPay.Models;

namespace KamPay.Converters
{
    public class ProductTypeToEmojiConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is ProductType type)
            {
                return type switch
                {
                    ProductType.Satis => "??",
                    ProductType.Takas => "??",
                    ProductType.Bagis => "??",
                    _ => "??"
                };
            }
            return "??";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
