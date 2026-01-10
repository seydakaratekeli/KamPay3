using System;
using System.Globalization;
using KamPay.Models;
using Microsoft.Maui.Controls;

namespace KamPay.Converters
{
    /// <summary>
    /// Pazarlýk yapýlabilir mi? (Status Pending VEYA IsNegotiating true ise true döner)
    /// </summary>
    public class CanNegotiateConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2)
                return false;

            // values[0] = TransactionStatus (Status)
            // values[1] = bool (IsNegotiating)
            
            bool isPending = values[0] is TransactionStatus status && status == TransactionStatus.Pending;
            bool isNegotiating = values[1] is bool negotiating && negotiating;

            // Status Pending veya pazarlýk devam ediyorsa true dön
            return isPending || isNegotiating;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
