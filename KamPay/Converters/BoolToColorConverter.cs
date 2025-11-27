using System.Globalization;
using Microsoft.Maui.Graphics;

namespace KamPay.Converters
{
    public class BoolToColorConverter : IValueConverter
    {
        public Color TrueColor { get; set; }
        public Color FalseColor { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isTrue && isTrue)
            {
                return TrueColor; // Beðenildiyse bu renk
            }
            return FalseColor; // Beðenilmediyse bu renk
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}