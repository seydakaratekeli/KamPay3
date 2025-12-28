using KamPay.ViewModels;
using KamPay.Services;
using KamPay.Resources;

namespace KamPay
{
    public partial class App : Application
    {
        public App(AppShell appShell)
        {
            try
            {
                InitializeComponent();

                // Varsayılan dili ayarla - hata olsa bile devam et
                try
                {
                    LocalizationResourceManager.Instance.SetCulture("tr");
                    System.Diagnostics.Debug.WriteLine("✓ Varsayılan dil (Türkçe) ayarlandı");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ Dil ayarlama hatası (devam ediliyor): {ex.Message}");
                }

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