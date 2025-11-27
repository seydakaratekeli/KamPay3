using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;

namespace KamPay.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
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
            // Gerekirse çýkýþ iþlemini burada yap
            await Shell.Current.GoToAsync("///LoginPage");
        }
    }
}
