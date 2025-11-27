using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace KamPay.Converters
{
    // Yalnýzca puana bakarak bool deðeri döndürür.
    // True = Puan 100'den az (Uyarýyý göster).
    // False = Puan 100 veya 100'den fazla (Uyarýyý gizle).
    public class LessThan100Converter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int points)
            {
                const int requiredPoints = 100;
                return points < requiredPoints;
            }
            // Hata durumunda veya null ise uyarýyý gizle (varsayým)
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}