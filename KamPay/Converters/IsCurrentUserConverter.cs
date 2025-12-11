using System.Globalization;
using KamPay.Services;
using Microsoft.Maui.Controls;

namespace KamPay.Converters
{
    public class IsCurrentUserConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string userId && !string.IsNullOrEmpty(userId))
            {
                try
                {
                    var authService = Application.Current?.Handler?.MauiContext?.Services
                        ?.GetService<IAuthenticationService>();
                    
                    if (authService != null)
                    {
                        var currentUser = authService.GetCurrentUserAsync().GetAwaiter().GetResult();
                        return currentUser?.UserId == userId;
                    }
                }
                catch
                {
                    // Hata durumunda false döndür
                }
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
