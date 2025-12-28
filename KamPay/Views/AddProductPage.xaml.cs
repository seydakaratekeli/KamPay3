using KamPay.ViewModels;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Projections;
using Mapsui.UI.Maui;

namespace KamPay.Views
{
    public partial class AddProductPage : ContentPage
    {
        private readonly AddProductViewModel _viewModel;
        private bool _hasAnimated = false;

        public AddProductPage(AddProductViewModel vm)
        {
            InitializeComponent();
            _viewModel = vm;
            BindingContext = vm;

            _viewModel.MapInitializationRequested += OnMapInitializationRequested;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // ✅ Kategorileri yükle
            if (_viewModel.Categories.Count == 0)
            {
                await _viewModel.LoadCategoriesCommand.ExecuteAsync(null);
            }

            // ✅ Haritayı başlat (küçük gecikme ile) - Public metod kullan
            await Task.Delay(300);
            _viewModel.TriggerMapInitialization();

            if (!_hasAnimated)
            {
                _hasAnimated = true;
                await Task.Delay(100);
                await AnimatePageAsync();
            }
        }

        private async Task AnimatePageAsync()
        {
            // Reset states
            HeaderSection.Opacity = 0;
            HeaderSection.TranslationY = -30;
            FormCard.Opacity = 0;
            FormCard.TranslationY = 50;

            // Background animation
            AnimateBackgroundCircle();

            // Header animation
            await Task.WhenAll(
                HeaderSection.FadeTo(1, 600, Easing.CubicOut),
                HeaderSection.TranslateTo(0, 0, 600, Easing.CubicOut)
            );

            await Task.Delay(150);

            // Form card animation
            await Task.WhenAll(
                FormCard.FadeTo(1, 700, Easing.CubicOut),
                FormCard.TranslateTo(0, 0, 700, Easing.CubicOut)
            );
        }

        private void AnimateBackgroundCircle()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            await Circle1.RotateTo(360, 30000, Easing.Linear);
                            Circle1.Rotation = 0;
                        });
                    }
                    catch
                    {
                        break;
                    }
                }
            });
        }

        private void OnMapInitializationRequested(object? sender, EventArgs e)
        {
            // Haritayı MainThread'de başlat
            MainThread.BeginInvokeOnMainThread(() =>
            {
                InitializeMap();
            });
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AddProductViewModel.Latitude) ||
                e.PropertyName == nameof(AddProductViewModel.Longitude))
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    UpdateMapLocation();
                });
            }
        }

        private void InitializeMap()
        {
            try
            {
                if (ProductMap?.Map == null)
                {
                    Console.WriteLine("⚠️ ProductMap.Map null!");
                    return;
                }

                // Önce mevcut layer'ları temizle
                ProductMap.Map.Layers.Clear();

                // OpenStreetMap tile layer ekle
                var tileLayer = Mapsui.Tiling.OpenStreetMap.CreateTileLayer();
                ProductMap.Map.Layers.Add(tileLayer);

                // Harita ayarları
                if (ProductMap.Map.Navigator != null)
                {
                    ProductMap.Map.Navigator.RotationLock = true;
                }

                // İlk konumu ayarla
                UpdateMapLocation();

                Console.WriteLine("✅ Harita başarıyla başlatıldı");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Map initialization error: {ex.Message}");
            }
        }

        private void UpdateMapLocation()
        {
            if (ProductMap?.Map == null)
            {
                Console.WriteLine("⚠️ UpdateMapLocation: Map null");
                return;
            }

            if (!_viewModel.Latitude.HasValue || !_viewModel.Longitude.HasValue)
            {
                Console.WriteLine("⚠️ UpdateMapLocation: Koordinatlar null");
                return;
            }

            try
            {
                var mercator = SphericalMercator.FromLonLat(_viewModel.Longitude.Value, _viewModel.Latitude.Value);
                ProductMap.Map.Navigator.CenterOn(new MPoint(mercator.x, mercator.y));
                ProductMap.Map.Navigator.ZoomTo(3); // Zoom seviyesini düşürdük (daha yakın)

                Console.WriteLine($"✅ Harita konumu güncellendi: {_viewModel.Latitude}, {_viewModel.Longitude}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Map location update error: {ex.Message}");
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _viewModel.MapInitializationRequested -= OnMapInitializationRequested;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }
    }
}