using Microsoft.Maui.Controls; // Application sınıfı için gerekli using
using KamPay.ViewModels;
using KamPay.Services;

namespace KamPay
{
    public partial class App : Application
    {
        public App(AppShell appShell)
        {
            InitializeComponent();

            LocalizationResourceManager.Instance.SetCulture("tr");

            this.MainPage = appShell;

            Dispatcher.Dispatch(async () =>
            {
                try
                {
                    await Task.Delay(200);

                    // DÜZELTME: "auth_token" yerine "current_user_id" kontrol ediliyor
                    var userId = Preferences.Get("current_user_id", string.Empty);

                    // userId boş değilse giriş yapmış demektir
                    if (!string.IsNullOrEmpty(userId))
                    {
                        await Shell.Current.GoToAsync("//MainApp");
                    }
                    // Boşsa hiçbir şey yapmaya gerek yok, LoginPage zaten varsayılan olarak açılacaktır.
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
