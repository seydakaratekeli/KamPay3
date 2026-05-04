using System.Globalization;
using KamPay.Models;

namespace KamPay.Converters;

public class MicroBusinessCategoryToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null)
            return "Tüm kategoriler";

        return value is MicroBusinessCategory category
            ? category switch
            {
                MicroBusinessCategory.FoodAndDrink => "Yeme İçme",
                MicroBusinessCategory.Stationery => "Kırtasiye",
                MicroBusinessCategory.Printing => "Fotokopi & Baskı",
                MicroBusinessCategory.Technology => "Teknoloji",
                MicroBusinessCategory.Health => "Sağlık",
                MicroBusinessCategory.Sports => "Spor",
                _ => "Diğer"
            }
            : value.ToString() ?? string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
