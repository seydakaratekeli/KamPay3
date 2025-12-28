using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.Messaging;
using KamPay.Resources.Languages;

namespace KamPay.Services;

public class LocalizationResourceManager : INotifyPropertyChanged
{
    private const string LanguagePreferenceKey = "AppLanguage";
    private const string DefaultLanguage = ""; // Neutral culture
    private bool _isInitialized = false;

    private static readonly Lazy<LocalizationResourceManager> _instance =
        new(() => new LocalizationResourceManager(), LazyThreadSafetyMode.ExecutionAndPublication);

    public static LocalizationResourceManager Instance => _instance.Value;
    
    public bool IsInitialized => _isInitialized;

    public event PropertyChangedEventHandler? PropertyChanged;

    private LocalizationResourceManager()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("⚙️ LocalizationResourceManager başlatılıyor...");
            
            // ResourceManager'ı başlat ve kontrol et
            var resourceManager = AppResources.ResourceManager;
            if (resourceManager == null)
            {
                System.Diagnostics.Debug.WriteLine("⚠️ KRITIK: ResourceManager başlatılamadı!");
                // ResourceManager null ise, varsayılan culture ile devam et
                AppResources.Culture = null;
                // Mark as not initialized - critical failure
                _isInitialized = false;
                return;
            }
            
            System.Diagnostics.Debug.WriteLine("✓ ResourceManager başarıyla başlatıldı");
            
            // Başlatma sırasında kaydedilmiş dil tercihini yükle
            var savedLanguage = Preferences.Get(LanguagePreferenceKey, DefaultLanguage);
            System.Diagnostics.Debug.WriteLine($"⚙️ Kaydedilmiş dil: '{savedLanguage}'");
            
            // Eğer kaydedilmiş dil boşsa veya "tr" ise, neutral culture kullan
            if (string.IsNullOrEmpty(savedLanguage) || savedLanguage == "tr")
            {
                savedLanguage = ""; // Neutral culture
            }
            
