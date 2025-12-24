using KamPay.Views;
using KamPay.ViewModels;
using KamPay.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Dispatching;

namespace KamPay
{
    public partial class App : Application
    {
        public App(AppShell appShell)
        {
            InitializeComponent();

            // Varsayılan dili Türkçe olarak ayarla
            LocalizationResourceManager.Instance.SetCulture("tr");

            this.MainPage = appShell;

            // Navigation işlemini UI thread hazır olduktan sonra yap
            Dispatcher.Dispatch(async () =>
            {
                try
                {
                    // Küçük bir gecikme ile Shell'in tamamen yüklenmesini bekle
                    await Task.Delay(200);
                    
                    // Kullanıcı giriş yapmış mı kontrol et
                    var token = Preferences.Get("auth_token", string.Empty);
                    
                   // if (!string.IsNullOrEmpty(token))
                    {
                        // Giriş yapmış, ana uygulamaya yönlendir
                        await Shell.Current.GoToAsync("//MainApp");
                    }
                    // Giriş yapmamışsa zaten LoginPage default olarak açık
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Navigation Error: {ex.Message}");
                }
            });
        }
        
        protected override void OnStart()
        {
            base.OnStart();

            // Her sayfa yüklendiğinde otomatik geri butonu tanımla
            Shell.Current.Navigated += (s, e) =>
            {
                if (Shell.Current.CurrentPage is ContentPage page)
                {
                    Shell.SetBackButtonBehavior(page, new BackButtonBehavior
                    {
                        IsVisible = true,
                        IsEnabled = true,
                        Command = new Command(async () => await Shell.Current.GoToAsync(".."))
                    });
                }
            };
        }

        protected override void OnSleep()
        {
            base.OnSleep();

            // Cache'leri temizle
            ChatViewModel.ClearOldCache(maxAgeMinutes: 30);
        }
    }
}
