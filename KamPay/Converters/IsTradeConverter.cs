using System.Globalization;
using KamPay.Models;

namespace KamPay.Converters
{
    public class IsTradeConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is ProductType type)
            {
                return type == ProductType.Takas;
            }
            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
