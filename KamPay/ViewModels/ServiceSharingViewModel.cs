using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System;
using KamPay.Models;
using KamPay.Services;
using System.Collections.Generic;
using KamPay.Helpers;
using Firebase.Database.Streaming;

namespace KamPay.ViewModels
{
    public partial class ServiceSharingViewModel : ObservableObject, IDisposable
    {
        private readonly IServiceSharingService _serviceService;
        private readonly IAuthenticationService _authService;
        private readonly IUserProfileService _userProfileService;
        private readonly IUserStateService _userStateService;

        // 🔥 UltraFast loader (snapshot + realtime)
        private readonly RealtimeSnapshotService<ServiceOffer> _loader;
        private IDisposable _listener;

        // 🔥 CACHE: Service tracking
        private readonly HashSet<string> _serviceIds = new();
        private bool _initialLoadComplete = false;

        // 🔥 Form görünürlüğü
        [ObservableProperty]
        private bool isPostFormVisible;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private bool isPosting;

        [ObservableProperty]
        private bool isRefreshing;

        [ObservableProperty]
        private string serviceTitle;

        [ObservableProperty]
        private string serviceDescription;

        [ObservableProperty]
        private ServiceCategory selectedCategory;

        [ObservableProperty]
        private int timeCredits = 1;

        [ObservableProperty]
        private decimal servicePrice;

        public ObservableCollection<ServiceOffer> Services { get; } = new();
        public List<ServiceCategory> Categories { get; } =
            Enum.GetValues(typeof(ServiceCategory)).Cast<ServiceCategory>().ToList();

        public ServiceSharingViewModel(
            IServiceSharingService serviceService,
            IAuthenticationService authService,
            IUserProfileService userProfileService,
            IUserStateService userStateService)
        {
            _serviceService = serviceService ?? throw new ArgumentNullException(nameof(serviceService));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _userProfileService = userProfileService ?? throw new ArgumentNullException(nameof(userProfileService));
            _userStateService = userStateService ?? throw new ArgumentNullException(nameof(userStateService));

            // 🔥 UltraFast loader
            _loader = new RealtimeSnapshotService<ServiceOffer>(Constants.FirebaseRealtimeDbUrl);

            // Kullanıcı profil değişikliklerini dinle
            _userStateService.UserProfileChanged += OnUserProfileChanged;

            _ = InitializeAsync();
        }

        private void OnUserProfileChanged(object sender, User updatedUser)
        {
            if (updatedUser == null) return;

            // 🔥 UI thread
            MainThread.BeginInvokeOnMainThread(() =>
            {
                foreach (var service in Services.Where(s => s.ProviderId == updatedUser.UserId))
                {
                    service.ProviderName = updatedUser.FullName;
                    service.ProviderPhotoUrl = updatedUser.ProfileImageUrl;
                }
            });
        }

        private async Task InitializeAsync()
        {
            IsLoading = true;
            await UltraFastLoadAsync();
        }

