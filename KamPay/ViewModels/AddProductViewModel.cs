using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KamPay.Models;
using KamPay.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.ApplicationModel;

namespace KamPay.ViewModels
{
    public partial class AddProductViewModel : ObservableObject
    {
        private readonly IReverseGeocodeService _reverseGeocodeService;
        private readonly IProductService _productService;
        private readonly IAuthenticationService _authService;
        private readonly IUserProfileService _userProfileService;
        private readonly ICategoryService _categoryService;
        private readonly IStorageService _storageService; // 🔥 YENİ: Direct access

        // 🔥 YENİ: Cache flag
        private bool _categoriesLoaded = false;
        private static List<Category> _cachedCategories; // Static cache

        [ObservableProperty]
        private double? latitude;

        [ObservableProperty]
        private double? longitude;

        [ObservableProperty]
        private string title;

        [ObservableProperty]
        private string description;

        [ObservableProperty]
        private Category selectedCategory;

        [ObservableProperty]
        private ProductCondition selectedCondition;

        [ObservableProperty]
        private ProductType selectedType;

        [ObservableProperty]
        private decimal price;

        [ObservableProperty]
        private string location;

        [ObservableProperty]
        private string exchangePreference;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private string errorMessage;

        [ObservableProperty]
        private bool showPriceField;

        [ObservableProperty]
        private bool showExchangeField;

        [ObservableProperty]
        private bool isForSurpriseBox;

        // 🔥 YENİ: Upload progress
        [ObservableProperty]
        private string uploadProgress;

        [ObservableProperty]
        private double uploadPercentage;

        public bool IsDonationTypeSelected => SelectedType == ProductType.Bagis;

        public ObservableCollection<Category> Categories { get; } = new();
        public ObservableCollection<string> ImagePaths { get; } = new();

        public List<ProductCondition> Conditions { get; } = Enum.GetValues(typeof(ProductCondition))
            .Cast<ProductCondition>()
            .ToList();

        public List<ProductType> ProductTypes { get; } = Enum.GetValues(typeof(ProductType))
            .Cast<ProductType>()
            .ToList();

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
            _storageService = storageService; // 🔥 YENİ

            // Varsayılan değerler
            SelectedCondition = ProductCondition.Iyi;
            SelectedType = ProductType.Satis;
            ShowPriceField = true;
            ShowExchangeField = false;

            // 🔥 Kategorileri cache'den yükle
            LoadCachedCategories();
        }

