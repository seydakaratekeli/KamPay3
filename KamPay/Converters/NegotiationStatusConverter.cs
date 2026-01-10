using System.Globalization;
using KamPay.Models;

namespace KamPay.Converters
{
    public class NegotiationStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not bool isNegotiating) return "Durum Bilinmiyor";
            
            return isNegotiating ? "🔄 Pazarlık Devam Ediyor" : "✅ Fiyat Anlaşıldı";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
