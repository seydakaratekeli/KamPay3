using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KamPay.Models;
using KamPay.Services;

namespace KamPay.ViewModels
{
    public partial class RegisterViewModel : ObservableObject
    {
        private readonly IAuthenticationService _authService;
        // YENÝ: IUserProfileService'i ekledik
        private readonly IUserProfileService _userProfileService;

        [ObservableProperty]
        private string firstName;

        [ObservableProperty]
        private string lastName;

        [ObservableProperty]
        private string email;

        [ObservableProperty]
        private string password;

        [ObservableProperty]
        private string passwordConfirm;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private string errorMessage;

        [ObservableProperty]
        private bool showVerificationSection;

        [ObservableProperty]
        private string verificationCode;

        // YENÝ: Constructor'ý IUserProfileService alacak þekilde güncelledik
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
                    FirstName = FirstName,
                    LastName = LastName,
                    Email = Email,
                    Password = Password,
                    PasswordConfirm = PasswordConfirm
                };

                // ÖNEMLÝ: FirebaseAuthService'de RegisterAsync'in adý RegisterUserAsync olabilir.
                // Projendeki isme göre burayý RegisterUserAsync olarak deðiþtirmen gerekebilir.
                var result = await _authService.RegisterAsync(request);

                if (result.Success)
                {
                    ShowVerificationSection = true;
                    VerificationCode = string.Empty;
                    await Application.Current.MainPage.DisplayAlert("Baþarýlý", result.Message ?? "Kayýt baþarýlý. Lütfen e-postanýza gönderilen doðrulama kodunu girin.", "Tamam");
                }
                else
                {
                    ErrorMessage = result.Message ?? "Kayýt yapýlamadý.";
                }
            }
            catch (Exception ex)
            {
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
                    // E-posta doðrulandý, þimdi giriþ yapalým
                    var loginRequest = new LoginRequest { Email = Email, Password = Password, RememberMe = true };
                    var loginResult = await _authService.LoginAsync(loginRequest);

                    if (loginResult.Success)
                    {
                        // GÝRÝÞ BAÞARILI, ÞÝMDÝ PROFÝLÝ OLUÞTURALIM
                        // Mevcut kullanýcýyý alarak UserId'ye eriþiyoruz.
                        var currentUser = await _authService.GetCurrentUserAsync();
                        if (currentUser != null && !string.IsNullOrEmpty(currentUser.UserId))
                        {
                            // Ad ve soyadý birleþtirerek tam isim oluþturuyoruz
                            string fullName = $"{FirstName} {LastName}".Trim();
                            await _userProfileService.CreateUserProfileAsync(currentUser.UserId, fullName, Email);
                        }

                        // Her þey tamam, ana sayfaya yönlendir
                        await Shell.Current.GoToAsync("//MainApp");
                    }
                    else
                    {
                        // Doðrulama baþarýlý ama giriþ baþarýsýzsa, kullanýcýyý login sayfasýna gönderelim
                        await Application.Current.MainPage.DisplayAlert("Doðrulandý", "E-postanýz doðrulandý. Lütfen giriþ yapýn.", "Tamam");
                        await Shell.Current.GoToAsync("//LoginPage");
                    }
                }
                else
                {
                    ErrorMessage = result.Message ?? "Doðrulama baþarýsýz.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Beklenmeyen hata: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ... (Diðer komutlar ayný kalacak: ResendVerificationAsync, CancelVerificationAsync, GoToLoginAsync) ...
        #region Other Commands
        [RelayCommand]
        private async Task ResendVerificationAsync()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;

                if (string.IsNullOrWhiteSpace(Email))
                {
                    ErrorMessage = "E-posta alaný boþ olamaz.";
                    return;
                }

                var result = await _authService.SendVerificationCodeAsync(Email);

                if (result.Success)
                {
                    await Application.Current.MainPage.DisplayAlert("Baþarýlý", result.Message ?? "Doðrulama kodu yeniden gönderildi.", "Tamam");
                }
                else
                {
                    ErrorMessage = result.Message ?? "Kod gönderilemedi.";
                }
            }
            catch (Exception ex)
            {
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
            ShowVerificationSection = false;
            VerificationCode = string.Empty;
            await Task.CompletedTask;
        }

        [RelayCommand]
        private async Task GoToLoginAsync()
        {
            await Shell.Current.GoToAsync("..");
        }
        #endregion
    }
}