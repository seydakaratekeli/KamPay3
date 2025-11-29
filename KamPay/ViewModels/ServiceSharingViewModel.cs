using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System;
using KamPay.Models;
using KamPay.Services;
using System.Collections.Generic;
using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Helpers;
using System.Reactive.Linq;
using Firebase.Database.Streaming;

namespace KamPay.ViewModels
{
    public partial class ServiceSharingViewModel : ObservableObject, IDisposable
    {
        private readonly IServiceSharingService _serviceService;
        private readonly IAuthenticationService _authService;
        private readonly FirebaseClient _firebaseClient;
        private IDisposable _servicesSubscription;

        private readonly IUserProfileService _userProfileService;
        private readonly IUserStateService _userStateService;

        // 🔥 CACHE: Service tracking
        private readonly HashSet<string> _serviceIds = new();
        private bool _initialLoadComplete = false;

        // 🔥 YENİ: Formun görünürlüğünü kontrol eder
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
        public List<ServiceCategory> Categories { get; } = Enum.GetValues(typeof(ServiceCategory)).Cast<ServiceCategory>().ToList();

        // 🔥 CONSTRUCTOR GÜNCELLENDİ
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

            _firebaseClient = new FirebaseClient(Constants.FirebaseRealtimeDbUrl);

            // Kullanıcı profil değişikliklerini dinle
            _userStateService.UserProfileChanged += OnUserProfileChanged;

            _ = InitializeAsync();
        }

