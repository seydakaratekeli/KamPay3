using System.Globalization;
using KamPay.Models;

namespace KamPay.Converters
{
    public class CanApproveAfterNegotiationConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // values[0] = IsNegotiating (bool)
            // values[1] = Status (TransactionStatus)
            // values[2] = ProposedPriceByBuyer (decimal?)
            // values[3] = CounterOfferBySeller (decimal?)
            
            if (values.Length != 4) return false;
            
            if (values[0] is not bool isNegotiating) return false;
            if (values[1] is not TransactionStatus status) return false;
            
            var proposedPrice = values[2] as decimal?;
            var counterOffer = values[3] as decimal?;
            
            bool hadNegotiation = proposedPrice.HasValue || counterOffer.HasValue;
            
            return !isNegotiating 
                   && status == TransactionStatus.Pending 
                   && hadNegotiation;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
