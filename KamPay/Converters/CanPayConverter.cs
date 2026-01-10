using System.Globalization;
using KamPay.Models;

namespace KamPay.Converters
{
    public class CanPayConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // values[0] = Status (TransactionStatus)
            // values[1] = PaymentStatus (PaymentStatus)
            // values[2] = IsNegotiating (bool)
            
            if (values.Length != 3) return false;
            
            if (values[0] is not TransactionStatus status) return false;
            if (values[1] is not PaymentStatus paymentStatus) return false;
            if (values[2] is not bool isNegotiating) return false;
            
            return status == TransactionStatus.Accepted 
                   && paymentStatus != PaymentStatus.Paid 
                   && !isNegotiating;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
