using System.Globalization;

namespace KamPay.Converters
{
    /// <summary>
    /// Birden fazla bool deðerini AND iþlemine tabi tutar
    /// Tüm deðerler true ise true döner
    /// </summary>
    public class AllTrueConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length == 0)
                return false;

            return values.All(v => v is bool b && b);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
