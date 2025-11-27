using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace KamPay.Converters
{
    public class IsNotNullOrEmptyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return false;

            if (value is string str)
                return !string.IsNullOrEmpty(str);

            if (value is System.Collections.IEnumerable enumerable)
                return enumerable.GetEnumerator().MoveNext();

            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}