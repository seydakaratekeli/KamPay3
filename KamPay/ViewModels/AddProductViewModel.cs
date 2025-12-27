using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using KamPay.Models;
using KamPay.Models.Messages;
using KamPay.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.ApplicationModel;
using KamPay.Helpers;

namespace KamPay.ViewModels
{
    public partial class AddProductViewModel : ObservableObject
    {
        private readonly IReverseGeocodeService _reverseGeocodeService;
        private readonly IProductService _productService;
        private readonly IAuthenticationService _authService;
        private readonly IUserProfileService _userProfileService;
        private readonly ICategoryService _categoryService;
        private readonly IStorageService _storageService;

        private bool _categoriesLoaded = false;
        private static List<Category>? _cachedCategories;

        [ObservableProperty] private double? latitude;
        [ObservableProperty] private double? longitude;
        [ObservableProperty] private string title = "";
        [ObservableProperty] private string description = "";
        [ObservableProperty] private Category? selectedCategory;

        // Enum değerlerini arka planda tutuyoruz
        [ObservableProperty] private ProductCondition selectedCondition;
        [ObservableProperty] private ProductType selectedType;

        [ObservableProperty] private decimal price;
        [ObservableProperty] private string location = "";
        [ObservableProperty] private string exchangePreference = "";
        [ObservableProperty] private bool isLoading;
        [ObservableProperty] private string errorMessage = "";
        [ObservableProperty] private bool showPriceField;
        [ObservableProperty] private bool showExchangeField;
        [ObservableProperty] private bool isForSurpriseBox;
        [ObservableProperty] private string uploadProgress = "";
        [ObservableProperty] private double uploadPercentage;

        // Localization Kısayolu
        private static LocalizationResourceManager Res => LocalizationResourceManager.Instance;

        //  Enum Referans Listeleri (Değişmez)
        private readonly List<ProductCondition> _conditionEnums = Enum.GetValues(typeof(ProductCondition)).Cast<ProductCondition>().ToList();
        private readonly List<ProductType> _typeEnums = Enum.GetValues(typeof(ProductType)).Cast<ProductType>().ToList();

        //  UI için Dinamik String Listeleri
        public List<string> ConditionStrings => _conditionEnums.Select(GetConditionText).ToList();
        public List<string> TypeStrings => _typeEnums.Select(GetTypeText).ToList();

        //  Picker İndeksleri
        [ObservableProperty] private int selectedConditionIndex;
        [ObservableProperty] private int selectedTypeIndex;


        public bool IsDonationTypeSelected => SelectedType == ProductType.Bagis;

        public bool HasLocation => !string.IsNullOrEmpty(Location) &&
                                   Location != Res["GettingLocation"] &&
                                   Latitude.HasValue &&
                                   Longitude.HasValue;

        public ObservableCollection<Category> Categories { get; } = new();
        public ObservableCollection<string> ImagePaths { get; } = new();

        public AddProductViewModel(
            IProductService productService,
            IAuthenticationService authService,
            IUserProfileService userProfileService,
            IStorageService storageService,
            ICategoryService categoryService,
            IReverseGeocodeService reverseGeocodeService)
        {
            _productService = productService;
            _authService = authService;
            _userProfileService = userProfileService;
            _categoryService = categoryService;
            _reverseGeocodeService = reverseGeocodeService;
            _storageService = storageService;

            // Varsayılan Değerler ve İndeksleri Ayarla
            SelectedCondition = ProductCondition.Iyi;
            SelectedType = ProductType.Satis;

            // İndeksleri Enum değerlerine göre eşle
            SelectedTypeIndex = _typeEnums.IndexOf(SelectedType);
            SelectedConditionIndex = _conditionEnums.IndexOf(SelectedCondition);

            ShowPriceField = true;
            ShowExchangeField = false;

            LoadCachedCategories();

            //  DİL DEĞİŞİMİNİ DİNLE
            LocalizationResourceManager.Instance.PropertyChanged += (sender, e) =>
            {
                // String Listelerini Yenile
                OnPropertyChanged(nameof(ConditionStrings));
                OnPropertyChanged(nameof(TypeStrings));

                // Seçili indeksleri tetikle (UI güncellemesi için)
                OnPropertyChanged(nameof(SelectedConditionIndex));
                OnPropertyChanged(nameof(SelectedTypeIndex));

                // Kategori listesini de yenile (isimlerin çevrilmesi için)
                if (Categories.Any())
                {
                    var temp = Categories.ToList();
                    Categories.Clear();
                    foreach (var item in temp) Categories.Add(item);

                    // Seçili kategoriyi koru
                    if (SelectedCategory != null)
                    {
                        var currentId = SelectedCategory.CategoryId;
                        SelectedCategory = Categories.FirstOrDefault(c => c.CategoryId == currentId);
                    }
                }
            };
        }

