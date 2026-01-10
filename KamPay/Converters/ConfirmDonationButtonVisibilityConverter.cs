using KamPay.Models;
using System.Globalization;

namespace KamPay.Converters
{
    public class ConfirmDonationButtonVisibilityConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Transaction transaction)
            {
                // ✅ DÜZELTME: PaymentStatus kontrolü kaldırıldı
                // Buton sadece şu koşullarda görünsün:
                // 1. İşlem "Bağış" ise
                // 2. Durumu "Kabul Edilmiş" ise
                // 3. Henüz tamamlanmamışsa
                return transaction.Type == ProductType.Bagis &&
                       transaction.Status == TransactionStatus.Accepted &&
                       transaction.Status != TransactionStatus.Completed;
            }
            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // ConvertBack is not needed for this one-way binding converter
            // Return null as this converter is for visibility only
            throw new NotImplementedException();
        }
    }
}