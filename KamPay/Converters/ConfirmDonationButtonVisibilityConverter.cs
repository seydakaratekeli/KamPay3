// KamPay/Converters/ConfirmDonationButtonVisibilityConverter.cs
using KamPay.Models;
using System.Globalization;

namespace KamPay.Converters
{
    public class ConfirmDonationButtonVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Transaction transaction)
            {
                // Buton sadece;
                // 1. İşlem "Bağış" ise
                // 2. Durumu "Kabul Edilmiş" ise
                // 3. (Ödeme durumu "Pending" ise - veya Status != Completed da diyebiliriz)
                //    PaymentStatus'u burada kullanmak Satış ile tutarlılık sağlar.
                return transaction.Type == ProductType.Bagis &&
                       transaction.Status == TransactionStatus.Accepted &&
                       transaction.PaymentStatus == PaymentStatus.Pending; // PaymentStatus'u Paid yapmayacağız ama tamamlanmadığını anlamak için Pending durumuna bakabiliriz.
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}