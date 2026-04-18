using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
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
using KamPay.Services.Auth;

namespace KamPay.ViewModels
{
    public partial class ServiceRequestsViewModel : ObservableObject, IDisposable
    {
        private readonly IServiceSharingService _serviceService;
        private readonly IAuthenticationService _authService;
        private readonly IUserStateService _userStateService;
        // âœ… DIP FIX: FirebaseClient artÄ±k DI'den geliyor (new keyword kaldÄ±rÄ±ldÄ±)
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

        // Zaman aÅŸÄ±mÄ± ayarlarÄ±
        private CancellationTokenSource? _loadingTimeoutCts;
        private const int LoadingTimeoutMs = 5000; // 5 saniye

        // Empty View mesajlarÄ± iÃ§in kontrol property'leri
        [ObservableProperty]
        private bool _hasOutgoingRequests; // HasOutgoingRequests Ã¶zelliÄŸini Ã¼retir
        [ObservableProperty]
        private bool _hasIncomingRequests; // HasIncomingRequests Ã¶zelliÄŸini Ã¼retir
      
        private void UpdateHasRequests()
        {
            // Property isimlerini kullanÄ±yoruz (Source generator tarafÄ±ndan Ã¼retilenler)
            HasIncomingRequests = IncomingRequests.Any();
            HasOutgoingRequests = OutgoingRequests.Any();
        }
        public ObservableCollection<ServiceRequest> IncomingRequests { get; } = new();
        public ObservableCollection<ServiceRequest> OutgoingRequests { get; } = new();
        public ObservableCollection<PaymentOption> PaymentMethods { get; }



