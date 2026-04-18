using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace KamPay.Converters
{
    // Mesaj zaman rengi
    public class MessageTimeColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var currentUserId = ConverterHelpers.GetCurrentUserId();
            var senderId = value as string;

            return senderId == currentUserId
                ? Color.FromArgb("#E8F5E9")
                : Color.FromArgb("#757575");
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // Mesaj text rengi
    public class MessageTextColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var currentUserId = ConverterHelpers.GetCurrentUserId();
            var senderId = value as string;

            return senderId == currentUserId
                ? Colors.White
                : Colors.Black;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // Mesaj balonu hizalama
    public class MessageBubbleAlignmentConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var currentUserId = ConverterHelpers.GetCurrentUserId();
            var senderId = value as string;

            return senderId == currentUserId
                ? LayoutOptions.End
                : LayoutOptions.Start;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // Mesaj balonu rengi (gönderen/alıcı)
    public class MessageBubbleColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var currentUserId = ConverterHelpers.GetCurrentUserId();
            var senderId = value as string;

            return senderId == currentUserId
                ? Color.FromArgb("#4CAF50")
                : Color.FromArgb("#E0E0E0");
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}