// KamPay/ViewModels/MainViewModel.cs

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;
using KamPay.Services.Auth; // Eklendi

namespace KamPay.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IAuthenticationService _authService; // Eklendi

        // Constructor eklendi
        public MainViewModel(IAuthenticationService authService)
        {
            _authService = authService;
        }

        [RelayCommand]
        private async Task NavigateProductListAsync()
        {
            await Shell.Current.GoToAsync("///ProductListPage");
        }

        [RelayCommand]
        private async Task NavigateFavoritesAsync()
        {
            await Shell.Current.GoToAsync("///FavoritesPage");
        }

        [RelayCommand]
        private async Task NavigateMessagesAsync()
        {
            await Shell.Current.GoToAsync("///MessagesPage");
        }

        [RelayCommand]
        private async Task NavigateProfileAsync()
        {
            await Shell.Current.GoToAsync("///ProfilePage");
        }

        [RelayCommand]
        private async Task LogoutAsync()
        {
            // 1. Servis seviyesinde oturumu kapat (Preferences ve State temizlenir)
            await _authService.LogoutAsync(); // içinde tanımlı

            // 2. LoginPage'e yönlendir ve navigasyon yığınını temizle
            await Shell.Current.GoToAsync("//LoginPage");
        }
    }
}