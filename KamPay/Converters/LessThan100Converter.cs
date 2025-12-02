using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace KamPay.Converters
{
    // Yaln�zca puana bakarak bool de�eri d�nd�r�r.
    // True = Puan 100'den az (Uyar�y� g�ster).
    // False = Puan 100 veya 100'den fazla (Uyar�y� gizle).
    public class LessThan100Converter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int points)
            {
                const int requiredPoints = 100;
                return points < requiredPoints;
            }
            // Hata durumunda veya null ise uyar�y� gizle (varsay�m)
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // ConvertBack is not needed for this one-way binding converter
            // Return 0 as default value if somehow called
            return 0;
        }
    }
}