using KamPay.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using KamPay.Models.Messages;
using Mapsui;
using Mapsui.Projections;
using Mapsui.Tiling;
using Mapsui.UI.Maui;
using Mapsui.Layers;
using Mapsui.Styles;
using MapsuiBrush = Mapsui.Styles.Brush;

namespace KamPay.Views;

public partial class EditProductPage : ContentPage
{
    private readonly EditProductViewModel _viewModel;
    private bool _hasAnimated = false;

    private const double DefaultLatitude = 41.5810;
    private const double DefaultLongitude = 32.4610;
    private const double DefaultZoomResolution = 5000;
    private const double SelectedZoomResolution = 500;
    private const double InitialZoomMultiplier = 2;
    private const double MinZoomResolution = 100;
    private const double MaxZoomResolution = 50000;
    private const double ZoomStep = 2.0;

    private const string PinFillColor = "#F44336";
    private const string PinOutlineColor = "#FFFFFF";

    private WritableLayer? _pinLayer;
    private bool _isMapInfoSubscribed;
    private bool _isMapInitialized;
    private MPoint? _selectedLocation;

    public EditProductPage(EditProductViewModel vm)
    {
        InitializeComponent();
        _viewModel = vm;
        BindingContext = vm;

        WeakReferenceMessenger.Default.Register<MapLocationUpdateMessage>(this, (r, message) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                UpdateMapLocation(message.Latitude, message.Longitude);
            });
        });
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            if (!_isMapInfoSubscribed && ProductMap?.Map != null)
            {
                ProductMap.Map.Info += OnMapInfo;
                _isMapInfoSubscribed = true;
            }

            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

            Console.WriteLine("📍 EditProductPage harita başlatılıyor...");
            await InitializeMapAsync();
            Console.WriteLine("✅ EditProductPage harita başlatıldı");

            // Animasyon
            if (!_hasAnimated)
            {
                _hasAnimated = true;
                await Task.Delay(100);
                await AnimatePageAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ EditProductPage OnAppearing Hatası: {ex.Message}");
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
                        await Circle1.RotateTo(360, 28000, Easing.Linear);
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

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        WeakReferenceMessenger.Default.Unregister<MapLocationUpdateMessage>(this);

        if (_isMapInfoSubscribed && ProductMap?.Map != null)
        {
            ProductMap.Map.Info -= OnMapInfo;
            _isMapInfoSubscribed = false;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if ((e.PropertyName == nameof(EditProductViewModel.Latitude) ||
             e.PropertyName == nameof(EditProductViewModel.Longitude)) &&
            _viewModel.Latitude.HasValue && _viewModel.Longitude.HasValue)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                UpdateMapLocation(_viewModel.Latitude.Value, _viewModel.Longitude.Value);
            });
        }
    }

    private async void OnMapInfo(object? sender, MapInfoEventArgs e)
    {
        if (BindingContext is EditProductViewModel viewModel &&
            e.MapInfo?.WorldPosition != null)
        {
            var worldPosition = e.MapInfo.WorldPosition;

            _selectedLocation = worldPosition;

            var lonLat = SphericalMercator.ToLonLat(worldPosition.X, worldPosition.Y);

            UpdatePinOnMap(worldPosition.X, worldPosition.Y);

            viewModel.Latitude = lonLat.lat;
            viewModel.Longitude = lonLat.lon;

            await viewModel.UpdateLocationFromCoordinatesAsync(lonLat.lat, lonLat.lon);

            ProductMap.Map?.Navigator.CenterOn(worldPosition);
            ProductMap.Map?.Navigator.ZoomTo(SelectedZoomResolution, 500);
        }
    }

    private void UpdatePinOnMap(double x, double y)
    {
        if (_pinLayer == null || ProductMap?.Map == null) return;

        _pinLayer.Clear();

        var point = new MPoint(x, y);
        var feature = new PointFeature(point);

        _pinLayer.Add(feature);
        _pinLayer.DataHasChanged();
    }

    private void UpdateMapLocation(double latitude, double longitude)
    {
        try
        {
            var spherical = SphericalMercator.FromLonLat(longitude, latitude);

            _selectedLocation = new MPoint(spherical.x, spherical.y);

            UpdatePinOnMap(spherical.x, spherical.y);

            ProductMap.Map?.Navigator.CenterOn(new MPoint(spherical.x, spherical.y));
            ProductMap.Map?.Navigator.ZoomTo(SelectedZoomResolution, 500);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"EditProductPage harita konum güncelleme hatası: {ex.Message}");
        }
    }

    private async Task InitializeMapAsync()
    {
        try
        {
            if (_isMapInitialized) return;

            var map = ProductMap.Map;
            if (map == null) return;

            map.Layers.Add(OpenStreetMap.CreateTileLayer());

            _pinLayer = new WritableLayer
            {
                Name = "Pins",
                Style = new SymbolStyle
                {
                    SymbolScale = 1.0,
                    Fill = new MapsuiBrush(Mapsui.Styles.Color.FromString(PinFillColor)),
                    Outline = new Pen(Mapsui.Styles.Color.FromString(PinOutlineColor), 2),
                    SymbolType = SymbolType.Ellipse
                }
            };

            map.Layers.Add(_pinLayer);

            if (_viewModel.Latitude.HasValue && _viewModel.Longitude.HasValue)
            {
                var spherical = SphericalMercator.FromLonLat(_viewModel.Longitude.Value, _viewModel.Latitude.Value);

                UpdatePinOnMap(spherical.x, spherical.y);
                map.Navigator.CenterOn(new MPoint(spherical.x, spherical.y));
                map.Navigator.ZoomTo(SelectedZoomResolution);
            }
            else
            {
                var location = await Geolocation.GetLastKnownLocationAsync();

                if (location != null)
                {
                    var spherical = SphericalMercator.FromLonLat(location.Longitude, location.Latitude);

                    map.Navigator.CenterOn(new MPoint(spherical.x, spherical.y));
                    map.Navigator.ZoomTo(SelectedZoomResolution * InitialZoomMultiplier);
                }
                else
                {
                    var spherical = SphericalMercator.FromLonLat(DefaultLongitude, DefaultLatitude);

                    map.Navigator.CenterOn(new MPoint(spherical.x, spherical.y));
                    map.Navigator.ZoomTo(DefaultZoomResolution);
                }
            }

            _isMapInitialized = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"EditProductPage harita başlatma hatası: {ex.Message}");

            if (ProductMap?.Map != null)
            {
                var spherical = SphericalMercator.FromLonLat(DefaultLongitude, DefaultLatitude);

                ProductMap.Map.Navigator.CenterOn(new MPoint(spherical.x, spherical.y));
                ProductMap.Map.Navigator.ZoomTo(DefaultZoomResolution);
            }
        }
    }

    private void OnZoomInClicked(object? sender, EventArgs e)
    {
        ZoomIn();
    }

    private void OnZoomOutClicked(object? sender, EventArgs e)
    {
        ZoomOut();
    }

    private void OnResetLocationClicked(object? sender, EventArgs e)
    {
        GoToSelectedLocation();
    }

    private void ZoomIn()
    {
        if (ProductMap?.Map?.Navigator == null) return;

        var currentResolution = ProductMap.Map.Navigator.Viewport.Resolution;
        var newResolution = Math.Max(MinZoomResolution, currentResolution / ZoomStep);

        ProductMap.Map.Navigator.ZoomTo(newResolution, 500);
    }

    private void ZoomOut()
    {
        if (ProductMap?.Map?.Navigator == null) return;

        var currentResolution = ProductMap.Map.Navigator.Viewport.Resolution;
        var newResolution = Math.Min(MaxZoomResolution, currentResolution * ZoomStep);

        ProductMap.Map.Navigator.ZoomTo(newResolution, 500);
    }

    private void GoToSelectedLocation()
    {
        if (_selectedLocation != null && ProductMap?.Map?.Navigator != null)
        {
            ProductMap.Map.Navigator.CenterOn(_selectedLocation);
            ProductMap.Map.Navigator.ZoomTo(SelectedZoomResolution, 500);
        }
    }
}