        // 🔥 YENİ: Cache'den hızlı yükleme
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
                Console.WriteLine("✅ Kategoriler cache'den yüklendi");
            }
        }

        partial void OnSelectedTypeChanged(ProductType value)
        {
            ShowPriceField = value == ProductType.Satis;
            ShowExchangeField = value == ProductType.Takas;
            OnPropertyChanged(nameof(IsDonationTypeSelected));

            if (value != ProductType.Satis)
            {
                Price = 0;
            }

            if (value != ProductType.Bagis)
            {
                IsForSurpriseBox = false;
            }
        }

        // 🔥 OPTİMİZE: Konum alma - Daha hızlı timeout
        [RelayCommand]
        private async Task UseCurrentLocationAsync()
        {
            if (IsLoading) return;
            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;
                Location = "Konum alınıyor...";

                var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                }

                if (status != PermissionStatus.Granted)
                {
                    Location = string.Empty;
                    await Shell.Current.DisplayAlert("İzin Gerekli", "Konum almak için izin vermeniz gerekmektedir.", "Tamam");
                    return;
                }

                // 🔥 5 saniye timeout (10'dan düştük)
                var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(5));

                // 🔥 CancellationToken ile timeout kontrolü
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(6));
                var deviceLocation = await Geolocation.GetLocationAsync(request, cts.Token);

                if (deviceLocation != null)
                {
                    Latitude = deviceLocation.Latitude;
                    Longitude = deviceLocation.Longitude;

                    // 🔥 Adres çözümleme arka planda
                    _ = Task.Run(async () =>
                    {
                        var address = await _reverseGeocodeService.GetAddressForLocation(deviceLocation);
                        MainThread.BeginInvokeOnMainThread(() => Location = address);
                    });

                    Location = $"{deviceLocation.Latitude:F2}, {deviceLocation.Longitude:F2}";
                }
                else
                {
                    Location = "Konum alınamadı. GPS'inizi kontrol edin.";
                }
            }
            catch (FeatureNotSupportedException)
            {
                Location = "Konum servisi desteklenmiyor.";
            }
            catch (PermissionException)
            {
                Location = "Konum izni verilmedi.";
            }
            catch (Exception ex)
            {
                Location = "Konum alınırken hata oluştu.";
                Console.WriteLine($"❌ Konum Hatası: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // 🔥 OPTİMİZE: Kategori yükleme - Cache ile
        [RelayCommand]
        private async Task LoadCategoriesAsync()
        {
            // 🔥 Eğer zaten yüklendiyse tekrar yükleme
            if (_categoriesLoaded && Categories.Any())
            {
                Console.WriteLine("✅ Kategoriler zaten yüklü");
                return;
            }

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

                    // 🔥 Static cache'e kaydet
                    _cachedCategories = categoryList.ToList();
                    _categoriesLoaded = true;

                    if (Categories.Any())
                    {
                        SelectedCategory = Categories.First();
                    }

                    Console.WriteLine($"✅ {Categories.Count} kategori yüklendi");
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Kategoriler yüklenemedi: {ex.Message}";
                Console.WriteLine($"❌ Kategori yükleme hatası: {ex.Message}");
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
                // Maksimum görsel sayısı kontrolü
                if (ImagePaths.Count >= 5)
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "Uyarı",
                        "En fazla 5 görsel ekleyebilirsiniz",
                        "Tamam"
                    );
                    return;
                }

                var photos = await MediaPicker.PickPhotoAsync(new MediaPickerOptions
                {
                    Title = "Ürün Görseli Seçin"
                });

                if (photos != null)
                {
                    ImagePaths.Add(photos.FullPath);
                    Console.WriteLine($"✅ Görsel eklendi: {ImagePaths.Count}/5");
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Görsel seçilirken hata oluştu: {ex.Message}";
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

        // 🔥 OPTİMİZE: Paralel resim upload + Progress tracking
        [RelayCommand]
        private async Task SaveProductAsync()
        {
            if (IsLoading) return;

            // Validation
            if (string.IsNullOrWhiteSpace(Title) || SelectedCategory == null || !ImagePaths.Any())
            {
                ErrorMessage = "Lütfen başlık, kategori ve en az bir resim eklediğinizden emin olun.";
                await Shell.Current.DisplayAlert("Eksik Bilgi", ErrorMessage, "Tamam");
                return;
            }

            if (Latitude == null || Longitude == null)
            {
                await Shell.Current.DisplayAlert("Eksik Bilgi", "Lütfen ürün konumu alın.", "Tamam");
                return;
            }

            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;
                UploadProgress = "Ürün kaydediliyor...";
                UploadPercentage = 0;

                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    await Shell.Current.DisplayAlert("Hata", "Oturum bulunamadı.", "Tamam");
                    return;
                }

                // 🔥 1. Ürün nesnesini oluştur (resim URL'leri olmadan)
                var productId = Guid.NewGuid().ToString();
                var product = new Product
                {
                    ProductId = productId,
                    Title = this.Title.Trim(),
                    Description = this.Description.Trim(),
                    CategoryId = SelectedCategory.CategoryId,
                    CategoryName = SelectedCategory.Name,
                    Condition = this.SelectedCondition,
                    Type = this.SelectedType,
                    Price = this.Price,
                    Location = this.Location?.Trim(),
                    Latitude = this.Latitude,
                    Longitude = this.Longitude,
                    UserId = currentUser.UserId,
                    UserName = currentUser.FullName,
                    UserEmail = currentUser.Email,
                    UserPhotoUrl = currentUser.ProfileImageUrl,
                    ExchangePreference = this.ExchangePreference?.Trim(),
                    IsForSurpriseBox = this.IsForSurpriseBox,
                    IsActive = true,
                    IsSold = false,
                    IsReserved = false,
                    CreatedAt = DateTime.UtcNow,
                    ImageUrls = new List<string>() // Boş liste
                };

                UploadPercentage = 10;
                UploadProgress = "Resimler yükleniyor...";

                // 🔥 2. Resimleri PARALEL yükle (Background thread)
                var imageUrls = await Task.Run(async () =>
                {
                    var urls = new List<string>();
                    var uploadTasks = new List<Task<ServiceResult<string>>>();

                    // Tüm upload işlemlerini başlat
                    for (int i = 0; i < Math.Min(ImagePaths.Count, 5); i++)
                    {
                        var imagePath = ImagePaths[i];
                        var task = _storageService.UploadProductImageAsync(imagePath, productId, i);
                        uploadTasks.Add(task);
                    }

                    // Paralel bekle
                    var results = await Task.WhenAll(uploadTasks);

                    // Başarılı URL'leri topla
                    foreach (var result in results)
                    {
                        if (result.Success)
                        {
                            urls.Add(result.Data);
                        }
                    }

                    return urls;
                });

                UploadPercentage = 60;

                if (!imageUrls.Any())
                {
                    throw new Exception("Resimler yüklenemedi.");
                }

                product.ImageUrls = imageUrls;
                product.ThumbnailUrl = imageUrls.First();

                UploadProgress = "Ürün kaydediliyor...";
                UploadPercentage = 80;

                // 🔥 3. Ürünü Firebase'e kaydet
                var saveResult = await _productService.SaveProductDirectlyAsync(product);

                if (!saveResult.Success)
                {
                    throw new Exception(saveResult.Message);
                }

                UploadPercentage = 90;

                // 🔥 4. Puan ekle (arka planda)
                _ = Task.Run(async () =>
                {
                    await _userProfileService.AddPointsForAction(currentUser.UserId, UserAction.AddProduct);
                });

                UploadPercentage = 100;
                UploadProgress = "Tamamlandı!";

                await Shell.Current.DisplayAlert("Başarılı", "Ürününüz başarıyla eklendi!", "Harika!");

                ClearForm();
                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Ürün kaydedilirken hata oluştu: {ex.Message}";
                Console.WriteLine($"❌ SaveProduct hatası: {ex.Message}");
                await Shell.Current.DisplayAlert("Hata", ErrorMessage, "Tamam");
            }
            finally
            {
                IsLoading = false;
                UploadProgress = string.Empty;
                UploadPercentage = 0;
            }
        }

        [RelayCommand]
        private async Task CancelAsync()
        {
            var confirm = await Application.Current.MainPage.DisplayAlert(
                "İptal",
                "Ürün eklemeyi iptal etmek istediğinize emin misiniz?",
                "Evet",
                "Hayır"
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

            if (Categories.Any())
            {
                SelectedCategory = Categories.First();
            }

            SelectedCondition = ProductCondition.Iyi;
            SelectedType = ProductType.Satis;
        }
    }
}