        public ServiceRequestsViewModel(
            IServiceSharingService serviceService, 
            IAuthenticationService authService, 
            IUserStateService userStateService,
            FirebaseClient firebaseClient) // âœ… DIP FIX: YENÄ° PARAMETRE
        {
            _serviceService = serviceService;
            _authService = authService;
            _userStateService = userStateService;
            _firebaseClient = firebaseClient ?? throw new ArgumentNullException(nameof(firebaseClient)); // âœ… DIP FIX: DI'den inject

            PaymentMethods = new ObservableCollection<PaymentOption>
            {
                new PaymentOption { Method = PaymentMethodType.CardSim, DisplayName = "Kart (SimÃ¼lasyon)" },
                new PaymentOption { Method = PaymentMethodType.BankTransferSim, DisplayName = "EFT / Havale (SimÃ¼lasyon)" }
            };

            // KullanÄ±cÄ± profil deÄŸiÅŸikliklerini dinle
            _userStateService.UserProfileChanged += OnUserProfileChanged;

            WeakReferenceMessenger.Default.Register<UserSessionChangedMessage>(this, (r, m) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (!m.Value) // Logout
                    {
                        _requestsSubscription?.Dispose();
                        _requestsSubscription = null;
                        IncomingRequests.Clear();
                        OutgoingRequests.Clear();
                        _incomingRequestIds.Clear();
                        _outgoingRequestIds.Clear();
                        _currentUserId = null;
                        _initialLoadComplete = false;
                    }
                    else // Login
                    {
                        _initialLoadComplete = false;
                        _ = InitializeAsync();
                    }
                });
            });

            KamPay.Helpers.AppLogger.DebugLog("âœ… ServiceRequestsViewModel oluÅŸturuldu (DIP uyumlu - FirebaseClient DI'den)");
            _ = InitializeAsync();
        }

        private void OnUserProfileChanged(object? sender, User updatedUser)
        {
            if (updatedUser == null) return;

            //  Kritik: UI'da anlÄ±k gÃ¼ncelleme iÃ§in MainThread'de Ã§alÄ±ÅŸtÄ±rÄ±lmalÄ±dÄ±r.
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Gelen taleplerdeki talep eden kiÅŸi bilgilerini gÃ¼ncelle
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
                
                // âœ… Snapshot + listener'Ä± baÅŸlat, loading indicator hÄ±zlÄ± kapansÄ±n
                await StartListeningForRequestsAsync();
            }
            else
            {
                IsLoading = false;
            }
        }

        // âœ… Snapshot yÃ¼kleme ve listener baÅŸlatma ayrÄ±ldÄ±
        private async Task StartListeningForRequestsAsync()
        {
            if (_requestsSubscription != null || string.IsNullOrEmpty(_currentUserId)) return;

            // Timeout mekanizmasÄ±nÄ± sÄ±fÄ±rla
            _loadingTimeoutCts?.Cancel();
            _loadingTimeoutCts = new CancellationTokenSource();
            var token = _loadingTimeoutCts.Token;

            try
            {
                // 1ï¸âƒ£ SNAPSHOT: HÄ±zlÄ± veri yÃ¼kleme
                var snapshotTask = LoadInitialSnapshotAsync(token);
                
                // 2ï¸âƒ£ LISTENER: Realtime gÃ¼ncellemeler iÃ§in
                StartRealtimeListener();
                
                // 3ï¸âƒ£ Snapshot yÃ¼klenene kadar bekle
                await snapshotTask;
                
                // âœ… Loading'i hemen kapat (snapshot yÃ¼klendi, liste dolu ya da boÅŸ)
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (!_initialLoadComplete)
                    {
                        _initialLoadComplete = true;
                        IsLoading = false;
                        UpdateHasRequests();
                        KamPay.Helpers.AppLogger.DebugLog("âœ… Snapshot yÃ¼klendi, loading kapatÄ±ldÄ±");
                    }
                });
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âŒ StartListeningForRequestsAsync hatasÄ±: {ex.Message}");
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    IsLoading = false;
                    UpdateHasRequests();
                });
            }
        }

        private async Task LoadInitialSnapshotAsync(CancellationToken token)
        {
            try
            {
                // Firebase'den snapshot al (iki sorgu: gelen + giden)
                var incomingTask = _firebaseClient
                    .Child(Constants.ServiceRequestsCollection)
                    .OrderBy("ProviderId")
                    .EqualTo(_currentUserId)
                    .OnceAsync<ServiceRequest>();

                var outgoingTask = _firebaseClient
                    .Child(Constants.ServiceRequestsCollection)
                    .OrderBy("RequesterId")
                    .EqualTo(_currentUserId)
                    .OnceAsync<ServiceRequest>();

                var results = await Task.WhenAll(incomingTask, outgoingTask);

                if (token.IsCancellationRequested) return;

                var incomingData = results[0];
                var outgoingData = results[1];

                // âœ… Verileri UI'a ekle
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    // Gelen talepler
                    foreach (var item in incomingData)
                    {
                        var request = item.Object;
                        request.RequestId = item.Key;
                        
                        if (!_incomingRequestIds.Contains(request.RequestId))
                        {
                            IncomingRequests.Add(request);
                            _incomingRequestIds.Add(request.RequestId);
                        }
                    }

                    // Giden talepler
                    foreach (var item in outgoingData)
                    {
                        var request = item.Object;
                        request.RequestId = item.Key;
                        
                        if (!_outgoingRequestIds.Contains(request.RequestId))
                        {
                            OutgoingRequests.Add(request);
                            _outgoingRequestIds.Add(request.RequestId);
                        }
                    }

                    // SÄ±ralama
                    SortRequestsInPlace(IncomingRequests);
                    SortRequestsInPlace(OutgoingRequests);

                    KamPay.Helpers.AppLogger.DebugLog($"ğŸ“Š Snapshot yÃ¼klendi: {IncomingRequests.Count} gelen, {OutgoingRequests.Count} giden talep");
                });
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âš ï¸ Snapshot yÃ¼kleme hatasÄ±: {ex.Message}");
            }
        }

        private void StartRealtimeListener()
        {
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
                                ProcessRequestBatch(events);
                                UpdateHasRequests();
                            }
                            catch (Exception ex)
                            {
                                KamPay.Helpers.AppLogger.DebugLog($"âŒ Request batch hatasÄ±: {ex.Message}");
                            }
                        });
                    },
                    error =>
                    {
                        KamPay.Helpers.AppLogger.DebugLog($"âŒ Firebase listener hatasÄ±: {error.Message}");
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

                // Gelen talep mi?  (ben hizmet saÄŸlayÄ±cÄ±yÄ±m)
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

            //  Sadece deÄŸiÅŸenler iÃ§in sÄ±ralama
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
                        // GÃ¼ncelleme
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

        //  In-place sorting (en yeni Ã¼stte)
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

        // SAÄLAYICI Ä°Ã‡Ä°N
        [RelayCommand]
        private async Task FinishServiceAsync(ServiceRequest request)
        {
            if (request == null) return;

            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser == null)
            {
                await Shell.Current.DisplayAlert("Hata", "Oturum bilgisi alÄ±namadÄ±.", "Tamam");
                return;
            }

            var confirm = await Shell.Current.DisplayAlert("Tamamla", "Hizmeti bitirdiÄŸinizi bildirmek istiyor musunuz?", "Evet", "HayÄ±r");
            if (!confirm) return;

            IsLoading = true;
            var result = await _serviceService.ProviderFinishServiceAsync(request.RequestId, currentUser.UserId);
            if (result.Success)
                await Shell.Current.DisplayAlert("BaÅŸarÄ±lÄ±", result.Message, "Tamam");
            IsLoading = false;
            await LoadRequestsAsync();
        }

        // TALEP EDEN Ä°Ã‡Ä°N
        [RelayCommand]
        private async Task ConfirmServiceAsync(ServiceRequest request)
        {
            if (request == null) return;

            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser == null)
            {
                await Shell.Current.DisplayAlert("Hata", "Oturum bilgisi alÄ±namadÄ±.", "Tamam");
                return;
            }

            var confirm = await Shell.Current.DisplayAlert("Onayla", "Hizmeti aldÄ±ÄŸÄ±nÄ±zÄ± onaylÄ±yor musunuz? (Krediler transfer edilecektir)", "Evet", "HayÄ±r");
            if (!confirm) return;

            IsLoading = true;
            var result = await _serviceService.RequesterConfirmServiceAsync(request.RequestId, currentUser.UserId);
            if (result.Success)
                await Shell.Current.DisplayAlert("BaÅŸarÄ±lÄ±", result.Message, "Tamam");
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

                // Listener'Ä± durdur
                _requestsSubscription?.Dispose();
                _requestsSubscription = null;

                // State'i sÄ±fÄ±rla
                _incomingRequestIds.Clear();
                _outgoingRequestIds.Clear();
                IncomingRequests.Clear();
                OutgoingRequests.Clear();
                _initialLoadComplete = false;

                // Listener'Ä± yeniden baÅŸlat (snapshot + realtime)
                await StartListeningForRequestsAsync();
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âŒ Refresh hatasÄ±: {ex.Message}");
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        [RelayCommand]
        private async Task LoadRequestsAsync()
        {
            // EÄŸer veriler zaten yÃ¼klendiyse ve listener aktifse bir ÅŸey yapma
            if (_initialLoadComplete && _requestsSubscription != null)
            {
                return;
            }

            try
            {
                IsLoading = true;

                // EÄŸer listener bir ÅŸekilde durduysa veya hiÃ§ baÅŸlamadÄ±ysa yeniden baÅŸlat
                if (_requestsSubscription == null)
                {
                    await StartListeningForRequestsAsync();
                }
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âŒ LoadRequestsAsync hatasÄ±: {ex.Message}");
                IsLoading = false;
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
                    // Real-time listener otomatik gÃ¼ncelleyecek
                    var message = accepted ? "Talep kabul edildi" : "Talep reddedildi";
                    await Shell.Current.DisplayAlert("BaÅŸarÄ±lÄ±", message, "Tamam");
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
                await Shell.Current.DisplayAlert("Hata", "Oturum bilgisi alÄ±namadÄ±.", "Tamam");
                return;
            }

            // QuotedPrice varsa onu, yoksa Price'Ä± kullan
            decimal price = request.QuotedPrice ?? request.Price;

            // âœ… YENÄ° AKIÅ: Ãœcretli hizmet iÃ§in PaymentPage'e yÃ¶nlendir
            if (price > 0)
            {
                try
                {
                    IsLoading = true;

                    // ServiceRequest'i Transaction modeline dÃ¶nÃ¼ÅŸtÃ¼r
                    var transaction = new Transaction
                    {
                        TransactionId = $"service_{request.RequestId}",
                        ProductId = request.ServiceId,
                        ProductTitle = request.ServiceTitle,
                        Type = ProductType.Satis, // Hizmet de satÄ±ÅŸ gibi iÅŸleniyor
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

                    // âœ… KRÄ°TÄ°K: Transaction'Ä± Firebase'e kaydet
                    await _firebaseClient
                        .Child(Constants.TransactionsCollection)
                        .Child(transaction.TransactionId)
                        .PutAsync(transaction);

                    KamPay.Helpers.AppLogger.DebugLog($"âœ… Hizmet iÃ§in geÃ§ici transaction oluÅŸturuldu: {transaction.TransactionId}");

                    // PaymentPage'e git (ÃœrÃ¼n satÄ±ÅŸÄ± ile aynÄ± akÄ±ÅŸ)
                    var navigationParameter = new Dictionary<string, object>
                    {
                        { "Transaction", transaction }
                    };

                    await Shell.Current.GoToAsync(nameof(PaymentPage), navigationParameter);
                }
                catch (Exception ex)
                {
                    KamPay.Helpers.AppLogger.DebugLog($"âŒ Transaction oluÅŸturma hatasÄ±: {ex.Message}");
                    await Shell.Current.DisplayAlert("Hata", "Ã–deme sayfasÄ± aÃ§Ä±lamadÄ±: " + ex.Message, "Tamam");
                }
                finally
                {
                    IsLoading = false;
                }
            }
            else
            {
                // âœ… Ãœcretsiz/Zaman Kredisi: Eski akÄ±ÅŸ (deÄŸiÅŸiklik yok)
                string priceInfo = request.TimeCreditValue > 0
                    ? $"Bu hizmet iÃ§in {request.TimeCreditValue} saat kredi transfer edilecektir.\n\n"
                    : "";

                var confirm = await Shell.Current.DisplayAlert(
                    "Onay",
                    $"{priceInfo}Hizmeti aldÄ±ÄŸÄ±nÄ±zÄ± onaylÄ±yor musunuz?",
                    "Evet, Onayla",
                    "HayÄ±r"
                );

                if (!confirm) return;

                try
                {
                    IsLoading = true;

                    // Kredi transferi yap
                    var result = await _serviceService.CompleteRequestAsync(request.RequestId, currentUser.UserId);

                    if (result.Success)
                    {
                        await Shell.Current.DisplayAlert("BaÅŸarÄ±lÄ±", "Hizmet baÅŸarÄ±yla tamamlandÄ±!", "Tamam");
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

        //  MesajlaÅŸma BaÅŸlatma Komutu
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
                    await Shell.Current.DisplayAlert("Hata", "Oturum bilgisi alÄ±namadÄ±.", "Tamam");
                    return;
                }

                var result = await _serviceService.StartConversationForRequestAsync(request.RequestId, currentUser.UserId);

                if (result.Success)
                {
                    //  : ChatPage kullanÄ±lÄ±yor, MessagingPage deÄŸil
                    KamPay.Helpers.AppLogger.DebugLog($"âœ… KonuÅŸma ID'si: {result.Data}");
                    await Shell.Current.GoToAsync($"{nameof(Views.ChatPage)}?conversationId={result.Data}");
                }
                else
                {
                    await Shell.Current.DisplayAlert("Hata", result.Message, "Tamam");
                }
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âŒ StartConversation hatasÄ±: {ex.Message}");
                await Shell.Current.DisplayAlert("Hata", ex.Message, "Tamam");
            }
            finally
            {
                IsLoading = false;
            }
        }

        //  Fiyat Teklifi GÃ¶nderme Komutu
        [RelayCommand]
        private async Task ProposePriceAsync(ServiceRequest request)
        {
            if (request == null) return;

            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser == null || request.RequesterId != currentUser.UserId)
            {
                await Shell.Current.DisplayAlert("UyarÄ±", "Sadece talep eden kiÅŸi fiyat teklif edebilir.", "Tamam");
                return;
            }

            try
            {
                string priceInput = await Shell.Current.DisplayPromptAsync(
                    "Fiyat Teklifi",
                    $"'{request.ServiceTitle}' iÃ§in teklif etmek istediÄŸiniz fiyatÄ± girin:\n(Mevcut fiyat: {request.Price} â‚º)",
                    "GÃ¶nder",
                    "Ä°ptal",
                    keyboard: Keyboard.Numeric,
                    initialValue: request.Price.ToString()
                );

                if (string.IsNullOrWhiteSpace(priceInput)) return;

                if (!decimal.TryParse(priceInput, out decimal proposedPrice) || proposedPrice <= 0)
                {
                    await Shell.Current.DisplayAlert("Hata", "GeÃ§erli bir fiyat giriniz.", "Tamam");
                    return;
                }

                IsLoading = true;

                var result = await _serviceService.ProposePrice(request.RequestId, proposedPrice, currentUser.UserId);

                if (result.Success)
                {
                    await Shell.Current.DisplayAlert("BaÅŸarÄ±lÄ±", result.Message, "Tamam");
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

        // : KarÅŸÄ± Teklif GÃ¶nderme Komutu
        [RelayCommand]
        private async Task SendCounterOfferAsync(ServiceRequest request)
        {
            if (request == null) return;

            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser == null || request.ProviderId != currentUser.UserId)
            {
                await Shell.Current.DisplayAlert("UyarÄ±", "Sadece hizmet saÄŸlayÄ±cÄ± karÅŸÄ± teklif verebilir.", "Tamam");
                return;
            }

            try
            {
                string priceInfo = request.ProposedPriceByRequester.HasValue
                    ? $"Talep eden kiÅŸinin teklifi: {request.ProposedPriceByRequester} â‚º\n"
                    : "";

                string priceInput = await Shell.Current.DisplayPromptAsync(
                    "KarÅŸÄ± Teklif",
                    $"{priceInfo}KarÅŸÄ± teklifinizi girin:\n(Orijinal fiyat: {request.Price} â‚º)",
                    "GÃ¶nder",
                    "Ä°ptal",
                    keyboard: Keyboard.Numeric,
                    initialValue: request.Price.ToString()
                );

                if (string.IsNullOrWhiteSpace(priceInput)) return;

                if (!decimal.TryParse(priceInput, out decimal counterOffer) || counterOffer <= 0)
                {
                    await Shell.Current.DisplayAlert("Hata", "GeÃ§erli bir fiyat giriniz.", "Tamam");
                    return;
                }

                IsLoading = true;

                var result = await _serviceService.SendCounterOfferAsync(request.RequestId, counterOffer, currentUser.UserId);

                if (result.Success)
                {
                    await Shell.Current.DisplayAlert("BaÅŸarÄ±lÄ±", result.Message, "Tamam");
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

        //  AnlaÅŸÄ±lan FiyatÄ± Kabul Etme Komutu
        [RelayCommand]
        private async Task AcceptNegotiatedPriceAsync(ServiceRequest request)
        {
            if (request == null || !request.IsNegotiating) return;

            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser == null)
            {
                await Shell.Current.DisplayAlert("Hata", "Oturum bilgisi alÄ±namadÄ±.", "Tamam");
                return;
            }

            try
            {
                // AnlaÅŸÄ±lan fiyatÄ± belirle (karÅŸÄ± teklif > teklif > orijinal fiyat)
                decimal agreedPrice = request.CounterOfferByProvider ?? request.ProposedPriceByRequester ?? request.Price;

                bool confirm = await Shell.Current.DisplayAlert(
                    "Fiyat KabulÃ¼",
                    $"'{request.ServiceTitle}' hizmeti iÃ§in {agreedPrice} â‚º fiyatÄ± kabul ediyor musunuz?",
                    "Evet",
                    "HayÄ±r"
                );

                if (!confirm) return;

                IsLoading = true;

                var result = await _serviceService.AcceptNegotiatedPriceAsync(request.RequestId, currentUser.UserId);

                if (result.Success)
                {
                    await Shell.Current.DisplayAlert("BaÅŸarÄ±lÄ±", result.Message, "Tamam");
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
            KamPay.Helpers.AppLogger.DebugLog("ğŸ§¹ ServiceRequestsViewModel dispose ediliyor...");

            // Zaman aÅŸÄ±mÄ± iÅŸlemini iptal et ve temizle
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
