using KamPay.Helpers;
using KamPay.Models;
using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace KamPay.Converters
{
    /// <summary>
    /// Sadece pazarlık için kalan süreyi metin olarak hesaplar ve döndürür.
    /// </summary>
    public class NegotiationTimerTextConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not Transaction transaction)
                return string.Empty;

            if (!transaction.IsNegotiating)
                return string.Empty;

            // Süre dolmuşsa uyar, dolmamışsa formatlanmış kalan süreyi ver
            if (NegotiationRules.IsNegotiationExpired(transaction.NegotiationStartedAt))
                return "Süre doldu";

            return NegotiationRules.FormatRemainingTime(transaction.NegotiationStartedAt);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}