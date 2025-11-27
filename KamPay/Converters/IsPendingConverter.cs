using System.Globalization;
using KamPay.Models;

namespace KamPay.Converters
{
    public class IsPendingConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 1. Durum: Ürün/Takas İşlemleri (Transaction)
            if (value is TransactionStatus tStatus)
            {
                return tStatus == TransactionStatus.Pending;
            }

            // 2. Durum: Hizmet Paylaşımı (ServiceRequest)
            if (value is ServiceRequestStatus sStatus)
            {
                return sStatus == ServiceRequestStatus.Pending;
            }

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}