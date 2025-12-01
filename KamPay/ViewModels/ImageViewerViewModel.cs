using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace KamPay.ViewModels
{
    [QueryProperty(nameof(PhotoUrl), "photoUrl")]
    public partial class ImageViewerViewModel : ObservableObject
    {
        [ObservableProperty]
        private string photoUrl = string.Empty;

        [RelayCommand]
        private async Task CloseAsync()
        {
            await Shell.Current.GoToAsync("..");
        }

        [RelayCommand]
        private async Task DownloadPhotoAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(PhotoUrl))
                {
                    await Application.Current.MainPage.DisplayAlert("Hata", 
                        "Fotoğraf URL'si bulunamadı.", "Tamam");
                    return;
                }

                using var client = new HttpClient();
                var bytes = await client.GetByteArrayAsync(PhotoUrl);
                var fileName = $"KamPay_Delivery_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                // CacheDirectory is appropriate for temporary sharing - file doesn't need to persist
                var path = Path.Combine(FileSystem.CacheDirectory, fileName);
                await File.WriteAllBytesAsync(path, bytes);
                
                await Share.RequestAsync(new ShareFileRequest 
                { 
                    Title = "Teslimat Fotoğrafı",
                    File = new ShareFile(path) 
                });
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Hata", 
                    $"Fotoğraf indirilemedi: {ex.Message}", "Tamam");
            }
        }
    }
}
