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
using MapsuiBrush = Mapsui.Styles.Brush;

namespace KamPay.Views;

public partial class AddProductPage : ContentPage
{
    private readonly AddProductViewModel _viewModel;

    // Default location (Bartın, Turkey)
    private const double DefaultLatitude = 41.5810;
    private const double DefaultLongitude = 32.4610;

    private const double DefaultZoomResolution = 10000;
    private const double SelectedZoomResolution = 200;
    private const double InitialZoomMultiplier = 5;

    // Pin styling
    private const string PinFillColor = "#F44336";
    private const string PinOutlineColor = "#FFFFFF";

    private WritableLayer? _pinLayer;
    private bool _isMapInfoSubscribed;

    public AddProductPage(AddProductViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;

        // Harita konum güncellemelerini dinle
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

            Console.WriteLine("📍 Kategoriler yükleniyor...");
            await _viewModel.LoadCategoriesCommand.ExecuteAsync(null);
            Console.WriteLine("✅ Kategoriler yüklendi");

            Console.WriteLine("📍 Harita başlatılıyor...");
            await InitializeMapAsync();
            Console.WriteLine("✅ Harita başlatıldı");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ OnAppearing Hatası: {ex.Message}");
            Console.WriteLine($"❌ StackTrace: {ex.StackTrace}");

            await DisplayAlert("Hata",
                $"Sayfa yüklenirken bir sorun oluştu: {ex.Message}",
                "Tamam");
        }
    }
    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        WeakReferenceMessenger.Default.Unregister<MapLocationUpdateMessage>(this);

        if (_isMapInfoSubscribed && ProductMap?.Map != null)
        {
            ProductMap.Map.Info -= OnMapInfo;
            _isMapInfoSubscribed = false;
        }
    }

    // Harita tıklama
    private async void OnMapInfo(object? sender, MapInfoEventArgs e)
    {
        if (BindingContext is AddProductViewModel viewModel &&
            e.MapInfo?.WorldPosition != null)
        {
            var worldPosition = e.MapInfo.WorldPosition;

            var lonLat = SphericalMercator.ToLonLat(worldPosition.X, worldPosition.Y);

            UpdatePinOnMap(worldPosition.X, worldPosition.Y);

            viewModel.Latitude = lonLat.lat;
            viewModel.Longitude = lonLat.lon;

            await viewModel.UpdateLocationFromCoordinatesAsync(lonLat.lat, lonLat.lon);

            ProductMap.Map?.Navigator.CenterOn(worldPosition);
            ProductMap.Map?.Navigator.ZoomTo(SelectedZoomResolution);
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

            UpdatePinOnMap(spherical.x, spherical.y);

            ProductMap.Map?.Navigator.CenterOn(new MPoint(spherical.x, spherical.y));
            ProductMap.Map?.Navigator.ZoomTo(SelectedZoomResolution);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Harita konum güncelleme hatası: {ex.Message}");
        }
    }

    private async Task InitializeMapAsync()
    {
        try
        {
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

            // Kullanıcı konumu
            var location = await Geolocation.GetLastKnownLocationAsync();

            if (location != null)
            {
                var spherical = SphericalMercator.FromLonLat(location.Longitude, location.Latitude);

                map.Navigator.CenterOn(new MPoint(spherical.x, spherical.y));
                map.Navigator.ZoomTo(SelectedZoomResolution * InitialZoomMultiplier);
            }
            else
            {
                // Varsayılan Bartın
                var spherical = SphericalMercator.FromLonLat(DefaultLongitude, DefaultLatitude);

                map.Navigator.CenterOn(new MPoint(spherical.x, spherical.y));
                map.Navigator.ZoomTo(DefaultZoomResolution);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Harita başlatma hatası: {ex.Message}");

            if (ProductMap?.Map != null)
            {
                var spherical = SphericalMercator.FromLonLat(DefaultLongitude, DefaultLatitude);

                ProductMap.Map.Navigator.CenterOn(new MPoint(spherical.x, spherical.y));
                ProductMap.Map.Navigator.ZoomTo(DefaultZoomResolution);
            }
        }
    }
}
