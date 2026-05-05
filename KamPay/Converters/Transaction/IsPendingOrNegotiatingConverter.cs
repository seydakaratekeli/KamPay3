using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace KamPay.Converters
{
    public class IsPendingOrNegotiatingConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null)
                return false;

            var status = value.ToString();
            return status == "Pending" || status == "Negotiating";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
