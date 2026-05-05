using KamPay.ViewModels;
using KamPay.Services;
using KamPay.Resources;
using KamPay.Services.Auth;
using KamPay.Models;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.Networking;
using System.Reflection;
using System.Text.Json;

namespace KamPay
{
    public partial class App : Application
    {
        private readonly IServiceProvider _serviceProvider;

        public App(AppShell appShell, IServiceProvider serviceProvider)
        {
            try
            {
                // Syncfusion Lisans Kaydı
                var syncfusionLicenseKey = GetSyncfusionLicenseKey();
                if (!string.IsNullOrWhiteSpace(syncfusionLicenseKey))
                {
                    Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(syncfusionLicenseKey);
                    KamPay.Helpers.AppLogger.DebugLog("Syncfusion lisansi yuklendi.");
                }
                else
                {
                    KamPay.Helpers.AppLogger.DebugLog("Syncfusion lisans anahtari bulunamadi. KAMPAY_SYNCFUSION_LICENSE_KEY veya appsettings icindeki SyncfusionSettings:LicenseKey alanini kontrol edin.");
                }

                InitializeComponent();

                _serviceProvider = serviceProvider;

                // MainPage'i önce ata
                MainPage = appShell;
                KamPay.Helpers.AppLogger.DebugLog("✓ MainPage (AppShell) atandı");
                RegisterConnectivityMonitor();

                // Localization'ı daha güvenli başlat - hata olsa bile devam et
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // LocalizationResourceManager'ın başlatılmasını bekle
                        await Task.Delay(200);
                        
                        // Kaydedilmiş dil tercihini al (Language preference'i güvenli değil, Preferences'ta kalabilir)
                        var savedLanguage = Preferences.Get("AppLanguage", "tr");
                        KamPay.Helpers.AppLogger.DebugLog($"⚙️ Kaydedilmiş dil tercihi: {savedLanguage}");
                        
                        // Culture ayarla - MainThread'de çalıştır
                        await MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            try
                            {
                                // LocalizationResourceManager instance'ının hazır olduğundan emin ol
                                if (LocalizationResourceManager.Instance != null)
                                {
                                    LocalizationResourceManager.Instance.SetCulture(savedLanguage);
                                    KamPay.Helpers.AppLogger.DebugLog($"✓ Dil ayarlandı: {savedLanguage}");
                                }
                                else
                                {
                                    KamPay.Helpers.AppLogger.DebugLog($"⚠️ LocalizationResourceManager instance null");
                                }
                            }
                            catch (Exception ex)
                            {
                                KamPay.Helpers.AppLogger.DebugLog($"⚠️ Dil ayarlama hatası (fallback kullanılıyor): {ex.Message}");
                                KamPay.Helpers.AppLogger.DebugLog($"⚠️ StackTrace: {ex.StackTrace}");
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        KamPay.Helpers.AppLogger.DebugLog($"⚠️ Localization başlatma hatası: {ex.Message}");
                        KamPay.Helpers.AppLogger.DebugLog($"⚠️ StackTrace: {ex.StackTrace}");
                    }
                });
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"⚠️ KRITIK: App constructor hatası: {ex.Message}");
                KamPay.Helpers.AppLogger.DebugLog($"⚠️ StackTrace: {ex.StackTrace}");
                throw; // Constructor'da kritik hatalar yeniden fırlatılmalı
            }
        }

        private const string SyncfusionLicenseEnvironmentVariable = "KAMPAY_SYNCFUSION_LICENSE_KEY";
        private const string SyncfusionLicensePlaceholder = "YOUR_SYNCFUSION_LICENSE_KEY";

        private static string? GetSyncfusionLicenseKey()
        {
            try
            {
                var keyFromEnvironment = Environment.GetEnvironmentVariable(SyncfusionLicenseEnvironmentVariable);
                if (!string.IsNullOrWhiteSpace(keyFromEnvironment))
                    return keyFromEnvironment;

                var keyFromDevelopmentConfig = ReadSyncfusionLicenseFromEmbeddedConfig("KamPay.appsettings.Development.json");
                if (!string.IsNullOrWhiteSpace(keyFromDevelopmentConfig))
                    return keyFromDevelopmentConfig;

                var keyFromProductionConfig = ReadSyncfusionLicenseFromEmbeddedConfig("KamPay.appsettings.json");
                if (!string.IsNullOrWhiteSpace(keyFromProductionConfig))
                    return keyFromProductionConfig;
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"Syncfusion lisans anahtari okunamadi: {ex.Message}");
            }

            return null;
        }

        private static string? ReadSyncfusionLicenseFromEmbeddedConfig(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(resourceName);

            if (stream == null)
                return null;

            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();

            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("SyncfusionSettings", out var syncfusionSettingsElement))
                return null;

            if (!syncfusionSettingsElement.TryGetProperty("LicenseKey", out var licenseKeyElement))
                return null;

            var licenseKey = licenseKeyElement.GetString();
            if (string.IsNullOrWhiteSpace(licenseKey) || string.Equals(licenseKey, SyncfusionLicensePlaceholder, StringComparison.OrdinalIgnoreCase))
                return null;

            return licenseKey;
        }

        private static void RegisterConnectivityMonitor()
        {
            Connectivity.ConnectivityChanged += (_, e) =>
            {
                if (e.NetworkAccess == NetworkAccess.Internet)
                {
                    WeakReferenceMessenger.Default.Send(new ConnectivityRestoredMessage());
                }
            };
        }

        protected override async void OnStart()
        {
            base.OnStart();
            
            // ✅ YENİ: Otomatik giriş kontrolü (Remember Me)
            await TryAutoLoginAsync();
            
            // 🔒 GÜVENLIK FIX: Logout kontrolü kaldırıldı
            // Çünkü artık SecureStorage kullanıyoruz, Preferences'ta userId yok

            // Navigasyon sonrası geri butonu davranışı
            Shell.Current.Navigated += (s, e) =>
            {
                try
                {
                    if (Shell.Current.CurrentPage is ContentPage page)
                    {
                        Shell.SetBackButtonBehavior(page, new BackButtonBehavior
                        {
                            IsVisible = true,
                            IsEnabled = true,
                            Command = new Command(async () =>
                            {
                                try
                                {
                                    await Shell.Current.GoToAsync("..");
                                }
                                catch (Exception ex)
                                {
                                    KamPay.Helpers.AppLogger.DebugLog($"[Back Navigation Error] {ex.Message}");
                                }
                            })
                        });
                    }
                }
                catch (Exception ex)
                {
                    KamPay.Helpers.AppLogger.DebugLog($"[Navigation Handler Error] {ex.Message}");
                }
            };
        }

        protected override void OnSleep()
        {
            base.OnSleep();
            ChatViewModel.ClearOldCache(maxAgeMinutes: 30);
        }
        
        // ✅ YENİ: Otomatik giriş kontrolü
        private async Task TryAutoLoginAsync()
        {
            try
            {
                KamPay.Helpers.AppLogger.DebugLog("🔐 Otomatik giriş kontrolü yapılıyor...");

                var authService = _serviceProvider.GetService<IAuthenticationService>();
                if (authService == null)
                {
                    KamPay.Helpers.AppLogger.DebugLog("⚠️ AuthenticationService bulunamadı");
                    return;
                }

                var result = await authService.TryAutoLoginAsync();
                
                if (result.Success)
                {
                    KamPay.Helpers.AppLogger.DebugLog($"✅ Otomatik giriş başarılı: {result.Data?.Email}");
                    
                    // UserStateService'i güncelle
                    var userStateService = _serviceProvider.GetService<IUserStateService>();
                    if (userStateService != null && result.Data != null)
                    {
                        userStateService.SetUser(result.Data);
                        KamPay.Helpers.AppLogger.DebugLog("✅ UserStateService güncellendi");
                    }
                    
                    // Ana ekrana yönlendir
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        try
                        {
                            await Shell.Current.GoToAsync("//MainApp");
                            KamPay.Helpers.AppLogger.DebugLog("✅ Ana ekrana yönlendirildi");
                        }
                        catch (Exception ex)
                        {
                            KamPay.Helpers.AppLogger.DebugLog($"⚠️ Navigation hatası: {ex.Message}");
                        }
                    });
                }
                else
                {
                    KamPay.Helpers.AppLogger.DebugLog($"⏭️ Otomatik giriş yapılmadı: {result.Message}");
                }
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"❌ TryAutoLoginAsync hatası: {ex.Message}");
            }
        }
    }
}
