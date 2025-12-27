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
        // Başlatma sırasında hiçbir işlem yapma - uygulama başlatmayı engelleme
        System.Diagnostics.Debug.WriteLine("LocalizationResourceManager başlatıldı - lazy initialization kullanılacak");
    }

    public string this[string key]
    {
        get
        {
            // Her zaman key değerini döndür - hata vermeden çalış
            return key ?? string.Empty;
        }
    }

    public void SetCulture(string cultureCode, bool savePreference = true)
    {
        // Hiçbir şey yapma - uygulamanın çalışmasını engelleme
        System.Diagnostics.Debug.WriteLine($"SetCulture çağrıldı ama devre dışı: {cultureCode}");
    }

    public string GetCurrentCulture()
    {
        // Her zaman varsayılan dili döndür
        return "tr";
    }

    public string GetString(string key)
    {
        // Her zaman key değerini döndür - hata vermeden çalış
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