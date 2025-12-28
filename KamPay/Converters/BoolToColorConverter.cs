using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace KamPay.Converters
{
    /// <summary>
    /// Boolean deðerini renge çevirir. Parameter formatý: "TrueColor|FalseColor"
    /// Örnek: "#FF5252|#4CAF50" -> true ise kýrmýzý, false ise yeþil
    /// </summary>
    public class BoolToColorConverter : IValueConverter
    {
        public Color? TrueColor { get; set; }
        public Color? FalseColor { get; set; }

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not bool boolValue)
                return Colors.Gray;

            // Parameter ile renk belirtilmiþse onu kullan
            if (parameter is string colorPair)
            {
                var colors = colorPair.Split('|');
                if (colors.Length == 2)
                {
                    try
                    {
                        var trueColor = Color.FromArgb(colors[0].Trim());
                        var falseColor = Color.FromArgb(colors[1].Trim());
                        
                        return boolValue ? trueColor : falseColor;
                    }
                    catch
                    {
                        // Parse hatasý varsa property'leri kullan
                    }
                }
            }

            // Property'ler tanýmlýysa onlarý kullan
            if (TrueColor != null && FalseColor != null)
            {
                return boolValue ? TrueColor : FalseColor;
            }

            // Varsayýlan renkler
            return boolValue ? Colors.Red : Colors.Green;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}