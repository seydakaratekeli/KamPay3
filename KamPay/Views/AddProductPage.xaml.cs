using KamPay.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using KamPay.Models.Messages;
using Mapsui;
using Mapsui.Projections;
using Mapsui.Tiling;
using Mapsui.UI.Maui;
using Mapsui.Layers;
using Mapsui.Styles;
using Mapsui.Nts;

namespace KamPay.Views;

public partial class AddProductPage : ContentPage
{
    private readonly AddProductViewModel _viewModel;
    
    // Default location (Ankara, Turkey center)
    private const double DefaultLatitude = 39.9334;
    private const double DefaultLongitude = 32.8597;
    private const double DefaultZoomResolution = 10000; // Higher means more zoomed out
    private const double SelectedZoomResolution = 200; // Zoom level when location is selected
    private const double InitialZoomMultiplier = 5; // Multiplier for initial zoom level
    
    // Pin styling constants
    private const string PinFillColor = "#F44336";
    private const string PinOutlineColor = "#FFFFFF";
    
    // Pin layer for markers
    private WritableLayer? _pinLayer;
    private bool _isMapInfoSubscribed;

    public AddProductPage(AddProductViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;

        // Harita güncelleme mesajlarını dinle
        WeakReferenceMessenger.Default.Register<MapLocationUpdateMessage>(this, (r, message) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                UpdateMapLocation(message.Latitude, message.Longitude);
            });
        });
    }

    // 🔥 Sayfa açıldığında kategorileri yükle ve haritayı başlat
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Subscribe to map info event if not already subscribed
        if (!_isMapInfoSubscribed && ProductMap?.Map != null)
        {
            ProductMap.Map.Info += OnMapInfo;
            _isMapInfoSubscribed = true;
        }

        // Kategoriler cache'de yoksa yükle
        await _viewModel.LoadCategoriesCommand.ExecuteAsync(null);
        
        // Haritayı başlat
        await InitializeMapAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        
        // Mesaj dinleyicisini kaldır
        WeakReferenceMessenger.Default.Unregister<MapLocationUpdateMessage>(this);
        
        // Clean up map event
        if (_isMapInfoSubscribed && ProductMap?.Map != null)
        {
            ProductMap.Map.Info -= OnMapInfo;
            _isMapInfoSubscribed = false;
        }
    }

    // Harita tıklama olayı (Mapsui Info event)
    private async void OnMapInfo(object? sender, MapInfoEventArgs e)
    {
        if (BindingContext is AddProductViewModel viewModel && e.MapInfo?.WorldPosition != null)
        {
            var worldPosition = e.MapInfo.WorldPosition;
            
            // Convert from Spherical Mercator to Lat/Lon
            var lonLat = SphericalMercator.ToLonLat(worldPosition.X, worldPosition.Y);
            
            // Update pin on map
            UpdatePinOnMap(worldPosition.X, worldPosition.Y);
            
            // ViewModel'i güncelle
            viewModel.Latitude = lonLat.lat;
            viewModel.Longitude = lonLat.lon;
            
            // Adres çözümleme (reverse geocoding)
            await viewModel.UpdateLocationFromCoordinatesAsync(lonLat.lat, lonLat.lon);
            
            // Haritayı seçilen noktaya ortala
            ProductMap.Map?.Navigator.CenterOn(worldPosition);
            ProductMap.Map?.Navigator.ZoomTo(SelectedZoomResolution);
        }
    }
    
    // Update or add pin on the map
    private void UpdatePinOnMap(double x, double y)
    {
        if (_pinLayer == null || ProductMap?.Map == null) return;
        
        // Clear existing pins
        _pinLayer.Clear();
        
        // Create a point feature for the pin
        var point = new MPoint(x, y);
        var feature = new PointFeature(point);
        
        // Add the feature to the layer
        _pinLayer.Add(feature);
        _pinLayer.DataHasChanged();
    }

    // Harita konumunu güncelle (mesaj ile)
    private void UpdateMapLocation(double latitude, double longitude)
    {
        try
        {
            // Convert to Spherical Mercator
            var sphericalMercator = SphericalMercator.FromLonLat(longitude, latitude);
            
            // Update pin on map
            UpdatePinOnMap(sphericalMercator.x, sphericalMercator.y);
            
            // Haritayı konuma götür
            ProductMap.Map?.Navigator.CenterOn(new MPoint(sphericalMercator.x, sphericalMercator.y));
            ProductMap.Map?.Navigator.ZoomTo(SelectedZoomResolution);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Harita güncelleme hatası: {ex.Message}");
        }
    }

    // Sayfa yüklendiğinde haritayı kullanıcının mevcut konumuna götür
    private async Task InitializeMapAsync()
    {
        try
        {
            // OpenStreetMap tile layer ekle
            var map = ProductMap.Map;
            if (map == null) return;
            
            map.Layers.Add(OpenStreetMap.CreateTileLayer());
            
            // Create and add a writable layer for pins
            _pinLayer = new WritableLayer("Pins")
            {
                Style = new SymbolStyle
                {
                    SymbolScale = 1.0,
                    Fill = new Brush(Mapsui.Styles.Color.FromString(PinFillColor)),
                    Outline = new Pen(Mapsui.Styles.Color.FromString(PinOutlineColor), 2),
                    SymbolType = SymbolType.Ellipse
                }
            };
            map.Layers.Add(_pinLayer);
            
            // Kullanıcının konumunu al
            var location = await Geolocation.GetLastKnownLocationAsync();
            
            if (location != null)
            {
                var sphericalMercator = SphericalMercator.FromLonLat(location.Longitude, location.Latitude);
                map.Navigator.CenterOn(new MPoint(sphericalMercator.x, sphericalMercator.y));
                map.Navigator.ZoomTo(SelectedZoomResolution * InitialZoomMultiplier);
            }
            else
            {
                // Varsayılan konum (Ankara)
                var sphericalMercator = SphericalMercator.FromLonLat(DefaultLongitude, DefaultLatitude);
                map.Navigator.CenterOn(new MPoint(sphericalMercator.x, sphericalMercator.y));
                map.Navigator.ZoomTo(DefaultZoomResolution);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Harita başlatma hatası: {ex.Message}");
            
            // Hata durumunda varsayılan konum
            if (ProductMap?.Map != null)
            {
                var sphericalMercator = SphericalMercator.FromLonLat(DefaultLongitude, DefaultLatitude);
                ProductMap.Map.Navigator.CenterOn(new MPoint(sphericalMercator.x, sphericalMercator.y));
                ProductMap.Map.Navigator.ZoomTo(DefaultZoomResolution);
            }
        }
    }
}