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

namespace KamPay.ViewModels
{
    public partial class ServiceSharingViewModel : ObservableObject, IDisposable
    {
        private readonly IServiceSharingService _serviceService;
        private readonly IAuthenticationService _authService;
        private readonly IUserProfileService _userProfileService;
        private readonly IUserStateService _userStateService;
        private readonly IMessagingService _messagingService;

        private readonly RealtimeSnapshotService<ServiceOffer> _loader;
        private IDisposable? _listener;

        private readonly HashSet<string> _serviceIds = new();

        // ------------ UI STATE ----------
        [ObservableProperty] private bool isPostFormVisible;
        [ObservableProperty] private bool isLoading;
        [ObservableProperty] private bool isPosting;
        [ObservableProperty] private bool isRefreshing;

        // ------------ FORM FIELDS ----------
        [ObservableProperty] private string serviceTitle = "";
        [ObservableProperty] private string serviceDescription = "";
        [ObservableProperty] private ServiceCategory selectedCategory;
        [ObservableProperty] private decimal servicePrice;
        [ObservableProperty] private int timeCredits = 1;

        // ------------ FILTERS ------------
        [ObservableProperty] private string searchText = "";

        // null -> Hepsi
        [ObservableProperty] private ServiceCategory? filterCategory = null;

        // Seçilen sıralama metni (Örn: "Artan", "Ascending" vb.)
        [ObservableProperty] private string? priceSort = null;

        // ------------ DATA COLLECTIONS --------
        public ObservableCollection<ServiceOffer> Services { get; } = new();
        public ObservableCollection<ServiceOffer> FilteredServices { get; } = new();

        /// <summary>
        /// Kategori listesi (null = "Hepsi" seçeneği dahil)
        /// </summary>
        public List<ServiceCategory?> Categories { get; } =
            new List<ServiceCategory?> { null }
            .Concat(Enum.GetValues(typeof(ServiceCategory)).Cast<ServiceCategory?>())
            .ToList();

        /// <summary>
        /// Dinamik Sıralama Seçenekleri
        /// Dil değiştiğinde bu liste otomatik olarak yeni dildeki karşılıklarını döndürür.
        /// </summary>
        public List<string> PriceSortOptions
        {
            get
            {
                var loc = LocalizationResourceManager.Instance;
                return new List<string>
                {
                    loc["PriceAll"],       // Hepsi / All
                    loc["PriceAscending"], // Artan / Ascending
                    loc["PriceDescending"] // Azalan / Descending
                };
            }
        }

        // ------------ CONSTRUCTOR ------------
        public ServiceSharingViewModel(
            IServiceSharingService serviceService,
            IAuthenticationService authService,
            IUserProfileService userProfileService,
            IUserStateService userStateService,
            IMessagingService messagingService)
        {
            _serviceService = serviceService;
            _authService = authService;
            _userProfileService = userProfileService;
            _userStateService = userStateService;
            _messagingService = messagingService;

            _loader = new RealtimeSnapshotService<ServiceOffer>(Constants.FirebaseRealtimeDbUrl);

            _userStateService.UserProfileChanged += OnUserProfileChanged;

            // Dil değiştiğinde sıralama listesini (Picker) güncelle
            LocalizationResourceManager.Instance.PropertyChanged += (sender, e) =>
            {
                OnPropertyChanged(nameof(PriceSortOptions));
                // Dil değişince filtreyi tekrar uygula (gerekirse sıralamayı varsayılana çekebiliriz)
                ApplyFilter();
            };

            _ = InitializeAsync();
        }

