using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using KamPay.Models;
using KamPay.Models.Messages;
using KamPay.Services;
using KamPay.Services.Products;
using System.Collections.ObjectModel;

namespace KamPay.ViewModels
{
    [QueryProperty(nameof(ProductId), "productId")]
    public partial class EditProductViewModel : ObservableObject
    {
        private readonly IProductService _productService;
        private readonly IReverseGeocodeService _reverseGeocodeService;
        private string _productId = string.Empty;

        [ObservableProperty]
        private string title = string.Empty;

        [ObservableProperty]
        private string description = string.Empty;

        [ObservableProperty]
        private Category? selectedCategory;

        [ObservableProperty]
        private ProductCondition selectedCondition;

        [ObservableProperty]
        private ProductType selectedType;

        [ObservableProperty]
        private decimal price;

        [ObservableProperty]
        private string location = string.Empty;

        [ObservableProperty]
        private string exchangePreference = string.Empty;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private string errorMessage = string.Empty;

        [ObservableProperty]
        private bool showPriceField;

        [ObservableProperty]
        private bool showExchangeField;

        [ObservableProperty]
        private double? latitude;

        [ObservableProperty]
        private double? longitude;

        // HasLocation property - checks if a valid location is set
        public bool HasLocation => !string.IsNullOrEmpty(Location) &&
                                   Location != "Konum alÄ±nÄ±yor..." &&
                                   Latitude.HasValue &&
                                   Longitude.HasValue;

        public ObservableCollection<Category> Categories { get; } = new();
        public ObservableCollection<string> ImagePaths { get; } = new();
        public List<ProductCondition> Conditions => Enum.GetValues(typeof(ProductCondition)).Cast<ProductCondition>().ToList();
        public List<ProductType> ProductTypes => Enum.GetValues(typeof(ProductType)).Cast<ProductType>().ToList();

        public string ProductId
        {
            get => _productId;
            set
            {
                _productId = value;
                if (!string.IsNullOrEmpty(_productId))
                {
                    LoadProductForEdit();
                }
            }
        }

        public EditProductViewModel(IProductService productService, IReverseGeocodeService reverseGeocodeService)
        {
            _productService = productService;
            _reverseGeocodeService = reverseGeocodeService;
        }

        private async void LoadProductForEdit()
        {
            IsLoading = true;
            await LoadCategoriesAsync();
            var result = await _productService.GetProductByIdAsync(ProductId);
            if (result.Success && result.Data != null)
            {
                var product = result.Data;
                Title = product.Title;
                Description = product.Description;
                SelectedCategory = Categories.FirstOrDefault(c => c.CategoryId == product.CategoryId) ?? Categories.FirstOrDefault();
                SelectedCondition = product.Condition;
                SelectedType = product.Type;
                Price = product.Price;
                Location = product.Location;
                ExchangePreference = product.ExchangePreference ?? string.Empty;
                Latitude = product.Latitude;
                Longitude = product.Longitude;

                ShowPriceField = product.Type == ProductType.Satis;
                ShowExchangeField = product.Type == ProductType.Takas;

                ImagePaths.Clear();
                foreach (var imageUrl in product.ImageUrls)
                {
                    ImagePaths.Add(imageUrl);
                }

                OnPropertyChanged(nameof(HasLocation));
            }
            else
            {
                ErrorMessage = "DÃ¼zenlenecek Ã¼rÃ¼n yÃ¼klenemedi.";
            }
            IsLoading = false;
        }

        partial void OnSelectedTypeChanged(ProductType value)
        {
            ShowPriceField = value == ProductType.Satis;
            ShowExchangeField = value == ProductType.Takas;
            if (value != ProductType.Satis) Price = 0;
        }

        partial void OnLocationChanged(string value)
        {
            OnPropertyChanged(nameof(HasLocation));
        }

        partial void OnLatitudeChanged(double? value)
        {
            OnPropertyChanged(nameof(HasLocation));
        }

        partial void OnLongitudeChanged(double? value)
        {
            OnPropertyChanged(nameof(HasLocation));
        }

        private async Task LoadCategoriesAsync()
        {
            var result = await _productService.GetCategoriesAsync();
            if (result.Success && result.Data != null)
            {
                Categories.Clear();
                foreach (var cat in result.Data) Categories.Add(cat);
            }
        }

        // Konum alma komutu - Harita ile entegre
        [RelayCommand]
        private async Task UseCurrentLocationAsync()
        {
            if (IsLoading) return;
            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;
                Location = "Konum alÄ±nÄ±yor...";
                OnPropertyChanged(nameof(HasLocation));

                var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                }

                if (status != PermissionStatus.Granted)
                {
                    Location = string.Empty;
                    OnPropertyChanged(nameof(HasLocation));
                    await Shell.Current.DisplayAlert("Ä°zin Gerekli", "Konum almak iÃ§in izin vermeniz gerekmektedir.", "Tamam");
                    return;
                }

