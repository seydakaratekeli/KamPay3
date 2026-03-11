#if IOS || MACCATALYST
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using UIKit;
using Foundation;

namespace KamPay.Handlers
{
    /// <summary>
    /// iOS için optimize edilmiþ görsel yükleme handler'ý
    /// Native UIImage cache ile performans
    /// NOT: Þu an için FFImageLoading kullanýlýyor (özel handler gerekli deðil)
    /// </summary>
    public class OptimizedImageHandler : ImageHandler
    {
        protected override void ConnectHandler(UIImageView platformView)
        {
            base.ConnectHandler(platformView);
            
            LoadImageOptimized(platformView);
        }

        protected override void DisconnectHandler(UIImageView platformView)
        {
            // Image'i temizle
            platformView.Image = null;
            base.DisconnectHandler(platformView);
        }

        private void LoadImageOptimized(UIImageView imageView)
        {
            if (VirtualView?.Source == null) return;

            // ? URI Source (web görsel)
            if (VirtualView.Source is UriImageSource uriSource)
            {
                var nsUrl = new NSUrl(uriSource.Uri.ToString());
                
                // Native UIImage cache kullan
                var task = Task.Run(async () =>
                {
                    try
                    {
                        using var httpClient = new System.Net.Http.HttpClient();
                        var imageData = await httpClient.GetByteArrayAsync(uriSource.Uri);
                        var data = NSData.FromArray(imageData);
                        return UIImage.LoadFromData(data);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"?? iOS Image yükleme hatasý: {ex.Message}");
                        return null;
                    }
                });

                task.ContinueWith(t =>
                {
                    if (t.IsCompletedSuccessfully && t.Result != null)
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            imageView.Image = t.Result;
                            System.Diagnostics.Debug.WriteLine($"? iOS: Görsel yüklendi - {uriSource.Uri}");
                        });
                    }
                }, TaskScheduler.Default);
            }
            // ? File Source (yerel dosya)
            else if (VirtualView.Source is FileImageSource fileSource)
            {
                var image = UIImage.FromFile(fileSource.File);
                imageView.Image = image;
                
                System.Diagnostics.Debug.WriteLine($"? iOS: Yerel görsel yüklendi - {fileSource.File}");
            }
            // ? Stream Source (bellek akýþý)
            else if (VirtualView.Source is StreamImageSource streamSource)
            {
                var cancellationToken = System.Threading.CancellationToken.None;
                var streamTask = streamSource.Stream(cancellationToken);
                
                streamTask.ContinueWith(task =>
                {
                    if (task.IsCompletedSuccessfully && task.Result != null)
                    {
                        using var stream = task.Result;
                        using var data = NSData.FromStream(stream);
                        var image = UIImage.LoadFromData(data);
                        
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            imageView.Image = image;
                        });
                    }
                }, TaskScheduler.FromCurrentSynchronizationContext());
            }
        }
    }
}
#endif