        private void OnUserProfileChanged(object sender, User updatedUser)
        {
            if (updatedUser == null) return;

            // 🔥 Kritik: UI'da anlık güncelleme için MainThread'de çalıştırılmalıdır.
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Kullanıcının hizmetlerinin bilgilerini güncelle
                foreach (var service in Services.Where(s => s.ProviderId == updatedUser.UserId))
                {
                    service.ProviderName = updatedUser.FullName;
                    service.ProviderPhotoUrl = updatedUser.ProfileImageUrl;
                }
            });
        }

        // InitializeAsync metodunu güncelleyin (satır 77-86):

        private async Task InitializeAsync()
        {
            IsLoading = true;
            StartListeningForServices();

        
        }
        // 🔥 YENİ: Paneli Aç
        [RelayCommand]
        private void OpenPostForm() => IsPostFormVisible = true;

        // 🔥 YENİ: Paneli Kapat
        [RelayCommand]
        private void ClosePostForm() => IsPostFormVisible = false;

        // 🔥 OPTİMİZE: Real-time listener + batch processing
        // StartListeningForServices metodunu güncelleyin (satır 97-138):

        private void StartListeningForServices()
        {
            if (_servicesSubscription != null) return;

            Console.WriteLine("🔥 Services listener başlatılıyor...");

            _servicesSubscription = _firebaseClient
                .Child(Constants.ServiceOffersCollection)
                .AsObservable<ServiceOffer>()
                .Where(e => e.Object != null && e.Object.IsAvailable)
                // 🔥 BUFFER KALDIRILDI - Her event anında işlenecek
                .Subscribe(
                    e =>
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            try
                            {
                                // Tek event işle
                                ProcessSingleServiceEvent(e);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"❌ Service event hatası: {ex.Message}");
                            }
                        });
                    },
                    error =>
                    {
                        Console.WriteLine($"❌ Firebase listener hatası: {error.Message}");
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            IsLoading = false;
                            _initialLoadComplete = true;
                        });
                    });
        }

        // 🔥 YENİ METOD: Tek event işleme
        private void ProcessSingleServiceEvent(Firebase.Database.Streaming.FirebaseEvent<ServiceOffer> e)
        {
            var service = e.Object;
            service.ServiceId = e.Key;

            var existingService = Services.FirstOrDefault(s => s.ServiceId == service.ServiceId);

            switch (e.EventType)
            {
                case Firebase.Database.Streaming.FirebaseEventType.InsertOrUpdate:
                    if (existingService != null)
                    {
                        // Güncelleme
                        var index = Services.IndexOf(existingService);
                        Services[index] = service;
                    }
                    else
                    {
                        // 🔥 Yeni ekleme - duplicate check
                        if (!_serviceIds.Contains(service.ServiceId))
                        {
                            InsertServiceSorted(service);
                            _serviceIds.Add(service.ServiceId);

                            // 🔥 İLK HİZMET EKLENDİĞİNDE LOADING'İ KAPAT
                            if (IsLoading)
                            {
                                IsLoading = false;
                                _initialLoadComplete = true;
                                Console.WriteLine("✅ İlk hizmet yüklendi, loading kapatıldı");
                            }
                        }
                    }
                    break;

                case Firebase.Database.Streaming.FirebaseEventType.Delete:
                    if (existingService != null)
                    {
                        Services.Remove(existingService);
                        _serviceIds.Remove(service.ServiceId);
                    }
                    break;
            }
        }

        private void ProcessServiceBatch(IList<FirebaseEvent<ServiceOffer>> events)
        {
            bool hasChanges = false;

            foreach (var e in events)
            {
                var service = e.Object;
                service.ServiceId = e.Key;

                var existingService = Services.FirstOrDefault(s => s.ServiceId == service.ServiceId);

                switch (e.EventType)
                {
                    case FirebaseEventType.InsertOrUpdate:
                        if (existingService != null)
                        {
                            // Güncelleme
                            var index = Services.IndexOf(existingService);
                            Services[index] = service;
                        }
                        else
                        {
                            // 🔥 Yeni ekleme - duplicate check
                            if (!_serviceIds.Contains(service.ServiceId))
                            {
                                InsertServiceSorted(service);
                                _serviceIds.Add(service.ServiceId);
                            }
                        }
                        hasChanges = true;
                        break;

                    case FirebaseEventType.Delete:
                        if (existingService != null)
                        {
                            Services.Remove(existingService);
                            _serviceIds.Remove(service.ServiceId);
                            hasChanges = true;
                        }
                        break;
                }
            }

            // 🔥 İLK VERİ GELDİĞİNDE LOADING'İ KAPAT
            if (hasChanges && IsLoading)
            {
                IsLoading = false;
                _initialLoadComplete = true;
                Console.WriteLine("✅ Hizmetler yüklendi (ilk veri geldi)");
            }

            // 🔥 Sadece değişiklik varsa sırala
            if (hasChanges)
            {
                SortServicesInPlace();
            }
        }


        // 🔥 YENİ: Sıralı insert (en yeni üstte)
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

        // 🔥 YENİ: In-place sorting
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

        // 🔥 OPTİMİZE: Refresh command
        [RelayCommand]
        private async Task RefreshServicesAsync()
        {
            if (IsRefreshing) return;

            try
            {
                IsRefreshing = true;

                // Listener'ı durdur
                _servicesSubscription?.Dispose();
                _servicesSubscription = null;

                // State'i sıfırla
                _serviceIds.Clear();
                Services.Clear();
                _initialLoadComplete = false;

                // Listener'ı yeniden başlat
                StartListeningForServices();

                await Task.Delay(300);
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

        [RelayCommand]
        private async Task LoadServicesAsync()
        {
            // Real-time listener zaten çalışıyor, ek yükleme gerekmez
            if (!_initialLoadComplete)
            {
                IsLoading = true;
            }
        }

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

                // 🔥 YENİ: Profil fotoğrafını alıyoruz
                var userProfile = await _userProfileService.GetUserProfileAsync(currentUser.UserId);
                string userImage = userProfile?.Data?.ProfileImageUrl ?? "person_icon.svg";

                var service = new ServiceOffer
                {
                    ProviderId = currentUser.UserId,
                    ProviderName = currentUser.FullName,
                    ProviderPhotoUrl = userImage, // 🔥 Modele kaydediyoruz
                    Category = SelectedCategory,
                    Title = ServiceTitle,
                    Description = ServiceDescription,
                    TimeCredits = TimeCredits,
                    Price = ServicePrice,
                    CreatedAt = DateTime.UtcNow
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
            _servicesSubscription?.Dispose();
            _servicesSubscription = null;
            _serviceIds.Clear();
        }
    }
}