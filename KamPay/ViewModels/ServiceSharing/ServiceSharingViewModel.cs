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
using Microsoft.Maui.ApplicationModel;
using KamPay.Services.Auth;
using KamPay.Services.Messaging;

namespace KamPay.ViewModels
{
    public partial class ServiceSharingViewModel : ObservableObject, IDisposable
    {
        private readonly IServiceSharingService _serviceService;
        private readonly IAuthenticationService _authService;
        private readonly IUserProfileService _userProfileService;
        private readonly IUserStateService _userStateService;
        private readonly IMessagingService _messagingService;
        private readonly IRealtimeSnapshotService<ServiceOffer> _loader; 
        private readonly KamPay.Services.Caching.ILocalDatabaseService<ServiceOffer> _cacheService;

        private IDisposable? _listener;

        private readonly HashSet<string> _serviceIds = new();
        
        private string? _lastLoadedKey;
        private bool _isLoadingMore;

        // UI STATE
        [ObservableProperty] private bool isPostFormVisible;
        [ObservableProperty] private bool isLoading;
        [ObservableProperty] private bool isPosting;
        [ObservableProperty] private bool isRefreshing;

        // FORM FIELDS (Service Offer)
        [ObservableProperty] private string serviceTitle = "";
        [ObservableProperty] private string serviceDescription = "";
        [ObservableProperty] private ServiceCategory selectedCategory;
        [ObservableProperty] private decimal servicePrice;
        [ObservableProperty] private int timeCredits = 1;

        // FILTERS
        [ObservableProperty] private string searchText = "";
        [ObservableProperty] private ServiceCategory? filterCategory = null;
        [ObservableProperty] private string? priceSort = null;

        // DATA COLLECTIONS
        public ObservableRangeCollection<ServiceOffer> Services { get; } = new();
        public ObservableRangeCollection<ServiceOffer> FilteredServices { get; } = new();

        public List<ServiceCategory?> Categories { get; } =
            new List<ServiceCategory?> { null }
            .Concat(Enum.GetValues(typeof(ServiceCategory)).Cast<ServiceCategory?>())
            .ToList();

        public List<string> PriceSortOptions
        {
            get
            {
                var loc = LocalizationResourceManager.Instance;
                return new List<string>
                {
                    loc["PriceAll"],
                    loc["PriceAscending"],
                    loc["PriceDescending"]
                };
            }
        }

        public ServiceSharingViewModel(
            IServiceSharingService serviceService,
            IAuthenticationService authService,
            IUserProfileService userProfileService,
            IUserStateService userStateService,
            IMessagingService messagingService,
            IRealtimeSnapshotService<ServiceOffer> realtimeLoader,
            KamPay.Services.Caching.ILocalDatabaseService<ServiceOffer> cacheService)
        {
            _serviceService = serviceService;
            _authService = authService;
            _userProfileService = userProfileService;
            _userStateService = userStateService;
            _messagingService = messagingService;
            _loader = realtimeLoader;
            _cacheService = cacheService;

            _userStateService.UserProfileChanged += OnUserProfileChanged;

            LocalizationResourceManager.Instance.PropertyChanged += (sender, e) =>
            {
                OnPropertyChanged(nameof(PriceSortOptions));
                ApplyFilter();
            };

            _ = InitializeAsync();
        }

