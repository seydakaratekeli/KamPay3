using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;

namespace KamPay;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        try
        {
            // ⚠️ SADECE DEBUG İÇİN: SSL sertifika doğrulamasını devre dışı bırak
#if DEBUG
#pragma warning disable SYSLIB0014 // Type or member is obsolete
            System.Net.ServicePointManager.ServerCertificateValidationCallback = 
                (sender, cert, chain, sslPolicyErrors) => true;
#pragma warning restore SYSLIB0014 // Type or member is obsolete
#endif

            base.OnCreate(savedInstanceState);

            // HATA YAKALAYICI: Kablosuz modda hataları görmek için
            AndroidEnvironment.UnhandledExceptionRaiser += (sender, args) =>
            {
                System.Diagnostics.Debug.WriteLine($"[FATAL ERROR] Unhandled Exception: {args.Exception.Message}");
                System.Diagnostics.Debug.WriteLine($"[FATAL ERROR] StackTrace: {args.Exception.StackTrace}");
                
                // Hatayı logla
                Android.Util.Log.Error("KamPay", $"Unhandled Exception: {args.Exception}");
                
                args.Handled = true; // Uygulamanın kapanmasını engellemeye çalış

                // Hata mesajını ana thread'de göster
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    try
                    {
#pragma warning disable CS0618 // Type or member is obsolete
                        if (App.Current?.MainPage != null)
                        {
                            await App.Current.MainPage.DisplayAlert("Hata Oluştu!",
                                $"Hata: {args.Exception.Message}\n\nDetay: {args.Exception.InnerException?.Message ?? "Yok"}",
                                "Tamam");
                        }
#pragma warning restore CS0618 // Type or member is obsolete
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ERROR] Could not show alert: {ex.Message}");
                    }
                });
            };

            // TaskScheduler hataları için
            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                System.Diagnostics.Debug.WriteLine($"[TASK ERROR] Unobserved Exception: {args.Exception.Message}");
                Android.Util.Log.Error("KamPay", $"Unobserved Task Exception: {args.Exception}");
                args.SetObserved();
            };

            // AppDomain hataları için
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var exception = args.ExceptionObject as Exception;
                System.Diagnostics.Debug.WriteLine($"[DOMAIN ERROR] Unhandled Exception: {exception?.Message}");
                Android.Util.Log.Error("KamPay", $"AppDomain Exception: {exception}");
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CRITICAL] OnCreate Error: {ex.Message}");
            Android.Util.Log.Error("KamPay", $"OnCreate Exception: {ex}");
            throw;
        }
    }
}
