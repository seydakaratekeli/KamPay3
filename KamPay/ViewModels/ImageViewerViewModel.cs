using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace KamPay.ViewModels
{
    [QueryProperty(nameof(ImageUrl), "imageUrl")]
    public partial class ImageViewerViewModel : ObservableObject
    {
        [ObservableProperty]
        private string imageUrl;

        [RelayCommand]
        private async Task CloseAsync()
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}