        //  İndeks Değişince Enum'ı Güncelle
        partial void OnSelectedTypeIndexChanged(int value)
        {
            if (value >= 0 && value < _typeEnums.Count)
            {
                SelectedType = _typeEnums[value];
            }
        }

        partial void OnSelectedConditionIndexChanged(int value)
        {
            if (value >= 0 && value < _conditionEnums.Count)
            {
                SelectedCondition = _conditionEnums[value];
            }
        }

        //  Enum Değişince Görünürlük Ayarlarını Yap
        partial void OnSelectedTypeChanged(ProductType value)
        {
            ShowPriceField = value == ProductType.Satis;
            ShowExchangeField = value == ProductType.Takas;
            OnPropertyChanged(nameof(IsDonationTypeSelected));

            if (value != ProductType.Satis) Price = 0;
            if (value != ProductType.Bagis) IsForSurpriseBox = false;

            // Eğer kod tarafından Enum değiştirilirse (örn: temizle butonuna basınca), indeksi de güncelle
            var index = _typeEnums.IndexOf(value);
            if (SelectedTypeIndex != index) SelectedTypeIndex = index;
        }

        partial void OnSelectedConditionChanged(ProductCondition value)
        {
            var index = _conditionEnums.IndexOf(value);
            if (SelectedConditionIndex != index) SelectedConditionIndex = index;
        }

        //  Çeviri Yardımcı Metotları
        private string GetConditionText(ProductCondition condition)
        {
            return condition switch
            {
                ProductCondition.YeniGibi => Res["ConditionLikeNew"],
                ProductCondition.CokIyi => Res["ConditionVeryGood"],
                ProductCondition.Iyi => Res["ConditionGood"],
                ProductCondition.Orta => Res["ConditionFair"],
                ProductCondition.Kullanilabilir => Res["ConditionUsable"],
                _ => condition.ToString()
            };
        }

        private string GetTypeText(ProductType type)
        {
            return type switch
            {
                ProductType.Satis => Res["ProductTypeSale"],
                ProductType.Bagis => Res["ProductTypeDonation"],
                ProductType.Takas => Res["ProductTypeExchange"],
                _ => type.ToString()
            };
        }

        private void LoadCachedCategories()
        {
            if (_cachedCategories != null && _cachedCategories.Any())
            {
                Categories.Clear();
                foreach (var category in _cachedCategories)
                {
                    Categories.Add(category);
                }
                SelectedCategory = Categories.FirstOrDefault();
                _categoriesLoaded = true;
            }
        }

        [RelayCommand]
        private async Task UseCurrentLocationAsync()
        {
            if (IsLoading) return;
            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;
                Location = Res["GettingLocation"];
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
                    await Shell.Current.DisplayAlert(Res["PermissionRequired"], Res["LocationPermissionMessage"], Res["Ok"]);
                    return;
                }

