using System;
using System.Globalization;
using KamPay.Models;
using KamPay.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;


namespace KamPay.Converters
{
    // String boş mu kontrolü
    public class StringIsNotNullOrEmptyConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => !string.IsNullOrEmpty(value as string);

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // ProductType'ı renk'e çevir
    public class ProductTypeToBadgeColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
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

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // ProductType'ı metne çevir
    public class ProductTypeConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is ProductType type)
            {
                var loc = LocalizationResourceManager.Instance;
                return type switch
                {
                    ProductType.Satis => loc.GetString("ProductTypeSale"),
                    ProductType.Bagis => loc.GetString("ProductTypeDonation"),
                    ProductType.Takas => loc.GetString("ProductTypeExchange"),
                    _ => loc.GetString("NotSpecified")
                };
            }
            return LocalizationResourceManager.Instance.GetString("NotSpecified");
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var text = value as string;
            var loc = LocalizationResourceManager.Instance;
            
            if (text == loc.GetString("ProductTypeSale")) return ProductType.Satis;
            if (text == loc.GetString("ProductTypeDonation")) return ProductType.Bagis;
            if (text == loc.GetString("ProductTypeExchange")) return ProductType.Takas;
            
            return ProductType.Satis;
        }
    }

    // ProductCondition'ı metne çevir
    public class ProductConditionConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is ProductCondition condition)
            {
                var loc = LocalizationResourceManager.Instance;
                return condition switch
                {
                    ProductCondition.YeniGibi => loc.GetString("ConditionLikeNew"),
                    ProductCondition.CokIyi => loc.GetString("ConditionVeryGood"),
                    ProductCondition.Iyi => loc.GetString("ConditionGood"),
                    ProductCondition.Orta => loc.GetString("ConditionFair"),
                    ProductCondition.Kullanilabilir => loc.GetString("ConditionUsable"),
                    _ => loc.GetString("NotSpecified")
                };
            }
            return LocalizationResourceManager.Instance.GetString("NotSpecified");
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var text = value as string;
            var loc = LocalizationResourceManager.Instance;
            
            if (text == loc.GetString("ConditionLikeNew")) return ProductCondition.YeniGibi;
            if (text == loc.GetString("ConditionVeryGood")) return ProductCondition.CokIyi;
            if (text == loc.GetString("ConditionGood")) return ProductCondition.Iyi;
            if (text == loc.GetString("ConditionFair")) return ProductCondition.Orta;
            if (text == loc.GetString("ConditionUsable")) return ProductCondition.Kullanilabilir;
            
            return ProductCondition.YeniGibi;
        }
    }

    // Mesaj zaman rengi
    public class MessageTimeColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var currentUserId = Preferences.Get("current_user_id", string.Empty);
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
            var currentUserId = Preferences.Get("current_user_id", string.Empty);
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
            var currentUserId = Preferences.Get("current_user_id", string.Empty);
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
            var currentUserId = Preferences.Get("current_user_id", string.Empty);
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

   

    public class InverseBoolConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is bool b && !b;
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is bool b && !b;
    }

    public class IntToBoolConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is int i && i > 0;
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class ColorToLightConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
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
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class PostTypeToIconConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is PostType pt ? pt switch { PostType.HelpRequest => "❓", PostType.Announcement => "📢", PostType.ThankYou => "💖", PostType.Volunteer => "🤝", _ => "📌" } : "📌";
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class PostTypeToTextConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var loc = LocalizationResourceManager.Instance;
            return value is PostType pt ? pt switch 
            { 
                PostType.HelpRequest => loc.GetString("PostTypeHelpRequest"), 
                PostType.Announcement => loc.GetString("PostTypeAnnouncement"), 
                PostType.ThankYou => loc.GetString("PostTypeThankYou"), 
                PostType.Volunteer => loc.GetString("PostTypeVolunteer"), 
                _ => loc.GetString("Other") 
            } : loc.GetString("Other");
        }
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class ServiceCategoryToIconConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is ServiceCategory sc ? sc switch { ServiceCategory.Education => "📚", ServiceCategory.Technical => "💻", ServiceCategory.Cooking => "🍳", ServiceCategory.Childcare => "👶", ServiceCategory.PetCare => "🐕", ServiceCategory.Translation => "🌐", ServiceCategory.Moving => "📦", _ => "📌" } : "📌";
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class ServiceCategoryToTextConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var loc = LocalizationResourceManager.Instance;
            
            // If value is null (i.e., "All" option)
            if (value == null)
                return loc.GetString("All");

            // If value is a category, do the normal conversion
            if (value is ServiceCategory category)
            {
                return category switch
                {
                    ServiceCategory.Education => loc.GetString("ServiceCategoryEducation"),
                    ServiceCategory.Technical => loc.GetString("ServiceCategoryTechnical"),
                    ServiceCategory.Cooking => loc.GetString("ServiceCategoryCooking"),
                    ServiceCategory.Childcare => loc.GetString("ServiceCategoryChildcare"),
                    ServiceCategory.PetCare => loc.GetString("ServiceCategoryPetCare"),
                    ServiceCategory.Translation => loc.GetString("ServiceCategoryTranslation"),
                    ServiceCategory.Moving => loc.GetString("ServiceCategoryMoving"),
                    ServiceCategory.Other => loc.GetString("ServiceCategoryOther"),
                    _ => loc.GetString("Other")
                };
            }

            // If unexpected, return "All" or "Other"
            return loc.GetString("All");
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // Used when going from Picker to ViewModel.
            // Generally returning null is sufficient.
            return null;
        }
    }

    public class EqualityToBorderColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value?.ToString() == parameter?.ToString() ? Color.FromArgb("#4CAF50") : Colors.Transparent;
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class EqualityToBackgroundConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value?.ToString() == parameter?.ToString() ? Color.FromArgb("#E8F5E9") : Colors.White;
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }
    
    public class EqualityToBoolConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // Gelen değeri ve parametreyi string'e çevirip karşılaştır
            return value?.ToString() == parameter?.ToString();
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class EqualityToTextColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value?.ToString() == parameter?.ToString() ? Color.FromArgb("#4CAF50") : Colors.Black;
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }
    public class EnumToBoolConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            // Parameter'ı parse et
            var paramString = parameter.ToString();
            if (paramString == null)
                return false;
                
            bool invert = false;
            
            // Invert kontrolü (örn: "System,Invert=True")
            if (paramString.Contains(","))
            {
                var parts = paramString.Split(',');
                paramString = parts[0].Trim();
                
                if (parts.Length > 1 && parts[1].Trim().Equals("Invert=True", StringComparison.OrdinalIgnoreCase))
                {
                    invert = true;
                }
            }

            // Enum değerini string'e çevir
            string? enumValue = value.ToString();
            if (enumValue == null)
                return false;

            // Karşılaştır
            bool result = enumValue.Equals(paramString, StringComparison.OrdinalIgnoreCase);
            
            // Invert varsa tersine çevir
            return invert ? !result : result;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    } 

    public class FirstCharConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string text && !string.IsNullOrEmpty(text))
            {
                // Türkçe karakterler için CultureInfo.CurrentCulture kullan
                return text.ToUpper(new CultureInfo("tr-TR"))[0];
            }
            return string.Empty;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // Ürün kategori adlarını yerelleştirilmiş metne dönüştür.

    // Firebase'de saklanan Türkçe kategori adlarını yerelleştirilmiş kaynak anahtarlarına eşler.
    
    public class ProductCategoryToTextConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string categoryName && !string.IsNullOrEmpty(categoryName))
            {
                var loc = LocalizationResourceManager.Instance;
                
                // Türkçe karakter karşılaştırması için ToLower(tr-TR) kullan
                var categoryLower = categoryName.ToLower(new CultureInfo("tr-TR"));
                
                // Map Turkish category names to localized resource keys
                return categoryLower switch
                {
                    "elektronik" => loc.GetString("CategoryElectronics"),
                    "kitap ve kırtasiye" => loc.GetString("CategoryBooks"),
                    "giyim" => loc.GetString("CategoryClothing"),
                    "ev eşyası" => loc.GetString("CategoryHomeGoods"),
                    "spor malzemeleri" => loc.GetString("CategorySports"),
                    "müzik aletleri" => loc.GetString("CategoryMusic"),
                    "oyun ve hobi" => loc.GetString("CategoryGames"),
                    "bebek ürünleri" => loc.GetString("CategoryBaby"),
                    "diğer" => loc.GetString("CategoryOther"),
                    _ => categoryName // Return original if not matched
                };
            }
            return value?.ToString() ?? string.Empty;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}