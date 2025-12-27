// KamPay/App.xaml.cs
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

            // Varsayılan dili ayarla
            LocalizationResourceManager.Instance.SetCulture("tr");

            // MainPage'i hemen ata, ancak yönlendirmeyi Shell'e bırak
            this.MainPage = appShell;
        }

        protected override void OnStart()
        {
            base.OnStart();

            // Navigasyon sonrası geri butonu davranışı (Mevcut kodun korunmuş hali)
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
            ChatViewModel.ClearOldCache(maxAgeMinutes: 30);
        }
    }
}