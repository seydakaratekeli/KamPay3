using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KamPay.Models;
using KamPay.Services;
using KamPay.Helpers;
using KamPay.Services.Auth;

namespace KamPay.ViewModels
{
    public partial class RegisterViewModel : ObservableObject
    {
        private readonly IAuthenticationService _authService;
        private readonly IUserProfileService _userProfileService;

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
                    FirstName = InputSanitizer.SanitizeName(FirstName),
                    LastName = InputSanitizer.SanitizeName(LastName),
                    Email = (Email ?? string.Empty).Trim().ToLower(),
                    Password = Password,
                    PasswordConfirm = PasswordConfirm
                };

                var result = await _authService.RegisterAsync(request);

                if (result.Success)
                {
                    // 🔥 YENİ: Artık manuel doğrulama ekranına geçmiyoruz
                    // Firebase otomatik link gönderdi, kullanıcıyı bilgilendir
                    
                    await Application.Current.MainPage.DisplayAlert(
                        "Kayıt Başarılı! 📧",
                        "E-postanıza bir doğrulama linki gönderildi.\n\n" +
                        "Lütfen e-postanızı kontrol edin ve linke tıklayarak hesabınızı doğrulayın.\n\n" +
                        "Doğruladıktan sonra giriş yapabilirsiniz.",
                        "Tamam"
                    );

                    // Login sayfasına yönlendir
                    await Shell.Current.GoToAsync("//LoginPage");
                }
                else
                {
                    if (result.Errors != null && result.Errors.Any())
                    {
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
                KamPay.Helpers.AppLogger.DebugLog($"❌ RegisterAsync hatası: {ex.Message}");
                ErrorMessage = $"Beklenmeyen hata: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        // 🔥 ARTIK MANUEL DOĞRULAMA YOK - Bu metodları kaldırıyoruz veya basitleştiriyoruz

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

                var result = await _authService.SendVerificationCodeAsync(Email);

                if (result.Success)
                {
                    await Application.Current!.MainPage!.DisplayAlert(
                        "Başarılı",
                        "Doğrulama linki yeniden gönderildi. Lütfen e-postanızı kontrol edin.",
                        "Tamam"
                    );
                }
                else
                {
                    ErrorMessage = result.Message ?? "Link gönderilemedi.";
                }
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"❌ ResendVerificationAsync hatası: {ex.Message}");
                ErrorMessage = $"Beklenmeyen hata: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task GoToLoginAsync()
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}