                var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(5));
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(6));
                var deviceLocation = await Geolocation.GetLocationAsync(request, cts.Token);

                if (deviceLocation != null)
                {
                    Latitude = deviceLocation.Latitude;
                    Longitude = deviceLocation.Longitude;

                    // Harita gÃ¼ncelleme mesajÄ± gÃ¶nder
                    WeakReferenceMessenger.Default.Send(new MapLocationUpdateMessage(
                        deviceLocation.Latitude,
                        deviceLocation.Longitude));

                    await UpdateLocationFromCoordinatesAsync(deviceLocation.Latitude, deviceLocation.Longitude);
                }
                else
                {
                    Location = "Konum alÄ±namadÄ±. GPS'inizi kontrol edin.";
                    OnPropertyChanged(nameof(HasLocation));
                }
            }
            catch (FeatureNotSupportedException)
            {
                Location = "Konum servisi desteklenmiyor.";
                OnPropertyChanged(nameof(HasLocation));
            }
            catch (PermissionException)
            {
                Location = "Konum izni verilmedi.";
                OnPropertyChanged(nameof(HasLocation));
            }
            catch (Exception ex)
            {
                Location = "Konum alÄ±nÄ±rken hata oluÅŸtu.";
                OnPropertyChanged(nameof(HasLocation));
                KamPay.Helpers.AppLogger.DebugLog($"âŒ Konum HatasÄ±: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // Koordinatlardan adres Ã§Ã¶zÃ¼mleme
        public async Task UpdateLocationFromCoordinatesAsync(double latitude, double longitude)
        {
            try
            {
                var location = new Location(latitude, longitude);
                var address = await _reverseGeocodeService.GetAddressForLocation(location);
                Location = address;
                OnPropertyChanged(nameof(HasLocation));
            }
            catch (Exception ex)
            {
                Location = $"{latitude:F4}, {longitude:F4}";
                OnPropertyChanged(nameof(HasLocation));
                KamPay.Helpers.AppLogger.DebugLog($"Adres Ã§Ã¶zÃ¼mleme hatasÄ±: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task SaveProductAsync()
        {
            IsLoading = true;
            var request = new ProductRequest
            {
                Title = Title,
                Description = Description,
                CategoryId = SelectedCategory?.CategoryId,
                Condition = SelectedCondition,
                Type = SelectedType,
                Price = Price,
                Location = Location,
                Latitude = Latitude,
                Longitude = Longitude,
                ExchangePreference = ExchangePreference,
                ImagePaths = ImagePaths.ToList()
            };

            var result = await _productService.UpdateProductAsync(ProductId, request);

            if (result.Success)
            {
                await Application.Current!.MainPage!.DisplayAlert("BaÅŸarÄ±lÄ±", "ÃœrÃ¼n gÃ¼ncellendi.", "Tamam");
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                ErrorMessage = result.Message ?? "ÃœrÃ¼n gÃ¼ncellenemedi.";
            }
            IsLoading = false;
        }

        [RelayCommand]
        private async Task CancelAsync()
        {
            await Shell.Current.GoToAsync("..");
        }

        [RelayCommand]
        private async Task PickImagesAsync()
        {
            try
            {
                var photos = await MediaPicker.PickPhotoAsync(new MediaPickerOptions
                {
                    Title = "ÃœrÃ¼n GÃ¶rseli SeÃ§in"
                });

                if (photos != null)
                {
                    // Maksimum gÃ¶rsel sayÄ±sÄ± kontrolÃ¼
                    if (ImagePaths.Count >= 5)
                    {
                        await Application.Current!.MainPage!.DisplayAlert(
                            "UyarÄ±",
                            "En fazla 5 gÃ¶rsel ekleyebilirsiniz",
                            "Tamam"
                        );
                        return;
                    }

                    // GÃ¶rseli listeye ekle
                    ImagePaths.Add(photos.FullPath);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"GÃ¶rsel seÃ§ilirken hata oluÅŸtu: {ex.Message}";
            }
        }

        [RelayCommand]
        private void RemoveImage(string imagePath)
        {
            if (ImagePaths.Contains(imagePath))
            {
                ImagePaths.Remove(imagePath);
            }
        }

        //  SeÃ§ili gÃ¶rseli tam ekran Ã¶nizle
        [RelayCommand]
        private async Task PreviewImageAsync(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath)) return;
            
            // Local file path ise file:// protocol ekle
            var imageUrl = imagePath.StartsWith("http") 
                ? imagePath 
                : $"file://{imagePath}";
            
            await Shell.Current.GoToAsync($"ImageViewerPage?photoUrl={Uri.EscapeDataString(imageUrl)}");
        }
    }
}
