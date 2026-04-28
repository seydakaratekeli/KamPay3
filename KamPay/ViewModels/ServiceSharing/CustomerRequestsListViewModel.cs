using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Firebase.Database;
using Firebase.Database.Streaming;
using KamPay.Helpers;
using KamPay.Models;
using KamPay.Services;
using KamPay.Services.Auth;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace KamPay.ViewModels
{
    /// <summary>
    /// ?? ARMUT MODELİ: Profesyonellerin müşteri taleplerini gördüğü liste
    /// </summary>
    public partial class CustomerRequestsListViewModel : ObservableObject, IDisposable
    {
        private readonly IServiceSharingService _serviceService;
        private readonly IAuthenticationService _authService;
        private readonly IUserStateService _userStateService;
        private readonly IUserProfileService _userProfileService;
        private readonly IRealtimeSnapshotService<CustomerServiceRequest> _loader; // ✅ Interface
        private readonly FirebaseClient _firebaseClient;

        private IDisposable? _listener;
        private readonly HashSet<string> _requestIds = new();

        private string? _lastLoadedKey;
        private bool _isLoadingMore;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private bool isRefreshing;

        [ObservableProperty]
        private ServiceCategory? filterCategory;

        [ObservableProperty]
        private string searchText = "";

        [ObservableProperty] private bool isCustomerRequestFormVisible;
        [ObservableProperty] private bool isPosting;

        // FORM FIELDS (Customer Request)
        [ObservableProperty] private string customerRequestTitle = "";
        [ObservableProperty] private string customerRequestDescription = "";
        [ObservableProperty] private ServiceCategory customerRequestCategory;
        [ObservableProperty] private string customerRequestLocation = "";
        [ObservableProperty] private decimal customerRequestBudgetMin;
        [ObservableProperty] private decimal customerRequestBudgetMax;
        [ObservableProperty] private DateTime customerRequestPreferredDate = DateTime.Now.AddDays(1);

        public ObservableCollection<CustomerServiceRequest> Requests { get; } = new();
        public ObservableCollection<CustomerServiceRequest> FilteredRequests { get; } = new();

        public List<ServiceCategory?> Categories { get; } = new List<ServiceCategory?> { null }
            .Concat(Enum.GetValues(typeof(ServiceCategory)).Cast<ServiceCategory?>())
            .ToList();

        public CustomerRequestsListViewModel(
            IServiceSharingService serviceService,
            IAuthenticationService authService,
            IUserStateService userStateService,
            IUserProfileService userProfileService,
            IRealtimeSnapshotService<CustomerServiceRequest> realtimeLoader,
            FirebaseClient firebaseClient) // ✅ DI ile inject
        {
            _serviceService = serviceService;
            _authService = authService;
            _userStateService = userStateService;
            _userProfileService = userProfileService;
            _loader = realtimeLoader; // ✅ Artık DI'den geliyor
            _firebaseClient = firebaseClient; // DI'den geliyor

            _ = InitializeAsync();
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

                // Sayfalama ile ilk yükleme
                var result = await _serviceService.GetCustomerRequestsPagedAsync(
                    pageSize: 20,
                    lastKey: null,
                    category: FilterCategory
                );

                Requests.Clear();
                _requestIds.Clear();

                if (result.Success && result.Data != null)
                {
                    foreach (var request in result.Data)
                    {
                        Requests.Add(request);
                        _requestIds.Add(request.RequestId);
                    }

                    Requests.SortDescending(x => x.CreatedAt);

                    if (result.Data.Any())
                    {
                        _lastLoadedKey = result.Data.Last().RequestId;
                    }
                }

                IsLoading = false;
                ApplyFilter();

                // Realtime listener
                _listener = _loader.Listen(Constants.CustomerServiceRequestsCollection, evt =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        ApplyRealtimeEvent(evt);
                    });
                });
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"? UltraFastLoadAsync Error: {ex.Message}");
                IsLoading = false;
            }
        }

        /// <summary>
        /// Daha fazla yükle (Sonsuz Kaydırma)
        /// </summary>
        [RelayCommand]
        private async Task LoadMoreRequestsAsync()
        {
            if (_isLoadingMore || string.IsNullOrEmpty(_lastLoadedKey)) return;

            try
            {
                _isLoadingMore = true;

                var result = await _serviceService.GetCustomerRequestsPagedAsync(
                    pageSize: 20,
                    lastKey: _lastLoadedKey,
                    category: FilterCategory
                );

                if (result.Success && result.Data != null && result.Data.Any())
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        foreach (var request in result.Data)
                        {
                            if (!_requestIds.Contains(request.RequestId))
                            {
                                Requests.Add(request);
                                _requestIds.Add(request.RequestId);
                            }
                        }
                    });

                    _lastLoadedKey = result.Data.Last().RequestId;
                    ApplyFilter();
                }
            }
            finally
            {
                _isLoadingMore = false;
            }
        }

        /// <summary>
        /// Realtime event handler
        /// </summary>
        private void ApplyRealtimeEvent(FirebaseEvent<CustomerServiceRequest> e)
        {
            var req = e.Object;
            if (req == null) return;

            req.RequestId = e.Key;
            var old = Requests.FirstOrDefault(x => x.RequestId == req.RequestId);

            bool changed = false;

            switch (e.EventType)
            {
                case FirebaseEventType.InsertOrUpdate:
                    if (!req.IsActive || req.Status != CustomerRequestStatus.Open)
                    {
                        if (old != null)
                        {
                            Requests.Remove(old);
                            _requestIds.Remove(req.RequestId);
                            changed = true;
                        }
                        break;
                    }

                    if (old != null)
                    {
                        var i = Requests.IndexOf(old);
                        Requests[i] = req;
                    }
                    else
                    {
                        if (!_requestIds.Contains(req.RequestId))
                        {
                            InsertSorted(req);
                            _requestIds.Add(req.RequestId);
                        }
                    }
                    changed = true;
                    break;

                case FirebaseEventType.Delete:
                    if (old != null)
                    {
                        Requests.Remove(old);
                        _requestIds.Remove(req.RequestId);
                        changed = true;
                    }
                    break;
            }

            if (changed)
                ApplyFilter();
        }

        private void InsertSorted(CustomerServiceRequest req)
        {
            if (Requests.Count == 0)
            {
                Requests.Add(req);
                return;
            }

            for (int i = 0; i < Requests.Count; i++)
            {
                if (req.CreatedAt > Requests[i].CreatedAt)
                {
                    Requests.Insert(i, req);
                    return;
                }
            }

            Requests.Add(req);
        }

        /// <summary>
        /// Filtreleme
        /// </summary>
        [RelayCommand]
        private void ApplyFilter()
        {
            var q = Requests.AsEnumerable();

            // Arama
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var t = SearchText.ToLower();
                q = q.Where(r =>
                    (r.Title ?? "").Contains(t, StringComparison.OrdinalIgnoreCase) ||
                    (r.Description ?? "").Contains(t, StringComparison.OrdinalIgnoreCase) ||
                    (r.Location ?? "").Contains(t, StringComparison.OrdinalIgnoreCase)
                );
            }

            // Kategori
            if (FilterCategory != null)
            {
                q = q.Where(r => r.Category == FilterCategory.Value);
            }

            // Sıralama
            q = q.OrderByDescending(r => r.CreatedAt);

            // Listeyi güncelle
            FilteredRequests.Clear();
            foreach (var r in q)
                FilteredRequests.Add(r);
        }

        [RelayCommand]
        private void ClearCategoryFilter()
        {
            FilterCategory = null;
            ApplyFilter();
        }

        partial void OnSearchTextChanged(string value) => ApplyFilter();
        
        partial void OnFilterCategoryChanged(ServiceCategory? value)
        {
            _lastLoadedKey = null;
            _requestIds.Clear();
            Requests.Clear();
            _ = UltraFastLoadAsync();
        }

        /// <summary>
        /// Talep detayına git
        /// </summary>
        [RelayCommand]
        private async Task ViewRequestDetailsAsync(CustomerServiceRequest request)
        {
            if (request == null) return;

            await Shell.Current.GoToAsync(
                $"CustomerRequestDetailsPage?requestId={request.RequestId}"
            );
        }

        /// <summary>
        /// Refresh
        /// </summary>
        [RelayCommand]
        private async Task RefreshRequestsAsync()
        {
            if (IsRefreshing) return;

            IsRefreshing = true;

            try
            {
                _listener?.Dispose();
                _listener = null;

                Requests.Clear();
                FilteredRequests.Clear();
                _requestIds.Clear();

                await UltraFastLoadAsync();
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        [RelayCommand]
        private void OpenCustomerRequestForm() => IsCustomerRequestFormVisible = true;

        [RelayCommand]
        private void CloseCustomerRequestForm() => IsCustomerRequestFormVisible = false;

        [RelayCommand]
        private async Task CreateCustomerRequestAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(CustomerRequestTitle))
                {
                    await DisplayAsync("Uyarı", "Başlık gerekli.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(CustomerRequestDescription))
                {
                    await DisplayAsync("Uyarı", "Açıklama gerekli.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(CustomerRequestLocation))
                {
                    await DisplayAsync("Uyarı", "Konum gerekli.");
                    return;
                }

                if (CustomerRequestBudgetMin < 0 || CustomerRequestBudgetMax < 0)
                {
                    await DisplayAsync("Uyarı", "Bütçe 0 veya daha büyük olmalıdır.");
                    return;
                }

                if (CustomerRequestBudgetMin > CustomerRequestBudgetMax)
                {
                    await DisplayAsync("Uyarı", "Minimum bütçe, maksimum bütçeden büyük olamaz.");
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

                var customerRequest = new CustomerServiceRequest
                {
                    RequestId = Guid.NewGuid().ToString(),
                    CustomerId = user.UserId,
                    CustomerName = user.FullName,
                    CustomerPhotoUrl = img,
                    Title = CustomerRequestTitle,
                    Description = CustomerRequestDescription,
                    Category = CustomerRequestCategory,
                    Location = CustomerRequestLocation,
                    BudgetMin = CustomerRequestBudgetMin,
                    BudgetMax = CustomerRequestBudgetMax,
                    PreferredDate = CustomerRequestPreferredDate,
                    Status = CustomerRequestStatus.Open,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    ProposalCount = 0
                };

                var result = await _serviceService.CreateCustomerRequestAsync(customerRequest);

                if (result.Success)
                {
                    CustomerRequestTitle = "";
                    CustomerRequestDescription = "";
                    CustomerRequestLocation = "";
                    CustomerRequestBudgetMin = 0;
                    CustomerRequestBudgetMax = 0;
                    CustomerRequestCategory = 0;
                    CustomerRequestPreferredDate = DateTime.Now.AddDays(1);

                    IsCustomerRequestFormVisible = false;

                    await DisplayAsync("Başarılı", "Hizmet talebiniz oluşturuldu! Profesyonellerden teklifler almaya başlayacaksınız.");
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

        private static Task DisplayAsync(string title, string message)
        {
            return Microsoft.Maui.Controls.Application.Current!.MainPage!.DisplayAlert(title, message, "Tamam");
        }

        public void Dispose()
        {
            _listener?.Dispose();
            Requests.Clear();
            FilteredRequests.Clear();
            _requestIds.Clear();
            GC.SuppressFinalize(this);
        }
    }
}

