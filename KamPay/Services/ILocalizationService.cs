using System.ComponentModel;

namespace KamPay.Services
{
    /// <summary>
    /// Çoklu dil desteði için lokalizasyon servisi
    /// </summary>
    public interface ILocalizationService : INotifyPropertyChanged
    {
        /// <summary>
        /// Lokalize string'e eriþim için indexer
        /// </summary>
        string this[string key] { get; }

        /// <summary>
        /// Servisin baþlatýlýp baþlatýlmadýðýný kontrol eder
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Uygulamanýn aktif dil kodunu deðiþtirir
        /// </summary>
        /// <param name="cultureCode">Dil kodu (örn: "tr", "en")</param>
        /// <param name="savePreference">Tercihi kaydet (varsayýlan: true)</param>
        void SetCulture(string cultureCode, bool savePreference = true);

        /// <summary>
        /// Mevcut dil kodunu döndürür
        /// </summary>
        string GetCurrentCulture();

        /// <summary>
        /// Belirli bir key için lokalize string döndürür
        /// </summary>
        string GetString(string key);
    }
}
