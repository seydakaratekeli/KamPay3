using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KamPay.Models;
using KamPay.Services;
using KamPay.Views;
using KamPay.Helpers;

namespace KamPay.ViewModels;

public partial class ProfileViewModel : ObservableObject, IDisposable
{
    private readonly IUserStateService _userStateService;
    private readonly IAuthenticationService _authService;
    private readonly IProductService _productService;
    private readonly IUserProfileService _profileService;
    private readonly IStorageService _storageService;
    private bool _disposed = false;

    // ✅ Cache Yönetimi
    private bool _isDataLoaded = false;
    private DateTime _lastLoadTime = DateTime.MinValue;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);

    [ObservableProperty]
    private User currentUser = new();

    [ObservableProperty]
    private UserStats userStats = new();

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isRefreshing;

    [ObservableProperty]
    private bool hasProfileImage;

    public ObservableCollection<Product> MyProducts { get; } = new();
    public ObservableCollection<UserBadge> MyBadges { get; } = new();

    private static LocalizationResourceManager Res => LocalizationResourceManager.Instance;

    public ProfileViewModel(
        IUserStateService userStateService,
        IAuthenticationService authService,
        IProductService productService,
        IUserProfileService profileService,
        IStorageService storageService)
    {
        _userStateService = userStateService;
        _authService = authService;
        _productService = productService;
        _profileService = profileService;
        _storageService = storageService;

        // ✅ Global durum değişikliklerini dinle
        _userStateService.UserProfileChanged += OnUserProfileChanged;
    }

    // ✅ Kullanıcı verisi değiştiğinde (EditProfile'dan dönüldüğünde) tetiklenir
    private void OnUserProfileChanged(object? sender, User? updatedUser)
    {
        if (updatedUser == null)
        {
            ResetViewModelState();
        }
        else
        {
            CurrentUser = updatedUser;
            HasProfileImage = !string.IsNullOrWhiteSpace(updatedUser.ProfileImageUrl);

            // Veri değiştiği için cache'i geçersiz kılıyoruz ki InitializeAsync gerekirse yenilesin
            _isDataLoaded = false;
        }
        OnPropertyChanged(nameof(CurrentUser));
    }

    public async Task InitializeAsync()
    {
        if (_userStateService.CurrentUser == null && !_authService.IsUserLoggedIn())
            return;

        // Cache kontrolü: Veri yüklü ve süresi dolmadıysa Firebase'e tekrar gitme
        if (_isDataLoaded && (DateTime.UtcNow - _lastLoadTime) < _cacheExpiration)
        {
            Console.WriteLine("✅ Profil cache'den yüklendi");
            return;
        }

        await LoadProfileAsync();
    }

    [RelayCommand]
    private async Task LoadProfileAsync()
    {
        try
        {
            IsLoading = true;

            // 1. Kullanıcı bilgilerini servisten tazele
            var userResult = await _userStateService.RefreshCurrentUserAsync();
            if (userResult.Success && userResult.Data != null)
            {
                CurrentUser = userResult.Data;
            }
            else
            {
                // Fallback: Servis hata verirse mevcut auth bilgisini kullan
                var authUser = await _authService.GetCurrentUserAsync();
                if (authUser != null) CurrentUser = authUser;
            }

            HasProfileImage = !string.IsNullOrWhiteSpace(CurrentUser.ProfileImageUrl);

            // 2. PARALEL YÜKLEME: İstatistik, Ürünler ve Rozetler
            var statsTask = _profileService.GetUserStatsAsync(CurrentUser.UserId);
            var productsTask = _productService.GetUserProductsAsync(CurrentUser.UserId);
            var badgesTask = _profileService.GetUserBadgesAsync(CurrentUser.UserId);

            await Task.WhenAll(statsTask, productsTask, badgesTask);

            // Sonuçları işle
            UserStats = statsTask.Result.Success ? statsTask.Result.Data : new UserStats();

            // Ürünler (İlk 10 ürünü göster)
            if (productsTask.Result.Success && productsTask.Result.Data != null)
            {
                MyProducts.Clear();
                foreach (var product in productsTask.Result.Data.Take(10))
                    MyProducts.Add(product);

                if (UserStats != null)
                    UserStats.TotalProducts = productsTask.Result.Data.Count;
            }

            // Rozetler
            if (badgesTask.Result.Success && badgesTask.Result.Data != null)
            {
                MyBadges.Clear();
                foreach (var badge in badgesTask.Result.Data)
                    MyBadges.Add(badge);
            }

            _isDataLoaded = true;
            _lastLoadTime = DateTime.UtcNow;
            Console.WriteLine("✅ Profil verileri Firebase'den çekildi ve cache'lendi");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ LoadProfileAsync hatası: {ex.Message}");
            await Application.Current.MainPage.DisplayAlert(Res["Error"], ex.Message, Res["Ok"]);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshProfileAsync()
    {
        IsRefreshing = true;
        _isDataLoaded = false;
        await LoadProfileAsync();
        IsRefreshing = false;
    }

    // ✅ YENİ: Ayrı Sayfaya Yönlendirme Komutu (Eski EditProfileAsync yerine)
    [RelayCommand]
    private async Task GoToEditProfileAsync()
    {
        if (CurrentUser == null) return;

        // Düzenleme sayfası için UserProfile modelini hazırla
        var profileData = new UserProfile
        {
            UserId = CurrentUser.UserId,
            FirstName = CurrentUser.FirstName,
            LastName = CurrentUser.LastName,
            Username = CurrentUser.Username,
            ProfileImageUrl = CurrentUser.ProfileImageUrl
        };

        var navigationParameter = new Dictionary<string, object>
        {
            { "UserProfile", profileData }
        };

        await Shell.Current.GoToAsync(nameof(EditProfilePage), navigationParameter);
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        var confirm = await Application.Current.MainPage.DisplayAlert(
            Res["LogoutTitle"], Res["ConfirmLogout"], Res["Yes"], Res["No"]);

        if (!confirm) return;

        try
        {
            IsLoading = true;
            ResetViewModelState();
            _userStateService.ClearUser();
            await _authService.LogoutAsync();

            // Tüm statik ve dinamik cache'leri temizle
            ChatViewModel.ClearCache();
            var productCache = Application.Current?.Handler?.MauiContext?.Services.GetService<IProductCacheService>();
            if (productCache != null) await productCache.InvalidateCacheAsync();

            await Shell.Current.GoToAsync("//LoginPage");
        }
        catch (Exception ex)
        {
            await Application.Current.MainPage.DisplayAlert(Res["Error"], ex.Message, Res["Ok"]);
        }
        finally { IsLoading = false; }
    }

    private void ResetViewModelState()
    {
        _isDataLoaded = false;
        _lastLoadTime = DateTime.MinValue;
        MyProducts.Clear();
        MyBadges.Clear();
        UserStats = new UserStats();
        CurrentUser = new User();
        HasProfileImage = false;
        Console.WriteLine("🧹 ProfileViewModel: State temizlendi.");
    }

    // ✅ Navigasyon Komutları
    [RelayCommand] private async Task ViewAllProductsAsync() => await Shell.Current.GoToAsync($"myproducts?userId={CurrentUser.UserId}");
    [RelayCommand] private async Task GoToOffersAsync() => await Shell.Current.GoToAsync(nameof(OffersPage));
    [RelayCommand] private async Task GoToFavoritesAsync() => await Shell.Current.GoToAsync(nameof(FavoritesPage));
    [RelayCommand] private async Task GoToNotificationsAsync() => await Shell.Current.GoToAsync(nameof(NotificationsPage));
    [RelayCommand] private async Task GoToServiceRequestsAsync() => await Shell.Current.GoToAsync(nameof(ServiceRequestsPage));

    [RelayCommand]
    private async Task ProductTappedAsync(Product product)
    {
        if (product != null) await Shell.Current.GoToAsync($"productdetail?productId={product.ProductId}");
    }

    [RelayCommand]
    private async Task ViewProfilePhotoAsync()
    {
        if (!string.IsNullOrEmpty(CurrentUser?.ProfileImageUrl))
            await Shell.Current.GoToAsync($"ImageViewerPage?photoUrl={Uri.EscapeDataString(CurrentUser.ProfileImageUrl)}");
    }

    [RelayCommand]
    private async Task ChangeLanguageAsync()
    {
        var action = await Application.Current.MainPage.DisplayActionSheet(Res["SelectLanguage"], Res["Cancel"], null, "Türkçe", "English");
        if (string.IsNullOrEmpty(action) || action == Res["Cancel"]) return;

        var cultureCode = action == "English" ? "en" : "tr";
        LocalizationResourceManager.Instance.SetCulture(cultureCode);
        await Application.Current.MainPage.DisplayAlert(Res["Success"], Res["LanguageChanged"], Res["Ok"]);
    }

    [RelayCommand]
    private async Task ShareProfileAsync()
    {
        if (CurrentUser == null) return;
        await Share.RequestAsync(new ShareTextRequest
        {
            Title = Res["ShareProfile"],
            Text = $"{CurrentUser.FullName}\n🎯 {UserStats?.Points ?? 0} {Res["Points"]}\n📦 {UserStats?.TotalProducts ?? 0} {Res["Product"]}\n\nKamPay"
        });
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _userStateService.UserProfileChanged -= OnUserProfileChanged;
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}