        private void OnUserProfileChanged(object? sender, User u)
        {
            if (u == null) return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                bool hasChanges = false;

                foreach (var s in Services.Where(x => x.ProviderId == u.UserId))
                {
                    s.ProviderName = u.FullName;
                    s.ProviderPhotoUrl = u.ProfileImageUrl;
                    hasChanges = true;
                }

                foreach (var s in FilteredServices.Where(x => x.ProviderId == u.UserId))
                {
                    s.ProviderName = u.FullName;
                    s.ProviderPhotoUrl = u.ProfileImageUrl;
                }

                if (hasChanges)
                {
                    ApplyFilter();
                }
            });
        }

        private async Task InitializeAsync()
        {
            IsLoading = true;
            await UltraFastLoadAsync();
        }

        public async Task UltraFastLoadAsync()
        {
            try
            {
                _listener?.Dispose();
                _listener = null;

                IsLoading = true;

                // 1) Load from local cache first (Offline Support & Speed)
                try
                {
                    var cachedData = _cacheService.GetAll();
                    if (cachedData != null && cachedData.Any())
                    {
                        var filteredCache = cachedData
                            .Where(o => o.IsAvailable && (!FilterCategory.HasValue || o.Category == FilterCategory.Value))
                            .OrderByDescending(o => o.CreatedAt)
                            .ToList();

                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            Services.Clear();
                            _serviceIds.Clear();
                            foreach (var service in filteredCache)
                            {
                                Services.Add(service);
                                _serviceIds.Add(service.ServiceId);
                            }
                            ApplyFilter();
                        });
                        
                        // We still continue to fetch fresh data to ensure we are up to date.
                    }
                }
                catch (Exception ex)
                {
                    KamPay.Helpers.AppLogger.DebugLog("Cache Load Error: " + ex.Message);
                }

                // 2) Fetch fresh data
                var result = await _serviceService.GetServiceOffersPagedAsync(
                    pageSize: 20,
                    lastKey: null,
                    category: FilterCategory
                );

                if (result.Success && result.Data != null)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        Services.Clear();
                        _serviceIds.Clear();
                        
                        foreach (var service in result.Data)
                        {
                            if (service.IsAvailable)
                            {
                                Services.Add(service);
                                _serviceIds.Add(service.ServiceId);
                            }
                        }

                        var sortedList = Services.OrderByDescending(x => x.CreatedAt).ToList();
                        Services.Clear();
                        foreach (var item in sortedList)
                        {
                            Services.Add(item);
                        }
                        
                        if (result.Data.Any())
                        {
                            _lastLoadedKey = result.Data.Last().ServiceId;
                        }
                        
                        ApplyFilter();
                    });

                    // 3) Update local cache in background
                    _ = Task.Run(() => 
                    {
                        try
                        {
                            if (FilterCategory == null) // Sadece genel liste geldiğinde temizleyip kaydediyoruz ki cache çok şişmesin
                            {
                                _cacheService.DeleteAll();
                                _cacheService.InsertOrUpdateBulk(result.Data);
                            }
                        }
                        catch(Exception ex)
                        {
                            KamPay.Helpers.AppLogger.DebugLog("Cache Update Error: " + ex.Message);
                        }
                    });
                }

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    IsLoading = false;

                    _listener = _loader.Listen(Constants.ServiceOffersCollection, evt =>
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            ApplyRealtimeEvent(evt);
                        });
                    });
                });
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog("UltraFastLoadAsync Error: " + ex.Message);
                MainThread.BeginInvokeOnMainThread(() => IsLoading = false);
            }
        }

        [RelayCommand]
        private async Task LoadMoreServicesAsync()
        {
            if (_isLoadingMore || string.IsNullOrEmpty(_lastLoadedKey)) return;

            try
            {
                _isLoadingMore = true;

                var result = await _serviceService.GetServiceOffersPagedAsync(
                    pageSize: 20,
                    lastKey: _lastLoadedKey,
                    category: FilterCategory
                );

                if (result.Success && result.Data != null && result.Data.Any())
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        foreach (var service in result.Data)
                        {
                            if (service.IsAvailable && !_serviceIds.Contains(service.ServiceId))
                            {
                                Services.Add(service);
                                _serviceIds.Add(service.ServiceId);
                            }
                        }
                    });

                    _lastLoadedKey = result.Data.Last().ServiceId;
                    ApplyFilter();
                }
            }
            finally
            {
                _isLoadingMore = false;
            }
        }

        [RelayCommand]
        private void ClearCategoryFilter()
        {
            FilterCategory = null;
            ApplyFilter();
        }

        private void ApplyRealtimeEvent(FirebaseEvent<ServiceOffer> e)
        {
            var s = e.Object;
            if (s == null) return;

            s.ServiceId = e.Key;
            var old = Services.FirstOrDefault(x => x.ServiceId == s.ServiceId);

            bool changed = false;

            switch (e.EventType)
            {
                case FirebaseEventType.InsertOrUpdate:

                    if (!s.IsAvailable)
                    {
                        if (old != null)
                        {
                            Services.Remove(old);
                            _serviceIds.Remove(s.ServiceId);
                            changed = true;
                        }
                        break;
                    }

                    if (old != null)
                    {
                        var i = Services.IndexOf(old);
                        Services[i] = s;
                    }
                    else
                    {
                        if (!_serviceIds.Contains(s.ServiceId))
                        {
                            InsertSorted(s);
                            _serviceIds.Add(s.ServiceId);
                        }
                    }

                    changed = true;
                    break;

                case FirebaseEventType.Delete:
                    if (old != null)
                    {
                        Services.Remove(old);
                        _serviceIds.Remove(s.ServiceId);
                        changed = true;
                    }
                    break;
            }

            if (changed)
                ApplyFilter();
        }

        private void InsertSorted(ServiceOffer s)
        {
            if (Services.Count == 0)
            {
                Services.Add(s);
                return;
            }

            for (int i = 0; i < Services.Count; i++)
            {
                if (s.CreatedAt > Services[i].CreatedAt)
                {
                    Services.Insert(i, s);
                    return;
                }
            }

            Services.Add(s);
        }

        private void FilterServices()
        {
            var q = Services.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var t = SearchText.ToLower();
                q = q.Where(s =>
                    (s.Title ?? "").ToLower().Contains(t) ||
                    (s.Description ?? "").ToLower().Contains(t) ||
                    (s.ProviderName ?? "").ToLower().Contains(t)
                );
            }

            if (FilterCategory != null)
            {
                q = q.Where(s => s.Category == FilterCategory.Value);
            }

            var loc = LocalizationResourceManager.Instance;

            if (PriceSort == loc["PriceAscending"])
                q = q.OrderBy(s => s.Price);
            else if (PriceSort == loc["PriceDescending"])
                q = q.OrderByDescending(s => s.Price);
            else
                q = q.OrderByDescending(s => s.CreatedAt);

            var items = q.ToList();
            
            MainThread.BeginInvokeOnMainThread(() =>
            {
                FilteredServices.ReplaceRange(items);
            });
        }

        [RelayCommand]
        private void ApplyFilter()
        {
            FilterServices();
        }

        partial void OnSearchTextChanged(string value) => ApplyFilter();
        
        partial void OnFilterCategoryChanged(ServiceCategory? value) 
        {
            _lastLoadedKey = null;
            _serviceIds.Clear();
            Services.Clear();
            _ = UltraFastLoadAsync();
        }

        partial void OnPriceSortChanged(string? value)
        {
            ApplyFilter();
        }

        [RelayCommand]
        private async Task RefreshServicesAsync()
        {
            if (IsRefreshing) return;

            IsRefreshing = true;

            try
            {
                _listener?.Dispose();
                _listener = null;

                Services.Clear();
                FilteredServices.Clear();
                _serviceIds.Clear();

                await UltraFastLoadAsync();
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        [RelayCommand]
        private void OpenPostForm() => IsPostFormVisible = true;

        [RelayCommand]
        private void ClosePostForm() => IsPostFormVisible = false;


        [RelayCommand]
        private async Task CreateServiceAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ServiceTitle))
                {
                    await DisplayAsync("Uyarı", "Başlık gerekli.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(ServiceDescription))
                {
                    await DisplayAsync("Uyarı", "Açıklama gerekli.");
                    return;
                }

                if (ServicePrice < 0)
                {
                    await DisplayAsync("Uyarı", "Fiyat 0 veya daha büyük olmalıdır.");
                    return;
                }

                if (TimeCredits <= 0 || TimeCredits > 10)
                {
                    await DisplayAsync("Uyarı", "Süre 1-10 saat arasında olmalıdır.");
                    return;
                }

                var user = await _authService.GetCurrentUserAsync();
                if (user == null)
                {
                    await DisplayAsync("Hata", "Giriş yapılmamış.");
                    return;
                }

                var profile = await _userProfileService.GetUserProfileAsync(user.UserId);
                var img = profile?.Data?.ProfileImageUrl ?? "person_icon.svg";

                IsPosting = true;

                var offer = new ServiceOffer
                {
                    ServiceId = Guid.NewGuid().ToString(),
                    ProviderId = user.UserId,
                    ProviderName = user.FullName,
                    ProviderPhotoUrl = img,
                    Title = ServiceTitle,
                    Description = ServiceDescription,
                    Category = SelectedCategory,
                    Price = ServicePrice,
                    TimeCredits = TimeCredits,
                    CreatedAt = DateTime.UtcNow,
                    IsAvailable = true
                };

                var result = await _serviceService.CreateServiceOfferAsync(offer);

                if (result.Success)
                {
                    ServiceTitle = "";
                    ServiceDescription = "";
                    ServicePrice = 0;
                    TimeCredits = 1;
                    SelectedCategory = 0;

                    IsPostFormVisible = false;

                    await DisplayAsync("Başarılı", "Hizmet paylaşıldı!");
                }
                else
                {
                    await DisplayAsync("Hata", result.Message ?? "Hata oluştu.");
                }
            }
            catch (Exception ex)
            {
                await DisplayAsync("Hata", ex.Message);
            }
            finally
            {
                IsPosting = false;
            }
        }

        [RelayCommand]
        private async Task GoToOfferDetailAsync(ServiceOffer offer)
        {
            if (offer == null) return;
            await Shell.Current.GoToAsync($"{nameof(Views.ServiceOfferDetailPage)}?offerId={offer.ServiceId}");
        }

        [RelayCommand]
        private async Task MessageProviderAsync(ServiceOffer offer)
        {
            if (offer == null || IsLoading) return;

            try
            {
                IsLoading = true;

                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    await DisplayAsync("Hata", "Giriş yapılmalı.");
                    return;
                }

                if (currentUser.UserId == offer.ProviderId)
                {
                    await DisplayAsync("Bilgi", "Kendinize mesaj gönderemezsiniz.");
                    return;
                }

                var conversationResult = await _messagingService.GetOrCreateConversationAsync(
                    currentUser.UserId,
                    offer.ProviderId,
                    offer.ServiceId,
                    "Negotiation");

                if (conversationResult.Success && conversationResult.Data != null)
                {
                    await Shell.Current.GoToAsync($"ChatPage?conversationId={conversationResult.Data.ConversationId}");
                }
                else
                {
                    await DisplayAsync("Hata", conversationResult.Message ?? "Mesaj gönderilemedi.");
                }
            }
            catch (Exception ex)
            {
                await DisplayAsync("Hata", ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void IncrementTimeCredits()
        {
            if (TimeCredits < 10)
                TimeCredits++;
        }

        [RelayCommand]
        private void DecrementTimeCredits()
        {
            if (TimeCredits > 1)
                TimeCredits--;
        }

        private static Task DisplayAsync(string title, string message)
        {
            return Application.Current!.MainPage!.DisplayAlert(title, message, "Tamam");
        }

        public void Dispose()
        {
            _listener?.Dispose();
            _userStateService.UserProfileChanged -= OnUserProfileChanged;

            Services.Clear();
            FilteredServices.Clear();
            _serviceIds.Clear();

            GC.SuppressFinalize(this);
        }
    }

    public static class ListSortExtensions
    {
        public static void SortDescending<T, K>(this ObservableCollection<T> list, Func<T, K> key)
        {
            var sorted = list.OrderByDescending(key).ToList();

            list.Clear();
            foreach (var i in sorted)
                list.Add(i);
        }
    }
}
