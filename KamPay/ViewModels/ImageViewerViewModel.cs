using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace KamPay.ViewModels
{
    // bu sayfa teslimat fotoğrafını görüntülemek ve indirmek için kullanılır
    [QueryProperty(nameof(PhotoUrl), "photoUrl")]
    public partial class ImageViewerViewModel : ObservableObject
    {
        [ObservableProperty]
        private string photoUrl = string.Empty;

        [ObservableProperty]
        private bool isLoading = true;

        partial void OnPhotoUrlChanged(string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                IsLoading = false;
            }
        }

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

                IsLoading = true;

                // Note: HttpClient created per request - acceptable for infrequent user-initiated downloads
                // For production at scale, consider IHttpClientFactory for connection pooling
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

                await Application.Current.MainPage.DisplayAlert("Başarılı", 
                    "Fotoğraf indirildi ve paylaşım menüsü açıldı.", "Tamam");
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Hata", 
                    $"Fotoğraf indirilemedi: {ex.Message}", "Tamam");
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