        private void OnUserProfileChanged(object? sender, User u)
        {
            if (u == null) return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                foreach (var s in Services.Where(x => x.ProviderId == u.UserId))
                {
                    s.ProviderName = u.FullName;
                    s.ProviderPhotoUrl = u.ProfileImageUrl;
                }

                ApplyFilter();
            });
        }

        private async Task InitializeAsync()
        {
            IsLoading = true;
            await UltraFastLoadAsync();
        }

        /// <summary>
        /// ULTRA FAST LOADING (Snapshot + Realtime)
        /// </summary>
        public async Task UltraFastLoadAsync()
        {
            try
            {
                _listener?.Dispose();
                _listener = null;

                IsLoading = true;

                var snapshot = await _loader.LoadSnapshotAsync(Constants.ServiceOffersCollection);

                Services.Clear();
                _serviceIds.Clear();

                foreach (var row in snapshot)
                {
                    if (row.Value == null) continue;

                    var s = row.Value;
                    s.ServiceId = row.Key;

                    if (s.IsAvailable)
                    {
                        Services.Add(s);
                        _serviceIds.Add(s.ServiceId);
                    }
                }

                Services.SortDescending(x => x.CreatedAt);

                IsLoading = false;

                ApplyFilter();

                //  REALTIME LISTENER
                _listener = _loader.Listen(Constants.ServiceOffersCollection, evt =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        ApplyRealtimeEvent(evt);
                    });
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine("UltraFastLoadAsync Error: " + ex.Message);
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void ClearCategoryFilter()
        {
            FilterCategory = null;
            ApplyFilter();
        }

        /// <summary>
        /// REALTIME UPDATE HANDLER
        /// </summary>
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

        ///  FILTERING
       
        private void FilterServices()
        {
            var q = Services.AsEnumerable();

            // Search
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var t = SearchText.ToLower();
                q = q.Where(s =>
                    (s.Title ?? "").Contains(t, StringComparison.OrdinalIgnoreCase) ||
                    (s.Description ?? "").Contains(t, StringComparison.OrdinalIgnoreCase) ||
                    (s.ProviderName ?? "").Contains(t, StringComparison.OrdinalIgnoreCase)
                );
            }

            // Category
            if (FilterCategory != null)
            {
                q = q.Where(s => s.Category == FilterCategory.Value);
            }

            // Price sort (Dil bağımsız kontrol)
            var loc = LocalizationResourceManager.Instance;

            if (PriceSort == loc["PriceAscending"]) // "Artan" veya "Ascending" kontrolü
                q = q.OrderBy(s => s.Price);
            else if (PriceSort == loc["PriceDescending"]) // "Azalan" veya "Descending" kontrolü
                q = q.OrderByDescending(s => s.Price);
            else
                q = q.OrderByDescending(s => s.CreatedAt); // "Hepsi" veya null durumu

            // Update Filtered list
            FilteredServices.Clear();
            foreach (var s in q)
                FilteredServices.Add(s);
        }

        [RelayCommand]
        private void ApplyFilter()
        {
            FilterServices();
        }

        partial void OnSearchTextChanged(string value) => ApplyFilter();
        partial void OnFilterCategoryChanged(ServiceCategory? value) => ApplyFilter();

        partial void OnPriceSortChanged(string? value)
        {
            // Seçim değiştiğinde filtreyi uygula
            ApplyFilter();
        }

       
        ///  REFRESH
       
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

/// <summary>
/// FORM OPEN/CLOSE
/// </summary>
[RelayCommand]
private void OpenPostForm() => IsPostFormVisible = true;

        [RelayCommand] private void ClosePostForm() => IsPostFormVisible = false;

       
        ///  CREATE SERVICE
       
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

                if (ServicePrice <= 0)
                {
                    await DisplayAsync("Uyarı", "Geçerli bir fiyat giriniz.");
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

       
        // REQUEST SERVICE
       
        [RelayCommand]
        private async Task RequestServiceAsync(ServiceOffer offer)
        {
            if (offer == null) return;

            var user = await _authService.GetCurrentUserAsync();
            if (user == null)
            {
                await DisplayAsync("Hata", "Giriş yapılmalı.");
                return;
            }

            if (offer.ProviderId == user.UserId)
            {
                await DisplayAsync("Bilgi", "Kendi hizmetinize talep gönderemezsiniz.");
                return;
            }

            try
            {
                var msg = await Application.Current!.MainPage!.DisplayPromptAsync(
                    "Hizmet Talebi",
                    $"'{offer.Title}' için mesajınız:",
                    "Gönder",
                    "İptal",
                    "Merhaba, hizmetinizle ilgileniyorum."
                );

                if (string.IsNullOrWhiteSpace(msg)) return;

                IsPosting = true;

                var res = await _serviceService.RequestServiceAsync(offer, user, msg);

                if (res.Success)
                    await DisplayAsync("Başarılı", res.Message);
                else
                    await DisplayAsync("Hata", res.Message ?? "Talep gönderilemedi.");
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

       
        ///  MESSAGE PROVIDER
       
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
                    offer.ServiceId);

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

       
        // TIME CREDITS (+ / -)
       
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

       
        // DISPOSE
       
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

   
    ///  SMALL EXTENSION FOR SORTING
   
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