            SetCulture(savedLanguage, savePreference: false);
            _isInitialized = true;
            System.Diagnostics.Debug.WriteLine("✓ LocalizationResourceManager başarıyla başlatıldı");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"⚠️ LocalizationResourceManager başlatma hatası: {ex.GetType().Name}");
            System.Diagnostics.Debug.WriteLine($"   Mesaj: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"   StackTrace: {ex.StackTrace}");
            
            // Fallback olarak neutral culture kullan
            try
            {
                AppResources.Culture = null;
                System.Diagnostics.Debug.WriteLine("⚙️ Fallback: Neutral culture kullanılıyor");
                // Even though initialization failed, we can still function with neutral culture
                // Mark as initialized so the app can continue
                _isInitialized = true;
            }
            catch (Exception fallbackEx)
            {
                // Son çare: hiçbir şey yapma
                System.Diagnostics.Debug.WriteLine($"⚠️ SetCulture fallback bile başarısız oldu: {fallbackEx.Message}");
                // Complete failure - mark as not initialized
                _isInitialized = false;
            }
        }
    }

    public string this[string key]
    {
        get
        {
            try
            {
                // Null check for key
                if (string.IsNullOrEmpty(key))
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ Boş anahtar ile kaynak erişimi denendi");
                    return string.Empty;
                }

                // ResourceManager referansını yerel değişkene al (thread-safe)
                var resourceManager = AppResources.ResourceManager;
                
                // ResourceManager kontrolü
                if (resourceManager == null)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ ResourceManager null - fallback key döndürülüyor: {key}");
                    return key;
                }

                // Culture referansını yerel değişkene al (yarış durumunu önler)
                var culture = AppResources.Culture;
                
                // Culture null ise neutral culture kullan (AppResources.resx)
                var value = culture == null || string.IsNullOrEmpty(culture.Name)
                    ? resourceManager.GetString(key)
                    : resourceManager.GetString(key, culture);

                if (string.IsNullOrEmpty(value))
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ Kaynak bulunamadı: {key}");
                    return key;
                }

                return value;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ Kaynak erişim hatası: {key}, Hata: {ex.Message}");
                return key;
            }
        }
    }

    public void SetCulture(string cultureCode, bool savePreference = true)
    {
        try
        {
            // Eğer boş veya "tr" ise, neutral culture kullan
            if (string.IsNullOrEmpty(cultureCode) || cultureCode == "tr")
            {
                // Neutral culture için Culture'ı null yap
                AppResources.Culture = null;
                
                // Thread culture'ları Türkçe yap (sayılar, tarihler için)
                var turkishCulture = new CultureInfo("tr-TR");
                CultureInfo.DefaultThreadCurrentCulture = turkishCulture;
                CultureInfo.DefaultThreadCurrentUICulture = turkishCulture;
                CultureInfo.CurrentCulture = turkishCulture;
                CultureInfo.CurrentUICulture = turkishCulture;
                Thread.CurrentThread.CurrentCulture = turkishCulture;
                Thread.CurrentThread.CurrentUICulture = turkishCulture;
                
                cultureCode = "tr";
                System.Diagnostics.Debug.WriteLine("Neutral culture (Türkçe) ayarlandı");
            }
            else if (cultureCode == "en")
            {
                // İngilizce için en-US kullan
                var englishCulture = new CultureInfo("en-US");
                CultureInfo.DefaultThreadCurrentCulture = englishCulture;
                CultureInfo.DefaultThreadCurrentUICulture = englishCulture;
                CultureInfo.CurrentCulture = englishCulture;
                CultureInfo.CurrentUICulture = englishCulture;
                Thread.CurrentThread.CurrentCulture = englishCulture;
                Thread.CurrentThread.CurrentUICulture = englishCulture;
                
                // AppResources.en.resx kullanılacak
                AppResources.Culture = englishCulture;
                System.Diagnostics.Debug.WriteLine("İngilizce kültür ayarlandı");
            }
            else
            {
                // Diğer diller için
                var culture = new CultureInfo(cultureCode);
                CultureInfo.DefaultThreadCurrentCulture = culture;
                CultureInfo.DefaultThreadCurrentUICulture = culture;
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;
                Thread.CurrentThread.CurrentCulture = culture;
                Thread.CurrentThread.CurrentUICulture = culture;
                AppResources.Culture = culture;
                
                System.Diagnostics.Debug.WriteLine($"Kültür ayarlandı: {culture.Name}");
            }

            if (savePreference)
            {
                Preferences.Set(LanguagePreferenceKey, cultureCode);
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
            WeakReferenceMessenger.Default.Send(new LanguageChangedMessage(cultureCode));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SetCulture hatası: {ex.Message}");
            // Hata durumunda neutral culture kullan
            AppResources.Culture = null;
        }
    }

    public string GetCurrentCulture()
    {
        try
        {
            // Culture referansını yerel değişkene al
            var culture = AppResources.Culture;
            
            // Eğer Culture null ise, "tr" döndür (neutral = Türkçe)
            if (culture == null || string.IsNullOrEmpty(culture.Name))
            {
                return "tr";
            }
            
            return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetCurrentCulture hatası: {ex.Message}");
            return "tr"; // Fallback
        }
    }

    public string GetString(string key)
    {
        try
        {
            // ResourceManager ve Culture referanslarını yerel değişkenlere al
            var resourceManager = AppResources.ResourceManager;
            if (resourceManager == null)
            {
                return key;
            }

            var culture = AppResources.Culture;
            var value = culture == null
                ? resourceManager.GetString(key)
                : resourceManager.GetString(key, culture);
                
            return value ?? key;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetString hatası: {key}, Hata: {ex.Message}");
            return key;
        }
    }
}

public class LanguageChangedMessage
{
    public string LanguageCode { get; }

    public LanguageChangedMessage(string languageCode)
    {
        LanguageCode = languageCode;
    }
}