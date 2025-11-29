using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;

namespace KamPay;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // 🔥 HATA YAKALAYICI: Kablosuz modda hataları görmek için
        AndroidEnvironment.UnhandledExceptionRaiser += (sender, args) =>
        {
            // Hatayı logla veya basitçe bir dosyaya yaz (Burada console logu göremeyeceğimiz için)
            // Ancak en azından uygulamanın sessizce kapanmasını önleyebiliriz.
            args.Handled = true; // Uygulamanın kapanmasını engellemeye çalış

            // Hata mesajını ana thread'de göster
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    if (App.Current?.MainPage != null)
                    {
                        await App.Current.MainPage.DisplayAlert("Hata Oluştu!",
                            $"Hata: {args.Exception.Message}\n\nDetay: {args.Exception.StackTrace}",
                            "Tamam");
                    }
                }
                catch { }
            });
        };
    }
}