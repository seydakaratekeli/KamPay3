using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace KamPay.Converters
{
    /// <summary>
    /// Boolean değeri kontrol eder - true ise görünür
    /// Pazarlık durumunda butonları göstermek için kullanılır
    /// </summary>
    public class IsNegotiatingConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isNegotiating)
            {
                return isNegotiating;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // ConvertBack is not needed for this one-way binding converter
            return false;
        }
    }
}
