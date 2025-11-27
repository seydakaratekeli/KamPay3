// KamPay/Converters/SimulatePaymentButtonVisibilityConverter.cs (Yeni dosya)
using KamPay.Models;
using System.Globalization;

namespace KamPay.Converters
{
    public class SimulatePaymentButtonVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Transaction transaction)
            {
                // Buton sadece;
                // 1. İşlem "Satış" ise
                // 2. Durumu "Kabul Edilmiş" (Accepted) ise
                // 3. Ödeme Durumu "Bekleniyor" (Pending) ise görünmelidir.
                return transaction.Type == ProductType.Satis &&
                       transaction.Status == TransactionStatus.Accepted &&
                       transaction.PaymentStatus == PaymentStatus.Pending;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}