using System.Globalization;
using KamPay.Models;

namespace KamPay.Converters
{
    public class IsPendingConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // 1. Durum: Ürün/Takas Ýþlemleri (Transaction)
            if (value is TransactionStatus tStatus)
            {
                return tStatus == TransactionStatus.Pending;
            }

            // 2. Durum: Hizmet Paylaþýmý (ServiceRequest)
            if (value is ServiceRequestStatus sStatus)
            {
                return sStatus == ServiceRequestStatus.Pending;
            }

            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // ConvertBack is not needed for this one-way binding converter
            // Return default status if somehow called
            return TransactionStatus.Pending;
        }
    }
}