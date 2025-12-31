using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KamPay.Models;
using KamPay.Services;
using KamPay.Views;
using System.Collections.ObjectModel;
using System.Collections.Generic;
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
        private readonly IUserStateService _userStateService;
        private readonly FirebaseClient _firebaseClient;
        private IDisposable? _requestsSubscription;
        private string? _currentUserId;

        //  CACHE: Request tracking
        private readonly HashSet<string> _incomingRequestIds = new();
        private readonly HashSet<string> _outgoingRequestIds = new();
        private bool _initialLoadComplete = false;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private bool isRefreshing;

        [ObservableProperty]
        private bool isIncomingSelected = true;

        [ObservableProperty]
        private bool isOutgoingSelected = false;

        // Zaman aşımı ayarları
        private CancellationTokenSource? _loadingTimeoutCts;
        private const int LoadingTimeoutMs = 5000; // 5 saniye

        // Empty View mesajları için kontrol property'leri
        [ObservableProperty]
        private bool _hasOutgoingRequests; // HasOutgoingRequests özelliğini üretir
        [ObservableProperty]
        private bool _hasIncomingRequests; // HasIncomingRequests özelliğini üretir
      
        private void UpdateHasRequests()
        {
            // Property isimlerini kullanıyoruz (Source generator tarafından üretilenler)
            HasIncomingRequests = IncomingRequests.Any();
            HasOutgoingRequests = OutgoingRequests.Any();
        }
        public ObservableCollection<ServiceRequest> IncomingRequests { get; } = new();
        public ObservableCollection<ServiceRequest> OutgoingRequests { get; } = new();
        public ObservableCollection<PaymentOption> PaymentMethods { get; }



        public ServiceRequestsViewModel(IServiceSharingService serviceService, IAuthenticationService authService, IUserStateService userStateService)
        {
            _serviceService = serviceService;
            _authService = authService;
            _userStateService = userStateService;
            _firebaseClient = new FirebaseClient(Constants.FirebaseRealtimeDbUrl);

            PaymentMethods = new ObservableCollection<PaymentOption>
            {
                new PaymentOption { Method = PaymentMethodType.CardSim, DisplayName = "Kart (Simülasyon)" },
                new PaymentOption { Method = PaymentMethodType.BankTransferSim, DisplayName = "EFT / Havale (Simülasyon)" }
            };

            // Kullanıcı profil değişikliklerini dinle
            _userStateService.UserProfileChanged += OnUserProfileChanged;

            _ = InitializeAsync();
        }

        private void OnUserProfileChanged(object? sender, User updatedUser)
        {
            if (updatedUser == null) return;

            //  Kritik: UI'da anlık güncelleme için MainThread'de çalıştırılmalıdır.
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Gelen taleplerdeki talep eden kişi bilgilerini güncelle
                foreach (var request in IncomingRequests.Where(r => r.RequesterId == updatedUser.UserId))
                {
                    request.RequesterName = updatedUser.FullName;
                }
            });
        }

        public class PaymentOption
        {
            public PaymentMethodType Method { get; set; }
            public string DisplayName { get; set; } = string.Empty;
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
        private async Task LoadInitialSnapshotAsync(CancellationToken token)
        {
            try
            {
                // Sadece bu kullanıcıya gelen veya giden taleplerden biri var mı diye bakmak yeterli
                var incomingTask = _firebaseClient
                    .Child(Constants.ServiceRequestsCollection)
                    .OrderBy("ProviderId")
                    .EqualTo(_currentUserId)
                    .LimitToFirst(1)
                    .OnceAsync<ServiceRequest>();

                var outgoingTask = _firebaseClient
                    .Child(Constants.ServiceRequestsCollection)
                    .OrderBy("RequesterId")
                    .EqualTo(_currentUserId)
                    .LimitToFirst(1)
                    .OnceAsync<ServiceRequest>();

                var results = await Task.WhenAll(incomingTask, outgoingTask);

                if (token.IsCancellationRequested) return;

                // Eğer her iki tarafta da hiç kayıt yoksa loading'i kapat
                bool isEmpty = results.All(r => r == null || !r.Any());

                if (isEmpty)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (!_initialLoadComplete)
                        {
                            _initialLoadComplete = true;
                            IsLoading = false;
                            UpdateHasRequests();
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Snapshot check hatası: {ex.Message}");
            }
        }

        private void StartListeningForRequests()
        {
            if (_requestsSubscription != null || string.IsNullOrEmpty(_currentUserId)) return;

            // Timeout mekanizmasını sıfırla
            _loadingTimeoutCts?.Cancel();
            _loadingTimeoutCts = new CancellationTokenSource();
            var token = _loadingTimeoutCts.Token;

            // 1. Boş veritabanı durumu için hızlı snapshot kontrolü
            _ = LoadInitialSnapshotAsync(token);

            // 2. Timeout: Belirlenen sürede veri gelmezse loading'i zorla kapat
            Task.Delay(LoadingTimeoutMs, token).ContinueWith(t =>
            {
                if (t.IsCanceled) return;
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (!_initialLoadComplete)
                    {
                        IsLoading = false;
                        _initialLoadComplete = true;
                        UpdateHasRequests();
                    }
                });
            }, TaskContinuationOptions.OnlyOnRanToCompletion);

            _requestsSubscription = _firebaseClient
                .Child(Constants.ServiceRequestsCollection)
                .AsObservable<ServiceRequest>()
                .Where(e => e.Object != null)
                .Buffer(TimeSpan.FromMilliseconds(300))
                .Subscribe(
                    events =>
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            try
                            {
                                // Veri geldiğinde timeout'u iptal et
                                _loadingTimeoutCts?.Cancel();

                                ProcessRequestBatch(events);

                                // İlk veri geldiğinde (boş olsa dahi batch içinden geçince) yüklemeyi bitir
                                if (!_initialLoadComplete)
                                {
                                    _initialLoadComplete = true;
                                    IsLoading = false;
                                }
                                UpdateHasRequests();
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"❌ Request batch hatası: {ex.Message}");
                            }
                        });
                    },
                    error =>
                    {
                        Console.WriteLine($"❌ Firebase listener hatası: {error.Message}");
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            IsLoading = false;
                            UpdateHasRequests();
                        });
                    });
        }
        private void ProcessRequestBatch(IList<FirebaseEvent<ServiceRequest>> events)
        {
            bool hasIncomingChanges = false;
            bool hasOutgoingChanges = false;

            foreach (var e in events)
            {
                var request = e.Object;
                request.RequestId = e.Key;

                // Gelen talep mi?  (ben hizmet sağlayıcıyım)
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

            //  İLK VERİ GELDİĞİNDE LOADING'İ KAPAT
            if ((hasIncomingChanges || hasOutgoingChanges) && IsLoading)
            {
                IsLoading = false;
            }

            //  Sadece değişenler için sıralama
            if (hasIncomingChanges)
            {
                SortRequestsInPlace(IncomingRequests);
            }

            if (hasOutgoingChanges)
            {
                SortRequestsInPlace(OutgoingRequests);
            }
        }

        //  Smart collection update
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
                        //  Duplicate check
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

        //  In-place sorting (en yeni üstte)
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
        // SAĞLAYICI İÇİN
        [RelayCommand]
        private async Task FinishServiceAsync(ServiceRequest request)
        {
            if (request == null) return;

            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser == null)
            {
                await Shell.Current.DisplayAlert("Hata", "Oturum bilgisi alınamadı.", "Tamam");
                return;
            }

            var confirm = await Shell.Current.DisplayAlert("Tamamla", "Hizmeti bitirdiğinizi bildirmek istiyor musunuz?", "Evet", "Hayır");
            if (!confirm) return;

            IsLoading = true;
            var result = await _serviceService.ProviderFinishServiceAsync(request.RequestId, currentUser.UserId);
            if (result.Success)
                await Shell.Current.DisplayAlert("Başarılı", result.Message, "Tamam");
            IsLoading = false;
            await LoadRequestsAsync();
        }

        // TALEP EDEN İÇİN
        [RelayCommand]
        private async Task ConfirmServiceAsync(ServiceRequest request)
        {
            if (request == null) return;

            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser == null)
            {
                await Shell.Current.DisplayAlert("Hata", "Oturum bilgisi alınamadı.", "Tamam");
                return;
            }

            var confirm = await Shell.Current.DisplayAlert("Onayla", "Hizmeti aldığınızı onaylıyor musunuz? (Krediler transfer edilecektir)", "Evet", "Hayır");
            if (!confirm) return;

            IsLoading = true;
            var result = await _serviceService.RequesterConfirmServiceAsync(request.RequestId, currentUser.UserId);
            if (result.Success)
                await Shell.Current.DisplayAlert("Başarılı", result.Message, "Tamam");
            IsLoading = false;
            await LoadRequestsAsync();
        }
        //  : Refresh command
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
        private Task LoadRequestsAsync()
        {
            // Real-time listener zaten çalışıyor
            if (!_initialLoadComplete)
            {
                IsLoading = true;
            }

            return Task.CompletedTask;
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
        private void SelectIncoming()
        {
            IsIncomingSelected = true;
            IsOutgoingSelected = false;
        }

        [RelayCommand]
        private void SelectOutgoing()
        {
            IsIncomingSelected = false;
            IsOutgoingSelected = true;
        }

        [RelayCommand]
        private async Task CompleteRequestAsync(ServiceRequest request)
        {
            if (request == null || request.Status != ServiceRequestStatus.Accepted)
                return;

            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser == null)
            {
                await Shell.Current.DisplayAlert("Hata", "Oturum bilgisi alınamadı.", "Tamam");
                return;
            }

            // QuotedPrice varsa onu, yoksa Price'ı kullan
            decimal price = request.QuotedPrice ?? request.Price;

            // ✅ YENİ AKIŞ: Ücretli hizmet için PaymentPage'e yönlendir
            if (price > 0)
            {
                try
                {
                    IsLoading = true;

                    // ServiceRequest'i Transaction modeline dönüştür
                    var transaction = new Transaction
                    {
                        TransactionId = $"service_{request.RequestId}",
                        ProductId = request.ServiceId,
                        ProductTitle = request.ServiceTitle,
                        Type = ProductType.Satis, // Hizmet de satış gibi işleniyor
                        SellerId = request.ProviderId,
                        SellerName = request.ProviderName,
                        BuyerId = request.RequesterId,
                        BuyerName = request.RequesterName,
                        Price = price,
                        QuotedPrice = price,
                        Status = TransactionStatus.Accepted,
                        PaymentStatus = PaymentStatus.Pending,
                        CreatedAt = request.RequestedAt,
                        UpdatedAt = DateTime.UtcNow
                    };

                    // ✅ KRİTİK: Transaction'ı Firebase'e kaydet
                    var firebaseClient = new FirebaseClient(Constants.FirebaseRealtimeDbUrl);
                    await firebaseClient
                        .Child(Constants.TransactionsCollection)
                        .Child(transaction.TransactionId)
                        .PutAsync(transaction);

                    Console.WriteLine($"✅ Hizmet için geçici transaction oluşturuldu: {transaction.TransactionId}");

                    // PaymentPage'e git (Ürün satışı ile aynı akış)
                    var navigationParameter = new Dictionary<string, object>
                    {
                        { "Transaction", transaction }
                    };

                    await Shell.Current.GoToAsync(nameof(PaymentPage), navigationParameter);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Transaction oluşturma hatası: {ex.Message}");
                    await Shell.Current.DisplayAlert("Hata", "Ödeme sayfası açılamadı: " + ex.Message, "Tamam");
                }
                finally
                {
                    IsLoading = false;
                }
            }
            else
            {
                // ✅ Ücretsiz/Zaman Kredisi: Eski akış (değişiklik yok)
                string priceInfo = request.TimeCreditValue > 0
                    ? $"Bu hizmet için {request.TimeCreditValue} saat kredi transfer edilecektir.\n\n"
                    : "";

                var confirm = await Shell.Current.DisplayAlert(
                    "Onay",
                    $"{priceInfo}Hizmeti aldığınızı onaylıyor musunuz?",
                    "Evet, Onayla",
                    "Hayır"
                );

                if (!confirm) return;

                try
                {
                    IsLoading = true;

                    // Kredi transferi yap
                    var result = await _serviceService.CompleteRequestAsync(request.RequestId, currentUser.UserId);

                    if (result.Success)
                    {
                        await Shell.Current.DisplayAlert("Başarılı", "Hizmet başarıyla tamamlandı!", "Tamam");
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
        }

        //  Mesajlaşma Başlatma Komutu
        [RelayCommand]
        private async Task StartConversationAsync(ServiceRequest request)
        {
            if (request == null) return;

            try
            {
                IsLoading = true;

                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    await Shell.Current.DisplayAlert("Hata", "Oturum bilgisi alınamadı.", "Tamam");
                    return;
                }

                var result = await _serviceService.StartConversationForRequestAsync(request.RequestId, currentUser.UserId);

                if (result.Success)
                {
                    //  : ChatPage kullanılıyor, MessagingPage değil
                    Console.WriteLine($"✅ Konuşma ID'si: {result.Data}");
                    await Shell.Current.GoToAsync($"{nameof(Views.ChatPage)}?conversationId={result.Data}");
                }
                else
                {
                    await Shell.Current.DisplayAlert("Hata", result.Message, "Tamam");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ StartConversation hatası: {ex.Message}");
                await Shell.Current.DisplayAlert("Hata", ex.Message, "Tamam");
            }
            finally
            {
                IsLoading = false;
            }
        }

        //  Fiyat Teklifi Gönderme Komutu
        [RelayCommand]
        private async Task ProposePriceAsync(ServiceRequest request)
        {
            if (request == null) return;

            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser == null || request.RequesterId != currentUser.UserId)
            {
                await Shell.Current.DisplayAlert("Uyarı", "Sadece talep eden kişi fiyat teklif edebilir.", "Tamam");
                return;
            }

            try
            {
                string priceInput = await Shell.Current.DisplayPromptAsync(
                    "Fiyat Teklifi",
                    $"'{request.ServiceTitle}' için teklif etmek istediğiniz fiyatı girin:\n(Mevcut fiyat: {request.Price} ₺)",
                    "Gönder",
                    "İptal",
                    keyboard: Keyboard.Numeric,
                    initialValue: request.Price.ToString()
                );

                if (string.IsNullOrWhiteSpace(priceInput)) return;

                if (!decimal.TryParse(priceInput, out decimal proposedPrice) || proposedPrice <= 0)
                {
                    await Shell.Current.DisplayAlert("Hata", "Geçerli bir fiyat giriniz.", "Tamam");
                    return;
                }

                IsLoading = true;

                var result = await _serviceService.ProposePrice(request.RequestId, proposedPrice, currentUser.UserId);

                if (result.Success)
                {
                    await Shell.Current.DisplayAlert("Başarılı", result.Message, "Tamam");
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

        // : Karşı Teklif Gönderme Komutu
        [RelayCommand]
        private async Task SendCounterOfferAsync(ServiceRequest request)
        {
            if (request == null) return;

            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser == null || request.ProviderId != currentUser.UserId)
            {
                await Shell.Current.DisplayAlert("Uyarı", "Sadece hizmet sağlayıcı karşı teklif verebilir.", "Tamam");
                return;
            }

            try
            {
                string priceInfo = request.ProposedPriceByRequester.HasValue
                    ? $"Talep eden kişinin teklifi: {request.ProposedPriceByRequester} ₺\n"
                    : "";

                string priceInput = await Shell.Current.DisplayPromptAsync(
                    "Karşı Teklif",
                    $"{priceInfo}Karşı teklifinizi girin:\n(Orijinal fiyat: {request.Price} ₺)",
                    "Gönder",
                    "İptal",
                    keyboard: Keyboard.Numeric,
                    initialValue: request.Price.ToString()
                );

                if (string.IsNullOrWhiteSpace(priceInput)) return;

                if (!decimal.TryParse(priceInput, out decimal counterOffer) || counterOffer <= 0)
                {
                    await Shell.Current.DisplayAlert("Hata", "Geçerli bir fiyat giriniz.", "Tamam");
                    return;
                }

                IsLoading = true;

                var result = await _serviceService.SendCounterOfferAsync(request.RequestId, counterOffer, currentUser.UserId);

                if (result.Success)
                {
                    await Shell.Current.DisplayAlert("Başarılı", result.Message, "Tamam");
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

        //  Anlaşılan Fiyatı Kabul Etme Komutu
        [RelayCommand]
        private async Task AcceptNegotiatedPriceAsync(ServiceRequest request)
        {
            if (request == null || !request.IsNegotiating) return;

            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser == null)
            {
                await Shell.Current.DisplayAlert("Hata", "Oturum bilgisi alınamadı.", "Tamam");
                return;
            }

            try
            {
                // Anlaşılan fiyatı belirle (karşı teklif > teklif > orijinal fiyat)
                decimal agreedPrice = request.CounterOfferByProvider ?? request.ProposedPriceByRequester ?? request.Price;

                bool confirm = await Shell.Current.DisplayAlert(
                    "Fiyat Kabulü",
                    $"'{request.ServiceTitle}' hizmeti için {agreedPrice} ₺ fiyatı kabul ediyor musunuz?",
                    "Evet",
                    "Hayır"
                );

                if (!confirm) return;

                IsLoading = true;

                var result = await _serviceService.AcceptNegotiatedPriceAsync(request.RequestId, currentUser.UserId);

                if (result.Success)
                {
                    await Shell.Current.DisplayAlert("Başarılı", result.Message, "Tamam");
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

            // Zaman aşımı işlemini iptal et ve temizle
            _loadingTimeoutCts?.Cancel();
            _loadingTimeoutCts?.Dispose();

            _requestsSubscription?.Dispose();
            _requestsSubscription = null;
            _userStateService.UserProfileChanged -= OnUserProfileChanged;
            _incomingRequestIds.Clear();
            _outgoingRequestIds.Clear();
        }
    }
}