using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace KamPay.Converters
{
    /// <summary>
    /// Boolean deðerini metne çevirir. Parameter formatý: "TrueText|FalseText"
    /// Örnek: "Kod süresi doldu|Kalan süre" -> true ise "Kod süresi doldu", false ise "Kalan süre"
    /// </summary>
    public class BoolToTextConverter : IValueConverter
    {
        public string? TrueText { get; set; }
        public string? FalseText { get; set; }

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not bool boolValue)
                return string.Empty;

            // Parameter ile metin belirtilmiþse onu kullan
            if (parameter is string textPair)
            {
                var texts = textPair.Split('|');
                if (texts.Length == 2)
                {
                    return boolValue ? texts[0].Trim() : texts[1].Trim();
                }
            }

            // Property'ler tanýmlýysa onlarý kullan
            if (!string.IsNullOrEmpty(TrueText) && !string.IsNullOrEmpty(FalseText))
            {
                return boolValue ? TrueText : FalseText;
            }

            // Varsayýlan metinler
            return boolValue ? "True" : "False";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