        // 🔥 ULTRA FAST: Snapshot + realtime listener
        public async Task UltraFastLoadAsync()
        {
            try
            {
                // Eski listener varsa kapat
                _listener?.Dispose();
                _listener = null;

                IsLoading = true;

                // 1️⃣ SNAPSHOT – ilk yükleme
                var snapshot = await _loader.LoadSnapshotAsync(Constants.ServiceOffersCollection);

                Services.Clear();
                _serviceIds.Clear();

                var list = snapshot
                    .Select(s =>
                    {
                        s.Value.ServiceId = s.Key;
                        return s.Value;
                    })
                    .Where(s => s.IsAvailable)
                    .OrderByDescending(s => s.CreatedAt)
                    .ToList();

                foreach (var service in list)
                {
                    Services.Add(service);
                    _serviceIds.Add(service.ServiceId);
                }

                _initialLoadComplete = true;
                IsLoading = false;

                Console.WriteLine($"✅ UltraFast snapshot: {Services.Count} hizmet yüklendi.");

                // 2️⃣ REALTIME – canlı güncellemeler
                _listener = _loader.Listen(Constants.ServiceOffersCollection, evt =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        try
                        {
                            ApplyRealtimeEvent(evt);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"❌ ApplyRealtimeEvent hatası: {ex.Message}");
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ UltraFastLoadAsync hata: {ex.Message}");
                IsLoading = false;
            }
        }

        // 🔥 Tek tek realtime event işleme
        private void ApplyRealtimeEvent(FirebaseEvent<ServiceOffer> e)
        {
            var service = e.Object;
            if (service == null) return;

            service.ServiceId = e.Key;
            var existingService = Services.FirstOrDefault(s => s.ServiceId == service.ServiceId);

            switch (e.EventType)
            {
                case FirebaseEventType.InsertOrUpdate:
                    if (!service.IsAvailable)
                    {
                        // Artık uygun değilse listeden kaldır
                        if (existingService != null)
                        {
                            Services.Remove(existingService);
                            _serviceIds.Remove(service.ServiceId);
                        }
                        return;
                    }

                    if (existingService != null)
                    {
                        // Güncelle
                        var index = Services.IndexOf(existingService);
                        Services[index] = service;
                    }
                    else
                    {
                        if (!_serviceIds.Contains(service.ServiceId))
                        {
                            InsertServiceSorted(service);
                            _serviceIds.Add(service.ServiceId);
                        }
                    }

                    // İlk event geldiğinde loading'i kapat (snapshot boşsa)
                    if (!_initialLoadComplete)
                    {
                        _initialLoadComplete = true;
                        IsLoading = false;
                        Console.WriteLine("✅ İlk realtime hizmet geldi, loading kapatıldı.");
                    }
                    break;

                case FirebaseEventType.Delete:
                    if (existingService != null)
                    {
                        Services.Remove(existingService);
                        _serviceIds.Remove(service.ServiceId);
                    }
                    break;
            }
        }

        // 🔥 En yeni hizmetler üstte olacak şekilde insert
        private void InsertServiceSorted(ServiceOffer service)
        {
            if (Services.Count == 0)
            {
                Services.Add(service);
                return;
            }

            if (Services[0].CreatedAt <= service.CreatedAt)
            {
                Services.Insert(0, service);
                return;
            }

            for (int i = 0; i < Services.Count; i++)
            {
                if (Services[i].CreatedAt < service.CreatedAt)
                {
                    Services.Insert(i, service);
                    return;
                }
            }

            Services.Add(service);
        }

        // 🔄 Elle sıralamak istersen (şu an ApplyRealtimeEvent içinde pek gerek yok ama dursun)
        private void SortServicesInPlace()
        {
            var sorted = Services.OrderByDescending(s => s.CreatedAt).ToList();

            for (int i = 0; i < sorted.Count; i++)
            {
                var currentIndex = Services.IndexOf(sorted[i]);
                if (currentIndex != i && currentIndex >= 0)
                {
                    Services.Move(currentIndex, i);
                }
            }
        }

        // 🔄 Pull-to-Refresh
        [RelayCommand]
        private async Task RefreshServicesAsync()
        {
            if (IsRefreshing) return;

            try
            {
                IsRefreshing = true;

                // Listener'ı durdur
                _listener?.Dispose();
                _listener = null;

                // State'i sıfırla
                _serviceIds.Clear();
                Services.Clear();
                _initialLoadComplete = false;

                // Tekrar ultra-fast yükle
                await UltraFastLoadAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Refresh hatası: {ex.Message}");
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        // Eğer XAML'de kullanıyorsan, sadece loading state'i yönetir
        [RelayCommand]
        private async Task LoadServicesAsync()
        {
            if (!_initialLoadComplete)
            {
                await UltraFastLoadAsync();
            }
        }

        // Paneli Aç / Kapat
        [RelayCommand]
        private void OpenPostForm() => IsPostFormVisible = true;

        [RelayCommand]
        private void ClosePostForm() => IsPostFormVisible = false;

        [RelayCommand]
        private async Task CreateServiceAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ServiceTitle) || string.IsNullOrWhiteSpace(ServiceDescription))
                {
                    await Application.Current.MainPage.DisplayAlert("Uyarı", "Başlık ve açıklama gerekli.", "Tamam");
                    return;
                }

                if (ServicePrice <= 0)
                {
                    await Application.Current.MainPage.DisplayAlert("Uyarı", "Lütfen geçerli bir fiyat giriniz.", "Tamam");
                    return;
                }

                IsPosting = true;

                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    await Application.Current.MainPage.DisplayAlert("Hata", "Giriş yapılmamış.", "Tamam");
                    return;
                }

