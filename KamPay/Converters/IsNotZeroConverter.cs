using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace KamPay.Converters
{
    public class IsNotZeroConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return false;

            if (value is int intValue)
                return intValue != 0;

            if (value is double doubleValue)
                return doubleValue != 0.0;

            if (value is float floatValue)
                return floatValue != 0.0f;

            if (value is decimal decimalValue)
                return decimalValue != 0m;

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}