using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KamPay.Models;
using KamPay.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System;
using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Helpers;
using System.Reactive.Linq;
using Firebase.Database.Streaming;

namespace KamPay.ViewModels
{
    public partial class ServiceRequestsViewModel : ObservableObject, IDisposable
    {
        private readonly IServiceSharingService _serviceService;
        private readonly IAuthenticationService _authService;
        private readonly FirebaseClient _firebaseClient;
        private IDisposable _requestsSubscription;

        // 🔥 CACHE: Request tracking
        private readonly HashSet<string> _incomingRequestIds = new();
        private readonly HashSet<string> _outgoingRequestIds = new();
        private bool _initialLoadComplete = false;
        private string _currentUserId;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private bool isRefreshing;

        public ObservableCollection<ServiceRequest> IncomingRequests { get; } = new();
        public ObservableCollection<ServiceRequest> OutgoingRequests { get; } = new();
        public ObservableCollection<PaymentOption> PaymentMethods { get; }

        public ServiceRequestsViewModel(IServiceSharingService serviceService, IAuthenticationService authService)
        {
            _serviceService = serviceService;
            _authService = authService;
            _firebaseClient = new FirebaseClient(Constants.FirebaseRealtimeDbUrl);

            PaymentMethods = new ObservableCollection<PaymentOption>
            {
                new PaymentOption { Method = PaymentMethodType.CardSim, DisplayName = "Kart (Simülasyon)" },
                new PaymentOption { Method = PaymentMethodType.BankTransferSim, DisplayName = "EFT / Havale (Simülasyon)" }
            };

            _ = InitializeAsync();
        }

        public class PaymentOption
        {
            public PaymentMethodType Method { get; set; }
            public string DisplayName { get; set; }
        }

        private PaymentMethodType _selectedPaymentMethod = PaymentMethodType.CardSim;
        public PaymentMethodType SelectedPaymentMethod
        {
            get => _selectedPaymentMethod;
            set
            {
                if (_selectedPaymentMethod != value)
                {
                    _selectedPaymentMethod = value;
                    OnPropertyChanged();
                }
            }
        }

        private async Task InitializeAsync()
        {
            IsLoading = true;

            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser != null)
            {
                _currentUserId = currentUser.UserId;
                StartListeningForRequests();
            }
            else
            {
                IsLoading = false;
            }
        }

