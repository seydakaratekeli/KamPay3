using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KamPay.Models;
using KamPay.Services;
using KamPay.Views;
using Microsoft.Maui.Controls;

namespace KamPay.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        private readonly IAuthenticationService _authService;

        // Localization helper
        private static LocalizationResourceManager Res => LocalizationResourceManager.Instance;

        [ObservableProperty] private string email;
        [ObservableProperty] private string password;
        [ObservableProperty] private bool rememberMe;
        [ObservableProperty] private bool isLoading;
        [ObservableProperty] private string errorMessage;

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
            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;

                var request = new LoginRequest
                {
                    Email = Email,
                    Password = Password,
                    RememberMe = RememberMe
                };

                var result = await _authService.LoginAsync(request);

                if (result.Success)
                {
                    await Application.Current.MainPage.DisplayAlert(Res["Welcome"], result.Message ?? Res["LoginSuccess"], Res["Ok"]);

                    // Ana sayfaya yönlendir ()
                    await Shell.Current.GoToAsync("//MainApp");
                    ClearCredentials();
                }
                else
                {
                    if (result.Errors != null && result.Errors.Any())
                        ErrorMessage = string.Join("\n", result.Errors);
                    else
                        ErrorMessage = result.Message ?? Res["LoginFailed"];
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"{Res["UnexpectedError"]}: {ex.Message}";
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
