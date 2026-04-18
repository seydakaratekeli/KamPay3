using KamPay.ViewModels;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Projections;
using Mapsui.UI.Maui;
using CommunityToolkit.Mvvm.Messaging;
using KamPay.Models.EventMessages;
using System;

namespace KamPay.Views
{
    public partial class EditProductPage : ContentPage
    {
        private readonly EditProductViewModel _viewModel;
        private bool _isMapInitialized = false;

        public EditProductPage(EditProductViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = viewModel;

            // Register for location updates from the ViewModel
            WeakReferenceMessenger.Default.Register<MapLocationUpdateMessage>(this, (r, m) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    UpdateMapLocation(m.Latitude, m.Longitude);
                });
            });

            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            
            await Task.Delay(300);
            InitializeMap();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            WeakReferenceMessenger.Default.Unregister<MapLocationUpdateMessage>(this);
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if ((e.PropertyName == nameof(EditProductViewModel.Latitude) ||
                 e.PropertyName == nameof(EditProductViewModel.Longitude)) && 
                 _viewModel.Latitude.HasValue && 
                 _viewModel.Longitude.HasValue)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    UpdateMapLocation(_viewModel.Latitude.Value, _viewModel.Longitude.Value);
                });
            }
        }

        private void InitializeMap()
        {
            try
            {
                if (_isMapInitialized) return;

                if (ProductMap?.Map == null)
                {
                    if (ProductMap != null)
                        ProductMap.Map = new Mapsui.Map();
                }

                ProductMap?.Map?.Layers.Clear();
                var tileLayer = Mapsui.Tiling.OpenStreetMap.CreateTileLayer();
                ProductMap?.Map?.Layers.Add(tileLayer);

                if (ProductMap?.Map?.Navigator != null)
                {
                    ProductMap.Map.Navigator.RotationLock = true;
                }

                _isMapInitialized = true;

                if (_viewModel.Latitude.HasValue && _viewModel.Longitude.HasValue)
                {
                    UpdateMapLocation(_viewModel.Latitude.Value, _viewModel.Longitude.Value);
                }
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"Map initialization error: {ex.Message}");
            }
        }

        private void UpdateMapLocation(double lat, double lon)
        {
            if (ProductMap?.Map?.Navigator == null) return;

            try
            {
                var mercator = SphericalMercator.FromLonLat(lon, lat);
                ProductMap.Map.Navigator.CenterOn(new MPoint(mercator.x, mercator.y));
                ProductMap.Map.Navigator.ZoomTo(3);
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"Map update error: {ex.Message}");
            }
        }

        private void OnZoomInClicked(object sender, EventArgs e)
        {
            if (ProductMap?.Map?.Navigator != null)
            {
                ProductMap.Map.Navigator.ZoomIn();
            }
        }

        private void OnZoomOutClicked(object sender, EventArgs e)
        {
            if (ProductMap?.Map?.Navigator != null)
            {
                ProductMap.Map.Navigator.ZoomOut();
            }
        }

        private void OnResetLocationClicked(object sender, EventArgs e)
        {
            if (_viewModel.Latitude.HasValue && _viewModel.Longitude.HasValue)
            {
                UpdateMapLocation(_viewModel.Latitude.Value, _viewModel.Longitude.Value);
            }
        }
    }
}