                var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(5));
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(6));
                var deviceLocation = await Geolocation.GetLocationAsync(request, cts.Token);

                if (deviceLocation != null)
                {
                    Latitude = deviceLocation.Latitude;
                    Longitude = deviceLocation.Longitude;

                    WeakReferenceMessenger.Default.Send(new MapLocationUpdateMessage(deviceLocation.Latitude, deviceLocation.Longitude));
                    await UpdateLocationFromCoordinatesAsync(deviceLocation.Latitude, deviceLocation.Longitude);
                }
                else
                {
                    Location = Res["LocationError"];
                    OnPropertyChanged(nameof(HasLocation));
                }
            }
            catch (FeatureNotSupportedException)
            {
                Location = Res["LocationNotSupported"];
                OnPropertyChanged(nameof(HasLocation));
            }
            catch (PermissionException)
            {
                Location = Res["LocationPermissionDenied"];
                OnPropertyChanged(nameof(HasLocation));
            }
            catch (Exception ex)
            {
                Location = Res["LocationError"];
                OnPropertyChanged(nameof(HasLocation));
                Console.WriteLine($"❌ Konum Hatası: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task UpdateLocationFromCoordinatesAsync(double latitude, double longitude)
        {
            try
            {
                var location = new Location(latitude, longitude);
                var address = await _reverseGeocodeService.GetAddressForLocation(location);
                Location = address;
                OnPropertyChanged(nameof(HasLocation));
            }
            catch (Exception)
            {
                Location = $"{latitude:F4}, {longitude:F4}";
                OnPropertyChanged(nameof(HasLocation));
            }
        }

        [RelayCommand]
        private async Task LoadCategoriesAsync()
        {
            if (_categoriesLoaded && Categories.Any()) return;
            if (IsLoading) return;

            try
            {
                IsLoading = true;
                var categoryList = await _categoryService.GetCategoriesAsync();

                if (categoryList != null)
                {
                    Categories.Clear();
                    foreach (var category in categoryList)
                    {
                        Categories.Add(category);
                    }
                    _cachedCategories = categoryList.ToList();
                    _categoriesLoaded = true;

                    if (Categories.Any()) SelectedCategory = Categories.First();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Kategoriler yüklenemedi: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task PickImagesAsync()
        {
            try
            {
                if (ImagePaths.Count >= 5)
                {
                    await Application.Current!.MainPage!.DisplayAlert(Res["Warning"], Res["MaxImagesWarning"], Res["Ok"]);
                    return;
                }

                var photos = await MediaPicker.PickPhotoAsync(new MediaPickerOptions { Title = "Ürün Görseli Seçin" });

                if (photos != null)
                {
                    ImagePaths.Add(photos.FullPath);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"{Res["ImageSelectionError"]}: {ex.Message}";
            }
        }

        [RelayCommand]
        private void RemoveImage(string imagePath)
        {
            if (ImagePaths.Contains(imagePath))
                ImagePaths.Remove(imagePath);
        }

        [RelayCommand]
        private async Task SaveProductAsync()
        {
            if (IsLoading) return; //
            if (!NetworkHelper.HasInternetConnection())
            {
                await Shell.Current.DisplayAlert(Res["Error"], "Ürün eklemek için internet bağlantısı gereklidir.", Res["Ok"]);
                return;
            }
            try
            {
                IsLoading = true; //
                ErrorMessage = string.Empty; //

                // 1. Önce mevcut kullanıcıyı alıyoruz
                var currentUser = await _authService.GetCurrentUserAsync(); //
                if (currentUser == null)
                {
                    await Shell.Current.DisplayAlert(Res["Error"], Res["SessionNotFoundError"], Res["Ok"]); //
                    return;
                }

                // 2. Hız Sınırı (Rate Limit) kontrolü yapıyoruz
                // RateLimiters.ProductCreation: Saatte 10 ürün ekleme sınırıdır.
                var limitCheck = RateLimiters.ProductCreation.CheckLimit(currentUser.UserId); //
                if (!limitCheck.IsAllowed)
                {
                    await Shell.Current.DisplayAlert(Res["Error"], limitCheck.Message, Res["Ok"]); //
                    return;
                }

                // 3. Temel Alan Validasyonları
                if (string.IsNullOrWhiteSpace(Title) || SelectedCategory == null || !ImagePaths.Any())
                {
                    ErrorMessage = Res["MissingFieldsError"]; //
                    await Shell.Current.DisplayAlert(Res["MissingInfo"], ErrorMessage, Res["Ok"]); //
                    return;
                }

                // 4. Konum Validasyonu
                if (Latitude == null || Longitude == null)
                {
                    await Shell.Current.DisplayAlert(Res["MissingInfo"], Res["MissingLocationError"], Res["Ok"]); //
                    return;
                }

                // 5. Kayıt ve Yükleme İşlemleri Başlıyor
                UploadProgress = Res["SavingProduct"]; //
                UploadPercentage = 0; //

                var productId = Guid.NewGuid().ToString(); //
                var product = new Product //
                {
                    ProductId = productId,
                    Title = InputSanitizer.SanitizeText(this.Title.Trim()),
                    Description = InputSanitizer.SanitizeText(this.Description.Trim()),
                    CategoryId = SelectedCategory.CategoryId,
                    CategoryName = SelectedCategory.Name,
                    Condition = this.SelectedCondition,
                    Type = this.SelectedType,
                    Price = this.Price,
                    Location = this.Location?.Trim() ?? "",
                    Latitude = this.Latitude,
                    Longitude = this.Longitude,
                    UserId = currentUser.UserId,
                    UserName = currentUser.FullName,
                    UserEmail = currentUser.Email,
                    UserPhotoUrl = currentUser.ProfileImageUrl,
                    ExchangePreference = this.ExchangePreference?.Trim() ?? "",
                    IsForSurpriseBox = this.IsForSurpriseBox,
                    IsActive = true,
                    IsSold = false,
                    IsReserved = false,
                    CreatedAt = DateTime.UtcNow,
                    ImageUrls = new List<string>()
                };

                // --- RESİM YÜKLEME HIZ SINIRI KONTROLÜ ---
                // Kullanıcının kalan yükleme hakkını alıyoruz
                int remainingQuota = RateLimiters.ImageUpload.GetRemainingRequests(currentUser.UserId);

                // Eğer yüklenmek istenen resim sayısı kalan kotadan fazlaysa işlemi durdur
                if (remainingQuota < ImagePaths.Count)
                {
                    var resetTime = RateLimiters.ImageUpload.GetResetTime(currentUser.UserId);
                    var deniedResult = RateLimitResult.Denied(resetTime); // Bekleme süresini içeren mesajı üretir

                    await Shell.Current.DisplayAlert("Sınır Aşıldı",
                        $"Resim yükleme limitine yaklaştınız. {deniedResult.Message}", "Tamam");
                    return;
                }

                // --- YÜKLEME İŞLEMİNİ KAYDET ---
                // Gerçek yükleme döngüsü içinde her başarılı işlem için sayacı tetikliyoruz
                UploadPercentage = 10;
                UploadProgress = Res["UploadingImages"];

                var imageUrls = await Task.Run(async () =>
                {
                    var urls = new List<string>();
                    for (int i = 0; i < Math.Min(ImagePaths.Count, 5); i++)
                    {
                        // IsRequestAllowed çağrısı sayacı 1 artırır
                        if (RateLimiters.ImageUpload.IsRequestAllowed(currentUser.UserId))
                        {
                            var result = await _storageService.UploadProductImageAsync(ImagePaths[i], productId, i);
                            if (result.Success && result.Data != null) urls.Add(result.Data);
                        }
                    }
                    return urls;
                });

                UploadPercentage = 60; //

                if (!imageUrls.Any())
                {
                    throw new Exception(Res["ImagesUploadError"]); //
                }

                product.ImageUrls = imageUrls; //
                product.ThumbnailUrl = imageUrls.First(); //

                UploadProgress = Res["SavingProduct"]; //
                UploadPercentage = 80; //

                var saveResult = await _productService.SaveProductDirectlyAsync(product); //

                if (!saveResult.Success)
                {
                    throw new Exception(saveResult.Message); //
                }

                UploadPercentage = 90; //

                // Puan ekleme
                _ = Task.Run(async () =>
                {
                    await _userProfileService.AddPointsForAction(currentUser.UserId, UserAction.AddProduct);
                });

                UploadPercentage = 100; //
                UploadProgress = Res["Completed"]; //

                await Shell.Current.DisplayAlert(Res["Success"], Res["ProductAddedSuccess"], Res["Ok"]); //

                ClearForm(); //
                await Shell.Current.GoToAsync(".."); //
            }
            catch (Exception ex)
            {
                ErrorMessage = $"{Res["Error"]}: {ex.Message}"; //
                await Shell.Current.DisplayAlert(Res["Error"], ErrorMessage, Res["Ok"]); //
            }
            finally
            {
                IsLoading = false; //
                UploadProgress = string.Empty; //
                UploadPercentage = 0; //
            }
        }

        [RelayCommand]
        private async Task CancelAsync()
        {
            var confirm = await Application.Current!.MainPage!.DisplayAlert(
                Res["Cancel"],
                Res["ConfirmCancelMessage"],
                Res["Yes"],
                Res["No"]
            );

            if (confirm)
            {
                ClearForm();
                await Shell.Current.GoToAsync("..");
            }
        }

        private void ClearForm()
        {
            Title = string.Empty;
            Description = string.Empty;
            Price = 0;
            Location = string.Empty;
            ExchangePreference = string.Empty;
            ImagePaths.Clear();
            ErrorMessage = string.Empty;
            Latitude = null;
            Longitude = null;
            IsForSurpriseBox = false;

            if (Categories.Any()) SelectedCategory = Categories.First();
            SelectedCondition = ProductCondition.Iyi;
            SelectedType = ProductType.Satis;
            // İndeksleri de sıfırla
            SelectedConditionIndex = _conditionEnums.IndexOf(SelectedCondition);
            SelectedTypeIndex = _typeEnums.IndexOf(SelectedType);
        }
    }
}

