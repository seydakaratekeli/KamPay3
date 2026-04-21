using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Firebase.Database;
using Firebase.Database.Query;
using Firebase.Database.Streaming;
using KamPay.Helpers;
using KamPay.Models;
using KamPay.Models.EventMessages;
using KamPay.Services;
using KamPay.Services.Auth;
using KamPay.Services.Products;
using KamPay.Views;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reactive.Linq;

namespace KamPay.ViewModels
{
    [QueryProperty(nameof(UserId), "userId")]
    public partial class ProductListViewModel : ObservableObject, IDisposable
    {
        private readonly IProductService _productService;
        private readonly IAuthenticationService _authService;
        private readonly ICategoryService _categoryService;
        private readonly IUserStateService _userStateService;
        private readonly FirebaseClient _firebaseClient;

        private IDisposable? _notificationSubscription;
        private IDisposable? _realtimeListener;
        private CancellationTokenSource? _searchCts;
        private CancellationTokenSource? _loadCts;

        // Cursor-based sayfalama durumu
        private string? _nextCursor;
        private bool _isLoadingMore;
        private bool _initialLoadDone;

        // Tüm ürünler (sıralama için tutulur, filtreler API tarafında uygulanır)
        private List<Product> _allLoadedProducts = new();
        private readonly object _allProductsLock = new();

        // Debounce timer (realtime event'ler için)
        private System.Threading.Timer? _realtimeDebounceTimer;
        private readonly object _realtimeLock = new();
        private bool _hasPendingRealtimeUpdate;

        [ObservableProperty] private string userId = string.Empty;
        [ObservableProperty] private bool isLoading = true;
        [ObservableProperty] private bool isSkeletonVisible = true;
        [ObservableProperty] private bool hasUnreadNotifications;
        [ObservableProperty] private string searchText = string.Empty;
        [ObservableProperty] private Category? selectedCategory;
        [ObservableProperty] private ProductType? selectedType;
        [ObservableProperty] private ProductSortOption selectedSortOption;
        [ObservableProperty] private bool showFilterPanel;
        [ObservableProperty] private string emptyMessage = "Henüz ürün eklenmemiş";
        [ObservableProperty] private bool _isRefreshing;

        public ObservableRangeCollection<Product> Products { get; } = new();
        public ObservableCollection<Category> Categories { get; } = new();

        /// <summary>
        /// Faz 4: ProductListPage.xaml.cs bu event'i dinler → sfListView.ScrollTo(0) çağırır.
        /// </summary>
        public event EventHandler? ScrollToTopRequested;

        // Sıralama picker için
        private readonly List<ProductSortOption> _sortOptionEnums =
            Enum.GetValues(typeof(ProductSortOption)).Cast<ProductSortOption>().ToList();
        public List<string> SortOptionStrings => _sortOptionEnums.Select(GetSortOptionText).ToList();

        private int _selectedSortIndex;
        public int SelectedSortIndex
        {
            get => _selectedSortIndex;
            set
            {
                if (SetProperty(ref _selectedSortIndex, value) && value >= 0 && value < _sortOptionEnums.Count)
                    SelectedSortOption = _sortOptionEnums[value];
            }
        }

        public ProductListViewModel(
            IProductService productService,
            IAuthenticationService authService,
            ICategoryService categoryService,
            IUserStateService userStateService,
            FirebaseClient firebaseClient)
        {
            _productService = productService;
            _authService = authService;
            _categoryService = categoryService;
            _userStateService = userStateService;
            _firebaseClient = firebaseClient;

            SelectedSortOption = ProductSortOption.Newest;
            _selectedSortIndex = _sortOptionEnums.IndexOf(ProductSortOption.Newest);

            _userStateService.UserProfileChanged += OnUserProfileChanged;

            LocalizationResourceManager.Instance.PropertyChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(SortOptionStrings));
                OnPropertyChanged(nameof(SelectedSortIndex));
            };

