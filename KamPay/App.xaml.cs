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

                // MainPage'i önce ata
                MainPage = appShell;
                System.Diagnostics.Debug.WriteLine("✓ MainPage (AppShell) atandı");

                // Localization'ı daha güvenli başlat - hata olsa bile devam et
                Task.Run(async () =>
                {
                    try
                    {
                        // LocalizationResourceManager'ın başlatılmasını bekle
                        await Task.Delay(200);
                        
                        // Kaydedilmiş dil tercihini al
                        var savedLanguage = Preferences.Get("AppLanguage", "tr");
                        System.Diagnostics.Debug.WriteLine($"⚙️ Kaydedilmiş dil tercihi: {savedLanguage}");
                        
                        // Culture ayarla - MainThread'de çalıştır
                        await MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            try
                            {
                                // LocalizationResourceManager instance'ının hazır olduğundan emin ol
                                if (LocalizationResourceManager.Instance != null)
                                {
                                    LocalizationResourceManager.Instance.SetCulture(savedLanguage);
                                    System.Diagnostics.Debug.WriteLine($"✓ Dil ayarlandı: {savedLanguage}");
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"⚠️ LocalizationResourceManager instance null");
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"⚠️ Dil ayarlama hatası (fallback kullanılıyor): {ex.Message}");
                                System.Diagnostics.Debug.WriteLine($"⚠️ StackTrace: {ex.StackTrace}");
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠️ Localization başlatma hatası: {ex.Message}");
                        System.Diagnostics.Debug.WriteLine($"⚠️ StackTrace: {ex.StackTrace}");
                    }
                });
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
            
            // ✅ CRITICAL FIX: Uygulama her başladığında logout kontrolü yap
            CheckLogoutStatus();

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
        
        // ✅ CRITICAL FIX: Logout kontrolü
        private void CheckLogoutStatus()
        {
            try
            {
                var userId = Preferences.Get("current_user_id", string.Empty);
                
                // Eğer userId varsa ama UserStateService'de kullanıcı yoksa, logout yapılmış demektir
                if (!string.IsNullOrEmpty(userId))
                {
                    var userStateService = MainPage?.Handler?.MauiContext?.Services.GetService<IUserStateService>();
                    if (userStateService?.CurrentUser == null)
                    {
                        Console.WriteLine("⚠️ Logout tespit edildi - Preferences temizleniyor");
                        Preferences.Remove("current_user_id");
                        Preferences.Remove("current_user_email");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ CheckLogoutStatus hatası: {ex.Message}");
            }
        }
    }
}