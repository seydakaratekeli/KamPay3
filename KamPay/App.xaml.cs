using KamPay.ViewModels;
using KamPay.Services;
using KamPay.Resources;

namespace KamPay
{
    public partial class App : Application
    {
        private readonly IServiceProvider _serviceProvider;

        public App(AppShell appShell, IServiceProvider serviceProvider)
        {
            try
            {
                InitializeComponent();

                _serviceProvider = serviceProvider;

                // MainPage'i önce ata
                MainPage = appShell;
                KamPay.Helpers.AppLogger.DebugLog("✓ MainPage (AppShell) atandı");

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
