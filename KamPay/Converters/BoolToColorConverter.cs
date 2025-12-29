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
            // Deðer bool deðilse gri dön (Default Safe State)
            if (value is not bool boolValue)
                return Colors.Gray;

            // 1. Parametre (ConverterParameter) kontrolü (Öncelikli)
            if (parameter is string colorPair)
            {
                var colors = colorPair.Split('|');
                if (colors.Length == 2)
                {
                    try
                    {
                        // MAUI'de string -> Color dönüþümü için en doðru yol:
                        var trueColor = Color.Parse(colors[0].Trim());
                        var falseColor = Color.Parse(colors[1].Trim());
                        return boolValue ? trueColor : falseColor;
                    }
                    catch (Exception ex)
                    {
                        // Loglama yapýlabilir: Debug.WriteLine(ex.Message);
                        // Hata durumunda Property'lere veya varsayýlana düþmesi için boþ býrakýlabilir
                    }
                }
            }

            // 2. XAML içinde tanýmlanan Property'ler kontrolü
            if (TrueColor != null && FalseColor != null)
            {
                return boolValue ? TrueColor : FalseColor;
            }

            // 3. Fallback: Hiçbir þey bulunamazsa uygulama temasýna göre güvenli renkler
            return boolValue ? Colors.Red : Colors.Green;
        }
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}