        // 🔥 OPTİMİZE: Real-time listener + batch processing
        private void StartListeningForRequests()
        {
            if (_requestsSubscription != null || string.IsNullOrEmpty(_currentUserId)) return;

            Console.WriteLine("🔥 Service requests listener başlatılıyor...");

            _requestsSubscription = _firebaseClient
                .Child(Constants.ServiceRequestsCollection)
                .AsObservable<ServiceRequest>()
                .Where(e => e.Object != null)
                .Buffer(TimeSpan.FromMilliseconds(300)) // 🔥 300ms batch
                .Where(batch => batch.Any())
                .Subscribe(
                    events =>
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            try
                            {
                                ProcessRequestBatch(events);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"❌ Request batch hatası: {ex.Message}");
                            }
                            finally
                            {
                                if (!_initialLoadComplete)
                                {
                                    _initialLoadComplete = true;
                                    IsLoading = false;
                                    Console.WriteLine("✅ Hizmet talepleri yüklendi");
                                }
                            }
                        });
                    },
                    error =>
                    {
                        Console.WriteLine($"❌ Firebase listener hatası: {error.Message}");
                        MainThread.BeginInvokeOnMainThread(() => IsLoading = false);
                    });
        }

        // 🔥 YENİ: Batch processing - Clear() YOK
        private void ProcessRequestBatch(IList<FirebaseEvent<ServiceRequest>> events)
        {
            bool hasIncomingChanges = false;
            bool hasOutgoingChanges = false;

            foreach (var e in events)
            {
                var request = e.Object;
                request.RequestId = e.Key;

                // Gelen talep mi? (ben hizmet sağlayıcıyım)
                if (request.ProviderId == _currentUserId)
                {
                    if (UpdateRequestInCollection(IncomingRequests, _incomingRequestIds, request, e.EventType))
                    {
                        hasIncomingChanges = true;
                    }
                }
                // Giden talep mi? (ben talep eden)
                else if (request.RequesterId == _currentUserId)
                {
                    if (UpdateRequestInCollection(OutgoingRequests, _outgoingRequestIds, request, e.EventType))
                    {
                        hasOutgoingChanges = true;
                    }
                }
            }

            // 🔥 Sadece değişenler için sıralama
            if (hasIncomingChanges)
            {
                SortRequestsInPlace(IncomingRequests);
            }

            if (hasOutgoingChanges)
            {
                SortRequestsInPlace(OutgoingRequests);
            }
        }

        // 🔥 YENİ: Smart collection update
        private bool UpdateRequestInCollection(
            ObservableCollection<ServiceRequest> collection,
            HashSet<string> idTracker,
            ServiceRequest request,
            FirebaseEventType eventType)
        {
            var existing = collection.FirstOrDefault(r => r.RequestId == request.RequestId);

            switch (eventType)
            {
                case FirebaseEventType.InsertOrUpdate:
                    if (existing != null)
                    {
                        // Güncelleme
                        var index = collection.IndexOf(existing);
                        collection[index] = request;
                        return true;
                    }
                    else
                    {
                        // 🔥 Duplicate check
                        if (!idTracker.Contains(request.RequestId))
                        {
                            collection.Add(request);
                            idTracker.Add(request.RequestId);
                            return true;
                        }
                    }
                    break;

                case FirebaseEventType.Delete:
                    if (existing != null)
                    {
                        collection.Remove(existing);
                        idTracker.Remove(request.RequestId);
                        return true;
                    }
                    break;
            }

            return false;
        }

        // 🔥 YENİ: In-place sorting (en yeni üstte)
        private void SortRequestsInPlace(ObservableCollection<ServiceRequest> collection)
        {
            var sorted = collection.OrderByDescending(r => r.RequestedAt).ToList();

            for (int i = 0; i < sorted.Count; i++)
            {
                var currentIndex = collection.IndexOf(sorted[i]);
                if (currentIndex != i && currentIndex >= 0)
                {
                    collection.Move(currentIndex, i);
                }
            }
        }

        // 🔥 OPTİMİZE: Refresh command
        [RelayCommand]
        private async Task RefreshRequestsAsync()
        {
            if (IsRefreshing) return;

            try
            {
                IsRefreshing = true;

                // Listener'ı durdur
                _requestsSubscription?.Dispose();
                _requestsSubscription = null;

                // State'i sıfırla
                _incomingRequestIds.Clear();
                _outgoingRequestIds.Clear();
                IncomingRequests.Clear();
                OutgoingRequests.Clear();
                _initialLoadComplete = false;

                // Listener'ı yeniden başlat
                StartListeningForRequests();

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
        private async Task LoadRequestsAsync()
        {
            // Real-time listener zaten çalışıyor
            if (!_initialLoadComplete)
            {
                IsLoading = true;
            }
        }

        [RelayCommand]
        private async Task AcceptRequestAsync(ServiceRequest request) =>
            await HandleResponseAsync(request, true);

        [RelayCommand]
        private async Task DeclineRequestAsync(ServiceRequest request) =>
            await HandleResponseAsync(request, false);

        private async Task HandleResponseAsync(ServiceRequest request, bool accepted)
        {
            if (request == null || request.Status != ServiceRequestStatus.Pending)
                return;

            try
            {
                IsLoading = true;

                var result = await _serviceService.RespondToRequestAsync(request.RequestId, accepted);

                if (result.Success)
                {
                    // Real-time listener otomatik güncelleyecek
                    var message = accepted ? "Talep kabul edildi" : "Talep reddedildi";
                    await Shell.Current.DisplayAlert("Başarılı", message, "Tamam");
                }
                else
                {
                    await Shell.Current.DisplayAlert("Hata", result.Message, "Tamam");
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", ex.Message, "Tamam");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task CompleteRequestAsync(ServiceRequest request)
        {
            if (request == null || request.Status != ServiceRequestStatus.Accepted)
                return;

            decimal price = request.QuotedPrice ?? 0;
            string priceInfo = price > 0 ? $"Bu hizmetin ücreti {price} ₺ olarak kaydedilmiştir.\n\n" : "";

            var confirm = await Shell.Current.DisplayAlert(
                "Onay",
                $"{priceInfo}Hizmeti aldığınızı onaylıyor musunuz? Bu işlem geri alınamaz ve ödeme simülasyonu başlatılacaktır.",
                "Evet, Onayla",
                "Hayır"
            );

            if (!confirm) return;

            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser == null)
            {
                await Shell.Current.DisplayAlert("Hata", "Oturum bilgisi alınamadı.", "Tamam");
                return;
            }

            try
            {
                IsLoading = true;

                var result = await _serviceService.SimulatePaymentAndCompleteAsync(
                    request.RequestId,
                    currentUser.UserId,
                    SelectedPaymentMethod
                );

                if (result.Success)
                {
                    string message = SelectedPaymentMethod switch
                    {
                        PaymentMethodType.CardSim => $"Kart (Simülasyon) ile {price} ₺ ödeme başarıyla gerçekleştirildi.",
                        PaymentMethodType.BankTransferSim => $"EFT / Havale (Simülasyon) ile {price} ₺ ödeme başarıyla tamamlandı.",
                        _ => "Ödeme simülasyonu başarıyla tamamlandı."
                    };

                    await Shell.Current.DisplayAlert("Başarılı", message, "Tamam");
                    // Real-time listener otomatik güncelleyecek
                }
                else
                {
                    await Shell.Current.DisplayAlert("Hata", result.Message, "Tamam");
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", ex.Message, "Tamam");
            }
            finally
            {
                IsLoading = false;
            }
        }

        public void Dispose()
        {
            Console.WriteLine("🧹 ServiceRequestsViewModel dispose ediliyor...");
            _requestsSubscription?.Dispose();
            _requestsSubscription = null;
            _incomingRequestIds.Clear();
            _outgoingRequestIds.Clear();
        }
    }
}