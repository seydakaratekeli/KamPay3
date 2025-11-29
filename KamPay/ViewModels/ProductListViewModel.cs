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
using System.Diagnostics; // Hata ayıklama için eklendi
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
        private readonly ICategoryService _categoryService; // DOĞRU SERVİS EKLENDİ
        private readonly IUserStateService _userStateService;
        private IDisposable _notificationSubscription;
        private IDisposable _productSubscription;
        private readonly FirebaseClient _firebaseClient = new(Constants.FirebaseRealtimeDbUrl);
        private CancellationTokenSource _searchCancellationTokenSource; // YENİ EKLENDİ
        private readonly CacheManager<List<Product>> _cacheManager = new();
        private const string CACHE_KEY = "all_products";                                                               // ✅ YENİ METOD EKLE - Sonsuz scroll için
        private string _lastLoadedKey;
        private bool _isLoadingMore;

        // Tüm ürünlerin tutulduğu ana liste (filtreleme için)
        private List<Product> _allProducts = new();

        private RealtimeSnapshotService<Product> _loader;
        private IDisposable _listener;

        [ObservableProperty]
        private bool isSkeletonVisible = true;

        // Arayüze bağlanan ve sadece filtrelenmiş ürünleri gösteren liste
        public ObservableCollection<Product> Products { get; } = new();
        public ObservableCollection<Category> Categories { get; } = new();

        #region Observable Properties (Arayüzle İletişim Kuran Özellikler)
        [ObservableProperty] private string userId;
        [ObservableProperty] private bool isLoading = true;
        [ObservableProperty] private bool hasUnreadNotifications;
        [ObservableProperty] private string searchText;
        [ObservableProperty] private Category selectedCategory;
        [ObservableProperty] private ProductType? selectedType;
        [ObservableProperty] private ProductSortOption selectedSortOption;
        [ObservableProperty] private bool showFilterPanel;
        [ObservableProperty] private string emptyMessage = "Henüz ürün eklenmemiş";
        [ObservableProperty] private bool _isRefreshing;

        #endregion

        public List<ProductSortOption> SortOptions { get; } = Enum.GetValues(typeof(ProductSortOption)).Cast<ProductSortOption>().ToList();

        public ProductListViewModel(IProductService productService, IAuthenticationService authService, ICategoryService categoryService, IUserStateService userStateService)
        {
            _productService = productService;
            _authService = authService;
            _categoryService = categoryService;
            _userStateService = userStateService;
            SelectedSortOption = ProductSortOption.Newest;

            _loader = new RealtimeSnapshotService<Product>(Constants.FirebaseRealtimeDbUrl);

            // Kullanıcı profil değişikliklerini dinle
            _userStateService.UserProfileChanged += OnUserProfileChanged;

            // ProductListViewModel.cs içindeki mevcut register bloğunu bununla değiştirin:

            WeakReferenceMessenger.Default.Register<FavoriteCountChangedMessage>(this, (r, m) =>
            {
                // 🔥 Kritik: UI'da anlık güncelleme için MainThread'de çalıştırılmalıdır.
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var receivedProduct = m.Value; // Detay sayfasından güncel gelen ürün

                    // 1. Arka plan listesini güncelle (Tüm verilerin tutarlılığı için)
                    var productInAll = _allProducts.FirstOrDefault(p => p.ProductId == receivedProduct.ProductId);
                    if (productInAll != null)
                    {
                        productInAll.FavoriteCount = receivedProduct.FavoriteCount;
                        productInAll.ViewCount = receivedProduct.ViewCount;
                    }

                    // 2. Arayüze bağlı ObservableCollection'ı anlık güncelle
                    var productInProducts = Products.FirstOrDefault(p => p.ProductId == receivedProduct.ProductId);
                    if (productInProducts != null)
                    {
                        // Hem Favori hem de Görüntülenme sayısını anlık olarak güncelle
                        // Product modelinde [ObservableProperty] olduğu için bu UI'ı hemen tetikler.
                        productInProducts.FavoriteCount = receivedProduct.FavoriteCount;
                        productInProducts.ViewCount = receivedProduct.ViewCount;
                    }

                    // Not: Filtreleme listesi değiştiği için ExecuteFiltering() çağrısı gerekebilir, 
                    // ancak sadece sayacın değişmesi için bu anlık güncelleme yeterlidir.
                });
            });
            WeakReferenceMessenger.Default.Register<UnreadGeneralNotificationStatusMessage>(this, (r, m) => { HasUnreadNotifications = m.Value; });

            _ = InitializeViewModel();
        }

        public async Task UltraFastLoadAsync()
        {
            try
            {
                // 1- SNAPSHOT (Hızlı yükleme)
                var snapshot = await _loader.LoadSnapshotAsync(Constants.ProductsCollection);

                if (snapshot.Any())
                {
                    _allProducts = snapshot
                        .Select(s =>
                        {
                            s.Value.ProductId = s.Key;
                            return s.Value;
                        })
                        .OrderByDescending(p => p.CreatedAt)
                        .ToList();

                    ExecuteFiltering();

                    IsSkeletonVisible = false;
                    IsLoading = false;
                }

                // 2- REALTIME (Canlı veri akışı)
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

        public async Task InitializeViewModel()
        {
            await LoadCategoriesAsync();
            StartListeningForNotifications();

            if (string.IsNullOrEmpty(UserId))
            {
                await UltraFastLoadAsync();
            }
        }

        #region Veri Yükleme ve Filtreleme Mantığı

        async partial void OnUserIdChanged(string value)
        {
            _productSubscription?.Dispose();
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

        private void StartListeningForProducts()
        {
            // ÇÖZÜM: Sadece ana ürün listesi boşsa yükleme animasyonunu göster.
            // Bu, sadece ilk yüklemede veya filtre tamamen boşaldığında çalışır.
            if (!_allProducts.Any())
            {
                IsLoading = true;
            }

            _productSubscription?.Dispose();
            // NOT: _allProducts listesini burada temizlemiyoruz.
            // Çünkü geri dönüldüğünde eski veriyi göstermek istiyoruz.
            // Dinleyici yeni verileri getirdikçe liste güncellenecektir.

            _productSubscription = _firebaseClient
                .Child(Constants.ProductsCollection)
                .AsObservable<Product>()
                .Subscribe(e =>
                {
                    // Hata Ayıklama Mesajı
                    Debug.WriteLine($"[DEBUG] Firebase Event: {e.EventType}, Key: {e.Key}");

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        // 🔥 DÜZELTME BURADA:
                        // e.Object (product) veritabanı silindiğinde NULL gelebilir.
                        // Bu yüzden önce null kontrolü yapıyoruz.

                        string key = e.Key;
                        var product = e.Object;

                        // Eğer product doluysa ID'sini atıyoruz
                        if (product != null)
                        {
                            product.ProductId = key;
                        }

                        // Listede var mı diye bakarken Key kullanıyoruz (Product nesnesine güvenmiyoruz)
                        var existingProduct = _allProducts.FirstOrDefault(p => p.ProductId == key);

                        if (e.EventType == Firebase.Database.Streaming.FirebaseEventType.InsertOrUpdate)
                        {
                            if (product != null) // Sadece ürün doluysa ekle/güncelle
                            {
                                if (existingProduct != null)
                                {
                                    var index = _allProducts.IndexOf(existingProduct);
                                    _allProducts[index] = product;
                                }
                                else
                                {
                                    _allProducts.Insert(0, product);
                                }
                            }
                        }
                        else if (e.EventType == Firebase.Database.Streaming.FirebaseEventType.Delete)
                        {
                            if (existingProduct != null) _allProducts.Remove(existingProduct);
                        }

                        ExecuteFiltering();
                        IsLoading = false;
                    });
                }, ex => {
                    if (ex is TimeoutException)
                    {
                        Debug.WriteLine("[DEBUG] Firebase dinleyicisi zaman aşımına uğradı.");
                    }
                    else
                    {
                        Debug.WriteLine($"[HATA] Firebase dinleyicisinde sorun: {ex.Message}");
                    }
                    // Her durumda yükleme animasyonunu kapat
                    MainThread.BeginInvokeOnMainThread(() => { IsLoading = false; });
                });
        }

        // ProductListViewModel.cs içindeki ApplyFilters metodunu BU KODLA DEĞİŞTİRİN

        private void ExecuteFiltering()
        {
            try
            {
                // 🔥 UI thread'de çalıştığımızdan emin ol
                if (!MainThread.IsMainThread)
                {
                    MainThread.BeginInvokeOnMainThread(ExecuteFiltering);
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"🔍 ExecuteFiltering başladı. Toplam ürün: {_allProducts.Count}");

                IEnumerable<Product> filtered = _allProducts.Where(p => p.IsActive && !p.IsSold);

                // Kategori filtresi
                if (SelectedCategory != null && !string.IsNullOrEmpty(SelectedCategory.CategoryId))
                {
                    filtered = filtered.Where(p => p.CategoryId == SelectedCategory.CategoryId);
                    System.Diagnostics.Debug.WriteLine($"📂 Kategori filtresi: {SelectedCategory.Name}");
                }

                // Arama filtresi
                if (!string.IsNullOrEmpty(SearchText))
                {
                    var searchLower = SearchText.ToLowerInvariant();
                    filtered = filtered.Where(p =>
                        p.Title.ToLowerInvariant().Contains(searchLower) ||
                        p.Description.ToLowerInvariant().Contains(searchLower));
                    System.Diagnostics.Debug.WriteLine($"🔎 Arama filtresi: {SearchText}");
                }

                // Tip filtresi
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
                    _ => filtered.OrderByDescending(p => p.CreatedAt),
                };

                var filteredList = filtered.ToList();

                // 🔥 OPTİMİZASYON: Sadece değişen ürünleri güncelle (Clear() yerine smart update)
                UpdateProductsCollection(filteredList);

                EmptyMessage = Products.Any() ? string.Empty : "Arama kriterlerinize uygun ürün bulunamadı";

                System.Diagnostics.Debug.WriteLine($"✅ Filtreleme tamamlandı. Gösterilen: {Products.Count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ ExecuteFiltering hatası: {ex.Message}");
            }
        }

        // 🔥 YENİ METOT: Smart Collection Update (Clear() yerine)
        private void UpdateProductsCollection(List<Product> newProducts)
        {
            // 1️⃣ Silinecekleri bul
            var toRemove = Products
                .Where(p => !newProducts.Any(np => np.ProductId == p.ProductId))
                .ToList();

            foreach (var item in toRemove)
            {
                Products.Remove(item);
            }

            // 2️⃣ Eklenecekleri veya güncellenecekleri bul
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
                    // Güncelleme (pozisyon değişmişse taşı)
                    if (existingIndex != i)
                    {
                        Products.Move(existingIndex, i);
                    }
                    // Veriler değişmişse güncelle
                    Products[i] = newProduct;
                }
                else
                {
                    // Yeni ekleme
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
        async partial void OnSearchTextChanged(string value)
        {
            // Önceki gecikme görevini iptal et (kullanıcı hala yazıyor)
            _searchCancellationTokenSource?.Cancel();
            _searchCancellationTokenSource = new CancellationTokenSource();

            try
            {
                // Kullanıcının yazmayı bırakması için 300 milisaniye bekle
                await Task.Delay(300, _searchCancellationTokenSource.Token);

                // Bekleme süresi dolduysa ve iptal edilmediyse, filtrelemeyi şimdi yap
                ExecuteFiltering();
            }
            catch (TaskCanceledException)
            {
                // Bu hata, kullanıcı hızlı yazdığında beklenen bir durumdur.
                // Görevin iptal edildiğini gösterir, görmezden gelebiliriz.
                Debug.WriteLine("Arama ertelendi (debounced).");
            }
        }
        partial void OnSelectedCategoryChanged(Category value) => ExecuteFiltering();
        partial void OnSelectedSortOptionChanged(ProductSortOption value) => ExecuteFiltering();
        partial void OnSelectedTypeChanged(ProductType? value) => ExecuteFiltering();

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
        // ProductListViewModel.cs içine ekle:

        [RelayCommand]
        private void SetProductType(string typeString)
        {
            if (string.IsNullOrEmpty(typeString))
            {
                SelectedType = null; // Tümü seçildiğinde
            }
            else if (Enum.TryParse(typeof(ProductType), typeString, out var result))
            {
                SelectedType = (ProductType)result;
            }
        }

        // ✅ RefreshCommand'i güncelle
        [RelayCommand]
        private async Task RefreshProductsAsync()
        {
            IsRefreshing = true;

            try
            {
                // Cache'i temizle
                _cacheManager.Clear();
                _lastLoadedKey = null;

                // Yeniden yükle
                await LoadProductsAsync();
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        // ✅ LoadProductsAsync'e cache kontrolü ekle
        [RelayCommand]
        private async Task LoadProductsAsync()
        {
            if (IsLoading) return;

            try
            {
                IsLoading = true;

                // 🔥 Önce cache'e bak
                if (_cacheManager.TryGet(CACHE_KEY, out var cachedProducts))
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        Products.Clear();
                        foreach (var p in cachedProducts) Products.Add(p);
                    });
                    return;
                }

                // Cache yoksa network'ten al
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

                if (productsResult.Success)
                {
                    // Cache'e kaydet
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

                if (moreProducts.Success && moreProducts.Data.Any())
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
            // Yeni ve doğru servisi kullanıyoruz
            var categoryList = await _categoryService.GetCategoriesAsync();

            if (categoryList != null)
            {
                // Mevcut XAML tasarımınız "Tümü" butonunu kendi içinde barındırdığı için,
                // ViewModel'de tekrar eklememize gerek yok. Listeyi temizleyip gelen verilerle doldurmak yeterli.
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

        private void OnUserProfileChanged(object sender, User updatedUser)
        {
            if (updatedUser == null) return;

            // 🔥 Kritik: UI'da anlık güncelleme için MainThread'de çalıştırılmalıdır.
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Tüm listelerdeki kullanıcıya ait ürünlerin bilgilerini güncelle
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

        public string GetSortOptionText(ProductSortOption option)
        {
            return option switch { ProductSortOption.Newest => "En Yeni", ProductSortOption.Oldest => "En Eski", ProductSortOption.PriceAsc => "Fiyat (Artan)", ProductSortOption.PriceDesc => "Fiyat (Azalan)", ProductSortOption.MostViewed => "En Çok Görüntülenen", ProductSortOption.MostFavorited => "En Çok Favorilenen", _ => "Sırala" };
        }

        public void Dispose()
        {
            _notificationSubscription?.Dispose();
            _productSubscription?.Dispose();
            _userStateService.UserProfileChanged -= OnUserProfileChanged;
            WeakReferenceMessenger.Default.UnregisterAll(this);
        }
        #endregion
    }
}