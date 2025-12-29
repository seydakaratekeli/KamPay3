using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KamPay.Models;
using KamPay.Services;
using KamPay.Helpers;

namespace KamPay.ViewModels
{
    public partial class RegisterViewModel : ObservableObject
    {
        private readonly IAuthenticationService _authService;
        private readonly IUserProfileService _userProfileService;
        private System.Timers.Timer? _countdownTimer;

        [ObservableProperty]
        private string firstName = string.Empty;

        [ObservableProperty]
        private string lastName = string.Empty;

        [ObservableProperty]
        private string email = string.Empty;

        [ObservableProperty]
        private string password = string.Empty;

        [ObservableProperty]
        private string passwordConfirm = string.Empty;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private string errorMessage = string.Empty;

        [ObservableProperty]
        private bool isVerificationStep;

        [ObservableProperty]
        private string verificationCode = string.Empty;

        //  CRITICAL FIX: ShowVerificationSection property eklendi (XAML binding için)
        [ObservableProperty]
        private bool showVerificationSection;

        // ✅ YENİ: Zamanlayıcı için property'ler
        [ObservableProperty]
        private string remainingTime = "15:00";

        [ObservableProperty]
        private bool isCodeExpired = false;

        private DateTime _codeExpiryTime;

        public RegisterViewModel(IAuthenticationService authService, IUserProfileService userProfileService)
        {
            _authService = authService;
            _userProfileService = userProfileService;
        }

        [RelayCommand]
        private async Task RegisterAsync()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;

                var request = new RegisterRequest
                {
                    // ✅ FIX: Türkçe karakter desteği için SanitizeName kullan
                    FirstName = InputSanitizer.SanitizeName(FirstName),
                    LastName = InputSanitizer.SanitizeName(LastName),
                    Email = (Email ?? string.Empty).Trim().ToLower(),
                    Password = Password,
                    PasswordConfirm = PasswordConfirm
                };

                var result = await _authService.RegisterAsync(request);

