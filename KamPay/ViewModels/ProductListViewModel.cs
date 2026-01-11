using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Firebase.Database;
using Firebase.Database.Query;
using Firebase.Database.Streaming;
using KamPay.Helpers;
using KamPay.Models;
using KamPay.Services;
using KamPay.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace KamPay.ViewModels
{
    [QueryProperty(nameof(UserId), "userId")]
    public partial class ProductListViewModel : ObservableObject, IDisposable
    {
        private readonly IProductService _productService;
        private readonly IAuthenticationService _authService;
        private readonly ICategoryService _categoryService;
        private readonly IUserStateService _userStateService;
        private IDisposable? _notificationSubscription;
        private readonly FirebaseClient _firebaseClient = new(Constants.FirebaseRealtimeDbUrl);
        private CancellationTokenSource? _searchCancellationTokenSource;
        private readonly CacheManager<List<Product>> _cacheManager = new();
        private const string CACHE_KEY = "all_products";
        private string? _lastLoadedKey;
        private bool _isLoadingMore;
        
        // ✅ YENİ: Cache boyutu limiti
        private const int MAX_CACHED_PRODUCTS = 100;

        // Tüm ürünlerin tutulduğu ana liste (filtreleme için)
        private List<Product> _allProducts = new();

        private IDisposable? _listener;
        private RealtimeSnapshotService<Product> _loader;

        [ObservableProperty]
        private bool isSkeletonVisible = true;

        // Arayüze bağlanan ve sadece filtrelenmiş ürünleri gösteren liste
        public ObservableCollection<Product> Products { get; } = new();
        public ObservableCollection<Category> Categories { get; } = new();

        #region Observable Properties (Arayüzle İletişim Kuran Özellikler)
        [ObservableProperty] private string userId = string.Empty;
        [ObservableProperty] private bool isLoading = true;
        [ObservableProperty] private bool hasUnreadNotifications;
        [ObservableProperty] private string searchText = string.Empty;
        [ObservableProperty] private Category? selectedCategory;
        [ObservableProperty] private ProductType? selectedType;
        [ObservableProperty] private ProductSortOption selectedSortOption;
        [ObservableProperty] private bool showFilterPanel;
        [ObservableProperty] private string emptyMessage = "Henüz ürün eklenmemiş";
        [ObservableProperty] private bool _isRefreshing;
        #endregion

        //  Enum listesini referans olarak tutuyoruz (Sıralama mantığı için)
        private readonly List<ProductSortOption> _sortOptionEnums = Enum.GetValues(typeof(ProductSortOption)).Cast<ProductSortOption>().ToList();

        //  Arayüzde (Picker) görünecek dinamik metin listesi
        public List<string> SortOptionStrings => _sortOptionEnums.Select(GetSortOptionText).ToList();

        // : Picker'ın SelectedIndex özelliği için
        private int _selectedSortIndex;
        public int SelectedSortIndex
        {
            get => _selectedSortIndex;
            set
            {
                // Değer değiştiyse ve geçerli bir aralıktaysa
                if (SetProperty(ref _selectedSortIndex, value) && value >= 0 && value < _sortOptionEnums.Count)
                {
                    // İndeks değişince asıl Enum değerini güncelle (Bu da ExecuteFiltering'i tetikler)
                    SelectedSortOption = _sortOptionEnums[value];
                }
            }
        }

        public ProductListViewModel(IProductService productService, IAuthenticationService authService, ICategoryService categoryService, IUserStateService userStateService)
        {
            _productService = productService;
            _authService = authService;
            _categoryService = categoryService;
            _userStateService = userStateService;

            // Varsayılan sıralama ayarları
            SelectedSortOption = ProductSortOption.Newest;
            _selectedSortIndex = _sortOptionEnums.IndexOf(ProductSortOption.Newest);

            _loader = new RealtimeSnapshotService<Product>(Constants.FirebaseRealtimeDbUrl);

            _userStateService.UserProfileChanged += OnUserProfileChanged;

            //  DİL DEĞİŞİMİ DİNLEYİCİSİ
            LocalizationResourceManager.Instance.PropertyChanged += (sender, e) =>
            {
                // Dil değiştiğinde listeyi ve arayüzü yenile
                OnPropertyChanged(nameof(SortOptionStrings));
                OnPropertyChanged(nameof(SelectedSortIndex));
                // Filtrelemeyi tekrar uygula (metin tabanlı filtreler varsa diye)
                ExecuteFiltering();
            };

            WeakReferenceMessenger.Default.Register<FavoriteCountChangedMessage>(this, (r, m) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var receivedProduct = m.Value;
                    var productInAll = _allProducts.FirstOrDefault(p => p.ProductId == receivedProduct.ProductId);
                    if (productInAll != null)
                    {
                        productInAll.FavoriteCount = receivedProduct.FavoriteCount;
                        productInAll.ViewCount = receivedProduct.ViewCount;
                    }

                    var productInProducts = Products.FirstOrDefault(p => p.ProductId == receivedProduct.ProductId);
                    if (productInProducts != null)
                    {
                        productInProducts.FavoriteCount = receivedProduct.FavoriteCount;
                        productInProducts.ViewCount = receivedProduct.ViewCount;
                    }
                });
            });
            WeakReferenceMessenger.Default.Register<UnreadGeneralNotificationStatusMessage>(this, (r, m) => { HasUnreadNotifications = m.Value; });

            // ✅ Constructor'da otomatik başlat
            _ = InitializeAsync();
        }

        // ✅ Tek InitializeAsync metodu
        public async Task InitializeAsync()
        {
            await LoadCategoriesAsync();
            StartListeningForNotifications();

            if (string.IsNullOrEmpty(UserId))
            {
                await UltraFastLoadAsync();
            }
        }

        public async Task UltraFastLoadAsync()
        {
            try
            {
                IsSkeletonVisible = true;
                IsLoading = true;

                // 🚀 PERFORMANS OPTİMİZASYONU: Optimize edilmiş service metodunu kullan
                var filter = new ProductFilter
                {
                    OnlyActive = true, // Sadece aktif ürünler
                    SortBy = SelectedSortOption,
                    CategoryId = SelectedCategory?.CategoryId,
                    Type = SelectedType,
                    SearchText = SearchText
                };

                var result = await _productService.GetAllProductsAsync(filter);

                if (result.Success && result.Data != null && result.Data.Any())
                {
                    _allProducts = result.Data;
                    CleanupCacheIfNeeded();
                    ExecuteFiltering();
                }
                else
                {
                    _allProducts.Clear();
                    Products.Clear();
                }

                IsSkeletonVisible = false;
                IsLoading = false;

                // Realtime listener'ı başlat (sadece değişiklikleri dinlemek için)
                _listener = _loader.Listen(Constants.ProductsCollection, evt =>
                {
                    MainThread.BeginInvokeOnMainThread(() => ApplyRealtimeEvent(evt));
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Product UltraFastLoad hata: {ex.Message}");
                IsSkeletonVisible = false;
                IsLoading = false;
            }
        }

        private void ApplyRealtimeEvent(FirebaseEvent<Product> evt)
        {
            var product = evt.Object;
            product.ProductId = evt.Key;

            var existing = _allProducts.FirstOrDefault(p => p.ProductId == product.ProductId);

            if (evt.EventType == FirebaseEventType.InsertOrUpdate)
            {
                if (existing != null)
                {
                    int index = _allProducts.IndexOf(existing);
                    _allProducts[index] = product;
                }
                else
                {
                    _allProducts.Insert(0, product);
                }
            }
            else if (evt.EventType == FirebaseEventType.Delete)
            {
                if (existing != null)
                    _allProducts.Remove(existing);
            }

            ExecuteFiltering();
        }
        
        // ✅ YENİ: Cache cleanup helper method
        private void CleanupCacheIfNeeded()
        {
            if (_allProducts.Count > MAX_CACHED_PRODUCTS)
            {
                // Eski ürünleri temizle (FIFO) - More efficient approach
                _allProducts = _allProducts.Skip(_allProducts.Count - MAX_CACHED_PRODUCTS).ToList();
                Console.WriteLine($"⚠️ Cache limit aşıldı, fazla ürünler temizlendi. Yeni boyut: {_allProducts.Count}");
            }
        }

        #region Veri Yükleme ve Filtreleme Mantığı

        async partial void OnUserIdChanged(string? value)
        {
            _allProducts.Clear();
            Products.Clear();

            if (!string.IsNullOrEmpty(value))
            {
                IsLoading = true;
                EmptyMessage = "Henüz ürün eklemediniz";
                var result = await _productService.GetUserProductsAsync(value);
                if (result.Success && result.Data != null)
                {
                    _allProducts = result.Data;
                    CleanupCacheIfNeeded();
                    ExecuteFiltering();
                }
                IsLoading = false;
            }
            else
            {
                EmptyMessage = "Arama kriterlerinize uygun ürün bulunamadı";
                await UltraFastLoadAsync();
            }
        }

        private void ExecuteFiltering()
        {
            try
            {
                if (!MainThread.IsMainThread)
                {
                    MainThread.BeginInvokeOnMainThread(ExecuteFiltering);
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"🔍 ExecuteFiltering başladı. Toplam ürün: {_allProducts.Count}");

                // 🚀 OPTİMİZASYON: Zaten filtrelenmiş verilerle çalışıyoruz
                // Sadece UI seviyesinde ek filtreler uygulayacağız
                IEnumerable<Product> filtered = _allProducts.Where(p => p.IsActive && !p.IsSold);

                // Kategori filtresi (eğer değiştirilmişse)
                if (SelectedCategory != null && !string.IsNullOrEmpty(SelectedCategory.CategoryId))
                {
                    filtered = filtered.Where(p => p.CategoryId == SelectedCategory.CategoryId);
                }

                // Arama metni (eğer varsa)
                if (!string.IsNullOrEmpty(SearchText))
                {
                    var searchLower = SearchText.ToLowerInvariant();
                    filtered = filtered.Where(p =>
                        p.Title.ToLowerInvariant().Contains(searchLower) ||
                        p.Description.ToLowerInvariant().Contains(searchLower));
                }

                // Tip filtresi (eğer varsa)
                if (SelectedType.HasValue)
                {
                    filtered = filtered.Where(p => p.Type == SelectedType.Value);
                }

                // Sıralama
                filtered = SelectedSortOption switch
                {
                    ProductSortOption.Oldest => filtered.OrderBy(p => p.CreatedAt),
                    ProductSortOption.PriceAsc => filtered.OrderBy(p => p.Price),
                    ProductSortOption.PriceDesc => filtered.OrderByDescending(p => p.Price),
                    ProductSortOption.MostViewed => filtered.OrderByDescending(p => p.ViewCount),
                    ProductSortOption.MostFavorited => filtered.OrderByDescending(p => p.FavoriteCount),
                    _ => filtered.OrderByDescending(p => p.CreatedAt), // Newest
                };

                var filteredList = filtered.ToList();
                UpdateProductsCollection(filteredList);

                IsLoading = false;
                EmptyMessage = Products.Any() ? string.Empty : "Arama kriterlerinize uygun ürün bulunamadı";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ ExecuteFiltering hatası: {ex.Message}");
                IsLoading = false;
            }
        }

        private void UpdateProductsCollection(List<Product> newProducts)
        {
            var toRemove = Products
                .Where(p => !newProducts.Any(np => np.ProductId == p.ProductId))
                .ToList();

            foreach (var item in toRemove)
            {
                Products.Remove(item);
            }

            for (int i = 0; i < newProducts.Count; i++)
            {
                var newProduct = newProducts[i];
                var existingIndex = -1;

                for (int j = 0; j < Products.Count; j++)
                {
                    if (Products[j].ProductId == newProduct.ProductId)
                    {
                        existingIndex = j;
                        break;
                    }
                }

                if (existingIndex >= 0)
                {
                    if (existingIndex != i)
                    {
                        Products.Move(existingIndex, i);
                    }
                    Products[i] = newProduct;
                }
                else
                {
                    if (i < Products.Count)
                    {
                        Products.Insert(i, newProduct);
                    }
                    else
                    {
                        Products.Add(newProduct);
                    }
                }
            }
        }

        async partial void OnSearchTextChanged(string? value)
        {
            _searchCancellationTokenSource?.Cancel();
            _searchCancellationTokenSource = new CancellationTokenSource();

            try
            {
                await Task.Delay(500, _searchCancellationTokenSource.Token); // 300ms -> 500ms (Daha az API çağrısı)
                
                // 🚀 OPTİMİZASYON: Arama için sunucu filtrelemesi kullan
                if (!string.IsNullOrEmpty(SearchText) && SearchText.Length >= 2)
                {
                    await ReloadWithFilterAsync();
                }
                else
                {
                    ExecuteFiltering(); // Lokal filtreleme
                }
            }
            catch (TaskCanceledException)
            {
                Debug.WriteLine("Arama ertelendi (debounced).");
            }
        }

        partial void OnSelectedCategoryChanged(Category? value)
        {
            // Kategori değiştiğinde sunucudan yeni veri çek
            _ = ReloadWithFilterAsync();
        }

        // Seçilen Enum değiştiğinde indeksi de güncelle (Kod tarafından değiştirilirse)
        partial void OnSelectedSortOptionChanged(ProductSortOption value)
        {
            var index = _sortOptionEnums.IndexOf(value);
            if (SelectedSortIndex != index)
            {
                SelectedSortIndex = index;
            }
            ExecuteFiltering(); // Sıralama için lokal filtreleme yeterli
        }

        partial void OnSelectedTypeChanged(ProductType? value)
        {
            // Tip değiştiğinde sunucudan yeni veri çek
            _ = ReloadWithFilterAsync();
        }

        // 🆕 YENİ METOD: Filtrelerle yeniden yükleme
        private async Task ReloadWithFilterAsync()
        {
            if (IsLoading) return;

            try
            {
                IsLoading = true;

                var filter = new ProductFilter
                {
                    OnlyActive = true,
                    SortBy = SelectedSortOption,
                    CategoryId = SelectedCategory?.CategoryId,
                    Type = SelectedType,
                    SearchText = SearchText
                };

                var result = await _productService.GetAllProductsAsync(filter);

                if (result.Success && result.Data != null)
                {
                    _allProducts = result.Data;
                    CleanupCacheIfNeeded();
                    ExecuteFiltering();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ ReloadWithFilter hatası: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        #endregion

        #region Komutlar
        [RelayCommand]
        private void ToggleFilterPanel() => ShowFilterPanel = !ShowFilterPanel;

        [RelayCommand]
        private void ApplyFilters()
        {
            ShowFilterPanel = false;
            ExecuteFiltering();
        }

        [RelayCommand]
        private void SetProductType(string typeString)
        {
            if (string.IsNullOrEmpty(typeString))
            {
                SelectedType = null;
            }
            else if (Enum.TryParse(typeof(ProductType), typeString, out var result))
            {
                SelectedType = (ProductType)result;
            }
        }

        [RelayCommand]
        private async Task RefreshProductsAsync()
        {
            IsRefreshing = true;
            try
            {
                _cacheManager.Clear();
                _lastLoadedKey = null;
                await LoadProductsAsync();
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        [RelayCommand]
        private async Task LoadProductsAsync()
        {
            if (IsLoading) return;

            try
            {
                // Mevcut kullanıcıyı al (Tanımlayıcı olarak UserId kullanacağız)
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser != null)
                {
                    // Arama Hız Sınırı Kontrolü (60 arama / dakika)
                    var limitCheck = RateLimiters.Search.CheckLimit(currentUser.UserId);
                    if (!limitCheck.IsAllowed)
                    {
                        await Shell.Current.DisplayAlert("Hata", limitCheck.Message, "Tamam");
                        return;
                    }
                }

                IsLoading = true;

                if (_cacheManager.TryGet(CACHE_KEY, out var cachedProducts) && cachedProducts != null)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        Products.Clear();
                        foreach (var p in cachedProducts) Products.Add(p);
                    });
                    return;
                }

                var productsResult = await Task.Run(async () =>
                {
                    var filter = new ProductFilter
                    {
                        SearchText = SearchText,
                        CategoryId = SelectedCategory?.CategoryId,
                        OnlyActive = true,
                        SortBy = ProductSortOption.Newest
                    };

                    return await _productService.GetProductsPagedAsync(20, null, filter);
                });

                if (productsResult.Success && productsResult.Data != null)
                {
                    _cacheManager.Set(CACHE_KEY, productsResult.Data, TimeSpan.FromMinutes(3));

                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        Products.Clear();
                        foreach (var product in productsResult.Data)
                        {
                            Products.Add(product);
                        }
                    });

                    if (productsResult.Data.Any())
                        _lastLoadedKey = productsResult.Data.Last().ProductId;
                }
            }
            catch (Exception ex)
            {
                EmptyMessage = $"Hata: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task LoadMoreProductsAsync()
        {
            if (_isLoadingMore || string.IsNullOrEmpty(_lastLoadedKey)) return;

            try
            {
                _isLoadingMore = true;

                var moreProducts = await Task.Run(async () =>
                {
                    var filter = new ProductFilter
                    {
                        SearchText = SearchText,
                        CategoryId = SelectedCategory?.CategoryId,
                        OnlyActive = true
                    };

                    return await _productService.GetProductsPagedAsync(20, _lastLoadedKey, filter);
                });

                if (moreProducts.Success && moreProducts.Data != null && moreProducts.Data.Any())
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        foreach (var product in moreProducts.Data)
                        {
                            Products.Add(product);
                        }
                    });

                    _lastLoadedKey = moreProducts.Data.Last().ProductId;
                }
            }
            finally
            {
                _isLoadingMore = false;
            }
        }

        [RelayCommand]
        private void ClearFilters()
        {
            SelectedCategory = Categories.FirstOrDefault();
            SelectedType = null;
            SelectedSortOption = ProductSortOption.Newest;
            SearchText = string.Empty;
        }

        [RelayCommand]
        private void CategoryTapped(Category category)
        {
            SelectedCategory = category;
        }

        [RelayCommand]
        private async Task GoToSurpriseBox()
        {
            await Shell.Current.GoToAsync(nameof(SurpriseBoxPage));
        }

        [RelayCommand]
        private async Task ProductTappedAsync(Product product)
        {
            if (product is null || product.IsSold) return;
            await Shell.Current.GoToAsync($"{nameof(ProductDetailPage)}?ProductId={product.ProductId}");
        }

        [RelayCommand]
        private async Task GoToNotificationsAsync()
        {
            if (HasUnreadNotifications) HasUnreadNotifications = false;
            await Shell.Current.GoToAsync(nameof(NotificationsPage));
        }

        [RelayCommand]
        private async Task GoToAddProductAsync()
        {
            await Shell.Current.GoToAsync(nameof(AddProductPage));
        }
        #endregion

        #region Yardımcı Metotlar
        private async Task LoadCategoriesAsync()
        {
            var categoryList = await _categoryService.GetCategoriesAsync();

            if (categoryList != null)
            {
                Categories.Clear();
                foreach (var category in categoryList)
                {
                    Categories.Add(category);
                }
            }
        }

        private async void StartListeningForNotifications()
        {
            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser == null) return;
            _notificationSubscription?.Dispose();
            var initialCheck = await _firebaseClient.Child(Constants.NotificationsCollection).OrderBy("UserId").EqualTo(currentUser.UserId).OnceAsync<Notification>();
            if (initialCheck.Any(n => !n.Object.IsRead && n.Object.Type != NotificationType.NewMessage))
            {
                HasUnreadNotifications = true;
            }
            _notificationSubscription = _firebaseClient.Child(Constants.NotificationsCollection).OrderBy("UserId").EqualTo(currentUser.UserId).AsObservable<Notification>().Where(e => e.EventType == Firebase.Database.Streaming.FirebaseEventType.InsertOrUpdate).Subscribe(entry =>
            {
                if (entry.Object != null && !entry.Object.IsRead && entry.Object.Type != NotificationType.NewMessage)
                {
                    MainThread.BeginInvokeOnMainThread(() => { HasUnreadNotifications = true; });
                }
            });
        }

        private void OnUserProfileChanged(object? sender, User updatedUser)
        {
            if (updatedUser == null) return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                UpdateUserInfoInProducts(_allProducts, updatedUser);
                UpdateUserInfoInProducts(Products, updatedUser);
            });
        }

        private void UpdateUserInfoInProducts(IEnumerable<Product> products, User updatedUser)
        {
            foreach (var product in products.Where(p => p.UserId == updatedUser.UserId))
            {
                product.UserName = updatedUser.FullName;
                product.UserPhotoUrl = updatedUser.ProfileImageUrl;
            }
        }

        //  Enum -> Localized String Çevirici
        private string GetSortOptionText(ProductSortOption option)
        {
            var loc = LocalizationResourceManager.Instance;
            return option switch
            {
                ProductSortOption.Newest => loc["SortNewest"],
                ProductSortOption.Oldest => loc["SortOldest"],
                ProductSortOption.PriceAsc => loc["SortPriceAsc"],
                ProductSortOption.PriceDesc => loc["SortPriceDesc"],
                ProductSortOption.MostViewed => loc["SortMostViewed"],
                ProductSortOption.MostFavorited => loc["SortMostFavorited"],
                _ => option.ToString()
            };
        }

        public void Dispose()
        {
            _notificationSubscription?.Dispose();
            _listener?.Dispose();
            _userStateService.UserProfileChanged -= OnUserProfileChanged;
            WeakReferenceMessenger.Default.UnregisterAll(this);
        }
        #endregion
    }
}