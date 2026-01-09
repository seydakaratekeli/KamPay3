using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Firebase.Database;
using Firebase.Database.Query;
using Firebase.Database.Streaming;
using KamPay.Helpers;
using KamPay.Models;
using KamPay.Services;
using KamPay.Views;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;

namespace KamPay.ViewModels
{
    public partial class OffersViewModel : ObservableObject, IDisposable
    {
        private readonly ITransactionService _transactionService;
        private readonly IAuthenticationService _authService;
        private readonly IUserStateService _userStateService;
        private IDisposable? _allOffersSubscription;
        private readonly FirebaseClient _firebaseClient;

        // Cache ve Durum Kontrolü
        private readonly HashSet<string> _incomingIds = new();
        private readonly HashSet<string> _outgoingIds = new();
        private bool _initialLoadComplete = false;

        // Yükleme kontrolü
        private bool _isInitialized = false;
        private string? _currentUserId;
        
        //  Timeout kontrolü için CancellationTokenSource
        private CancellationTokenSource? _loadingTimeoutCts;
        private const int LoadingTimeoutMs = 5000; // 5 saniye timeout

        public ObservableCollection<Transaction> IncomingOffers { get; } = new();
        public ObservableCollection<Transaction> OutgoingOffers { get; } = new();

        [ObservableProperty]
        private bool isLoading = true;

        [ObservableProperty]
        private bool isRefreshing;

        [ObservableProperty]
        private bool isIncomingSelected = true;

        [ObservableProperty]
        private bool isOutgoingSelected = false;

        //  Skeleton loader kontrolü
        [ObservableProperty]
        private bool isSkeletonVisible = true;

        //  Veri var mı kontrolü (empty message için)
        [ObservableProperty]
        private bool hasIncomingOffers = false;

        [ObservableProperty]
        private bool hasOutgoingOffers = false;

        public OffersViewModel(ITransactionService transactionService, IAuthenticationService authService, IUserStateService userStateService)
        {
            _transactionService = transactionService;
            _authService = authService;
            _userStateService = userStateService;
            _firebaseClient = new FirebaseClient(Constants.FirebaseRealtimeDbUrl);

            // Kullanıcı profil değişikliklerini dinle
            _userStateService.UserProfileChanged += OnUserProfileChanged;

            _ = InitializeAsync();
        }

