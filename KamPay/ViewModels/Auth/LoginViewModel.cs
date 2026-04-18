using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KamPay.Models;
using KamPay.Services;
using KamPay.Views;
using Microsoft.Maui.Controls;
using KamPay.Helpers;
using KamPay.Services.Auth;
using KamPay.Services.Products;

namespace KamPay.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        private readonly IAuthenticationService _authService;

        // Localization helper
        private static LocalizationResourceManager Res => LocalizationResourceManager.Instance;

        [ObservableProperty]
        private string email = string.Empty;

        [ObservableProperty]
        private string password = string.Empty;

        [ObservableProperty] private bool rememberMe;
        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private string errorMessage = string.Empty;

        public LoginViewModel(IAuthenticationService authService)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        }
        
        public void ClearCredentials()
        {
            Email = string.Empty;
            Password = string.Empty;
            ErrorMessage = string.Empty;
        }
        
        [RelayCommand]
        private async Task LoginAsync()
        {
            // AÄ KONTROLÃœ: Ä°ÅŸlem baÅŸlamadan Ã¶nce interneti kontrol et
            if (!NetworkHelper.HasInternetConnection())
            {
                ErrorMessage = "Ä°nternet baÄŸlantÄ±sÄ± yok. LÃ¼tfen baÄŸlantÄ±nÄ±zÄ± kontrol edin.";
                return;
            }

            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;

                // Rate Limiting KontrolÃ¼: 15 dakikada en fazla 3 deneme
                var limitCheck = KamPay.Helpers.SecureRateLimiters.Login.CheckRequest(Email);
                if (!limitCheck.IsAllowed)
                {
                    ErrorMessage = limitCheck.Message; // "Ã‡ok fazla deneme yaptÄ±nÄ±z... X dakika bekleyin"
                    return;
                }

                var request = new LoginRequest
                {
                    Email = Email,
                    Password = Password,
                    RememberMe = RememberMe
                };

                var result = await _authService.LoginAsync(request);

                if (result.Success)
                {
                    // GiriÅŸ baÅŸarÄ±lÄ±ysa deneme sayacÄ±nÄ± sÄ±fÄ±rla
                    KamPay.Helpers.SecureRateLimiters.Login.RemoveBan(Email);
                    
                    // âœ… CRITICAL FIX: Yeni kullanÄ±cÄ± giriÅŸinde tÃ¼m static cache'leri temizle
                    KamPay.Helpers.AppLogger.DebugLog("âœ… Yeni kullanÄ±cÄ± giriÅŸi - tÃ¼m cache'ler temizleniyor...");
                    
                    // ChatViewModel cache'ini temizle
                    ChatViewModel.ClearCache();
                    KamPay.Helpers.AppLogger.DebugLog("âœ… ChatViewModel cache temizlendi");
                    
                    // ProductCacheService'i temizle
                    try
                    {
                        var productCacheService = Application.Current?.Handler?.MauiContext?.Services.GetService<IProductCacheService>();
                        if (productCacheService != null)
                        {
                            await productCacheService.InvalidateCacheAsync();
                            KamPay.Helpers.AppLogger.DebugLog("âœ… ProductCache temizlendi");
                        }
                    }
                    catch (Exception cacheEx)
                    {
                        KamPay.Helpers.AppLogger.DebugLog($"âš ï¸ ProductCache temizleme hatasÄ±: {cacheEx.Message}");
                    }

                    await Application.Current.MainPage.DisplayAlert(Res["Welcome"], result.Message ?? Res["LoginSuccess"], Res["Ok"]);
                    await Shell.Current.GoToAsync("//MainApp");
                    ClearCredentials();
                }
                else
                {
                    // âœ… FIX: TÃ¼m hatalarÄ± detaylÄ± ÅŸekilde gÃ¶ster
                    if (result.Errors != null && result.Errors.Any())
                    {
                        // HatalarÄ± madde iÅŸareti ile listele
                        var errorList = new List<string> { result.Message ?? "GiriÅŸ bilgilerinde hatalar var:" };
                        errorList.AddRange(result.Errors.Select(e => $"â€¢ {e}"));
                        ErrorMessage = string.Join("\n", errorList);
                    }
                    else
                    {
                        ErrorMessage = result.Message ?? Res["LoginFailed"];
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = NetworkHelper.GetUserFriendlyErrorMessage(ex);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task GoToRegisterAsync()
        {
            // YÄ±ÄŸÄ±nÄ± sÄ±fÄ±rlama
            await Shell.Current.GoToAsync(nameof(RegisterPage)); 
        }

        [RelayCommand]
        private async Task ForgotPasswordAsync()
        {
            try
            {
                // E-posta adresi sor
                var email = await Application.Current.MainPage.DisplayPromptAsync(
                    "Åifremi Unuttum",
                    "E-posta adresinizi girin:",
                    "GÃ¶nder",
                    "Ä°ptal",
                    placeholder: "ornek@bartin.edu.tr",
                    keyboard: Keyboard.Email
                );

                if (string.IsNullOrWhiteSpace(email))
                    return;

                IsLoading = true;
                ErrorMessage = string.Empty;

                // ğŸ”¥ Firebase native ÅŸifre sÄ±fÄ±rlama linki gÃ¶nder
                var result = await _authService.SendPasswordResetEmailAsync(email);

                if (result.Success)
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "BaÅŸarÄ±lÄ±! ğŸ“§",
                        "Åifre sÄ±fÄ±rlama linki e-postanÄ±za gÃ¶nderildi.\n\n" +
                        "LÃ¼tfen e-postanÄ±zÄ± kontrol edin ve linke tÄ±klayarak yeni ÅŸifrenizi belirleyin.\n\n" +
                        "Link 1 saat geÃ§erlidir.",
                        "Tamam"
                    );

                    // E-posta alanÄ±nÄ± doldur (kullanÄ±cÄ± sÄ±fÄ±rladÄ±ktan sonra giriÅŸ yapabilsin)
                    Email = email;
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "Hata",
                        result.Message ?? "Åifre sÄ±fÄ±rlama linki gÃ¶nderilemedi.",
                        "Tamam"
                    );
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert(
                    "Hata",
                    $"Bir hata oluÅŸtu: {ex.Message}",
                    "Tamam"
                );
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}

