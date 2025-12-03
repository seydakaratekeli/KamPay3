using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using KamPay.Models;

namespace KamPay.Converters
{
    public class ProductTypeToBadgeTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ProductType type)
            {
                return type switch
                {
                    ProductType.Satis => "Satýlýk",
                    ProductType.Bagis => "Baðýþ",
                    ProductType.Takas => "Takas",
                    _ => "Diðer"
                };
            }
            return "Diðer";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}