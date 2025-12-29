using KamPay.ViewModels;

namespace KamPay.Views
{
    public partial class ImageViewerPage : ContentPage
    {
        private readonly ImageViewerViewModel _viewModel;
        private double _currentScale = 1;
        private double _startScale = 1;
        private double _xOffset = 0;
        private double _yOffset = 0;
        private bool _hasAnimated = false;

        public ImageViewerPage(ImageViewerViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            if (!_hasAnimated)
            {
                _hasAnimated = true;
                await AnimatePageEntryAsync();
            }

            // Arka plan animasyonunu başlat
            AnimateBackgroundCircles();
        }

        private async Task AnimatePageEntryAsync()
        {
            // Başlangıç durumları
            MainContainer.Opacity = 0;
            MainContainer.Scale = 0.9;
            TopBar.TranslationY = -100;
            BottomBar.TranslationY = 100;
            BackgroundCircles.Opacity = 0;

            // Arka plan fade-in
            await BackgroundCircles.FadeTo(0.08, 800, Easing.CubicOut);

            // Ana container animasyonu
            await Task.WhenAll(
                MainContainer.FadeTo(1, 400, Easing.CubicOut),
                MainContainer.ScaleTo(1, 400, Easing.CubicOut)
            );

            // Kontrol barları animasyonu
            await Task.WhenAll(
                TopBar.TranslateTo(0, 0, 350, Easing.CubicOut),
                BottomBar.TranslateTo(0, 0, 350, Easing.CubicOut)
            );
        }

        protected override async void OnDisappearing()
        {
            base.OnDisappearing();
            await AnimatePageExitAsync();
        }

        private async Task AnimatePageExitAsync()
        {
            await Task.WhenAll(
                MainContainer.FadeTo(0, 200, Easing.CubicIn),
                MainContainer.ScaleTo(0.9, 200, Easing.CubicIn),
                TopBar.TranslateTo(0, -100, 200, Easing.CubicIn),
                BottomBar.TranslateTo(0, 100, 200, Easing.CubicIn)
            );
        }

        private void AnimateBackgroundCircles()
        {
            // Sürekli dönen arka plan çemberleri
            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            await Circle1.RotateTo(360, 40000, Easing.Linear);
                            Circle1.Rotation = 0;
                        });
                    }
                    catch
                    {
                        break;
                    }
                }
            });

            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            await Circle2.RotateTo(-360, 35000, Easing.Linear);
                            Circle2.Rotation = 0;
                        });
                    }
                    catch
                    {
                        break;
                    }
                }
            });
        }

        // ✅ TAMAMEN DÜZELTİLMİŞ: Pinch to Zoom (Anchor problemi çözüldü)
        private void OnPinchUpdated(object? sender, PinchGestureUpdatedEventArgs e)
        {
            if (sender is not Image image)
                return;

            switch (e.Status)
            {
                case GestureStatus.Started:
                    _startScale = _currentScale;
                    // ✅ Anchor'ı merkeze sabitle - titreme önlenir
                    image.AnchorX = 0.5;
                    image.AnchorY = 0.5;
                    break;

                case GestureStatus.Running:
                    // Zoom seviyesini hesapla (1.0 ile 4.0 arasında sınırla)
                    var newScale = _startScale * e.Scale;
                    _currentScale = Math.Max(1, Math.Min(newScale, 4));
                    
                    // Zoom'u uygula
                    image.Scale = _currentScale;
                    
                    // Zoom göstergesini güncelle
                    _ = ShowZoomIndicatorAsync(_currentScale);
                    break;

                case GestureStatus.Completed:
                    // Minimum zoom seviyesinin altındaysa sıfırla
                    if (_currentScale <= 1)
                    {
                        ResetZoom(image);
                    }
                    break;
            }
        }

        // ✅ TAMAMEN DÜZELTİLMİŞ: Pan Gesture (Titreme tamamen giderildi)
        private void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
        {
            if (sender is not Image image)
                return;

            // Sadece zoom yapıldıysa kaydırmaya izin ver
            if (_currentScale <= 1.05) // Küçük bir tolerans payı
                return;

            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    // Mevcut offset'i kaydet
                    _xOffset = image.TranslationX;
                    _yOffset = image.TranslationY;
                    break;

                case GestureStatus.Running:
                    // ✅核心 DÜZELTME: Sadece delta (TotalX/Y) kullan, kümülatif toplama
                    var deltaX = e.TotalX;
                    var deltaY = e.TotalY;

                    // Yeni pozisyonu hesapla (başlangıç + delta)
                    var newX = _xOffset + deltaX;
                    var newY = _yOffset + deltaY;

                    // Kaydırma limitlerini hesapla (resmin görüntü alanı dışına çıkmaması için)
                    var maxOffsetX = Math.Max(0, (image.Width * _currentScale - image.Width) / 2);
                    var maxOffsetY = Math.Max(0, (image.Height * _currentScale - image.Height) / 2);

                    // Limitleri uygula ve direkt ata (animasyon yok, anında hareket)
                    image.TranslationX = Math.Max(-maxOffsetX, Math.Min(maxOffsetX, newX));
                    image.TranslationY = Math.Max(-maxOffsetY, Math.Min(maxOffsetY, newY));
                    break;

                case GestureStatus.Completed:
                    // Son pozisyonu kaydet (bir sonraki pan için)
                    _xOffset = image.TranslationX;
                    _yOffset = image.TranslationY;
                    break;

                case GestureStatus.Canceled:
                    // İptal durumunda da pozisyonu kaydet
                    _xOffset = image.TranslationX;
                    _yOffset = image.TranslationY;
                    break;
            }
        }

        // ✅ İYİLEŞTİRİLMİŞ: Double tap to zoom
        private async void OnImageTapped(object? sender, EventArgs e)
        {
            if (sender is not Image image)
                return;

            if (_currentScale > 1.05)
            {
                // Zoomlu ise reset yap
                ResetZoom(image);
            }
            else
            {
                // 2x zoom yap
                _currentScale = 2;
                _xOffset = 0;
                _yOffset = 0;
                
                await Task.WhenAll(
                    image.ScaleTo(_currentScale, 250, Easing.CubicInOut),
                    image.TranslateTo(0, 0, 250, Easing.CubicInOut)
                );
            }

            await ShowZoomIndicatorAsync(_currentScale);
        }

        // ✅ Reset zoom metodu
        private async void ResetZoom(Image image)
        {
            _currentScale = 1;
            _xOffset = 0;
            _yOffset = 0;

            await Task.WhenAll(
                image.ScaleTo(1, 250, Easing.CubicInOut),
                image.TranslateTo(0, 0, 250, Easing.CubicInOut)
            );

            await ShowZoomIndicatorAsync(1);
        }

        // ✅ Reset butonu için event handler
        private void OnResetZoomTapped(object? sender, EventArgs e)
        {
            if (MainImage != null && _currentScale > 1)
            {
                ResetZoom(MainImage);
            }
        }

        private async Task ShowZoomIndicatorAsync(double zoom)
        {
            if (ZoomIndicator == null || ZoomLabel == null) return;

            try
            {
                ZoomLabel.Text = $"{(int)(zoom * 100)}%";
                ZoomIndicator.IsVisible = true;

                // Fade in
                await ZoomIndicator.FadeTo(1, 150);

                // 1.5 saniye bekle
                await Task.Delay(1500);

                // Fade out
                await ZoomIndicator.FadeTo(0, 300);
                ZoomIndicator.IsVisible = false;
            }
            catch
            {
                // Animasyon sırasında sayfa kapatılırsa hata vermesin
            }
        }
    }
}
