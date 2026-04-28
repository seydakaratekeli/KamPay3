using System;
using System.Globalization;
using KamPay.Models;
using Microsoft.Maui.Controls;

namespace KamPay.Converters
{
    /// <summary>
    /// Pazarlık yapılabilir mi?
    /// values[0] = TransactionStatus (Status)
    /// values[1] = bool (IsNegotiating)
    /// values[2] = bool (IsFixedPriceRequest) — ✅ SORUN 1 FIX
    /// </summary>
    public class CanNegotiateConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2)
                return false;

            // ✅ SORUN 1 FIX: Liste fiyatıyla alım → pazarlık yapılamaz
            // values[2] varsa ve IsFixedPriceRequest == true ise direkt false döndür
            if (values.Length >= 3 && values[2] is bool isFixedPrice && isFixedPrice)
                return false;

            // values[0] = TransactionStatus (Status)
            // values[1] = bool (IsNegotiating)
            bool isPending = values[0] is TransactionStatus status && status == TransactionStatus.Pending;
            bool isNegotiating = values[1] is bool negotiating && negotiating;

            return isPending || isNegotiating;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}