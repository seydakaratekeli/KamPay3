using KamPay.ViewModels;
using KamPay.Services;
using KamPay.Resources;
using System.Globalization;

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

        protected override async void OnStart()
        {
            base.OnStart();
            
            // ✅ YENİ: Otomatik giriş kontrolü (Remember Me)
            await TryAutoLoginAsync();
            
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
        
        // ✅ YENİ: Otomatik giriş kontrolü
        private async Task TryAutoLoginAsync()
        {
            try
            {
                Console.WriteLine("🔐 Otomatik giriş kontrolü yapılıyor...");

                // ✅ DEBUG: Preferences'ları kontrol et
                var rememberMeCheck = Preferences.Get("remember_me", false);
                var userIdCheck = Preferences.Get("current_user_id", string.Empty);
                var tokenCheck = Preferences.Get("firebase_token", string.Empty);
                
                Console.WriteLine($"📋 Preferences Durumu:");
                Console.WriteLine($"  - RememberMe: {rememberMeCheck}");
                Console.WriteLine($"  - UserId: {(string.IsNullOrEmpty(userIdCheck) ? "YOK" : "VAR")}");
                Console.WriteLine($"  - Token: {(string.IsNullOrEmpty(tokenCheck) ? "YOK" : "VAR")}");

                var authService = _serviceProvider.GetService<IAuthenticationService>();
                if (authService == null)
                {
                    Console.WriteLine("⚠️ AuthenticationService bulunamadı");
                    return;
                }

                var result = await authService.TryAutoLoginAsync();
                
                if (result.Success)
                {
                    Console.WriteLine($"✅ Otomatik giriş başarılı: {result.Data?.Email}");
                    
                    // UserStateService'i güncelle
                    var userStateService = _serviceProvider.GetService<IUserStateService>();
                    if (userStateService != null && result.Data != null)
                    {
                        userStateService.SetUser(result.Data);
                        Console.WriteLine("✅ UserStateService güncellendi");
                    }
                    
                    // Ana ekrana yönlendir
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        try
                        {
                            await Shell.Current.GoToAsync("//MainApp");
                            Console.WriteLine("✅ Ana ekrana yönlendirildi");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"⚠️ Navigation hatası: {ex.Message}");
                        }
                    });
                }
                else
                {
                    Console.WriteLine($"⏭️ Otomatik giriş yapılmadı: {result.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ TryAutoLoginAsync hatası: {ex.Message}");
            }
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
                        Preferences.Remove("firebase_token");
                        Preferences.Remove("remember_me");
                        Preferences.Remove("token_expiry");
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