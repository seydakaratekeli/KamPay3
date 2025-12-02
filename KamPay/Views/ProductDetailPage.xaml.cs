using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.Messaging;
using KamPay.Services;
using KamPay.ViewModels;
using Mapsui;
using Mapsui.Projections;
using Mapsui.Tiling;
using Mapsui.Layers;
using Mapsui.Styles;
using MapsuiBrush = Mapsui.Styles.Brush;

namespace KamPay.Views;

public partial class ProductDetailPage : ContentPage
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ProductDetailViewModel _viewModel;

    // Default location (Bartın, Turkey)
    private const double DefaultLatitude = 41.5810;
    private const double DefaultLongitude = 32.4610;
    // Optimized zoom resolutions for better user experience
    private const double DefaultZoomResolution = 5000;      // Default city view
    private const double SelectedZoomResolution = 500;      // Selected location view
    private const double MinZoomResolution = 100;            // Maximum zoom in
    private const double MaxZoomResolution = 50000;          // Maximum zoom out
    private const double ZoomStep = 2.0;                     // Zoom step factor

    // Pin styling
    private const string PinFillColor = "#F44336";
    private const string PinOutlineColor = "#FFFFFF";

    private WritableLayer? _pinLayer;
    private bool _isMapInitialized;
    private MPoint? _productLocation; // Store product location for reset

    public ProductDetailPage(ProductDetailViewModel viewModel, IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        _serviceProvider = serviceProvider;

        WeakReferenceMessenger.Default.Register<ShowTradeOfferPopupMessage>(this, async (r, m) =>
        {
            var tradePopup = _serviceProvider.GetRequiredService<TradeOfferView>();

            if (tradePopup.BindingContext is TradeOfferViewModel vm)
            {
                vm.ProductId = m.TargetProduct.ProductId;
            }

            await this.ShowPopupAsync(tradePopup);
        });
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Subscribe to property changes to know when product is loaded
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        
        // Enable double-tap zoom if map is ready
        if (ProductMap != null)
        {
            ProductMap.DoubleTapped += OnMapDoubleTapped;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProductDetailViewModel.Product) && _viewModel.Product != null)
        {
            // Product loaded, initialize map
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await InitializeMapAsync();
            });
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        WeakReferenceMessenger.Default.Unregister<ShowTradeOfferPopupMessage>(this);
        
        if (ProductMap != null)
        {
            ProductMap.DoubleTapped -= OnMapDoubleTapped;
        }
    }

    private async Task InitializeMapAsync()
    {
        try
        {
            if (_isMapInitialized || ProductMap?.Map == null) return;
            if (_viewModel.Product == null) return;
            if (!_viewModel.Product.Latitude.HasValue || !_viewModel.Product.Longitude.HasValue) return;

            var map = ProductMap.Map;

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

            // Set product location
            var lat = _viewModel.Product.Latitude.Value;
            var lon = _viewModel.Product.Longitude.Value;
            var spherical = SphericalMercator.FromLonLat(lon, lat);
            
            // Store product location for reset functionality
            _productLocation = new MPoint(spherical.x, spherical.y);

            // Add pin
            UpdatePinOnMap(spherical.x, spherical.y);

            // Center map on product location
            map.Navigator.CenterOn(_productLocation);
            map.Navigator.ZoomTo(SelectedZoomResolution, 500); // Add smooth animation

            _isMapInitialized = true;

            Console.WriteLine($"✅ ProductDetailPage haritası başlatıldı: {lat}, {lon}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ProductDetailPage harita başlatma hatası: {ex.Message}");
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
        GoToProductLocation();
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

    // Go back to product location
    private void GoToProductLocation()
    {
        if (_productLocation != null && ProductMap?.Map?.Navigator != null)
        {
            ProductMap.Map.Navigator.CenterOn(_productLocation);
            ProductMap.Map.Navigator.ZoomTo(SelectedZoomResolution, 500);
        }
    }
}