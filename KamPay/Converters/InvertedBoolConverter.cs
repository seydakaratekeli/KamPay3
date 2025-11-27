using System.Globalization;

namespace KamPay.Converters
{
    public class InvertedBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Gelen değer bool mu kontrol et
            if (value is bool boolValue)
            {
                return !boolValue;
            }

            // Değer null ise veya bool değilse (hata almamak için) true döndür
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return true;
        }
    }
}