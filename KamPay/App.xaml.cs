using KamPay.Views;
using KamPay.ViewModels; 
namespace KamPay
{
    public partial class App : Application
    {
        public App(AppShell appShell)
        {
            InitializeComponent();

            MainPage = appShell;

            // Kullanıcı giriş yapmış mı kontrol et
            var token = Preferences.Get("auth_token", string.Empty);
            
            if (string.IsNullOrEmpty(token))
            {
                // Giriş yapmamış, LoginPage'e yönlendir
                Shell.Current.GoToAsync("//LoginPage");
            }
            else
            {
                // Giriş yapmış, ana uygulamaya yönlendir
                Shell.Current.GoToAsync("//MainApp");
            }

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
