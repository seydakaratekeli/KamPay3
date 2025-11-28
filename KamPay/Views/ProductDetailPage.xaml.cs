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
    private const double DefaultZoomResolution = 10000;
    private const double SelectedZoomResolution = 200;

    // Pin styling
    private const string PinFillColor = "#F44336";
    private const string PinOutlineColor = "#FFFFFF";

    private WritableLayer? _pinLayer;
    private bool _isMapInitialized;

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

            // Add pin
            UpdatePinOnMap(spherical.x, spherical.y);

            // Center map on product location
            map.Navigator.CenterOn(new MPoint(spherical.x, spherical.y));
            map.Navigator.ZoomTo(SelectedZoomResolution);

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
}