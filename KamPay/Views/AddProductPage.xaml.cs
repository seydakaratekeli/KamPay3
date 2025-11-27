using KamPay.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using KamPay.Models.Messages;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;

namespace KamPay.Views;

public partial class AddProductPage : ContentPage
{
    private readonly AddProductViewModel _viewModel;
    
    // Default location (Ankara, Turkey center)
    private static readonly Location DefaultLocation = new Location(39.9334, 32.8597);
    private static readonly double DefaultZoomKilometers = 100;

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
    }

    // Harita tıklama olayı
    private async void OnMapClicked(object sender, MapClickedEventArgs e)
    {
        if (BindingContext is AddProductViewModel viewModel)
        {
            var position = e.Location;
            
            // Pin'i güncelle
            ProductMap.Pins.Clear();
            var pin = new Pin
            {
                Label = "Seçili Konum",
                Type = PinType.Place,
                Location = position
            };
            ProductMap.Pins.Add(pin);
            
            // ViewModel'i güncelle
            viewModel.Latitude = position.Latitude;
            viewModel.Longitude = position.Longitude;
            
            // Adres çözümleme (reverse geocoding)
            await viewModel.UpdateLocationFromCoordinatesAsync(position.Latitude, position.Longitude);
            
            // Haritayı seçilen noktaya ortala
            var mapSpan = MapSpan.FromCenterAndRadius(position, Distance.FromKilometers(0.5));
            ProductMap.MoveToRegion(mapSpan);
        }
    }

    // Harita konumunu güncelle (mesaj ile)
    private void UpdateMapLocation(double latitude, double longitude)
    {
        try
        {
            var position = new Location(latitude, longitude);
            
            // Pin'i güncelle
            ProductMap.Pins.Clear();
            var pin = new Pin
            {
                Label = "Mevcut Konum",
                Type = PinType.Place,
                Location = position
            };
            ProductMap.Pins.Add(pin);
            
            // Haritayı konuma götür
            var mapSpan = MapSpan.FromCenterAndRadius(position, Distance.FromKilometers(0.5));
            ProductMap.MoveToRegion(mapSpan);
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
            var location = await Geolocation.GetLastKnownLocationAsync();
            
            if (location != null)
            {
                var position = new Location(location.Latitude, location.Longitude);
                var mapSpan = MapSpan.FromCenterAndRadius(position, Distance.FromKilometers(1));
                ProductMap.MoveToRegion(mapSpan);
            }
            else
            {
                // Varsayılan konum
                var mapSpan = MapSpan.FromCenterAndRadius(DefaultLocation, Distance.FromKilometers(DefaultZoomKilometers));
                ProductMap.MoveToRegion(mapSpan);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Harita başlatma hatası: {ex.Message}");
            
            // Hata durumunda varsayılan konum
            var mapSpan = MapSpan.FromCenterAndRadius(DefaultLocation, Distance.FromKilometers(DefaultZoomKilometers));
            ProductMap.MoveToRegion(mapSpan);
        }
    }
}