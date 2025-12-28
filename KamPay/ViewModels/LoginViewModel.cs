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
            // AĞ KONTROLÜ: İşlem başlamadan önce interneti kontrol et
            if (!NetworkHelper.HasInternetConnection())
            {
                ErrorMessage = "İnternet bağlantısı yok. Lütfen bağlantınızı kontrol edin.";
                return;
            }

            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;

                // Rate Limiting Kontrolü: 15 dakikada en fazla 5 deneme
                var limitCheck = RateLimiters.Login.CheckLimit(Email);
                if (!limitCheck.IsAllowed)
                {
                    ErrorMessage = limitCheck.Message; // "Çok fazla deneme yaptınız... X dakika bekleyin"
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
                    // Giriş başarılıysa deneme sayacını sıfırla
                    RateLimiters.Login.Reset(Email);
                    
                    // ✅ CRITICAL FIX: Yeni kullanıcı girişinde tüm static cache'leri temizle
                    Console.WriteLine("✅ Yeni kullanıcı girişi - tüm cache'ler temizleniyor...");
                    
                    // ChatViewModel cache'ini temizle
                    ChatViewModel.ClearCache();
                    Console.WriteLine("✅ ChatViewModel cache temizlendi");
                    
                    // ProductCacheService'i temizle
                    try
                    {
                        var productCacheService = Application.Current?.Handler?.MauiContext?.Services.GetService<IProductCacheService>();
                        if (productCacheService != null)
                        {
                            await productCacheService.InvalidateCacheAsync();
                            Console.WriteLine("✅ ProductCache temizlendi");
                        }
                    }
                    catch (Exception cacheEx)
                    {
                        Console.WriteLine($"⚠️ ProductCache temizleme hatası: {cacheEx.Message}");
                    }

                    await Application.Current.MainPage.DisplayAlert(Res["Welcome"], result.Message ?? Res["LoginSuccess"], Res["Ok"]);
                    await Shell.Current.GoToAsync("//MainApp");
                    ClearCredentials();
                }
                else
                {
                    // ✅ FIX: Tüm hataları detaylı şekilde göster
                    if (result.Errors != null && result.Errors.Any())
                    {
                        // Hataları madde işareti ile listele
                        var errorList = new List<string> { result.Message ?? "Giriş bilgilerinde hatalar var:" };
                        errorList.AddRange(result.Errors.Select(e => $"• {e}"));
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
            // Yığını sıfırlama
            await Shell.Current.GoToAsync(nameof(RegisterPage)); 
        }
    }
}