            WeakReferenceMessenger.Default.Register<FavoriteCountChangedMessage>(this, (_, m) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var p = m.Value;
                    lock (_allProductsLock)
                    {
                        var item = _allLoadedProducts.FirstOrDefault(x => x.ProductId == p.ProductId);
                        if (item != null) { item.FavoriteCount = p.FavoriteCount; item.ViewCount = p.ViewCount; }
                    }
                    var vis = Products.FirstOrDefault(x => x.ProductId == p.ProductId);
                    if (vis != null) { vis.FavoriteCount = p.FavoriteCount; vis.ViewCount = p.ViewCount; }
                });
            });

            WeakReferenceMessenger.Default.Register<UnreadGeneralNotificationStatusMessage>(this, (_, m) =>
                HasUnreadNotifications = m.Value);

            // Faz 4: Ürün eklendi → listeye ekle + scroll-to-top
            WeakReferenceMessenger.Default.Register<ProductAddedMessage>(this, (_, m) =>
            {
                var newProduct = m.Value;
                if (newProduct == null) return;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    // _allLoadedProducts'a da ekle
                    lock (_allProductsLock)
                    {
                        var existsInAll = _allLoadedProducts.Any(p => p.ProductId == newProduct.ProductId);
                        if (!existsInAll) _allLoadedProducts.Insert(0, newProduct);
                    }

                    // Görünür listeye ekle (realtime listener'dan önce gelirse duplikasyonu önle)
                    var existsInProducts = Products.Any(p => p.ProductId == newProduct.ProductId);
                    if (!existsInProducts)
                    {
                        Products.Insert(0, newProduct);
                    }

                    // Scroll-to-top tetikle
                    ScrollToTopRequested?.Invoke(this, EventArgs.Empty);
                });
            });

            WeakReferenceMessenger.Default.Register<UserSessionChangedMessage>(this, (_, m) =>
            {
                if (!m.Value) // Logout
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        lock (_allProductsLock) { _allLoadedProducts.Clear(); }
                        Products.Clear();
                        _nextCursor = null;
                        UserId = string.Empty;
                        EmptyMessage = "Ürünler yükleniyor...";
                    });
                }
                else // Login
                {
                    MainThread.BeginInvokeOnMainThread(() => _ = InitializeAsync());
                }
            });

            _ = InitializeAsync();
        }

        public async Task InitializeAsync()
        {
            await LoadCategoriesAsync();
            StartListeningForNotifications();

            if (string.IsNullOrEmpty(UserId))
                await LoadAsync(reset: true);
        }

        // ─────────────────────────────────────────────────────────────────────
        // TEK YÜKLEME METODU: tüm senaryolar buraya gelir
        // reset=true  → ilk sayfa, Products temizlenir
        // reset=false → sonraki sayfa, Products'a eklenir
        // ─────────────────────────────────────────────────────────────────────
        private async Task LoadAsync(bool reset = true, CancellationToken cancellationToken = default)
        {
            if (!reset && (_isLoadingMore || _nextCursor == null)) return;
            if (reset && _isLoadingMore) return;

            try
            {
                if (reset)
                {
                    IsLoading = true;
                    _nextCursor = null;

                    // ── Cache-first: İlk açılışta cache varsa anında göster, skeleton yok ──
                    if (!_initialLoadDone)
                    {
                        var cached = await _productService.GetCachedProductsAsync();
                        if (cached?.Count > 0)
                        {
                            lock (_allProductsLock) { _allLoadedProducts = new List<Product>(cached); }
                            await MainThread.InvokeOnMainThreadAsync(() =>
                            {
                                Products.ReplaceRange(ApplySorting(cached));
                                IsSkeletonVisible = false;
                            });
                        }
                        else
                        {
                            await MainThread.InvokeOnMainThreadAsync(() => IsSkeletonVisible = true);
                        }
                    }
                    else
                    {
                        await MainThread.InvokeOnMainThreadAsync(() => IsSkeletonVisible = false);
                    }
                }
                else
                {
                    _isLoadingMore = true;
                }

                var filter = BuildFilter();
                var result = await _productService.GetProductsPagedAsync(20, reset ? null : _nextCursor, filter, cancellationToken);

                if (cancellationToken.IsCancellationRequested) return;

                if (result.Success && result.Data != null)
                {
                    var items = result.Data.Items;
                    _nextCursor = result.Data.NextCursor;

                    items = ApplySorting(items);

                    lock (_allProductsLock)
                    {
                        if (reset) _allLoadedProducts = new List<Product>(items);
                        else _allLoadedProducts.AddRange(items);
                    }

                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        if (reset)
                            // Faz 2: Cache'den zaten veri varsa SmartMerge kullan (flickering önlenir)
                            // Cache yoksa (Products boş) direkt ReplaceRange daha hızlı
                            SmartMergeProducts(items);
                        else
                            Products.AddRange(items);

                        EmptyMessage = items.Count == 0 ? "Ürün bulunamadı" : EmptyMessage;
                    });

                    if (reset && !_initialLoadDone)
                    {
                        _initialLoadDone = true;
                        StartRealtimeListenerForNewItems();
                    }
                }
                else
                {
                    if (reset && Products.Count == 0)
                    {
                        await MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            EmptyMessage = result.Message ?? "Ürünler yüklenemedi";
                        });
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("LoadAsync iptal edildi.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ LoadAsync hata: {ex.Message}");
            }
            finally
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    IsLoading = false;
                    IsSkeletonVisible = false;
                });
                if (!reset) _isLoadingMore = false;
            }
        }

        private ProductFilter BuildFilter() => new ProductFilter
        {
            OnlyActive = true,
            SortBy = SelectedSortOption,
            CategoryId = SelectedCategory?.CategoryId,
            Type = SelectedType,
            SearchText = SearchText
        };

        /// <summary>
        /// Faz 2: Cache'den yüklenen liste üzerine API verisini akıllıca birleştirir.
        /// Yalnızca değişen property'ler güncellenir → CachedImage yeniden yüklenmez → flickering yok.
        /// </summary>
        private void SmartMergeProducts(List<Product> newItems)
        {
            // Products boşsa (cache yoktu) direkt ReplaceRange daha hızlı
            if (Products.Count == 0)
            {
                Products.ReplaceRange(newItems);
                return;
            }

            var newDict = newItems.ToDictionary(p => p.ProductId);
            var existingIds = Products.Select(p => p.ProductId).ToHashSet();

            // 1) Artık olmayan ürünleri sil
            var toRemove = Products.Where(p => !newDict.ContainsKey(p.ProductId)).ToList();
            foreach (var item in toRemove) Products.Remove(item);

            // 2) Mevcut ürünleri yerinde güncelle (görsel yeniden yüklenmez)
            foreach (var existing in Products)
            {
                if (newDict.TryGetValue(existing.ProductId, out var updated))
                    UpdateProductProperties(existing, updated);
            }

            // 3) Yeni ürünleri doğru konuma ekle
            for (int i = 0; i < newItems.Count; i++)
            {
                if (!existingIds.Contains(newItems[i].ProductId))
                {
                    Products.Insert(Math.Min(i, Products.Count), newItems[i]);
                }
            }
        }

        private List<Product> ApplySorting(List<Product> items) => SelectedSortOption switch
        {
            ProductSortOption.PriceAsc => items.OrderBy(p => p.Price).ToList(),
            ProductSortOption.PriceDesc => items.OrderByDescending(p => p.Price).ToList(),
            ProductSortOption.MostViewed => items.OrderByDescending(p => p.ViewCount).ToList(),
            ProductSortOption.MostFavorited => items.OrderByDescending(p => p.FavoriteCount).ToList(),
            ProductSortOption.Oldest => items.OrderBy(p => p.CreatedAt).ToList(),
            _ => items.OrderByDescending(p => p.CreatedAt).ToList() // Newest (default)
        };

        // ─────────────────────────────────────────────────────────────────────
        // REALTIME LISTENER — Sadece yeni eklenen son 1 kaydı izle
        // (İlk yükleme API üzerinden yapıldı, listener sadece delta için)
        // ─────────────────────────────────────────────────────────────────────
        private void StartRealtimeListenerForNewItems()
        {
            _realtimeListener?.Dispose();

            try
            {
                // LimitToLast(1) → sadece en son eklenen/değiştirilen 1 kayıt gelir
                // Initial snapshot yükü sıfır (50+ event değil, 1 event)
                _realtimeListener = _firebaseClient
                    .Child(Constants.ProductsCollection)
                    .OrderByKey()
                    .LimitToLast(1)
                    .AsObservable<Product>()
                    .Where(e => e.Object != null && e.Object.IsActive && !e.Object.IsSold)
                    .Subscribe(evt => ApplyRealtimeEvent(evt));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ Realtime listener başlatılamadı: {ex.Message}");
            }
        }

        private void ApplyRealtimeEvent(FirebaseEvent<Product> evt)
        {
            if (evt.Object == null) return;
            var product = evt.Object;
            product.ProductId = evt.Key;

            // Faz 3: _allLoadedProducts'ı güncelle
            lock (_allProductsLock)
            {
                var index = _allLoadedProducts.FindIndex(p => p.ProductId == product.ProductId);

                if (evt.EventType == FirebaseEventType.InsertOrUpdate)
                {
                    if (index >= 0) _allLoadedProducts[index] = product;
                    else _allLoadedProducts.Insert(0, product);
                }
                else if (evt.EventType == FirebaseEventType.Delete)
                {
                    if (index >= 0) _allLoadedProducts.RemoveAt(index);
                }
            }

            // Faz 3: Granüler UI güncellemesi — ReplaceRange yerine tek item işlemi
            ScheduleGranularUiUpdate(product, evt.EventType);
        }

        private void ScheduleGranularUiUpdate(Product product, FirebaseEventType eventType)
        {
            // Debounce: Kısa sürede çok event gelirse son olanı uygula
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

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        // Mevcut listedeki index'i bul
                        var existingIndex = -1;
                        for (int i = 0; i < Products.Count; i++)
                        {
                            if (Products[i].ProductId == product.ProductId)
                            {
                                existingIndex = i;
                                break;
                            }
                        }

                        if (eventType == FirebaseEventType.InsertOrUpdate)
                        {
                            if (existingIndex >= 0)
                            {
                                // Mevcut ürünü yerinde güncelle — görsel yeniden yüklenmez!
                                UpdateProductProperties(Products[existingIndex], product);
                            }
                            else
                            {
                                // Yeni ürün: ProductAddedMessage ile zaten eklendiyse ekleme (duplikasyon önle)
                                var alreadyAdded = Products.Any(p => p.ProductId == product.ProductId);
                                if (!alreadyAdded)
                                {
                                    Products.Insert(0, product);
                                }
                            }
                        }
                        else if (eventType == FirebaseEventType.Delete && existingIndex >= 0)
                        {
                            Products.RemoveAt(existingIndex);
                        }
                    });

                    Debug.WriteLine($"✅ Granüler UI güncellendi (realtime) — ProductId: {product.ProductId}");
                }, null, 300, Timeout.Infinite);
            }
        }

        /// <summary>
        /// Faz 3 yardımcı: Mevcut Product nesnesinin property'lerini yerinde günceller.
        /// CachedImage binding'i değişince sadece değişen property için güncelleme olur,
        /// tüm item yeniden render edilmez → flickering yok.
        /// </summary>
        private static void UpdateProductProperties(Product existing, Product updated)
        {
            if (existing.ThumbnailUrl != updated.ThumbnailUrl)
                existing.ThumbnailUrl = updated.ThumbnailUrl;
            if (existing.Title != updated.Title)
                existing.Title = updated.Title;
            if (existing.Price != updated.Price)
                existing.Price = updated.Price;
            if (existing.IsActive != updated.IsActive)
                existing.IsActive = updated.IsActive;
            if (existing.IsSold != updated.IsSold)
                existing.IsSold = updated.IsSold;
            if (existing.IsReserved != updated.IsReserved)
                existing.IsReserved = updated.IsReserved;
            if (existing.FavoriteCount != updated.FavoriteCount)
                existing.FavoriteCount = updated.FavoriteCount;
            if (existing.ViewCount != updated.ViewCount)
                existing.ViewCount = updated.ViewCount;
            if (existing.UserName != updated.UserName)
                existing.UserName = updated.UserName;
            if (existing.UserPhotoUrl != updated.UserPhotoUrl)
                existing.UserPhotoUrl = updated.UserPhotoUrl;
            if (existing.HasPendingOffer != updated.HasPendingOffer)
                existing.HasPendingOffer = updated.HasPendingOffer;
        }

        // ─────────────────────────────────────────────────────────────────────
        // PROPERTY CHANGED HANDLERS — tümü LoadAsync'e yönlendirir
        // ─────────────────────────────────────────────────────────────────────

        async partial void OnUserIdChanged(string? value)
        {
            lock (_allProductsLock) { _allLoadedProducts.Clear(); }
            Products.Clear();
            _nextCursor = null;

            if (!string.IsNullOrEmpty(value))
            {
                EmptyMessage = "Henüz ürün eklemediniz";
                var result = await _productService.GetUserProductsAsync(value);
                if (result.Success && result.Data?.Any() == true)
                {
                    lock (_allProductsLock) { _allLoadedProducts = result.Data; }
                    await MainThread.InvokeOnMainThreadAsync(() => Products.ReplaceRange(result.Data));
                }
                await MainThread.InvokeOnMainThreadAsync(() => IsLoading = false);
            }
            else
            {
                EmptyMessage = "Arama kriterlerinize uygun ürün bulunamadı";
                await LoadAsync(reset: true);
            }
        }

        partial void OnSelectedCategoryChanged(Category? value)
        {
            _loadCts?.Cancel();
            _loadCts = new CancellationTokenSource();
            _ = LoadAsync(reset: true, _loadCts.Token);
        }

        partial void OnSelectedTypeChanged(ProductType? value)
        {
            _loadCts?.Cancel();
            _loadCts = new CancellationTokenSource();
            _ = LoadAsync(reset: true, _loadCts.Token);
        }

        partial void OnSelectedSortOptionChanged(ProductSortOption value)
        {
            var index = _sortOptionEnums.IndexOf(value);
            if (SelectedSortIndex != index) SelectedSortIndex = index;

            // Sıralama client-side — API'ye gitme
            List<Product> snapshot;
            lock (_allProductsLock) { snapshot = ApplySorting(_allLoadedProducts.ToList()); }
            // Sıralama değişince tüm liste yeniden sıralanmalı → ReplaceRange burada doğru
            MainThread.BeginInvokeOnMainThread(() => Products.ReplaceRange(snapshot));
        }

        async partial void OnSearchTextChanged(string? value)
        {
            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();

            try
            {
                await Task.Delay(500, _searchCts.Token);

                if (string.IsNullOrEmpty(value) || value.Length >= 2)
                {
                    _loadCts?.Cancel();
                    _loadCts = new CancellationTokenSource();
                    await LoadAsync(reset: true, _loadCts.Token);
                }
            }
            catch (TaskCanceledException) { }
        }

        // ─────────────────────────────────────────────────────────────────────
        // KOMUTLAR
        // ─────────────────────────────────────────────────────────────────────

        [RelayCommand]
        private async Task RefreshProducts()
        {
            IsRefreshing = true;
            try
            {
                _nextCursor = null;
                await LoadAsync(reset: true);
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        [RelayCommand]
        private async Task LoadMoreProductsAsync()
        {
            if (_isLoadingMore || _nextCursor == null) return;
            await LoadAsync(reset: false);
        }

        [RelayCommand]
        private void ToggleFilterPanel() => ShowFilterPanel = !ShowFilterPanel;

        [RelayCommand]
        private async Task ApplyFilters()
        {
            ShowFilterPanel = false;
            await LoadAsync(reset: true);
        }

        [RelayCommand]
        private void SetProductType(string typeString)
        {
            SelectedType = string.IsNullOrEmpty(typeString) ? null
                : Enum.TryParse(typeof(ProductType), typeString, out var result) ? (ProductType?)result : null;
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
        private void CategoryTapped(Category category) => SelectedCategory = category;

        [RelayCommand]
        private async Task GoToSurpriseBox() =>
            await Shell.Current.GoToAsync(nameof(SurpriseBoxPage));

        [RelayCommand]
        private async Task ProductTapped(Product product)
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
        private async Task GoToAddProductAsync() =>
            await Shell.Current.GoToAsync(nameof(AddProductPage));

        // ─────────────────────────────────────────────────────────────────────
        // YARDIMCI METODLAR
        // ─────────────────────────────────────────────────────────────────────

        private async Task LoadCategoriesAsync()
        {
            var categoryList = await _categoryService.GetCategoriesAsync();
            if (categoryList != null)
            {
                Categories.Clear();
                foreach (var c in categoryList) Categories.Add(c);
            }
        }

        private async void StartListeningForNotifications()
        {
            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser == null) return;

            _notificationSubscription?.Dispose();

            var initialCheck = await _firebaseClient
                .Child(Constants.NotificationsCollection)
                .OrderBy("UserId")
                .EqualTo(currentUser.UserId)
                .OnceAsync<Notification>();

            if (initialCheck.Any(n => !n.Object.IsRead && n.Object.Type != NotificationType.NewMessage))
                HasUnreadNotifications = true;

            _notificationSubscription = _firebaseClient
                .Child(Constants.NotificationsCollection)
                .OrderBy("UserId")
                .EqualTo(currentUser.UserId)
                .AsObservable<Notification>()
                .Where(e => e.EventType == FirebaseEventType.InsertOrUpdate)
                .Subscribe(entry =>
                {
                    if (entry.Object?.IsRead == false && entry.Object.Type != NotificationType.NewMessage)
                        MainThread.BeginInvokeOnMainThread(() => HasUnreadNotifications = true);
                });
        }

        private void OnUserProfileChanged(object? sender, User updatedUser)
        {
            if (updatedUser == null) return;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                lock (_allProductsLock)
                {
                    foreach (var p in _allLoadedProducts.Where(p => p.UserId == updatedUser.UserId))
                    {
                        p.UserName = updatedUser.FullName;
                        p.UserPhotoUrl = updatedUser.ProfileImageUrl;
                    }
                }
                foreach (var p in Products.Where(p => p.UserId == updatedUser.UserId))
                {
                    p.UserName = updatedUser.FullName;
                    p.UserPhotoUrl = updatedUser.ProfileImageUrl;
                }
            });
        }

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
            _realtimeListener?.Dispose();
            _realtimeDebounceTimer?.Dispose();
            _searchCts?.Dispose();
            _loadCts?.Dispose();
            _userStateService.UserProfileChanged -= OnUserProfileChanged;
            WeakReferenceMessenger.Default.UnregisterAll(this);
        }
    }
}
