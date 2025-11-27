using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KamPay.Converters
{
    public class DateTimeToTimeAgoConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not DateTime dt) return string.Empty;
            var timeSpan = DateTime.UtcNow - dt;
            if (timeSpan.TotalMinutes < 1) return "Az önce";
            if (timeSpan.TotalMinutes < 60) return $"{(int)timeSpan.TotalMinutes} dakika önce";
            if (timeSpan.TotalHours < 24) return $"{(int)timeSpan.TotalHours} saat önce";
            if (timeSpan.TotalDays < 7) return $"{(int)timeSpan.TotalDays} gün önce";
            return dt.ToString("dd MMM yyyy");
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

}
