using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.Messaging;
using KamPay.Resources.Languages;

namespace KamPay.Services;

// Yerelleştirme ve dil değiştirme işlemlerini yönetmek için tekil (singleton) servis.
// Dil değiştiğinde kullanıcı arayüzü güncellemelerini desteklemek için INotifyPropertyChanged arayüzünü uygular.
public class LocalizationResourceManager : INotifyPropertyChanged
{
    // bu sayfa, uygulamanın çok dilli desteğini yönetir ve dil değişikliklerini bildirir.
    private const string LanguagePreferenceKey = "AppLanguage";
    private const string DefaultLanguage = "tr"; 
    
    private static readonly Lazy<LocalizationResourceManager> _instance = 
        new(() => new LocalizationResourceManager());

    public static LocalizationResourceManager Instance => _instance.Value;

    public event PropertyChangedEventHandler? PropertyChanged;

    private LocalizationResourceManager()
    {
        // Başlatma sırasında kaydedilmiş dil tercihini yükle
        var savedLanguage = Preferences.Get(LanguagePreferenceKey, DefaultLanguage);
        SetCulture(savedLanguage, savePreference: false);
    }

    // Kaynak dizelerine anahtar ile erişmek için dizinleyici.
    // Kullanım: LocalizationResourceManager.Instance["Profile"]
    public string this[string key]
    {
        get
        {
            var value = AppResources.ResourceManager.GetString(key, AppResources.Culture);
            return value ?? key;
        }
    }

    // Uygulama kültürünü ayarlar ve kullanıcı arayüzünü günceller.
    // <param name="cultureCode">Kültür kodu (örneğin, Türkçe için "tr", İngilizce için "en")</param>
    // <param name="savePreference">Tercihi kaydedip kaydetmeme (varsayılan: true)</param>
    public void SetCulture(string cultureCode, bool savePreference = true)
    {
        CultureInfo culture;
        try
        {
            culture = new CultureInfo(cultureCode);
        }
        catch (CultureNotFoundException)
        {
            culture = new CultureInfo(DefaultLanguage);
            cultureCode = DefaultLanguage;
        }
        
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        AppResources.Culture = culture;
        
        if (savePreference)
        {
            Preferences.Set(LanguagePreferenceKey, cultureCode);
        }

        // Kaynakların değiştiğini tüm bağlantılara bildir
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));

        // ViewModel'lerin yenilenmesi için global mesaj gönder
        WeakReferenceMessenger.Default.Send(new LanguageChangedMessage(cultureCode));
    }

    // Geçerli kültür kodunu alır.
    public string GetCurrentCulture()
    {
        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
    }


    // Anahtara göre yerelleştirilmiş bir dize alır.

    // <param name="key">Kaynak anahtarı</param>
    // <returns>Yerelleştirilmiş dize veya bulunamazsa anahtar</returns>
    public string GetString(string key)
    {
        return AppResources.ResourceManager.GetString(key, AppResources.Culture) ?? key;
    }
}

// Uygulama dili değiştiğinde gönderilen mesaj.
public class LanguageChangedMessage
{
    public string LanguageCode { get; }
    
    public LanguageChangedMessage(string languageCode)
    {
        LanguageCode = languageCode;
    }
}
