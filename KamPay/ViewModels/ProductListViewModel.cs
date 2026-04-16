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
        private readonly IRealtimeSnapshotService<Product> _loader; // ✅ Interface
        private readonly FirebaseClient _firebaseClient;

        private IDisposable? _notificationSubscription;
        private CancellationTokenSource? _searchCancellationTokenSource;
        private readonly CacheManager<List<Product>> _cacheManager = new();
        private const string CACHE_KEY = "all_products";
        private string? _lastLoadedKey;
        private bool _isLoadingMore;

        // ✅ PERFORMANS: Realtime eventleri debounce etmek için timer
        private System.Threading.Timer? _realtimeDebounceTimer;
        private readonly object _realtimeLock = new();
        private bool _hasPendingRealtimeUpdate;

        // Tüm ürünlerin tutulduğu ana liste (filtreleme için)
        private List<Product> _allProducts = new();
        private readonly object _allProductsLock = new();

        private IDisposable? _listener;

        [ObservableProperty]
        private bool isSkeletonVisible = true;

        // Arayüze bağlanan ve sadece filtrelenmiş ürünleri gösteren liste
        public ObservableRangeCollection<Product> Products { get; } = new();
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

        public ProductListViewModel(
            IProductService productService,
            IAuthenticationService authService,
            ICategoryService categoryService,
            IUserStateService userStateService,
            IRealtimeSnapshotService<Product> realtimeLoader, // ✅ DI ile inject
            FirebaseClient firebaseClient) // ✅ DI ile inject
        {
            _productService = productService;
            _authService = authService;
            _categoryService = categoryService;
            _userStateService = userStateService;
            _loader = realtimeLoader; // ✅ Artık DI'den geliyor
            _firebaseClient = firebaseClient;

            // Varsayılan sıralama ayarları
            SelectedSortOption = ProductSortOption.Newest;
            _selectedSortIndex = _sortOptionEnums.IndexOf(ProductSortOption.Newest);

            _userStateService.UserProfileChanged += OnUserProfileChanged;

            //  DİL DEĞİŞİMİ DİNLEYİCİSİ
            LocalizationResourceManager.Instance.PropertyChanged += (sender, e) =>
            {
                // Dil değiştiğinde listeyi ve arayüzü yenile
                OnPropertyChanged(nameof(SortOptionStrings));
                OnPropertyChanged(nameof(SelectedSortIndex));
                // Filtrelemeyi tekrar uygula (metin tabanlı filtreler varsa diye)
                _ = ExecuteFilteringAsync();
            };

            WeakReferenceMessenger.Default.Register<FavoriteCountChangedMessage>(this, (r, m) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var receivedProduct = m.Value;
                    lock (_allProductsLock)
                    {
                        var productInAll = _allProducts.FirstOrDefault(p => p.ProductId == receivedProduct.ProductId);
                        if (productInAll != null)
                        {
                            productInAll.FavoriteCount = receivedProduct.FavoriteCount;
                            productInAll.ViewCount = receivedProduct.ViewCount;
                        }
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

            WeakReferenceMessenger.Default.Register<UserSessionChangedMessage>(this, (r, m) =>
            {
                if (!m.Value) // Logout
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        lock (_allProductsLock) { _allProducts.Clear(); }
                        Products.Clear();
                        _cacheManager.Clear();
                        _lastLoadedKey = null;
                        UserId = string.Empty;
                        EmptyMessage = "Ürünler yükleniyor...";
                    });
                }
                else // Login
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        _ = InitializeAsync();
                    });
                }
            });

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
                    lock (_allProductsLock) { _allProducts = result.Data; }
                    await ExecuteFilteringAsync();
                }
                else
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        lock (_allProductsLock) { _allProducts.Clear(); }
                        Products.Clear();
                        EmptyMessage = string.IsNullOrEmpty(result.Message) || result.Success ? "Henüz ürün eklenmemiş" : $"Bağlantı Hatası:\n{result.Message}\n({string.Join("\n", result.Errors ?? new List<string>())})";
                    });
                }

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    IsSkeletonVisible = false;
                    IsLoading = false;
                });

                // Realtime listener'ı başlat (sadece değişiklikleri dinlemek için)
                // 🚀 DÜZELTME: Listener eventlerinin artık hiçbir şekilde MainThread.BeginInvoke içine GİRMEMESİ gerekiyor. (Binlerce mesaj arayüzü felç eder)
                _listener = _loader.Listen(Constants.ProductsCollection, evt =>
                {
                    ApplyRealtimeEvent(evt);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Product UltraFastLoad hata: {ex.Message}");
                IsSkeletonVisible = false;
                IsLoading = false;
            }
        }

        /// <summary>
        /// ✅ DÜZELTME: Realtime event'leri uygula ama ExecuteFiltering'i DEBOUNCE et.
        /// Birden fazla event hızlıca geldiğinde, tek bir UI güncellemesi yapılır.
        /// Ayrıca arkaplanda çalışarak UI kilitlenmesini engeller.
        /// </summary>
        private void ApplyRealtimeEvent(FirebaseEvent<Product> evt)
        {
            var product = evt.Object;
            product.ProductId = evt.Key;

            // 🚀 DÜZELTME: RAM erişimi için lock bloklandı.
            lock (_allProductsLock)
            {
                var index = _allProducts.FindIndex(p => p.ProductId == product.ProductId);

                if (evt.EventType == FirebaseEventType.InsertOrUpdate)
                {
                    if (index >= 0)
                    {
                        _allProducts[index] = product;
                    }
                    else
                    {
                        _allProducts.Insert(0, product);
                    }
                }
                else if (evt.EventType == FirebaseEventType.Delete)
                {
                    if (index >= 0)
                        _allProducts.RemoveAt(index);
                }
            }

            // ✅ DEBOUNCE: 300ms içinde gelen tüm eventleri topla, sonra tek seferde filtrele
            ScheduleDebouncedFiltering();
        }

        /// <summary>
        /// 300ms debounce ile ExecuteFiltering çağırır.
        /// Birden fazla Firebase event'i art arda geldiğinde yalnızca son çağrı sonrası 300ms bekleyip tek sefer çalışır.
        /// </summary>
        private void ScheduleDebouncedFiltering()
        {
            lock (_realtimeLock)
            {
                _hasPendingRealtimeUpdate = true;
                _realtimeDebounceTimer?.Dispose();
                _realtimeDebounceTimer = new System.Threading.Timer(_ =>
                {
                    lock (_realtimeLock)
                    {
                        if (!_hasPendingRealtimeUpdate) return;
                        _hasPendingRealtimeUpdate = false;
                    }
                    _ = ExecuteFilteringAsync();
                    Debug.WriteLine("✅ Debounced ExecuteFiltering çalıştı");
                }, null, 300, Timeout.Infinite);
            }
        }

        #region Veri Yükleme ve Filtreleme Mantığı

        async partial void OnUserIdChanged(string? value)
        {
            lock (_allProductsLock) { _allProducts.Clear(); }
            Products.Clear();

            if (!string.IsNullOrEmpty(value))
            {
                IsLoading = true;
                await MainThread.InvokeOnMainThreadAsync(() => EmptyMessage = "Henüz ürün eklemediniz");
                var result = await _productService.GetUserProductsAsync(value);
                if (result.Success && result.Data != null && result.Data.Any())
                {
                    lock (_allProductsLock) { _allProducts = result.Data; }
                    await ExecuteFilteringAsync();
                }
                else
                {
                     await MainThread.InvokeOnMainThreadAsync(() =>
                     {
                         lock (_allProductsLock) { _allProducts.Clear(); }
                         Products.Clear();
                     });
                }
                await MainThread.InvokeOnMainThreadAsync(() => IsLoading = false);
            }
            else
            {
                await MainThread.InvokeOnMainThreadAsync(() => EmptyMessage = "Arama kriterlerinize uygun ürün bulunamadı");
                await UltraFastLoadAsync();
            }
        }

        private async Task ExecuteFilteringAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"🔍 ExecuteFilteringAsync başladı. Toplam ürün: {_allProducts.Count}");

                // UI nesnelerine diğer thread'den erişirken sorun çıkmasını önlemek için yerel değişkenlere alalım
                var categoryId = SelectedCategory?.CategoryId;
                var searchText = SearchText;
                var selectedType = SelectedType;
                var sortOption = SelectedSortOption;

                // 🚀 OPTİMİZASYON: Tüm filtreleme arka planda (Task.Run) yapılıyor, UI Thread bloklanmıyor.
                var filteredList = await Task.Run(() =>
                {
                    List<Product> safeSnapshot;
                    lock (_allProductsLock)
                    {
                        safeSnapshot = _allProducts.ToList();
                    }

                    // 🚀 OPTİMİZASYON: Zaten filtrelenmiş verilerle çalışıyoruz
                    // Sadece UI seviyesinde ek filtreler uygulayacağız
                    IEnumerable<Product> filtered = safeSnapshot.Where(p => p.IsActive && !p.IsSold);

                    // Kategori filtresi (eğer değiştirilmişse)
                    if (!string.IsNullOrEmpty(categoryId))
                    {
                        filtered = filtered.Where(p => p.CategoryId == categoryId);
                    }

                    // Arama metni (eğer varsa)
                    if (!string.IsNullOrEmpty(searchText))
                    {
                        var searchLower = searchText.ToLowerInvariant();
                        filtered = filtered.Where(p =>
                            p.Title.ToLowerInvariant().Contains(searchLower) ||
                            p.Description.ToLowerInvariant().Contains(searchLower));
                    }

                    // Tip filtresi (eğer varsa)
                    if (selectedType.HasValue)
                    {
                        filtered = filtered.Where(p => p.Type == selectedType.Value);
                    }

                    // Sıralama
                    filtered = sortOption switch
                    {
                        ProductSortOption.Oldest => filtered.OrderBy(p => p.CreatedAt),
                        ProductSortOption.PriceAsc => filtered.OrderBy(p => p.Price),
                        ProductSortOption.PriceDesc => filtered.OrderByDescending(p => p.Price),
                        ProductSortOption.MostViewed => filtered.OrderByDescending(p => p.ViewCount),
                        ProductSortOption.MostFavorited => filtered.OrderByDescending(p => p.FavoriteCount),
                        _ => filtered.OrderByDescending(p => p.CreatedAt), // Newest
                    };

                    return filtered.ToList();
                });

                // Sonuçları ve UI güncellemelerini UI Thread üzerinden yap (UpdateProductsCollection zaten MainThread içinde çalışmış olacak)
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    UpdateProductsCollection(filteredList);

                    IsLoading = false;
                    EmptyMessage = Products.Any() ? string.Empty : "Arama kriterlerinize uygun ürün bulunamadı";
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ ExecuteFilteringAsync hatası: {ex.Message}");
                await MainThread.InvokeOnMainThreadAsync(() => IsLoading = false);
            }
        }

        /// <summary>
        /// ✅ DÜZELTME: O(n) karmaşıklığında ve batch-friendly koleksiyon güncelleme.
        /// - Küçük değişikliklerde (<%30 fark) → yerinde güncelleme (az CollectionChanged event)
        /// - Büyük değişikliklerde → Clear + toplu Add (tek CollectionChanged event)
        /// </summary>
        private void UpdateProductsCollection(List<Product> newProducts)
        {
            // Eğer mevcut liste boş veya fark çok büyükse → toplu yenileme (en hızlı yol)
            if (Products.Count == 0 || newProducts.Count == 0 ||
                Math.Abs(Products.Count - newProducts.Count) > Products.Count * 0.3)
            {
                Products.ReplaceRange(newProducts);
                return;
            }

            // 1. Silinecekleri kaldır (sondan başa — index kaymasını önle)
            // Sadece tek bir HashSet kullanarak bellek dostu bir O(N) arama yapıyoruz.
            var newIdSet = new HashSet<string>(newProducts.Count);
            foreach (var p in newProducts)
            {
                newIdSet.Add(p.ProductId);
            }

            for (int i = Products.Count - 1; i >= 0; i--)
            {
                if (!newIdSet.Contains(Products[i].ProductId))
                {
                    Products.RemoveAt(i);
                }
            }

            // 2. Yeni/güncellenen ürünleri ekle veya yerinde güncelle (GC Allocation Yaratmadan)
            // Liste içerisinde yer değiştirme (Move) işlemleri için Dictionary oluşturup silmek yerine,
            // direkt liste elemanlarını tarıyoruz. Zaten ufak değişikliklerde ( < %30 ) GC oluşturmamak CPU'dan daha değerlidir.
            for (int i = 0; i < newProducts.Count; i++)
            {
                var newProduct = newProducts[i];

                if (i < Products.Count)
                {
                    var currentProduct = Products[i];

                    // Eleman zaten doğru sıradaysa, sadece verisini güncelle
                    if (currentProduct.ProductId == newProduct.ProductId)
                    {
                        Products[i] = newProduct;
                    }
                    else
                    {
                        // Yanlış sıradaysa, listede ilerde mi duruyor diye bak
                        int existingIndex = -1;
                        for (int j = i + 1; j < Products.Count; j++)
                        {
                            if (Products[j].ProductId == newProduct.ProductId)
                            {
                                existingIndex = j;
                                break;
                            }
                        }

                        if (existingIndex != -1) // Ürün aşağıdaki indekslerde var, buraya taşı (Move)
                        {
                            Products.Move(existingIndex, i);
                            Products[i] = newProduct;
                        }
                        else // Ürün listede yok (Yeni ürün), o zaman araya ekle
                        {
                            Products.Insert(i, newProduct);
                        }
                    }
                }
                else
                {
                    // Listenin sonuna geldik, geri kalanları ekle
                    Products.Add(newProduct);
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
                    await ReloadWithFilterAsync(_searchCancellationTokenSource.Token);
                }
                else
                {
                    await ExecuteFilteringAsync(); // Lokal filtreleme
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
            _ = ExecuteFilteringAsync(); // Sıralama için lokal filtreleme yeterli
        }

        partial void OnSelectedTypeChanged(ProductType? value)
        {
            // Tip değiştiğinde sunucudan yeni veri çek
            _ = ReloadWithFilterAsync();
        }

        // 🆕 YENİ METOD: Filtrelerle yeniden yükleme
        private async Task ReloadWithFilterAsync(CancellationToken cancellationToken = default)
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

                var result = await _productService.GetAllProductsAsync(filter, cancellationToken);

                if (result.Success && result.Data != null && result.Data.Any())
                {
                    lock (_allProductsLock) { _allProducts = result.Data; }
                    await ExecuteFilteringAsync();
                }
                else
                {
                     await MainThread.InvokeOnMainThreadAsync(() =>
                     {
                         lock (_allProductsLock) { _allProducts.Clear(); }
                         Products.Clear();
                         // API hatası varsa EmptyMessage'a hatayı yaz, ki kullanıcı görebilsin.
                         EmptyMessage = string.IsNullOrEmpty(result.Message) || result.Success ? "Arama kriterlerinize uygun ürün bulunamadı" : $"API'ye ulaşılamıyor:\n{result.Message}\n({string.Join(", ", result.Errors ?? new List<string>())})";
                     });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ ReloadWithFilter hatası: {ex.Message}");
            }
            finally
            {
                await MainThread.InvokeOnMainThreadAsync(() => IsLoading = false);
            }
        }

        #endregion

        #region Komutlar
        [RelayCommand]
        private void ToggleFilterPanel() => ShowFilterPanel = !ShowFilterPanel;

        [RelayCommand]
        private async Task ApplyFilters()
        {
            ShowFilterPanel = false;
            await ExecuteFilteringAsync();
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
                        Products.ReplaceRange(cachedProducts);
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
                        Products.ReplaceRange(productsResult.Data);
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
                        Products.AddRange(moreProducts.Data);
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
            // _allProducts için çağrıldığında kilit alıyoruz
            if (ReferenceEquals(products, _allProducts))
            {
                lock (_allProductsLock)
                {
                    foreach (var product in _allProducts.Where(p => p.UserId == updatedUser.UserId))
                    {
                        product.UserName = updatedUser.FullName;
                        product.UserPhotoUrl = updatedUser.ProfileImageUrl;
                    }
                }
            }
            else
            {
                foreach (var product in products.Where(p => p.UserId == updatedUser.UserId))
                {
                    product.UserName = updatedUser.FullName;
                    product.UserPhotoUrl = updatedUser.ProfileImageUrl;
                }
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
            _realtimeDebounceTimer?.Dispose(); // ✅ Debounce timer'ı temizle
            _userStateService.UserProfileChanged -= OnUserProfileChanged;
            WeakReferenceMessenger.Default.UnregisterAll(this);
        }
        #endregion
    }
}