        private void OnUserProfileChanged(object? sender, User updatedUser)
        {
            if (updatedUser == null) return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Gelen tekliflerdeki kullanıcı bilgilerini güncelle (alıcı bilgisi)
                foreach (var offer in IncomingOffers.Where(o => o.BuyerId == updatedUser.UserId))
                {
                    offer.BuyerName = updatedUser.FullName;
                }

                // Giden tekliflerdeki satıcı bilgilerini güncelle
                foreach (var offer in OutgoingOffers.Where(o => o.SellerId == updatedUser.UserId))
                {
                    offer.SellerName = updatedUser.FullName;
                }
            });
        }

        public async Task InitializeAsync()
        {
            if (_isInitialized) return;

            IsLoading = true;
            IsSkeletonVisible = true;

            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser == null)
            {
                IsLoading = false;
                IsSkeletonVisible = false;
                IsRefreshing = false;
                UpdateHasOffers();
                Debug.WriteLine("⚠️ Kullanıcı oturum açmamış");
                return;
            }

            _currentUserId = currentUser.UserId;
            StartListeningForOffers(_currentUserId);
            _isInitialized = true;
        }

        private void StartListeningForOffers(string userId)
        {
            if (_allOffersSubscription != null) return;

            Debug.WriteLine($" Offers listener başlatılıyor: {userId}");

            //  Timeout mekanizması
            _loadingTimeoutCts?.Cancel();
            _loadingTimeoutCts?.Dispose();
            _loadingTimeoutCts = new CancellationTokenSource();
            var timeoutToken = _loadingTimeoutCts.Token;

            //  Snapshot ile hızlı ilk yükleme
            _ = LoadInitialSnapshotAsync(userId, timeoutToken);

            //  Loading timeout - belirlenen süre içinde veri gelmezse loading'i kapat
            Task.Delay(LoadingTimeoutMs, timeoutToken).ContinueWith(t =>
            {
                if (t.IsCanceled) return;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (!_initialLoadComplete)
                    {
                        Debug.WriteLine("⏳ Loading timeout - veri gelmedi.");
                        IsLoading = false;
                        IsSkeletonVisible = false;
                        IsRefreshing = false;
                        UpdateHasOffers();
                    }
                });
            }, TaskContinuationOptions.OnlyOnRanToCompletion);

            //  Realtime listener
            _allOffersSubscription = _firebaseClient
                .Child(Constants.TransactionsCollection)
                .AsObservable<Transaction>()
                .Where(e => e.Object != null)
                .Buffer(TimeSpan.FromMilliseconds(400))
                .Where(batch => batch.Any())
                .Subscribe(
                    events =>
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            try
                            {
                                // Veri geldi → timeout'u iptal et
                                _loadingTimeoutCts?.Cancel();

                                ProcessOfferBatch(events, userId);

                                //  SADECE GERÇEK VERİ GELİNCE loading kapat
                                if (!_initialLoadComplete && ContainsRealOffer(events, userId))
                                {
                                    _initialLoadComplete = true;
                                    IsLoading = false;
                                    IsSkeletonVisible = false;
                                    IsRefreshing = false;
                                    Debug.WriteLine("✅ İlk gerçek realtime offer geldi — loading kapatıldı.");
                                }

                                UpdateHasOffers();
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"❌ Offer batch hatası: {ex.Message}");
                            }
                        });
                    },
                    error =>
                    {
                        Debug.WriteLine($"❌ Firebase listener hatası: {error.Message}");
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            IsLoading = false;
                            IsSkeletonVisible = false;
                            IsRefreshing = false;
                            UpdateHasOffers();
                        });
                    });
        }

        //  Snapshot ile hızlı ilk yükleme
        private async Task LoadInitialSnapshotAsync(string userId, CancellationToken token)
        {
            try
            {
                if (token.IsCancellationRequested) return;

                Debug.WriteLine("📸 Snapshot yükleniyor...");

                var snapshot = await _firebaseClient
                    .Child(Constants.TransactionsCollection)
                    .OnceAsync<Transaction>();

                if (token.IsCancellationRequested) return;

                var userOffers = snapshot
                    .Where(s => s.Object != null && 
                               (s.Object.SellerId == userId || s.Object.BuyerId == userId) &&
                               // ✅ HİZMET TRANSACTION'LARINI FİLTRELE
                               !(s.Key != null && s.Key.StartsWith("service_")))
                    .Select(s =>
                    {
                        var transaction = s.Object;
                        transaction.TransactionId = s.Key;
                        return transaction;
                    })
                    .OrderByDescending(t => t.CreatedAt)
                    .ToList();

                if (!userOffers.Any())
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        if (!_initialLoadComplete)
                        {
                            _initialLoadComplete = true;
                            IsLoading = false;
                            IsSkeletonVisible = false;
                            IsRefreshing = false; // ✅ EKLE: IsRefreshing'i de kapat
                            UpdateHasOffers();
                            Debug.WriteLine("✅ Snapshot yüklendi — teklif yok.");
                        }
                    });
                    return;
                }

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    foreach (var transaction in userOffers)
                    {
                        if (transaction.SellerId == userId)
                        {
                            if (!_incomingIds.Contains(transaction.TransactionId))
                            {
                                IncomingOffers.Add(transaction);
                                _incomingIds.Add(transaction.TransactionId);
                            }
                        }
                        else if (transaction.BuyerId == userId)
                        {
                            if (!_outgoingIds.Contains(transaction.TransactionId))
                            {
                                OutgoingOffers.Add(transaction);
                                _outgoingIds.Add(transaction.TransactionId);
                            }
                        }
                    }

                    if (!_initialLoadComplete)
                    {
                        _initialLoadComplete = true;
                        IsLoading = false;
                        IsSkeletonVisible = false;
                        IsRefreshing = false; // ✅ EKLE: IsRefreshing'i de kapat
                        UpdateHasOffers();
                        Debug.WriteLine($"✅ Snapshot yüklendi — {userOffers.Count} teklif bulundu.");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ Snapshot yüklenirken hata: {ex.Message}");
                // ✅ EKLE: Hata durumunda da IsRefreshing'i kapat
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    IsLoading = false;
                    IsSkeletonVisible = false;
                    IsRefreshing = false;
                    UpdateHasOffers();
                });
            }
        }
        // KamPay/ViewModels/OffersViewModel.cs içine ekleyin

        [RelayCommand]
        private async Task GoToPaymentAsync(Transaction transaction)
        {
            if (transaction == null) return;

            // Sadece onaylanmış ve ödemesi bekleyen satış/hizmet işlemleri için
            if (transaction.Status == TransactionStatus.Accepted &&
                transaction.PaymentStatus == PaymentStatus.Pending)
            {
                var navigationParameter = new Dictionary<string, object>
        {
            { "Transaction", transaction }
        };

                // PaymentPage'e yönlendir [daha önce AppShell'e kaydetmiştik]
                await Shell.Current.GoToAsync(nameof(PaymentPage), navigationParameter);
            }
            else
            {
                await Shell.Current.DisplayAlert("Bilgi", "Bu işlem için şu an ödeme yapılamaz.", "Tamam");
            }
        }
        //  Gerçek teklif geldi mi kontrol et
        private bool ContainsRealOffer(IList<FirebaseEvent<Transaction>> events, string userId)
        {
            return events.Any(e =>
                e.Object != null &&
                !string.IsNullOrWhiteSpace(e.Key) &&
                !string.IsNullOrWhiteSpace(e.Object?.TransactionId) &&
                (e.Object.SellerId == userId || e.Object.BuyerId == userId) &&
                // ✅ HİZMET TRANSACTION'LARINI FİLTRELE
                !(e.Key != null && e.Key.StartsWith("service_"))
            );
        }

        private void ProcessOfferBatch(IList<FirebaseEvent<Transaction>> events, string userId)
        {
            bool hasIncomingChanges = false;
            bool hasOutgoingChanges = false;

            foreach (var e in events)
            {
                if (e.Object == null) continue;

                var transaction = e.Object;
                transaction.TransactionId = e.Key;

                // ✅ Sadece ilgili kullanıcıya ait teklifleri işle
                if (transaction.SellerId != userId && transaction.BuyerId != userId)
                    continue;

                // ✅ KRİTİK FİLTRE: Hizmet transaction'larını hariç tut
                // Hizmetler için oluşturulan geçici transaction'lar "service_" ile başlıyor
                if (!string.IsNullOrEmpty(transaction.TransactionId) && 
                    transaction.TransactionId.StartsWith("service_"))
                {
                    Debug.WriteLine($"⚠️ Hizmet transaction'ı atlanıyor: {transaction.TransactionId}");
                    continue;
                }

                // ✅ EXTRA KORUMA: ProductId kontrolü
                // Eğer ProductId bir ServiceId ise (ServiceOffer koleksiyonunda varsa), atla
                if (!string.IsNullOrEmpty(transaction.ProductId) && 
                    transaction.ProductId.Length > 10) // ServiceId'ler genellikle GUID formatında
                {
                    // Bu ekstra bir kontrol, gerekirse ServiceOffer collection'ında arama yapabilirsiniz
                    // Şimdilik TransactionId kontrolü yeterli olacaktır
                }

                if (transaction.SellerId == userId)
                {
                    if (UpdateOfferInCollection(IncomingOffers, _incomingIds, transaction, e.EventType))
                        hasIncomingChanges = true;
                }
                else if (transaction.BuyerId == userId)
                {
                    if (UpdateOfferInCollection(OutgoingOffers, _outgoingIds, transaction, e.EventType))
                        hasOutgoingChanges = true;
                }
            }

            if (hasIncomingChanges) SortOffersInPlace(IncomingOffers);
            if (hasOutgoingChanges) SortOffersInPlace(OutgoingOffers);
        }

        private bool UpdateOfferInCollection(ObservableCollection<Transaction> collection, HashSet<string> idTracker, Transaction transaction, FirebaseEventType eventType)
        {
            var existing = collection.FirstOrDefault(t => t.TransactionId == transaction.TransactionId);

            switch (eventType)
            {
                case FirebaseEventType.InsertOrUpdate:
                    if (existing != null)
                    {
                        var index = collection.IndexOf(existing);
                        collection[index] = transaction;
                        return true;
                    }
                    else if (!idTracker.Contains(transaction.TransactionId))
                    {
                        collection.Add(transaction);
                        idTracker.Add(transaction.TransactionId);
                        return true;
                    }
                    break;

                case FirebaseEventType.Delete:
                    if (existing != null)
                    {
                        collection.Remove(existing);
                        idTracker.Remove(transaction.TransactionId);
                        return true;
                    }
                    break;
            }
            return false;
        }

        private void SortOffersInPlace(ObservableCollection<Transaction> collection)
        {
            var sorted = collection.OrderByDescending(t => t.CreatedAt).ToList();
            for (int i = 0; i < sorted.Count; i++)
            {
                var currentIndex = collection.IndexOf(sorted[i]);
                if (currentIndex != i && currentIndex >= 0) collection.Move(currentIndex, i);
            }
        }

        //  Veri durumunu güncelle
        private void UpdateHasOffers()
        {
            HasIncomingOffers = IncomingOffers.Any();
            HasOutgoingOffers = OutgoingOffers.Any();
            Debug.WriteLine($"📊 UpdateHasOffers: Gelen={IncomingOffers.Count} (HasIncoming={HasIncomingOffers}), Giden={OutgoingOffers.Count} (HasOutgoing={HasOutgoingOffers})");
        }

        [RelayCommand]
        private async Task RefreshOffersAsync()
        {
            if (IsRefreshing) return;
            
            try
            {
                IsRefreshing = true;
                
                // Listener'ı durdur
                _allOffersSubscription?.Dispose();
                _allOffersSubscription = null;
                
                // Cache'i temizle
                _incomingIds.Clear();
                _outgoingIds.Clear();
                IncomingOffers.Clear();
                OutgoingOffers.Clear();

                _initialLoadComplete = false;
                _isInitialized = false;

                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser != null)
                {
                    _currentUserId = currentUser.UserId;
                    StartListeningForOffers(currentUser.UserId);
                }

                // Listener'ın veri yüklemesi için kısa bir bekleme
                await Task.Delay(500);
            }
            catch (Exception ex) 
            { 
                Debug.WriteLine($"❌ Refresh hatası: {ex.Message}"); 
            }
            finally
            {
                // ✅ KRİTİK: IsRefreshing'i mutlaka false yap
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    IsRefreshing = false;
                    UpdateHasOffers();
                    Debug.WriteLine("✅ Refresh tamamlandı, IsRefreshing = false");
                });
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
        private async Task ManageDeliveryAsync(Transaction transaction)
        {
            if (transaction == null) return;
            await Shell.Current.GoToAsync($"{nameof(QRCodeDisplayPage)}?transactionId={transaction.TransactionId}");
        }

        [RelayCommand]
        private async Task AcceptOfferAsync(Transaction transaction) => await RespondToOfferInternalAsync(transaction, true);

        [RelayCommand]
        private async Task RejectOfferAsync(Transaction transaction) => await RespondToOfferInternalAsync(transaction, false);

        private async Task RespondToOfferInternalAsync(Transaction transaction, bool accept)
        {
            if (transaction == null) return;
            if (Application.Current?.MainPage == null) return;

            try
            {
                var result = await _transactionService.RespondToOfferAsync(transaction.TransactionId, accept);
                if (result.Success) 
                    await Application.Current.MainPage.DisplayAlert("Başarılı", $"Teklif {(accept ? "kabul edildi" : "reddedildi")}.", "Tamam");
                else 
                    await Application.Current.MainPage.DisplayAlert("Hata", result.Message, "Tamam");
            }
            catch (Exception ex) 
            { 
                await Application.Current.MainPage.DisplayAlert("Hata", ex.Message, "Tamam"); 
            }
        }

        [RelayCommand]
        private async Task CompletePaymentAsync(Transaction transaction)
        {
            if (transaction == null) return;

            // ✅ Sadece navigasyon yap - Ödeme işlemi PaymentPage'de gerçekleşecek
            if (transaction.Type == ProductType.Satis &&
                transaction.Status == TransactionStatus.Accepted &&
                transaction.PaymentStatus == PaymentStatus.Pending)
            {
                var navigationParameter = new Dictionary<string, object>
                {
                    { "Transaction", transaction }
                };

                // PaymentPage'e yönlendir
                await Shell.Current.GoToAsync(nameof(PaymentPage), navigationParameter);
            }
            else
            {
                await Application.Current.MainPage.DisplayAlert(
                    "Bilgi", 
                    "Bu işlem için şu an ödeme yapılamaz.", 
                    "Tamam"
                );
            }
        }

        [RelayCommand]
        private async Task ConfirmDonationReceivedAsync(Transaction transaction)
        {
            if (transaction == null) return;
            if (Application.Current?.MainPage == null) return;

            if (transaction.Type != ProductType.Bagis || transaction.Status != TransactionStatus.Accepted) return;

            var confirm = await Application.Current.MainPage.DisplayAlert("Onay",
                $"'{transaction.ProductTitle}' ürününü teslim aldığınızı onaylıyor musunuz?", "Evet, Teslim Aldım", "Hayır");

            if (!confirm) return;

            IsLoading = true;
            try
            {
                if (_transactionService is FirebaseTransactionService firebaseService)
                {
                    var currentUser = await _authService.GetCurrentUserAsync();
                    if (currentUser != null)
                    {
                        var result = await firebaseService.ConfirmDonationAsync(transaction.TransactionId, currentUser.UserId);
                        if (result.Success) 
                            await Application.Current.MainPage.DisplayAlert("Başarılı", "Bağış alındı.", "Tamam");
                        else
                            await Application.Current.MainPage.DisplayAlert("Hata", result.Message, "Tamam");
                    }
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

        public void StopListening()
        {
            Debug.WriteLine("🛑 Offers listener durduruluyor...");
            _loadingTimeoutCts?.Cancel();
            _allOffersSubscription?.Dispose();
            _allOffersSubscription = null;
        }

        public void ResumeListening()
        {
            if (_allOffersSubscription == null && !string.IsNullOrEmpty(_currentUserId))
            {
                Debug.WriteLine("▶️ Offers listener devam ediyor...");
                StartListeningForOffers(_currentUserId);
            }
        }

        public void Dispose()
        {
            Debug.WriteLine("🧹 OffersViewModel dispose ediliyor...");
            _loadingTimeoutCts?.Cancel();
            _loadingTimeoutCts?.Dispose();
            _allOffersSubscription?.Dispose();
            _allOffersSubscription = null;
            _userStateService.UserProfileChanged -= OnUserProfileChanged;
            _incomingIds.Clear();
            _outgoingIds.Clear();
            _initialLoadComplete = false;
            _isInitialized = false;
        }

        #region 💰 PAZARLIK KOMUTLARI

        [RelayCommand]
        private async Task MessagePartnerAsync(Transaction transaction)
        {
            if (transaction == null) return;
            if (Application.Current?.MainPage == null) return;
            if (string.IsNullOrEmpty(_currentUserId)) return;

            try
            {
                IsLoading = true;

                var result = await _transactionService.StartConversationForTransactionAsync(
                    transaction.TransactionId,
                    _currentUserId
                );

                if (result.Success)
                {
                    //  : ChatPage kullanılıyor
                    Console.WriteLine($"✅ Konuşma ID'si: {result.Data}");
                    await Shell.Current.GoToAsync($"{nameof(ChatPage)}?conversationId={result.Data}");
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert("Hata", result.Message, "Tamam");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ MessagePartner hatası: {ex.Message}");
                await Application.Current.MainPage.DisplayAlert("Hata", ex.Message, "Tamam");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task ProposePriceAsync(Transaction transaction)
        {
            if (transaction == null || transaction.BuyerId != _currentUserId) return;
            if (Application.Current?.MainPage == null) return;

            try
            {
                var currentPriceText = transaction.Type == ProductType.Satis 
                    ? $"Mevcut Fiyat: {transaction.Price:N2}₺\n\n"
                    : "";

                var amountText = await Application.Current.MainPage.DisplayPromptAsync(
                    transaction.Type == ProductType.Satis ? "Fiyat Teklifi" : "Ek Nakit Teklifi",
                    $"{currentPriceText}Ne kadar teklif etmek istiyorsunuz?",
                    placeholder: "Örn: 150",
                    keyboard: Keyboard.Numeric
                );

                if (string.IsNullOrWhiteSpace(amountText))
                    return;

                if (!decimal.TryParse(amountText, out var amount) || amount < 0)
                {
                    await Application.Current.MainPage.DisplayAlert("Hata", "Geçerli bir tutar girin", "Tamam");
                    return;
                }

                if (string.IsNullOrEmpty(_currentUserId)) return;

                IsLoading = true;
                ServiceResult<bool> result;

                if (transaction.Type == ProductType.Satis)
                {
                    result = await _transactionService.ProposePriceForSaleAsync(
                        transaction.TransactionId,
                        amount,
                        _currentUserId
                    );
                }
                else // Takas
                {
                    result = await _transactionService.ProposeAdditionalCashAsync(
                        transaction.TransactionId,
                        amount,
                        _currentUserId
                    );
                }

                if (result.Success)
                {
                    await Application.Current.MainPage.DisplayAlert("Başarılı", result.Message, "Tamam");
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
        private async Task SendCounterOfferAsync(Transaction transaction)
        {
            if (transaction == null || transaction.SellerId != _currentUserId) return;
            if (Application.Current?.MainPage == null) return;

            try
            {
                string currentOfferText = "";
                if (transaction.Type == ProductType.Satis && transaction.ProposedPriceByBuyer.HasValue)
                {
                    currentOfferText = $"Mevcut Teklif: {transaction.ProposedPriceByBuyer:N2}₺\n";
                }
                else if (transaction.Type == ProductType.Takas && transaction.AdditionalCashByRequester.HasValue)
                {
                    currentOfferText = $"Mevcut Teklif: {transaction.AdditionalCashByRequester:N2}₺\n";
                }

                var amountText = await Application.Current.MainPage.DisplayPromptAsync(
                    "Karşı Teklif",
                    $"{currentOfferText}'{transaction.ProductTitle}' için karşı teklifiniz nedir?",
                    placeholder: "Örn: 175",
                    keyboard: Keyboard.Numeric
                );

                if (string.IsNullOrWhiteSpace(amountText))
                    return;

                if (!decimal.TryParse(amountText, out var amount) || amount < 0)
                {
                    await Application.Current.MainPage.DisplayAlert("Hata", "Geçerli bir tutar girin", "Tamam");
                    return;
                }

                if (string.IsNullOrEmpty(_currentUserId)) return;

                IsLoading = true;
                ServiceResult<bool> result;

                if (transaction.Type == ProductType.Satis)
                {
                    result = await _transactionService.SendCounterOfferForSaleAsync(
                        transaction.TransactionId,
                        amount,
                        _currentUserId
                    );
                }
                else // Takas
                {
                    result = await _transactionService.SendCounterCashOfferAsync(
                        transaction.TransactionId,
                        amount,
                        _currentUserId
                    );
                }

                if (result.Success)
                {
                    await Application.Current.MainPage.DisplayAlert("Başarılı", result.Message, "Tamam");
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
        private async Task AcceptNegotiatedPriceAsync(Transaction transaction)
        {
            if (transaction == null || !transaction.IsNegotiating) return;
            if (Application.Current?.MainPage == null) return;
            if (string.IsNullOrEmpty(_currentUserId)) return;

            try
            {
                var loc = LocalizationResourceManager.Instance;
                decimal agreedAmount = transaction.AgreedAmount;
                string agreementText = transaction.Type == ProductType.Satis
                    ? string.Format(loc["AcceptPriceForProduct"], transaction.ProductTitle, agreedAmount)
                    : string.Format(loc["AcceptAdditionalCashForTrade"], transaction.ProductTitle, agreedAmount);

                var confirm = await Application.Current.MainPage.DisplayAlert(
                    loc["NegotiationApproval"],
                    agreementText,
                    loc["YesIAccept"],
                    loc["No"]
                );

                if (!confirm)
                    return;

                IsLoading = true;
                var result = await _transactionService.AcceptNegotiatedPriceAsync(
                    transaction.TransactionId,
                    _currentUserId
                );

                if (result.Success)
                {
                    await Application.Current.MainPage.DisplayAlert(
                        loc["Success"], 
                        result.Message, 
                        loc["Ok"]);
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert(
                        loc["Error"], 
                        result.Message, 
                        loc["Ok"]);
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert(
                    LocalizationResourceManager.Instance["Error"], 
                    ex.Message, 
                    LocalizationResourceManager.Instance["Ok"]);
            }
            finally
            {
                IsLoading = false;
            }
        }

        #endregion
    }
}