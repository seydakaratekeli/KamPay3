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
            // âš ï¸ SADECE DEBUG Ä°Ã‡Ä°N: SSL sertifika doÄŸrulamasÄ±nÄ± devre dÄ±ÅŸÄ± bÄ±rak
#if DEBUG
#pragma warning disable SYSLIB0014 // Type or member is obsolete
            System.Net.ServicePointManager.ServerCertificateValidationCallback = 
                (sender, cert, chain, sslPolicyErrors) => true;
#pragma warning restore SYSLIB0014 // Type or member is obsolete
#endif

            base.OnCreate(savedInstanceState);

            // HATA YAKALAYICI: Kablosuz modda hatalarÄ± gÃ¶rmek iÃ§in
            AndroidEnvironment.UnhandledExceptionRaiser += (sender, args) =>
            {
                KamPay.Helpers.AppLogger.DebugLog($"[FATAL ERROR] Unhandled Exception: {args.Exception.Message}");
                KamPay.Helpers.AppLogger.DebugLog($"[FATAL ERROR] StackTrace: {args.Exception.StackTrace}");
                
                // HatayÄ± logla
                Android.Util.Log.Error("KamPay", $"Unhandled Exception: {args.Exception}");
                
                args.Handled = true; // UygulamanÄ±n kapanmasÄ±nÄ± engellemeye Ã§alÄ±ÅŸ

                // Hata mesajÄ±nÄ± ana thread'de gÃ¶ster
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    try
                    {
#pragma warning disable CS0618 // Type or member is obsolete
                        if (App.Current?.MainPage != null)
                        {
                            await App.Current.MainPage.DisplayAlert("Hata OluÅŸtu!",
                                $"Hata: {args.Exception.Message}\n\nDetay: {args.Exception.InnerException?.Message ?? "Yok"}",
                                "Tamam");
                        }
#pragma warning restore CS0618 // Type or member is obsolete
                    }
                    catch (Exception ex)
                    {
                        KamPay.Helpers.AppLogger.DebugLog($"[ERROR] Could not show alert: {ex.Message}");
                    }
                });
            };

            // TaskScheduler hatalarÄ± iÃ§in
            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                KamPay.Helpers.AppLogger.DebugLog($"[TASK ERROR] Unobserved Exception: {args.Exception.Message}");
                Android.Util.Log.Error("KamPay", $"Unobserved Task Exception: {args.Exception}");
                args.SetObserved();
            };

            // AppDomain hatalarÄ± iÃ§in
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var exception = args.ExceptionObject as Exception;
                KamPay.Helpers.AppLogger.DebugLog($"[DOMAIN ERROR] Unhandled Exception: {exception?.Message}");
                Android.Util.Log.Error("KamPay", $"AppDomain Exception: {exception}");
            };
        }
        catch (Exception ex)
        {
            KamPay.Helpers.AppLogger.DebugLog($"[CRITICAL] OnCreate Error: {ex.Message}");
            Android.Util.Log.Error("KamPay", $"OnCreate Exception: {ex}");
            throw;
        }
    }
}