                if (result.Success)
                {
                    //  İKİ PROPERTY'Yİ DE SET ET
                    IsVerificationStep = true;
                    ShowVerificationSection = true;
                    VerificationCode = string.Empty;

                    // ✅ YENİ: Zamanlayıcıyı başlat (15 dakika) - hataları yakala
                    try
                    {
                        StartCountdownTimer(15);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ StartCountdownTimer hatası: {ex.Message}");
                    }

                    // Console'a da log bas
                    Console.WriteLine("✅ Kayıt başarılı! Doğrulama ekranına geçiliyor...");

                    // Safe DisplayAlert: Application.Current veya MainPage null olabilir
                    var page = Application.Current?.MainPage;
                    if (page != null)
                    {
                        try
                        {
                            await page.DisplayAlert("Başarılı", result.Message ?? "Kayıt başarılı. Lütfen e-postanıza gönderilen doğrulama kodunu girin.", "Tamam");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"⚠️ DisplayAlert hatası: {ex.Message}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("⚠️ DisplayAlert atlanıyor: Application.Current.MainPage null");
                    }
                }
                else
                {
                    // ✅ FIX: Tüm hataları detaylı şekilde göster
                    if (result.Errors != null && result.Errors.Any())
                    {
                        // Hataları madde işareti ile listele
                        var errorList = new List<string> { result.Message ?? "Kayıt bilgilerinde hatalar var:" };
                        errorList.AddRange(result.Errors.Select(e => $"• {e}"));
                        ErrorMessage = string.Join("\n", errorList);
                    }
                    else
                    {
                        ErrorMessage = result.Message ?? "Kayıt yapılamadı.";
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ RegisterAsync hatası: {ex.Message}");
                ErrorMessage = $"Beklenmeyen hata: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task VerifyEmailAsync()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;

                var vreq = new VerificationRequest
                {
                    Email = Email,
                    VerificationCode = VerificationCode
                };

                var result = await _authService.VerifyEmailAsync(vreq);

                if (result.Success)
                {
                    // ✅ Zamanlayıcıyı durdur
                    StopCountdownTimer();

                    // ✅ CRITICAL FIX: Profil oluşturma işlemi artık FirebaseAuthService.VerifyEmailAsync içinde yapılıyor
                    // Bu yüzden burada tekrar çağırmıyoruz
                    
                    var loginRequest = new LoginRequest { Email = Email, Password = Password, RememberMe = true };
                    var loginResult = await _authService.LoginAsync(loginRequest);

                    if (loginResult.Success)
                    {
                        // Profil zaten VerifyEmailAsync içinde oluşturuldu, doğrudan ana sayfaya yönlendir
                        await Shell.Current.GoToAsync("//MainApp");
                    }
                    else
                    {
                        await Application.Current!.MainPage!.DisplayAlert("Doğrulandı", "E-postanız doğrulandı. Lütfen giriş yapın.", "Tamam");
                        await Shell.Current.GoToAsync("//LoginPage");
                    }
                }
                else
                {
                    ErrorMessage = result.Message ?? "Doğrulama başarısız.";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ VerifyEmailAsync hatası: {ex.Message}");
                ErrorMessage = $"Beklenmeyen hata: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task ResendVerificationAsync()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;

                if (string.IsNullOrWhiteSpace(Email))
                {
                    ErrorMessage = "E-posta alanı boş olamaz.";
                    return;
                }

                // Rate Limiting Kontrolü: Saatte en fazla 3 deneme
                var limitCheck = RateLimiters.PasswordReset.CheckLimit(Email);
                if (!limitCheck.IsAllowed)
                {
                    ErrorMessage = limitCheck.Message;
                    return;
                }

                var result = await _authService.SendVerificationCodeAsync(Email);

                if (result.Success)
                {
                    // ✅ YENİ: Zamanlayıcıyı yeniden başlat
                    StartCountdownTimer(15);
                    IsCodeExpired = false;

                    await Application.Current!.MainPage!.DisplayAlert("Başarılı", result.Message ?? "Doğrulama kodu yeniden gönderildi.", "Tamam");
                }
                else
                {
                    ErrorMessage = result.Message ?? "Kod gönderilemedi.";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ResendVerificationAsync hatası: {ex.Message}");
                ErrorMessage = $"Beklenmeyen hata: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task CancelVerificationAsync()
        {
            // ✅ Zamanlayıcıyı durdur
            StopCountdownTimer();

            IsVerificationStep = false;
            ShowVerificationSection = false;
            VerificationCode = string.Empty;
            await Task.CompletedTask;
        }

        [RelayCommand]
        private async Task GoToLoginAsync()
        {
            // ✅ Zamanlayıcıyı durdur
            StopCountdownTimer();
            
            await Shell.Current.GoToAsync("..");
        }

        // ✅ YENİ: Zamanlayıcı metodları
        private void StartCountdownTimer(int minutes)
        {
            // Önceki zamanlayıcıyı durdur
            StopCountdownTimer();

            // Bitiş zamanını hesapla
            _codeExpiryTime = DateTime.Now.AddMinutes(minutes);
            IsCodeExpired = false;

            // Zamanlayıcıyı oluştur (her saniye güncelle)
            _countdownTimer = new System.Timers.Timer(1000);
            _countdownTimer.Elapsed += (sender, e) =>
            {
                try
                {
                    var remaining = _codeExpiryTime - DateTime.Now;

                    if (remaining.TotalSeconds <= 0)
                    {
                        // Süre doldu
                        if (Application.Current != null)
                        {
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                try
                                {
                                    RemainingTime = "00:00";
                                    IsCodeExpired = true;
                                    StopCountdownTimer();
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"⚠️ Timer UI update hatası: {ex.Message}");
                                }
                            });
                        }
                        else
                        {
                            // Güvenli mod: direkt durdur
                            StopCountdownTimer();
                        }
                    }
                    else
                    {
                        if (Application.Current != null)
                        {
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                try
                                {
                                    RemainingTime = $"{(int)remaining.TotalMinutes:D2}:{remaining.Seconds:D2}";
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"⚠️ Timer UI update hatası: {ex.Message}");
                                }
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Timer callback hatası: {ex.Message}");
                    // Hata durumunda timer'ı güvenli şekilde durdur
                    try { StopCountdownTimer(); } catch { }
                }
            };

            _countdownTimer.Start();
        }

        private void StopCountdownTimer()
        {
            if (_countdownTimer != null)
            {
                try
                {
                    _countdownTimer.Stop();
                    _countdownTimer.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ StopCountdownTimer hatası: {ex.Message}");
                }
                finally
                {
                    _countdownTimer = null;
                }
            }
        }
    }
}