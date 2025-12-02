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

    // Default location (Bartın, Turkey)
    private const double DefaultLatitude = 41.5810;
    private const double DefaultLongitude = 32.4610;

    // Optimized zoom resolutions for better user experience
    private const double DefaultZoomResolution = 5000;      // Default city view
    private const double SelectedZoomResolution = 500;      // Selected location view
    private const double InitialZoomMultiplier = 2;         // Initial zoom multiplier
    private const double MinZoomResolution = 100;            // Maximum zoom in
    private const double MaxZoomResolution = 50000;          // Maximum zoom out
    private const double ZoomStep = 2.0;                     // Zoom step factor

    // Pin styling
    private const string PinFillColor = "#F44336";
    private const string PinOutlineColor = "#FFFFFF";

    private WritableLayer? _pinLayer;
    private bool _isMapInfoSubscribed;
    private bool _isMapInitialized;
    private MPoint? _selectedLocation; // Store selected location for reset

    public EditProductPage(EditProductViewModel vm)
    {
        InitializeComponent();
        _viewModel = vm;
        BindingContext = vm;

        // Harita konum güncellemelerini dinle (📍 butonu için)
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
                
                // Enable double-tap zoom
                ProductMap.DoubleTapped += OnMapDoubleTapped;
            }

            // Subscribe to property changes to initialize map when product loads
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

            Console.WriteLine("📍 EditProductPage harita başlatılıyor...");
            await InitializeMapAsync();
            Console.WriteLine("✅ EditProductPage harita başlatıldı");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ EditProductPage OnAppearing Hatası: {ex.Message}");
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // When Latitude/Longitude changes from product load, update map
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

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        WeakReferenceMessenger.Default.Unregister<MapLocationUpdateMessage>(this);

        if (_isMapInfoSubscribed && ProductMap?.Map != null)
        {
            ProductMap.Map.Info -= OnMapInfo;
            ProductMap.DoubleTapped -= OnMapDoubleTapped;
            _isMapInfoSubscribed = false;
        }
    }

    // Harita tıklama
    private async void OnMapInfo(object? sender, MapInfoEventArgs e)
    {
        if (BindingContext is EditProductViewModel viewModel &&
            e.MapInfo?.WorldPosition != null)
        {
            var worldPosition = e.MapInfo.WorldPosition;
            
            // Store selected location for reset functionality
            _selectedLocation = worldPosition;

            var lonLat = SphericalMercator.ToLonLat(worldPosition.X, worldPosition.Y);

            UpdatePinOnMap(worldPosition.X, worldPosition.Y);

            viewModel.Latitude = lonLat.lat;
            viewModel.Longitude = lonLat.lon;

            await viewModel.UpdateLocationFromCoordinatesAsync(lonLat.lat, lonLat.lon);

            ProductMap.Map?.Navigator.CenterOn(worldPosition);
            ProductMap.Map?.Navigator.ZoomTo(SelectedZoomResolution, 500); // Add smooth animation
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
            
            // Store as selected location
            _selectedLocation = new MPoint(spherical.x, spherical.y);

            UpdatePinOnMap(spherical.x, spherical.y);

            ProductMap.Map?.Navigator.CenterOn(new MPoint(spherical.x, spherical.y));
            ProductMap.Map?.Navigator.ZoomTo(SelectedZoomResolution, 500); // Add smooth animation
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

            // OpenStreetMap layer
            map.Layers.Add(OpenStreetMap.CreateTileLayer());

            // Pin layer
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

            // Check if product already has location
            if (_viewModel.Latitude.HasValue && _viewModel.Longitude.HasValue)
            {
                var spherical = SphericalMercator.FromLonLat(_viewModel.Longitude.Value, _viewModel.Latitude.Value);
                
                UpdatePinOnMap(spherical.x, spherical.y);
                map.Navigator.CenterOn(new MPoint(spherical.x, spherical.y));
                map.Navigator.ZoomTo(SelectedZoomResolution);
            }
            else
            {
                // Try to get user's location
                var location = await Geolocation.GetLastKnownLocationAsync();

                if (location != null)
                {
                    var spherical = SphericalMercator.FromLonLat(location.Longitude, location.Latitude);

                    map.Navigator.CenterOn(new MPoint(spherical.x, spherical.y));
                    map.Navigator.ZoomTo(SelectedZoomResolution * InitialZoomMultiplier);
                }
                else
                {
                    // Default Bartın
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

    // Double-tap to zoom in
    private void OnMapDoubleTapped(object? sender, TappedEventArgs e)
    {
        ZoomIn();
    }

    // Event handlers for XAML buttons
    private void OnZoomInClicked(object? sender, EventArgs e)
    {
        ZoomIn();
    }

    private void OnZoomOutClicked(object? sender, EventArgs e)
    {
        ZoomOut();
    }

    private void OnMyLocationClicked(object? sender, EventArgs e)
    {
        GoToMyLocation();
    }

    private void OnResetLocationClicked(object? sender, EventArgs e)
    {
        GoToSelectedLocation();
    }

    // Zoom in method
    private void ZoomIn()
    {
        if (ProductMap?.Map?.Navigator == null) return;

        var currentResolution = ProductMap.Map.Navigator.Viewport.Resolution;
        var newResolution = Math.Max(MinZoomResolution, currentResolution / ZoomStep);
        
        ProductMap.Map.Navigator.ZoomTo(newResolution, 500); // 500ms animation
    }

    // Zoom out method  
    private void ZoomOut()
    {
        if (ProductMap?.Map?.Navigator == null) return;

        var currentResolution = ProductMap.Map.Navigator.Viewport.Resolution;
        var newResolution = Math.Min(MaxZoomResolution, currentResolution * ZoomStep);
        
        ProductMap.Map.Navigator.ZoomTo(newResolution, 500); // 500ms animation
    }

    // Go to current user location
    private async void GoToMyLocation()
    {
        try
        {
            var location = await Geolocation.GetLocationAsync(new GeolocationRequest
            {
                DesiredAccuracy = GeolocationAccuracy.Best,
                Timeout = TimeSpan.FromSeconds(10)
            });

            if (location != null)
            {
                var spherical = SphericalMercator.FromLonLat(location.Longitude, location.Latitude);
                
                ProductMap.Map?.Navigator.CenterOn(new MPoint(spherical.x, spherical.y));
                ProductMap.Map?.Navigator.ZoomTo(SelectedZoomResolution, 500);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Konum alınamadı: {ex.Message}");
        }
    }

    // Go back to selected location
    private void GoToSelectedLocation()
    {
        if (_selectedLocation != null && ProductMap?.Map?.Navigator != null)
        {
            ProductMap.Map.Navigator.CenterOn(_selectedLocation);
            ProductMap.Map.Navigator.ZoomTo(SelectedZoomResolution, 500);
        }
    }
}