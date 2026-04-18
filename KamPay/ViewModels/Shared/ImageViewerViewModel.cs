using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace KamPay.ViewModels
{
    // bu sayfa teslimat fotoğrafını görüntülemek ve indirmek için kullanılır
    [QueryProperty("PhotoUrl", "photoUrl")]
    [QueryProperty("ImagesJson", "imagesJson")] // Keep for deep links if needed, map to different key
    [QueryProperty("InputImages", "images")]
    [QueryProperty("SelectedIndex", "index")]
    public partial class ImageViewerViewModel : ObservableObject
    {
        [ObservableProperty] private string photoUrl = string.Empty;
        [ObservableProperty] private string imagesJson = string.Empty;

        private List<string> _inputImages = new();
        public List<string> InputImages
        {
            get => _inputImages;
            set
            {
                if (SetProperty(ref _inputImages, value))
                {
                    if (value != null && value.Count > 0)
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            ProductImages.Clear();
                            foreach (var img in value) ProductImages.Add(img);
                            IsLoading = false;
                        });
                    }
                }
            }
        }

        [ObservableProperty] private int selectedIndex;
        [ObservableProperty] private bool isLoading = true;

        public System.Collections.ObjectModel.ObservableCollection<string> ProductImages { get; } = new();

        partial void OnPhotoUrlChanged(string value)
        {
            if (!string.IsNullOrEmpty(value) && ProductImages.Count == 0)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ProductImages.Clear();
                    ProductImages.Add(value);
                    IsLoading = false;
                });
            }
        }

        partial void OnImagesJsonChanged(string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                try
                {
                    var decoded = System.Net.WebUtility.UrlDecode(value);
                    var images = System.Text.Json.JsonSerializer.Deserialize<List<string>>(decoded);
                    if (images != null)
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            ProductImages.Clear();
                            foreach (var img in images) ProductImages.Add(img);
                        });
                    }
                }
                catch { }
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
