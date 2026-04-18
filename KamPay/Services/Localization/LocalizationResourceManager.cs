using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.Messaging;
using KamPay.Resources.Languages;

namespace KamPay.Services;

/// <summary>
/// ✅ DI ile kullanılabilir lokalizasyon servisi
/// Hem Singleton pattern hem de DI desteği var
/// </summary>
public class LocalizationResourceManager : ILocalizationService
{
    private const string LanguagePreferenceKey = "AppLanguage";
    private const string DefaultLanguage = ""; // Neutral culture
    private bool _isInitialized = false;

    // ✅ Backward compatibility için static instance korunuyor
    private static readonly Lazy<LocalizationResourceManager> _instance =
        new(() => new LocalizationResourceManager(), LazyThreadSafetyMode.ExecutionAndPublication);

    public static LocalizationResourceManager Instance => _instance.Value;
    
    /// <summary>
    /// Forces the lazy initialization of the singleton instance.
    /// Call this method early in app startup to ensure the instance is ready.
    /// </summary>
    public static void EnsureInitialized()
    {
        var _ = Instance;
        KamPay.Helpers.AppLogger.DebugLog($"✓ LocalizationResourceManager zorla başlatıldı - IsInitialized: {Instance.IsInitialized}");
    }
    
    public bool IsInitialized => _isInitialized;

    public event PropertyChangedEventHandler? PropertyChanged;

    // ✅ DI için public constructor
    public LocalizationResourceManager()
    {
        try
        {
            KamPay.Helpers.AppLogger.DebugLog("⚙️ LocalizationResourceManager başlatılıyor...");
            
            try
            {
                AppResources.Culture = null;
                KamPay.Helpers.AppLogger.DebugLog("✓ Neutral culture ayarlandı (başlangıç)");
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"⚠️ Culture ayarlama hatası: {ex.Message}");
            }
            
            var resourceManager = AppResources.ResourceManager;
            if (resourceManager == null)
            {
                KamPay.Helpers.AppLogger.DebugLog("⚠️ UYARI: ResourceManager null");
                _isInitialized = false;
                return;
            }
            
            KamPay.Helpers.AppLogger.DebugLog("✓ ResourceManager başarıyla erişildi");
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            KamPay.Helpers.AppLogger.DebugLog($"⚠️ LocalizationResourceManager başlatma hatası: {ex.GetType().Name}");
            KamPay.Helpers.AppLogger.DebugLog($"   Mesaj: {ex.Message}");
            _isInitialized = true;
        }
    }

    public string this[string key]
    {
        get
        {
            try
            {
                if (string.IsNullOrEmpty(key))
                {
                    KamPay.Helpers.AppLogger.DebugLog("⚠️ Boş anahtar ile kaynak erişimi denendi");
                    return string.Empty;
                }

                var resourceManager = AppResources.ResourceManager;
                
                if (resourceManager == null)
                {
                    KamPay.Helpers.AppLogger.DebugLog($"⚠️ ResourceManager null - fallback key döndürülüyor: {key}");
                    return key;
                }

                var culture = AppResources.Culture;
                
                var value = culture == null || string.IsNullOrEmpty(culture.Name)
                    ? resourceManager.GetString(key)
                    : resourceManager.GetString(key, culture);

                if (string.IsNullOrEmpty(value))
                {
                    KamPay.Helpers.AppLogger.DebugLog($"⚠️ Kaynak bulunamadı: {key}");
                    return key;
                }

                return value;
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"⚠️ Kaynak erişim hatası: {key}, Hata: {ex.Message}");
                return key;
            }
        }
    }

    public void SetCulture(string cultureCode, bool savePreference = true)
    {
        try
        {
            KamPay.Helpers.AppLogger.DebugLog($"⚙️ Culture ayarlanıyor: {cultureCode}");
            
            if (string.IsNullOrEmpty(cultureCode) || cultureCode == "tr")
            {
                AppResources.Culture = null;
                cultureCode = "tr";
                KamPay.Helpers.AppLogger.DebugLog("✓ Neutral culture (Türkçe) ayarlandı");
            }
            else if (cultureCode == "en")
            {
                try
                {
                    var englishCulture = new CultureInfo("en");
                    AppResources.Culture = englishCulture;
                    KamPay.Helpers.AppLogger.DebugLog("✓ İngilizce kültür ayarlandı");
                }
                catch (Exception cultureEx)
                {
                    KamPay.Helpers.AppLogger.DebugLog($"⚠️ İngilizce culture ayarlama hatası: {cultureEx.Message}");
                    AppResources.Culture = null;
                }
            }
            else
            {
                try
                {
                    var culture = new CultureInfo(cultureCode);
                    AppResources.Culture = culture;
                    KamPay.Helpers.AppLogger.DebugLog($"✓ Kültür ayarlandı: {culture.Name}");
                }
                catch (CultureNotFoundException)
                {
                    KamPay.Helpers.AppLogger.DebugLog($"⚠️ Geçersiz culture kodu: {cultureCode}, neutral kullanılıyor");
                    AppResources.Culture = null;
                }
            }

            if (savePreference)
            {
                Preferences.Set(LanguagePreferenceKey, cultureCode);
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
            
            try
            {
                WeakReferenceMessenger.Default.Send(new LanguageChangedMessage(cultureCode));
            }
            catch (Exception msgEx)
            {
                KamPay.Helpers.AppLogger.DebugLog($"⚠️ Message gönderme hatası: {msgEx.Message}");
            }
        }
        catch (System.Resources.MissingManifestResourceException mmrEx)
        {
            KamPay.Helpers.AppLogger.DebugLog($"⚠️ UYARI: Kaynak dosyası bulunamadı: {mmrEx.Message}");
            try
            {
                AppResources.Culture = null;
            }
            catch { }
        }
        catch (Exception ex)
        {
            KamPay.Helpers.AppLogger.DebugLog($"⚠️ SetCulture hatası: {ex.GetType().Name} - {ex.Message}");
            try
            {
                AppResources.Culture = null;
            }
            catch { }
        }
    }

    public string GetCurrentCulture()
    {
        try
        {
            var culture = AppResources.Culture;
            
            if (culture == null || string.IsNullOrEmpty(culture.Name))
            {
                return "tr";
            }
            
            return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        }
        catch (Exception ex)
        {
            KamPay.Helpers.AppLogger.DebugLog($"GetCurrentCulture hatası: {ex.Message}");
            return "tr";
        }
    }

    public string GetString(string key)
    {
        try
        {
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
            KamPay.Helpers.AppLogger.DebugLog($"GetString hatası: {key}, Hata: {ex.Message}");
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
