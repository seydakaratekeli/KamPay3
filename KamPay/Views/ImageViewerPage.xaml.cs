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
        private double _startX = 0;
        private double _startY = 0;
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

        // ✅ İYİLEŞTİRİLMİŞ: Pinch to Zoom Gesture (Titreme düzeltildi)
        private void OnPinchUpdated(object? sender, PinchGestureUpdatedEventArgs e)
        {
            if (sender is not Image image)
                return;

            switch (e.Status)
            {
                case GestureStatus.Started:
                    _startScale = _currentScale;
                    // Anchor noktasını parmağın olduğu yere ayarla
                    image.AnchorX = e.ScaleOrigin.X;
                    image.AnchorY = e.ScaleOrigin.Y;
                    break;

                case GestureStatus.Running:
                    // Zoom seviyesini hesapla (1.0 ile 4.0 arasında sınırla)
                    _currentScale = Math.Max(1, Math.Min(_startScale * e.Scale, 4));
                    
                    // Zoom'u uygula
                    image.Scale = _currentScale;
                    
                    // Zoom göstergesini güncelle
                    _ = ShowZoomIndicatorAsync(_currentScale);
                    break;

                case GestureStatus.Completed:
                    // Minimum zoom seviyesinin altındaysa sıfırla
                    if (_currentScale < 1)
                    {
                        ResetZoom(image);
                    }
                    // Zoom yapıldıysa anchor'ı merkeze al
                    else if (_currentScale > 1)
                    {
                        image.AnchorX = 0.5;
                        image.AnchorY = 0.5;
                    }
                    break;
            }
        }

        // ✅ İYİLEŞTİRİLMİŞ: Pan Gesture (Titreme düzeltildi)
        private void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
        {
            if (sender is not Image image)
                return;

            // Sadece zoom yapıldıysa kaydırmaya izin ver
            if (_currentScale <= 1)
                return;

            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    // Başlangıç pozisyonunu kaydet
                    _startX = image.TranslationX;
                    _startY = image.TranslationY;
                    break;

                case GestureStatus.Running:
                    // ✅ DÜZELTME: TotalX/Y yerine delta kullan
                    var newX = _startX + e.TotalX;
                    var newY = _startY + e.TotalY;

                    // Kaydırma limitlerini hesapla
                    var maxOffsetX = (image.Width * (_currentScale - 1)) / 2;
                    var maxOffsetY = (image.Height * (_currentScale - 1)) / 2;

                    // Limitleri uygula
                    image.TranslationX = Math.Max(-maxOffsetX, Math.Min(maxOffsetX, newX));
                    image.TranslationY = Math.Max(-maxOffsetY, Math.Min(maxOffsetY, newY));
                    break;

                case GestureStatus.Completed:
                    // Son pozisyonu kaydet
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

            if (_currentScale > 1)
            {
                // Zoomlu ise reset yap
                ResetZoom(image);
            }
            else
            {
                // 2x zoom yap (dokunulan noktaya göre)
                _currentScale = 2;
                
                await Task.WhenAll(
                    image.ScaleTo(_currentScale, 250, Easing.CubicInOut)
                );
            }

            await ShowZoomIndicatorAsync(_currentScale);
        }

        // ✅ YENİ: Reset zoom metodu
        private async void ResetZoom(Image image)
        {
            _currentScale = 1;
            _xOffset = 0;
            _yOffset = 0;

            await Task.WhenAll(
                image.ScaleTo(1, 250, Easing.CubicInOut),
                image.TranslateTo(0, 0, 250, Easing.CubicInOut)
            );

            image.AnchorX = 0.5;
            image.AnchorY = 0.5;

            await ShowZoomIndicatorAsync(1);
        }

        // ✅ YENİ: Reset butonu için command handler
        private void OnResetZoomTapped(object? sender, EventArgs e)
        {
            if (MainImage != null)
            {
                ResetZoom(MainImage);
            }
        }

        private async Task ShowZoomIndicatorAsync(double zoom)
        {
            if (ZoomIndicator == null || ZoomLabel == null) return;

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
    }
}
