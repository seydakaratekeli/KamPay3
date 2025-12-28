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

    //  Cache flag - Sadece bir kez yükle
    private bool _isDataLoaded = false;
    private DateTime _lastLoadTime = DateTime.MinValue;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);

    [ObservableProperty]
    private User currentUser = new User();

    [ObservableProperty]
    private UserStats userStats = new UserStats();

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isRefreshing;

    [ObservableProperty]
    private bool hasProfileImage;

    public ObservableCollection<Product> MyProducts { get; } = new();
    public ObservableCollection<UserBadge> MyBadges { get; } = new();

    // Localization helper
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

        // Global state değişikliklerini dinle
        _userStateService.UserProfileChanged += OnUserProfileChanged;
    }

    // Olay dinleyicisini güncelle: Kullanıcı değiştiğinde cache'i patlatmalıyız.
    private void OnUserProfileChanged(object sender, User updatedUser)
    {
        CurrentUser = updatedUser;
        HasProfileImage = !string.IsNullOrWhiteSpace(updatedUser?.ProfileImageUrl);

        // EĞER KULLANICI NULL İSE (ÇIKIŞ YAPILDIYSA) VEYA DEĞİŞTİYSE VERİLERİ TEMİZLE
        if (updatedUser == null)
        {
            ResetViewModelState();
        }
        else
        {
            // Yeni bir kullanıcı geldiyse cache'i geçersiz kıl ki veriler tekrar çekilsin
            _isDataLoaded = false;
        }

        OnPropertyChanged(nameof(CurrentUser));
    }

    // bellek sızıntılarını önlemek için temizleme yöntemi
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Bellek sızıntılarını önlemek için iptal 
                _userStateService.UserProfileChanged -= OnUserProfileChanged;
            }
            _disposed = true;
        }
    }

    //  Sayfa OnAppearing'den çağrılacak
    public async Task InitializeAsync()
    {
        // Eğer kullanıcı yoksa yükleme yapma (Logout durumunda tetiklenirse diye)
        if (_userStateService.CurrentUser == null && !_authService.IsUserLoggedIn())
        {
            return;
        }

        // Cache kontrolü
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

            // UserStateService üzerinden kullanıcı bilgilerini yükle
            var userResult = await _userStateService.RefreshCurrentUserAsync();
            if (!userResult.Success || userResult.Data == null)
            {
                // ✅ Fallback: If UserStateService fails (e.g., network issues with profile service),
                // use direct auth service to ensure basic user info is available for this session
                Console.WriteLine($"⚠️ UserStateService başarısız: {userResult.Message}");
                CurrentUser = await _authService.GetCurrentUserAsync();
                
                if (CurrentUser == null)
                {
                    Console.WriteLine("❌ Auth service de başarısız oldu");
                    return;
                }
            }
            else
            {
                //  CRITICAL FIX: Boş string kontrolü ekle
                var user = userResult.Data;
                
                // Eğer FirstName/LastName boşsa, mevcut değerleri koru
                if (CurrentUser != null)
                {
                    user.FirstName = string.IsNullOrWhiteSpace(user.FirstName) 
                        ? (CurrentUser.FirstName ?? string.Empty) 
                        : user.FirstName;
                        
                    user.LastName = string.IsNullOrWhiteSpace(user.LastName) 
                        ? (CurrentUser.LastName ?? string.Empty) 
                        : user.LastName;
                        
                    user.ProfileImageUrl = string.IsNullOrWhiteSpace(user.ProfileImageUrl) 
                        ? (CurrentUser.ProfileImageUrl ?? string.Empty) 
                        : user.ProfileImageUrl;
                }
                
                CurrentUser = user;
            }
            
            // ✅ Kullanıcı bilgilerini logla
            Console.WriteLine($"👤 LoadProfileAsync - CurrentUser:");
            Console.WriteLine($"   Name: {CurrentUser.FullName}");
            Console.WriteLine($"   Email: {CurrentUser.Email}");
            Console.WriteLine($"   ProfileImage: {CurrentUser.ProfileImageUrl ?? "YOK"}");

            //  PARALEL YÜKLEME: 3 işlemi aynı anda başlat (profil artık UserStateService'den geliyor)
            var statsTask = _profileService.GetUserStatsAsync(CurrentUser.UserId);
            var productsTask = _productService.GetUserProductsAsync(CurrentUser.UserId);
            var badgesTask = _profileService.GetUserBadgesAsync(CurrentUser.UserId);

            // Tüm işlemleri paralel bekle
            await Task.WhenAll(statsTask, productsTask, badgesTask);

            // Sonuçları al
            var statsResult = await statsTask;
            var productsResult = await productsTask;
            var badgesResult = await badgesTask;

            // Profil bilgilerini güncelle
            HasProfileImage = !string.IsNullOrWhiteSpace(CurrentUser.ProfileImageUrl);

            // İstatistikler
            UserStats = statsResult.Success ? statsResult.Data : new UserStats();

            // Ürünler
            if (productsResult.Success && productsResult.Data != null)
            {
                MyProducts.Clear();
                foreach (var product in productsResult.Data.Take(10))
                {
                    MyProducts.Add(product);
                }
                if (UserStats != null)
                {
                    UserStats.TotalProducts = productsResult.Data.Count;
                }
            }

            // Rozetler
            if (badgesResult.Success && badgesResult.Data != null)
            {
                MyBadges.Clear();
                foreach (var badge in badgesResult.Data)
                {
                    MyBadges.Add(badge);
                }
            }

            //  Cache'i işaretle
            _isDataLoaded = true;
            _lastLoadTime = DateTime.UtcNow;
            Console.WriteLine("✅ Profil verileri yüklendi ve cache'lendi");
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
        //  Refresh'te cache'i sıfırla ve yeniden yükle
        _isDataLoaded = false;
        await LoadProfileAsync();
        IsRefreshing = false;
    }

    [RelayCommand]
    private async Task EditProfileAsync()
    {
        if (CurrentUser == null)
        {
            await Application.Current.MainPage.DisplayAlert(Res["Error"], Res["UserNotFound"], Res["Ok"]);
            return;
        }

        string newFirstName = await Application.Current.MainPage.DisplayPromptAsync(
            Res["UpdateProfile"],
            Res["EnterNewFirstName"],
            initialValue: CurrentUser.FirstName);

        if (string.IsNullOrWhiteSpace(newFirstName))
            return;

        string newLastName = await Application.Current.MainPage.DisplayPromptAsync(
            Res["UpdateProfile"],
            Res["EnterNewLastName"],
            initialValue: CurrentUser.LastName);

        if (string.IsNullOrWhiteSpace(newLastName))
            return;

        string newUsername = await Application.Current.MainPage.DisplayPromptAsync(
            Res["UpdateProfile"],
            Res["EnterNewUsername"],
            initialValue: CurrentUser.FirstName + CurrentUser.LastName);

        string uploadedImageUrl = null;
        bool changePhoto = await Application.Current.MainPage.DisplayAlert(
            Res["ProfilePhoto"],
            Res["ChangePhotoQuestion"],
            Res["Yes"],
            Res["No"]);

        if (changePhoto)
        {
            var limitCheck = RateLimiters.ImageUpload.CheckLimit(CurrentUser.UserId);
            if (!limitCheck.IsAllowed)
            {
                await Application.Current.MainPage.DisplayAlert(Res["Error"], limitCheck.Message, Res["Ok"]);
                return;
            }
            try
            {
                var file = await MediaPicker.PickPhotoAsync(new MediaPickerOptions
                {
                    Title = Res["SelectNewPhoto"]
                });

                if (file != null)
                {
                    var uploadResult = await _storageService.UploadProfileImageAsync(file.FullPath, CurrentUser.UserId);
                    if (uploadResult.Success)
                    {
                        uploadedImageUrl = uploadResult.Data;
                    }
                    else
                    {
                        await Application.Current.MainPage.DisplayAlert(Res["Error"], uploadResult.Message, Res["Ok"]);
                    }
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert(Res["Error"], Res["PhotoUploadFailed"] + ": " + ex.Message, Res["Ok"]);
            }
        }

        IsLoading = true;

        string sanitizedFirstName = InputSanitizer.SanitizeUsername(newFirstName);
        string sanitizedLastName = InputSanitizer.SanitizeUsername(newLastName);
        string sanitizedUsername = InputSanitizer.SanitizeUsername(newUsername);

        try
        {
            // UserStateService üzerinden profil güncelle - tüm sayfalara bildirim yapılır
            var result = await _userStateService.UpdateUserProfileAsync(
         firstName: sanitizedFirstName,
         lastName: sanitizedLastName,
         username: sanitizedUsername,
         profileImageUrl: uploadedImageUrl
             );

            if (result.Success)
            {
                HasProfileImage = !string.IsNullOrWhiteSpace(CurrentUser?.ProfileImageUrl);

                await Application.Current.MainPage.DisplayAlert(Res["Success"], Res["ProfileUpdated"], Res["Ok"]);

                //  Cache'i sıfırla ve yeniden yükle
                _isDataLoaded = false;
                await LoadProfileAsync();
            }
            else
            {
                await Application.Current.MainPage.DisplayAlert(Res["Error"], result.Message, Res["Ok"]);
            }
        }
        catch (Exception ex)
        {
            await Application.Current.MainPage.DisplayAlert(Res["Error"], ex.Message, Res["Ok"]);
        }
        finally
        {
            IsLoading = false;
        }
    }

    //  HELPER METOD: ViewModel'i fabrika ayarlarına döndürür
    private void ResetViewModelState()
    {
        // Cache flag'ini sıfırla
        _isDataLoaded = false;
        _lastLoadTime = DateTime.MinValue;

        // Listeleri temizle
        MyProducts.Clear();
        MyBadges.Clear();

        // İstatistikleri sıfırla
        UserStats = new UserStats(); // veya null
        CurrentUser = null;
        HasProfileImage = false;

        Console.WriteLine("🧹 ViewModel state temizlendi.");
    }

    [RelayCommand]
    private async Task ViewAllProductsAsync()
    {
        await Shell.Current.GoToAsync($"myproducts?userId={CurrentUser.UserId}");
    }

    [RelayCommand]
    private async Task ViewAllBadgesAsync()
    {
        await Application.Current.MainPage.DisplayAlert(
            "🏆 " + Res["MyBadges"],
            string.Format(Res["TotalBadges"], MyBadges.Count) + "\n\n" +
            string.Join("\n", MyBadges.Select(b => $"• {b.BadgeName}")),
            Res["Ok"]
        );
    }

    [RelayCommand]
    private async Task ShareProfileAsync()
    {
        if (CurrentUser == null) return;

        try
        {
            await Share.RequestAsync(new ShareTextRequest
            {
                Title = Res["ShareProfile"],
                Text = $"{CurrentUser.FullName}\n" +
               $"🎯 {UserStats?.Points ?? 0} {Res["Points"].ToLower()}\n" +
               $"📦 {UserStats?.TotalProducts ?? 0} {Res["Product"].ToLower()}\n" +
               $"🏆 {MyBadges.Count} {Res["Badge"].ToLower()}\n\n" +
               Res["SharedWithKamPay"]
            });
        }
        catch (Exception ex)
        {
            await Application.Current!.MainPage!.DisplayAlert(Res["Error"], ex.Message, Res["Ok"]);
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        var confirm = await Application.Current!.MainPage!.DisplayAlert(
            Res["LogoutTitle"],
            Res["ConfirmLogout"],
            Res["Yes"],
            Res["No"]
        );

        if (!confirm) return;

        try
        {
            IsLoading = true; // Yükleniyor göster
            
            Console.WriteLine("🔓 ProfileViewModel: Logout başlatılıyor...");

            // 1. Önce ViewModel üzerindeki verileri manuel temizle
            ResetViewModelState();

            // 2. Global user state'i temizle (bu tüm dinleyicilere bildirim gönderir)
            _userStateService.ClearUser();

            // 3. Auth servisinden çıkış yap (Preferences temizlenir)
            await _authService.LogoutAsync();
            
            // ✅ CRITICAL FIX: Tüm static cache'leri temizle
            ChatViewModel.ClearCache();
            Console.WriteLine("✅ ChatViewModel cache temizlendi");
            
            // ✅ CRITICAL FIX: ProductCacheService'i de temizle
            try
            {
                var productCacheService = Application.Current?.Handler?.MauiContext?.Services.GetService<IProductCacheService>();
                if (productCacheService != null)
                {
                    await productCacheService.InvalidateCacheAsync();
                    Console.WriteLine("✅ ProductCache temizlendi");
                }
            }
            catch (Exception cacheEx)
            {
                Console.WriteLine($"⚠️ Cache temizleme hatası: {cacheEx.Message}");
            }

            // 4. Login sayfasına yönlendir
            // "///" kullanımı stack'i tamamen sıfırlar
            await Shell.Current.GoToAsync("//LoginPage");
            
            Console.WriteLine("✅ Logout tamamlandı, LoginPage'e yönlendirildi");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Logout hatası: {ex.Message}");
            await Application.Current!.MainPage!.DisplayAlert(Res["Error"], ex.Message, Res["Ok"]);
        }
        finally
        {
            IsLoading = false;
        }
    }


    [RelayCommand]
    private async Task GoToOffersAsync()
    {
        await Shell.Current.GoToAsync(nameof(Views.OffersPage));
    }

    [RelayCommand]
    private async Task GoToServiceRequests()
    {
        await Shell.Current.GoToAsync(nameof(ServiceRequestsPage));
    }

    [RelayCommand]
    private async Task GoToFavoritesAsync()
    {
        await Shell.Current.GoToAsync(nameof(FavoritesPage));
    }

    [RelayCommand]
    private async Task GoToNotificationsAsync()
    {
        await Shell.Current.GoToAsync(nameof(NotificationsPage));
    }

    [RelayCommand]
    private async Task ChangeLanguageAsync()
    {
        var action = await Application.Current!.MainPage!.DisplayActionSheet(
            Res["SelectLanguage"],
            Res["Cancel"],
            null,
            "Türkçe",
            "English");

        if (string.IsNullOrEmpty(action) || action == Res["Cancel"])
            return;

        var cultureCode = action switch
        {
            "English" => "en",
            "Türkçe" => "tr",
            _ => "tr"
        };

        // Dil değişikliğini uygula (bu zaten LanguageChangedMessage gönderir)
        LocalizationResourceManager.Instance.SetCulture(cultureCode);
        
        // Kullanıcıya bilgi ver
        await Application.Current!.MainPage!.DisplayAlert(
            Res["Success"],
            Res["LanguageChanged"],
            Res["Ok"]);
    }

    [RelayCommand]
    private async Task ProductTappedAsync(Product product)
    {
        if (product == null) return;
        await Shell.Current.GoToAsync($"productdetail?productId={product.ProductId}");
    }

    //  Cache'i manuel sıfırlama metodu (ihtiyaç halinde)
    public void InvalidateCache()
    {
        _isDataLoaded = false;
        Console.WriteLine("🗑️ Profil cache'i temizlendi");
    }
}