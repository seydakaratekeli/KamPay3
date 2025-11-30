using System.ComponentModel;
using System.Globalization;
using KamPay.Resources.Languages;

namespace KamPay.Services;

/// <summary>
/// Singleton service for managing localization and culture switching.
/// Implements INotifyPropertyChanged to support UI updates when language changes.
/// </summary>
public class LocalizationResourceManager : INotifyPropertyChanged
{
    private const string LanguagePreferenceKey = "AppLanguage";
    private const string DefaultLanguage = "tr";
    
    private static readonly Lazy<LocalizationResourceManager> _instance = 
        new(() => new LocalizationResourceManager());

    public static LocalizationResourceManager Instance => _instance.Value;

    public event PropertyChangedEventHandler? PropertyChanged;

    private LocalizationResourceManager()
    {
        // Load saved language preference on initialization
        var savedLanguage = Preferences.Get(LanguagePreferenceKey, DefaultLanguage);
        SetCulture(savedLanguage, savePreference: false);
    }

    /// <summary>
    /// Indexer to access resource strings by key.
    /// Usage: LocalizationResourceManager.Instance["Profile"]
    /// </summary>
    public string this[string key]
    {
        get
        {
            var value = AppResources.ResourceManager.GetString(key, AppResources.Culture);
            return value ?? key;
        }
    }

    /// <summary>
    /// Sets the application culture and updates UI.
    /// </summary>
    /// <param name="cultureCode">Culture code (e.g., "tr" for Turkish, "en" for English)</param>
    /// <param name="savePreference">Whether to save the preference (default: true)</param>
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
        
        // Notify all bindings that resources have changed
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    /// <summary>
    /// Gets the current culture code.
    /// </summary>
    public string GetCurrentCulture()
    {
        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
    }
}
