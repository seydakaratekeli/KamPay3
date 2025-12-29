using System.Globalization;
using Microsoft.Maui.Graphics;

namespace KamPay.Converters
{
    /// <summary>
    /// Bool değerine göre pipe (|) ile ayrılmış iki renkten birini döndürür
    /// ConverterParameter formatı: "TrueColor|FalseColor" (örn: "#EF5350|#66BB6A")
    /// </summary>
    public class BoolToMultiColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not bool boolValue || parameter is not string colorPair)
                return Colors.Transparent;

            try
            {
                var colors = colorPair.Split('|');
                if (colors.Length != 2)
                    return Colors.Transparent;

                // true ise ilk renk, false ise ikinci renk
                var selectedColor = boolValue ? colors[0].Trim() : colors[1].Trim();
                return Color.FromArgb(selectedColor);
            }
            catch
            {
                return Colors.Transparent;
            }
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}