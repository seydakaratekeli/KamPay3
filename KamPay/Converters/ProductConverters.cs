using System;
using System.Globalization;
using KamPay.Models;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;


namespace KamPay.Converters
{
    // String boş mu kontrolü
    public class StringIsNotNullOrEmptyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => !string.IsNullOrEmpty(value as string);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // ProductType'ı renk'e çevir
    public class ProductTypeToBadgeColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ProductType type)
            {
                return type switch
                {
                    ProductType.Satis => Color.FromArgb("#4CAF50"), // Yeşil
                    ProductType.Bagis => Color.FromArgb("#FF9800"), // Turuncu
                    ProductType.Takas => Color.FromArgb("#2196F3"), // Mavi
                    _ => Color.FromArgb("#757575")
                };
            }
            return Color.FromArgb("#757575");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // ProductType'ı metne çevir
    public class ProductTypeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ProductType type)
            {
                return type switch
                {
                    ProductType.Satis => "Satılık",
                    ProductType.Bagis => "Bağış",
                    ProductType.Takas => "Takas",
                    _ => "Belirtilmemiş"
                };
            }
            return "Belirtilmemiş";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var text = value as string;
            return text switch
            {
                "Satılık" => ProductType.Satis,
                "Bağış" => ProductType.Bagis,
                "Takas" => ProductType.Takas,
                _ => ProductType.Satis
            };
        }
    }

    // ProductCondition'ı metne çevir
    public class ProductConditionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ProductCondition condition)
            {
                return condition switch
                {
                    ProductCondition.YeniGibi => "Yeni Gibi",
                    ProductCondition.CokIyi => "Çok İyi",
                    ProductCondition.Iyi => "İyi",
                    ProductCondition.Orta => "Orta",
                    ProductCondition.Kullanilabilir => "Kullanılabilir",
                    _ => "Belirtilmemiş"
                };
            }
            return "Belirtilmemiş";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var text = value as string;
            return text switch
            {
                "Yeni Gibi" => ProductCondition.YeniGibi,
                "Çok İyi" => ProductCondition.CokIyi,
                "İyi" => ProductCondition.Iyi,
                "Orta" => ProductCondition.Orta,
                "Kullanılabilir" => ProductCondition.Kullanilabilir,
                _ => ProductCondition.YeniGibi
            };
        }
    }

    // Mesaj zaman rengi
    public class MessageTimeColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var currentUserId = Preferences.Get("current_user_id", string.Empty);
            var senderId = value as string;

            return senderId == currentUserId
                ? Color.FromArgb("#E8F5E9")
                : Color.FromArgb("#757575");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // Mesaj text rengi
    public class MessageTextColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var currentUserId = Preferences.Get("current_user_id", string.Empty);
            var senderId = value as string;

            return senderId == currentUserId
                ? Colors.White
                : Colors.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // Mesaj balonu hizalama
    public class MessageBubbleAlignmentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var currentUserId = Preferences.Get("current_user_id", string.Empty);
            var senderId = value as string;

            return senderId == currentUserId
                ? LayoutOptions.End
                : LayoutOptions.Start;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // Mesaj balonu rengi (gönderen/alıcı)
    public class MessageBubbleColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var currentUserId = Preferences.Get("current_user_id", string.Empty);
            var senderId = value as string;

            return senderId == currentUserId
                ? Color.FromArgb("#4CAF50")
                : Color.FromArgb("#E0E0E0");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

   

    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => !(bool)value;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => !(bool)value;
    }

    public class IntToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => (int)value > 0;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class ColorToLightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string colorHex)
            {
                try
                {
                    var color = Color.FromArgb(colorHex);
                    return color.WithLuminosity((float)Math.Min(1.0, color.GetLuminosity() + 0.8));
                }
                catch { return Colors.Transparent; }
            }
            return Colors.Transparent;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class PostTypeToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => (PostType)value switch { PostType.HelpRequest => "❓", PostType.Announcement => "📢", PostType.ThankYou => "💖", PostType.Volunteer => "🤝", _ => "📌" };
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class PostTypeToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => (PostType)value switch { PostType.HelpRequest => "Yardım Talebi", PostType.Announcement => "Duyuru", PostType.ThankYou => "Teşekkür", PostType.Volunteer => "Gönüllü Aranıyor", _ => "Diğer" };
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class ServiceCategoryToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => (ServiceCategory)value switch { ServiceCategory.Education => "📚", ServiceCategory.Technical => "💻", ServiceCategory.Cooking => "🍳", ServiceCategory.Childcare => "👶", ServiceCategory.PetCare => "🐕", ServiceCategory.Translation => "🌐", ServiceCategory.Moving => "📦", _ => "📌" };
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class ServiceCategoryToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => (ServiceCategory)value switch { ServiceCategory.Education => "Eğitim", ServiceCategory.Technical => "Teknik", ServiceCategory.Cooking => "Yemek", ServiceCategory.Childcare => "Çocuk Bakımı", ServiceCategory.PetCare => "Evcil Hayvan", ServiceCategory.Translation => "Çeviri", ServiceCategory.Moving => "Taşıma", _ => "Diğer" };
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

   
    public class EqualityToBorderColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value?.ToString() == parameter?.ToString() ? Color.FromArgb("#4CAF50") : Colors.Transparent;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class EqualityToBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value?.ToString() == parameter?.ToString() ? Color.FromArgb("#E8F5E9") : Colors.White;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
    
    public class EqualityToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Gelen değeri ve parametreyi string'e çevirip karşılaştır
            return value?.ToString() == parameter?.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class EqualityToTextColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value?.ToString() == parameter?.ToString() ? Color.FromArgb("#4CAF50") : Colors.Black;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
    public class EnumToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            string enumValue = value.ToString();
            string parameterValue = parameter.ToString();

            return enumValue.Equals(parameterValue);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    } 

    public class FirstCharConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string text && !string.IsNullOrEmpty(text))
            {
                return text.ToUpper()[0];
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}