                var userProfile = await _userProfileService.GetUserProfileAsync(currentUser.UserId);
                string userImage = userProfile?.Data?.ProfileImageUrl ?? "person_icon.svg";

                var service = new ServiceOffer
                {
                    ProviderId = currentUser.UserId,
                    ProviderName = currentUser.FullName,
                    ProviderPhotoUrl = userImage,
                    Category = SelectedCategory,
                    Title = ServiceTitle,
                    Description = ServiceDescription,
                    TimeCredits = TimeCredits,
                    Price = ServicePrice,
                    CreatedAt = DateTime.UtcNow,
                    IsAvailable = true
                };

                var result = await _serviceService.CreateServiceOfferAsync(service);

                if (result.Success && result.Data != null)
                {
                    ServiceTitle = string.Empty;
                    ServiceDescription = string.Empty;
                    ServicePrice = 0;
                    TimeCredits = 1;
                    IsPostFormVisible = false;

                    await Application.Current.MainPage.DisplayAlert("Başarılı", "Hizmet paylaşıldı!", "Tamam");
                }
                else if (!result.Success)
                {
                    await Application.Current.MainPage.DisplayAlert("Hata", result.Message ?? "Bir hata oluştu.", "Tamam");
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Hata", ex.Message, "Tamam");
            }
            finally
            {
                IsPosting = false;
            }
        }

        [RelayCommand]
        private async Task RequestServiceAsync(ServiceOffer offer)
        {
            if (offer == null) return;

            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser == null)
            {
                await Application.Current.MainPage.DisplayAlert("Hata", "Bu işlem için giriş yapmalısınız.", "Tamam");
                return;
            }

            if (offer.ProviderId == currentUser.UserId)
            {
                await Application.Current.MainPage.DisplayAlert("Bilgi", "Kendi hizmetinizi talep edemezsiniz.", "Tamam");
                return;
            }

            try
            {
                var message = await Application.Current.MainPage.DisplayPromptAsync(
                    "Hizmet Talebi",
                    $"'{offer.Title}' hizmeti için talebinizi iletin (Fiyat: {offer.Price} ₺):",
                    "Gönder",
                    "İptal",
                    "Merhaba, bu hizmetinizden yararlanmak istiyorum."
                );

                if (string.IsNullOrWhiteSpace(message)) return;

                IsPosting = true;

                var result = await _serviceService.RequestServiceAsync(offer, currentUser, message);

                if (result.Success)
                {
                    await Application.Current.MainPage.DisplayAlert("Başarılı", result.Message, "Tamam");
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert("Hata", result.Message ?? "Talep gönderilemedi.", "Tamam");
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Hata", ex.Message, "Tamam");
            }
            finally
            {
                IsPosting = false;
            }
        }

        public void Dispose()
        {
            Console.WriteLine("🧹 ServiceSharingViewModel dispose ediliyor...");
            _userStateService.UserProfileChanged -= OnUserProfileChanged;

            _listener?.Dispose();
            _listener = null;

            _serviceIds.Clear();
            _initialLoadComplete = false;
        }
    }
}
