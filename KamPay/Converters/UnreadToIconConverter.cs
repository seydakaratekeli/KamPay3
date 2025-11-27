using System.Globalization;
using Microsoft.Maui.Controls; 

namespace KamPay.Converters
{
    public class UnreadToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Gelen değer 'true' ise (yani okunmamış mesaj varsa)
            if (value is bool hasUnread && hasUnread)
            {
                // Kırmızı noktalı (rozetli) ikonu kullan
                return "message_icon_badge.svg";
            }

            // Diğer tüm durumlarda normal ikonu kullan
            return "message_icon.svg";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}