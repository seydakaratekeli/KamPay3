using KamPay.ViewModels;
using KamPay.Services;
using KamPay.Resources;

namespace KamPay
{
    public partial class App : Application
    {
        public App(AppShell appShell)
        {
            InitializeComponent();

            // LocalizationResourceManager.Instance.SetCulture çağrısı kaldırıldı
            // Uygulama başlatmayı engellemesini önlemek için devre dışı bırakıldı

            // MainPage'i ata
            MainPage = appShell;
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