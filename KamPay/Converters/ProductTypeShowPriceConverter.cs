using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using KamPay.Models;

namespace KamPay.Converters
{
    public class ProductTypeShowPriceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Sadece Satýlýk ürünlerde fiyat gösterilsin
            if (value is ProductType type)
            {
                return type == ProductType.Satis;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}