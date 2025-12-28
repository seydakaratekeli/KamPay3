using KamPay.ViewModels;
using KamPay.Services;
using KamPay.Resources;
using System.Globalization;

namespace KamPay
{
    public partial class App : Application
    {
        public App(AppShell appShell)
        {
            try
            {
                InitializeComponent();

                // Localization'ı daha güvenli başlat - hata olsa bile devam et
                Task.Run(async () =>
                {
                    try
                    {
                        // Biraz bekle - runtime'ın hazır olmasını sağla
                        await Task.Delay(100);
                        
                        // Kaydedilmiş dil tercihini al
                        var savedLanguage = Preferences.Get("AppLanguage", "tr");
                        System.Diagnostics.Debug.WriteLine($"⚙️ Kaydedilmiş dil tercihi: {savedLanguage}");
                        
                        // Culture ayarla
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            try
                            {
                                LocalizationResourceManager.Instance.SetCulture(savedLanguage);
                                System.Diagnostics.Debug.WriteLine($"✓ Dil ayarlandı: {savedLanguage}");
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"⚠️ Dil ayarlama hatası (fallback kullanılıyor): {ex.Message}");
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠️ Localization başlatma hatası: {ex.Message}");
                    }
                });

                // MainPage'i ata
                MainPage = appShell;
                System.Diagnostics.Debug.WriteLine("✓ MainPage (AppShell) atandı");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ KRITIK: App constructor hatası: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"⚠️ StackTrace: {ex.StackTrace}");
                throw; // Constructor'da kritik hatalar yeniden fırlatılmalı
            }
        }

        protected override void OnStart()
        {
            base.OnStart();

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
                                    System.Diagnostics.Debug.WriteLine($"[Back Navigation Error] {ex.Message}");
                                }
                            })
                        });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Navigation Handler Error] {ex.Message}");
                }
            };
        }

        protected override void OnSleep()
        {
            base.OnSleep();
            ChatViewModel.ClearOldCache(maxAgeMinutes: 30);
        }
    }
}