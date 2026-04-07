#if ANDROID
using Android.Widget;
using Bumptech.Glide;
using Bumptech.Glide.Load.Engine;
using Bumptech.Glide.Request;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

namespace KamPay.Handlers
{
    /// <summary>
    /// Android için optimize edilmiþ görsel yükleme handler'ý
    /// Glide kütüphanesi ile native performans
    /// </summary>
    public class OptimizedImageHandler : ImageHandler
    {
        protected override void ConnectHandler(ImageView platformView)
        {
            base.ConnectHandler(platformView);
            
            LoadImageWithGlide(platformView);
        }

        protected override void DisconnectHandler(ImageView platformView)
        {
            // Glide cache'ini temizle
            Glide.With(platformView.Context).Clear(platformView);
            base.DisconnectHandler(platformView);
        }

        private void LoadImageWithGlide(ImageView imageView)
        {
            if (VirtualView?.Source == null) return;

            var context = imageView.Context;
            if (context == null) return;

            // ? URI Source (web görsel)
            if (VirtualView.Source is UriImageSource uriSource)
            {
                Glide.With(context!)
                    .Load(uriSource.Uri.ToString())
                    .Apply(RequestOptions.DiskCacheStrategyOf(DiskCacheStrategy.All!)) // Disk cache
                    .Placeholder(Android.Resource.Drawable.IcMenuGallery) // Placeholder
                    .Error(Android.Resource.Drawable.StatNotifyError) // Hata görseli
                    .CenterCrop() // AspectFill
                    .Into(imageView);

                System.Diagnostics.Debug.WriteLine($"? Glide: Görsel yüklendi - {uriSource.Uri}");
            }
            // ? File Source (yerel dosya)
            else if (VirtualView.Source is FileImageSource fileSource)
            {
                Glide.With(context!)
                    .Load(fileSource.File)
                    .Apply(RequestOptions.DiskCacheStrategyOf(DiskCacheStrategy.All!))
                    .CenterCrop()
                    .Into(imageView);

                System.Diagnostics.Debug.WriteLine($"? Glide: Yerel görsel yüklendi - {fileSource.File}");
            }
            // ? Stream Source (bellek akýþý)
            else if (VirtualView.Source is StreamImageSource streamSource)
            {
                // Stream'i byte array'e çevir (Glide byte[] kabul eder)
                var cancellationToken = System.Threading.CancellationToken.None;
                var streamTask = streamSource.Stream(cancellationToken);

                streamTask.ContinueWith(task =>
                {
                    if (task.IsCompletedSuccessfully && task.Result != null)
                    {
                        using var stream = task.Result;
                        using var memoryStream = new System.IO.MemoryStream();
                        stream.CopyTo(memoryStream);
                        var bytes = memoryStream.ToArray();

                        Glide.With(context!)
                            .Load(bytes)
                            .Apply(RequestOptions.DiskCacheStrategyOf(DiskCacheStrategy.None!)) // Stream cache'lenmesin
                            .CenterCrop()
                            .Into(imageView);
                    }
                }, TaskScheduler.FromCurrentSynchronizationContext());
            }
        }
    }
}
#endif
