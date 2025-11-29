using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KamPay.Models;
using KamPay.Services;
using KamPay.Views;

namespace KamPay.ViewModels;

public partial class ProfileViewModel : ObservableObject, IDisposable
{
    private readonly IUserStateService _userStateService;
    private readonly IAuthenticationService _authService;
    private readonly IProductService _productService;
    private readonly IUserProfileService _profileService;
    private readonly IStorageService _storageService;
    private bool _disposed = false;

    // 🔥 YENİ: Cache flag - Sadece bir kez yükle
    private bool _isDataLoaded = false;
    private DateTime _lastLoadTime = DateTime.MinValue;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);

    [ObservableProperty]
    private User currentUser;

    [ObservableProperty]
    private UserStats userStats;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isRefreshing;

    [ObservableProperty]
    private bool hasProfileImage;

    public ObservableCollection<Product> MyProducts { get; } = new();
    public ObservableCollection<UserBadge> MyBadges { get; } = new();

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

    private void OnUserProfileChanged(object sender, User updatedUser)
    {
        CurrentUser = updatedUser;
        HasProfileImage = !string.IsNullOrWhiteSpace(updatedUser?.ProfileImageUrl);
        OnPropertyChanged(nameof(CurrentUser));
    }

    /// <summary>
    /// Cleanup method to unsubscribe from events and prevent memory leaks
    /// </summary>
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
                // Unsubscribe from event to prevent memory leaks
                _userStateService.UserProfileChanged -= OnUserProfileChanged;
            }
            _disposed = true;
        }
    }

    // 🔥 YENİ: Public initialize metodu - Sayfa OnAppearing'den çağrılacak
    public async Task InitializeAsync()
    {
        // Cache kontrolü: Eğer veri yüklenmişse ve süre dolmamışsa yeniden yükleme
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
                // Fallback: If UserStateService fails (e.g., network issues with profile service),
                // use direct auth service to ensure basic user info is available for this session
                CurrentUser = await _authService.GetCurrentUserAsync();
            }
            else
            {
                CurrentUser = userResult.Data;
            }
            
            if (CurrentUser == null) return;

            // 🔥 PARALEL YÜKLEME: 3 işlemi aynı anda başlat (profil artık UserStateService'den geliyor)
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

            // 🔥 Cache'i işaretle
            _isDataLoaded = true;
            _lastLoadTime = DateTime.UtcNow;
            Console.WriteLine("✅ Profil verileri yüklendi ve cache'lendi");
        }
        catch (Exception ex)
        {
            await Application.Current.MainPage.DisplayAlert("Hata", ex.Message, "Tamam");
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
        // 🔥 Refresh'te cache'i sıfırla ve yeniden yükle
        _isDataLoaded = false;
        await LoadProfileAsync();
        IsRefreshing = false;
    }

    [RelayCommand]
    private async Task EditProfileAsync()
    {
        if (CurrentUser == null)
        {
            await Application.Current.MainPage.DisplayAlert("Hata", "Kullanıcı bilgisi bulunamadı.", "Tamam");
            return;
        }

        string newFirstName = await Application.Current.MainPage.DisplayPromptAsync(
            "Profil Güncelle",
            "Yeni adınızı girin:",
            initialValue: CurrentUser.FirstName);

        if (string.IsNullOrWhiteSpace(newFirstName))
            return;

        string newLastName = await Application.Current.MainPage.DisplayPromptAsync(
            "Profil Güncelle",
            "Yeni soyadınızı girin:",
            initialValue: CurrentUser.LastName);

        if (string.IsNullOrWhiteSpace(newLastName))
            return;

        string newUsername = await Application.Current.MainPage.DisplayPromptAsync(
            "Profil Güncelle",
            "Yeni kullanıcı adınızı girin:",
            initialValue: CurrentUser.FirstName + CurrentUser.LastName);

        string uploadedImageUrl = null;
        bool changePhoto = await Application.Current.MainPage.DisplayAlert(
            "Profil Fotoğrafı",
            "Profil fotoğrafını değiştirmek ister misin?",
            "Evet",
            "Hayır");

        if (changePhoto)
        {
            try
            {
                var file = await MediaPicker.PickPhotoAsync(new MediaPickerOptions
                {
                    Title = "Yeni profil fotoğrafı seç"
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
                        await Application.Current.MainPage.DisplayAlert("Hata", uploadResult.Message, "Tamam");
                    }
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Hata", "Fotoğraf yüklenemedi: " + ex.Message, "Tamam");
            }
        }

        IsLoading = true;

        try
        {
            // UserStateService üzerinden profil güncelle - tüm sayfalara bildirim yapılır
            var result = await _userStateService.UpdateUserProfileAsync(
                firstName: newFirstName,
                lastName: newLastName,
                username: newUsername,
                profileImageUrl: uploadedImageUrl
            );

            if (result.Success)
            {
                HasProfileImage = !string.IsNullOrWhiteSpace(CurrentUser?.ProfileImageUrl);

                await Application.Current.MainPage.DisplayAlert("Başarılı", "Profil güncellendi!", "Tamam");

                // 🔥 Cache'i sıfırla ve yeniden yükle
                _isDataLoaded = false;
                await LoadProfileAsync();
            }
            else
            {
                await Application.Current.MainPage.DisplayAlert("Hata", result.Message, "Tamam");
            }
        }
        catch (Exception ex)
        {
            await Application.Current.MainPage.DisplayAlert("Hata", ex.Message, "Tamam");
        }
        finally
        {
            IsLoading = false;
        }
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
            "🏆 Rozetlerim",
            $"Toplam {MyBadges.Count} rozet kazandınız!\n\n" +
            string.Join("\n", MyBadges.Select(b => $"• {b.BadgeName}")),
            "Tamam"
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
                Title = "Profilimi Paylaş",
                Text = $"{CurrentUser.FullName}\n" +
               $"🎯 {UserStats?.Points ?? 0} puan\n" +
               $"📦 {UserStats?.TotalProducts ?? 0} ürün\n" +
               $"🏆 {MyBadges.Count} rozet\n\n" +
               "KamPay ile paylaşıldı"
            });
        }
        catch (Exception ex)
        {
            await Application.Current.MainPage.DisplayAlert("Hata", ex.Message, "Tamam");
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        var confirm = await Application.Current.MainPage.DisplayAlert(
            "Çıkış",
            "Çıkış yapmak istediğinize emin misiniz?",
            "Evet",
            "Hayır"
        );

        if (!confirm) return;

        try
        {
            // Global user state'i temizle
            _userStateService.ClearUser();
            await _authService.LogoutAsync();
            await Shell.Current.GoToAsync("//LoginPage");
        }
        catch (Exception ex)
        {
            await Application.Current.MainPage.DisplayAlert("Hata", ex.Message, "Tamam");
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
    private async Task ProductTappedAsync(Product product)
    {
        if (product == null) return;
        await Shell.Current.GoToAsync($"productdetail?productId={product.ProductId}");
    }

    // 🔥 YENİ: Cache'i manuel sıfırlama metodu (ihtiyaç halinde)
    public void InvalidateCache()
    {
        _isDataLoaded = false;
        Console.WriteLine("🗑️ Profil cache'i temizlendi");
    }
}