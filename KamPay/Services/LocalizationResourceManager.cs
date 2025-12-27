using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.Messaging;
using KamPay.Resources.Languages;

namespace KamPay.Services;

public class LocalizationResourceManager : INotifyPropertyChanged
{
    private const string LanguagePreferenceKey = "AppLanguage";
    private const string DefaultLanguage = ""; // Neutral culture

    private static readonly Lazy<LocalizationResourceManager> _instance =
        new(() => new LocalizationResourceManager(), LazyThreadSafetyMode.ExecutionAndPublication);

    public static LocalizationResourceManager Instance => _instance.Value;

    public event PropertyChangedEventHandler? PropertyChanged;

    private LocalizationResourceManager()
    {
        // Başlatma tamamen devre dışı - uygulama başlatmayı engellemesini önle
        // AppResources erişimi kaldırıldı
        System.Diagnostics.Debug.WriteLine("LocalizationResourceManager başlatıldı - tüm metodlar devre dışı (fallback mode)");
    }

    public string this[string key]
    {
        get
        {
            // LocalizationResourceManager devre dışı - key değerini döndür
            // Bu sayede uygulama çalışmaya devam eder (AppResources erişimi yok)
            return key ?? string.Empty;
        }
    }

    public void SetCulture(string cultureCode, bool savePreference = true)
    {
        // SetCulture devre dışı - AppResources ve event trigger'ları kaldırıldı
        // Uygulama başlatmayı engellemesini önlemek için hiçbir işlem yapılmıyor
        System.Diagnostics.Debug.WriteLine($"SetCulture çağrıldı ama devre dışı: {cultureCode}");
    }

    public string GetCurrentCulture()
    {
        // Varsayılan dil döndürülüyor - gerçek culture kontrolü devre dışı
        return "tr";
    }

    public string GetString(string key)
    {
        // LocalizationResourceManager devre dışı - key değerini döndür
        // Bu sayede uygulama çalışmaya devam eder (AppResources erişimi yok)
        return key ?? string.Empty;
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