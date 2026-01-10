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

        //  veri durumunu güncelle
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
                // ✅ KRİTİK: Sadece SATICI teklifi kabul/reddedebilir
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    await Application.Current.MainPage.DisplayAlert("Hata", "Oturum bilgisi alınamadı.", "Tamam");
                    return;
                }

                // ✅ Kullanıcı SATICI değilse işlemi engelle
                if (transaction.SellerId != currentUser.UserId)
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "⚠️ Yetkiniz Yok", 
                        "Sadece satıcı/ürün sahibi bu teklifi onaylayabilir veya reddedebilir.", 
                        "Tamam"
                    );
                    return;
                }

                // ✅ DÜZELTİLDİ: PAZARLIKSIZ SATIŞ için direkt onay akışı
                // Eğer IsNegotiating = false ve QuotedPrice set edilmişse → Pazarlıksız satış
                if (!transaction.IsNegotiating && transaction.QuotedPrice > 0)
                {
                    // Pazarlıksız satış: Alıcı direkt talep gönderdi, satıcı onaylıyor
                    var message = accept
                        ? $"'{transaction.ProductTitle}' için {transaction.BuyerName} tarafından gönderilen satın alma talebini kabul ediyor musunuz?\n\n"
                        : $"'{transaction.ProductTitle}' için {transaction.BuyerName} tarafından gönderilen satın alma talebini reddetmek istediğinizden emin misiniz?";

                    // Fiyat bilgisi ekle
                    message += $"💰 Satış Fiyatı: {transaction.QuotedPrice:N2}₺";

                    var confirm = await Application.Current.MainPage.DisplayAlert(
                        accept ? "✅ Satın Alma Talebini Onayla" : "❌ Satın Alma Talebini Reddet",
                        message,
                        accept ? "Evet, Kabul Et" : "Evet, Reddet",
                        "Vazgeç"
                    );

                    if (!confirm) return;

                    IsLoading = true;

                    var result = await _transactionService.RespondToOfferAsync(transaction.TransactionId, accept);
                    
                    if (result.Success)
                    {
                        // ✅ ANLIK GÜNCELLEME: UI'ı hemen güncelle
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            var incoming = IncomingOffers.FirstOrDefault(t => t.TransactionId == transaction.TransactionId);
                            if (incoming != null)
                            {
                                var index = IncomingOffers.IndexOf(incoming);
                                incoming.Status = accept ? TransactionStatus.Accepted : TransactionStatus.Rejected;
                                incoming.UpdatedAt = DateTime.UtcNow;
                                IncomingOffers[index] = incoming;
                                
                                Debug.WriteLine($"✅ Pazarlıksız satış onaylandı: {transaction.TransactionId}");
                            }
                        });

                        var successMessage = accept 
                            ? "Satın alma talebi kabul edildi! Alıcı ödeme yapabilir." 
                            : "Satın alma talebi reddedildi.";

                        await Application.Current.MainPage.DisplayAlert(
                            "✅ Başarılı", 
                            successMessage, 
                            "Tamam"
                        );
                    }
                    else
                    {
                        await Application.Current.MainPage.DisplayAlert("Hata", result.Message, "Tamam");
                    }
                    
                    IsLoading = false;
                    return;
                }

                // ✅ DÜZELTİLDİ: PAZARLIKLI SATIŞ için iki aşamalı onay akışı
                // 1. AŞAMA: Pazarlık devam ediyorsa, önce fiyat üzerinde anlaşılmalı
                if (transaction.IsNegotiating)
                {
                    var agreedAmount = transaction.AgreedAmount;
                    var confirmMessage = transaction.Type == ProductType.Satis
                        ? $"Pazarlık devam ediyor!\n\n" +
                          $"💰 Son Teklif: {agreedAmount:N2}₺\n\n" +
                          $"Bu fiyat üzerinde anlaştınız mı? Eğer anlaştıysanız, önce pazarlık kutusundaki '✓' butonuna basarak fiyatı onaylayın."
                        : $"Takas için ek nakit pazarlığı devam ediyor!\n\n" +
                          $"💰 Son Teklif: {agreedAmount:N2}₺\n\n" +
                          $"Bu tutar üzerinde anlaştınız mı? Eğer anlaştıysanız, önce pazarlık kutusundaki '✓' butonuna basarak tutarı onaylayın.";

                    await Application.Current.MainPage.DisplayAlert(
                        "⚠️ Pazarlık Devam Ediyor",
                        confirmMessage,
                        "Anladım"
                    );
                    return;
                }

                // ✅ 2. AŞAMA: Pazarlık bitti, fiyat üzerinde anlaşıldı, şimdi son onay
                // IsNegotiating = false ANCAK ProposedPriceByBuyer veya CounterOfferBySeller set edilmiş
                bool hadNegotiation = transaction.ProposedPriceByBuyer.HasValue || transaction.CounterOfferBySeller.HasValue;
                
                if (hadNegotiation && !transaction.IsNegotiating)
                {
                    // Pazarlık sonrası onay
                    var agreedPrice = transaction.QuotedPrice > 0 ? transaction.QuotedPrice : transaction.Price;
                    
                    var message = accept
                        ? $"'{transaction.ProductTitle}' için {transaction.BuyerName} ile pazarlık sonucu {agreedPrice:N2}₺ üzerinde anlaştınız.\n\n" +
                          $"Bu fiyatla satışı onaylıyor musunuz?"
                        : $"'{transaction.ProductTitle}' için pazarlığı iptal etmek ve teklifi reddetmek istediğinizden emin misiniz?";

                    message += $"\n\n📦 Orijinal Fiyat: {transaction.Price:N2}₺";
                    if (transaction.ProposedPriceByBuyer.HasValue)
                    {
                        message += $"\n💰 Alıcının Teklifi: {transaction.ProposedPriceByBuyer:N2}₺";
                    }
                    if (transaction.CounterOfferBySeller.HasValue)
                    {
                        message += $"\n🔄 Sizin Karşı Teklifiniz: {transaction.CounterOfferBySeller:N2}₺";
                    }

                    var confirm = await Application.Current.MainPage.DisplayAlert(
                        accept ? "✅ Pazarlık Sonucu Onayı" : "❌ Teklifi Reddet",
                        message,
                        accept ? "Evet, Onayla" : "Evet, Reddet",
                        "Vazgeç"
                    );

                    if (!confirm) return;

                    IsLoading = true;

                    var result = await _transactionService.RespondToOfferAsync(transaction.TransactionId, accept);
                    
                    if (result.Success)
                    {
                        // ✅ ANLIK GÜNCELLEME
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            var incoming = IncomingOffers.FirstOrDefault(t => t.TransactionId == transaction.TransactionId);
                            if (incoming != null)
                            {
                                var index = IncomingOffers.IndexOf(incoming);
                                incoming.Status = accept ? TransactionStatus.Accepted : TransactionStatus.Rejected;
                                incoming.UpdatedAt = DateTime.UtcNow;
                                incoming.IsNegotiating = false;
                                IncomingOffers[index] = incoming;
                                
                                Debug.WriteLine($"✅ Pazarlık sonrası onaylandı: {transaction.TransactionId}");
                            }
                        });

                        var successMessage = accept 
                            ? "Pazarlık sonucu onaylandı! Alıcı ödeme yapabilir." 
                            : "Teklif reddedildi.";

                        await Application.Current.MainPage.DisplayAlert(
                            "✅ Başarılı", 
                            successMessage, 
                            "Tamam"
                        );
                    }
                    else
                    {
                        await Application.Current.MainPage.DisplayAlert("Hata", result.Message, "Tamam");
                    }
                    
                    IsLoading = false;
                    return;
                }

                // ✅ 3. DURUM: Hiç pazarlık olmamış, direkt alıcı talep göndermiş (QuotedPrice = Price)
                // Bu durum yukarıdaki ilk kontrol tarafından yakalanır
                // Buraya düşmemeli ama güvenlik için ekstra kontrol:
                await Application.Current.MainPage.DisplayAlert(
                    "Bilgi",
                    "Bu işlem için uygun akış belirlenemedi. Lütfen sayfayı yenileyip tekrar deneyin.",
                    "Tamam"
                );
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
        private async Task CompletePaymentAsync(Transaction transaction)
        {
            if (transaction == null) return;
            if (Application.Current?.MainPage == null) return;

            // ✅ Ödeme sayfasına yönlendirmeden önce tüm koşulları kontrol et
            if (transaction.Type == ProductType.Satis)
            {
                // ✅ Status kontrolü
                if (transaction.Status != TransactionStatus.Accepted)
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "⚠️ Onay Bekleniyor",
                        "Satıcının teklifi onaylaması gerekiyor. Henüz ödeme yapılamaz.",
                        "Tamam"
                    );
                    return;
                }

                // ✅ PaymentStatus kontrolü (tekrar ödeme engellemesi)
                if (transaction.PaymentStatus == PaymentStatus.Paid)
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "✅ Ödeme Tamamlandı",
                        "Bu işlem için ödeme zaten yapılmış.",
                        "Tamam"
                    );
                    return;
                }

                // ✅ Pazarlık kontrolü
                if (transaction.IsNegotiating)
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "⚠️ Pazarlık Devam Ediyor",
                        "Önce fiyat üzerinde anlaşmanız gerekiyor.",
                        "Tamam"
                    );
                    return;
                }

                // ✅ Tüm kontroller başarılı - Ödeme sayfasına yönlendir
                var navigationParameter = new Dictionary<string, object>
        {
            { "Transaction", transaction }
        };

                await Shell.Current.GoToAsync(nameof(PaymentPage), navigationParameter);
            }
            else if (transaction.Type == ProductType.Takas || transaction.Type == ProductType.Bagis)
            {
                // Takas/Bağış için QR sayfasına yönlendir
                await Shell.Current.GoToAsync($"QRCodeDisplayPage?transactionId={transaction.TransactionId}");
            }
        }
        // ✅ YENİ: Ödeme başlatma komutu (daha açık isim)
        [RelayCommand]
        private async Task StartPaymentAsync(Transaction transaction)
        {
            if (transaction == null) return;
            if (Application.Current?.MainPage == null) return;

            // Kontroller
            if (transaction.Type != ProductType.Satis)
            {
                await Application.Current.MainPage.DisplayAlert(
                    "Bilgi", 
                    "Ödeme sadece satış işlemleri için geçerlidir.", 
                    "Tamam"
                );
                return;
            }

            if (transaction.Status != TransactionStatus.Accepted)
            {
                await Application.Current.MainPage.DisplayAlert(
                    "Bilgi", 
                    "Bu işlem henüz onaylanmamış. Satıcının onayını bekleyin.", 
                    "Tamam"
                );
                return;
            }

            if (transaction.PaymentStatus == PaymentStatus.Paid)
            {
                await Application.Current.MainPage.DisplayAlert(
                    "Bilgi", 
                    "Bu işlem için ödeme zaten yapılmış.", 
                    "Tamam"
                );
                return;
            }

            // ✅ PaymentPage'e yönlendir
            var navigationParameter = new Dictionary<string, object>
            {
                { "Transaction", transaction }
            };

            await Shell.Current.GoToAsync(nameof(PaymentPage), navigationParameter);
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

        // ✅ YENİ: Bağış onaylama komutu (daha kısa isim)
        [RelayCommand]
        private async Task ConfirmDonationAsync(Transaction transaction)
        {
            if (transaction == null) return;
            if (Application.Current?.MainPage == null) return;

            // Kontroller
            if (transaction.Type != ProductType.Bagis)
            {
                await Application.Current.MainPage.DisplayAlert(
                    "Bilgi", 
                    "Bu komut sadece bağış işlemleri için geçerlidir.", 
                    "Tamam"
                );
                return;
            }

            if (transaction.Status != TransactionStatus.Accepted)
            {
                await Application.Current.MainPage.DisplayAlert(
                    "Bilgi", 
                    "Bu bağış henüz onaylanmamış.", 
                    "Tamam"
                );
                return;
            }

            if (transaction.PaymentStatus == PaymentStatus.Paid)
            {
                await Application.Current.MainPage.DisplayAlert(
                    "Bilgi", 
                    "Bu bağış zaten tamamlanmış.", 
                    "Tamam"
                );
                return;
            }

            // Onay al
            var confirm = await Application.Current.MainPage.DisplayAlert(
                "Bağış Onayı",
                $"'{transaction.ProductTitle}' ürününü teslim aldığınızı onaylıyor musunuz?\n\n" +
                $"Bu işlem geri alınamaz ve puanlar hesaplara eklenecektir.",
                "Evet, Teslim Aldım",
                "Hayır"
            );

            if (!confirm) return;

            IsLoading = true;
            try
            {
                if (_transactionService is FirebaseTransactionService firebaseService)
                {
                    if (string.IsNullOrEmpty(_currentUserId)) return;

                    var result = await firebaseService.ConfirmDonationAsync(
                        transaction.TransactionId, 
                        _currentUserId
                    );

                    if (result.Success)
                    {
                        await Application.Current.MainPage.DisplayAlert(
                            "Başarılı", 
                            "Bağış onaylandı! Puanlarınız eklendi.", 
                            "Harika!"
                        );
                    }
                    else
                    {
                        await Application.Current.MainPage.DisplayAlert(
                            "Hata", 
                            result.Message, 
                            "Tamam"
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert(
                    "Hata", 
                    $"Bağış onaylanırken hata oluştu: {ex.Message}", 
                    "Tamam"
                );
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

        // ✅ YENİ: Pazarlık Sonucunu Onayla (Hem Satış Hem Takas)
        [RelayCommand]
        private async Task AcceptNegotiatedPriceAsync(Transaction transaction)
        {
            if (transaction == null) return;
            if (Application.Current?.MainPage == null) return;
            if (string.IsNullOrEmpty(_currentUserId)) return;

            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    await Application.Current.MainPage.DisplayAlert("Hata", "Oturum bilgisi alınamadı.", "Tamam");
                    return;
                }

                // Pazarlık devam etmiyor mu?
                if (!transaction.IsNegotiating)
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "Bilgi",
                        "Şu anda aktif bir pazarlık bulunmuyor.",
                        "Tamam"
                    );
                    return;
                }

                string confirmMessage = "";
                string confirmTitle = "";

                if (transaction.Type == ProductType.Satis)
                {
                    // SATIŞ: Alıcı, satıcının karşı teklifini onaylıyor
                    if (transaction.BuyerId != _currentUserId)
                    {
                        await Application.Current.MainPage.DisplayAlert(
                            "⚠️ Yetkiniz Yok",
                            "Sadece alıcı bu karşı teklifi onaylayabilir.",
                            "Tamam"
                        );
                        return;
                    }

                    if (!transaction.CounterOfferBySeller.HasValue)
                    {
                        await Application.Current.MainPage.DisplayAlert(
                            "Bilgi",
                            "Satıcının karşı teklifi bekleniyor.",
                            "Tamam"
                        );
                        return;
                    }

                    confirmTitle = "✅ Karşı Teklifi Onayla";
                    confirmMessage = $"'{transaction.ProductTitle}' için satıcının karşı teklifini kabul ediyor musunuz?\n\n" +
                                   $"💰 Sizin Teklifiniz: {transaction.ProposedPriceByBuyer:N2}₺\n" +
                                   $"🔄 Satıcının Karşı Teklifi: {transaction.CounterOfferBySeller:N2}₺\n\n" +
                                   $"✅ Onaylanan Fiyat: {transaction.CounterOfferBySeller:N2}₺";
                }
                else if (transaction.Type == ProductType.Takas)
                {
                    // TAKAS: Talep eden, sahip'in karşı nakit teklifini onaylıyor
                    if (transaction.BuyerId != _currentUserId)
                    {
                        await Application.Current.MainPage.DisplayAlert(
                            "⚠️ Yetkiniz Yok",
                            "Sadece talep eden bu karşı teklifi onaylayabilir.",
                            "Tamam"
                        );
                        return;
                    }

                    if (!transaction.CounterCashByOwner.HasValue)
                    {
                        await Application.Current.MainPage.DisplayAlert(
                            "Bilgi",
                            "Sahip'in karşı teklifi bekleniyor.",
                            "Tamam"
                        );
                        return;
                    }

                    confirmTitle = "✅ Karşı Teklifi Onayla";
                    confirmMessage = $"'{transaction.ProductTitle}' için sahip'in ek nakit karşı teklifini kabul ediyor musunuz?\n\n" +
                                   $"💰 Sizin Ek Nakit Teklifiniz: {transaction.AdditionalCashByRequester:N2}₺\n" +
                                   $"🔄 Sahip'in Karşı Teklifi: {transaction.CounterCashByOwner:N2}₺\n\n" +
                                   $"✅ Onaylanan Ek Nakit: {transaction.CounterCashByOwner:N2}₺";
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "Bilgi",
                        "Bu işlem türü için pazarlık onayı geçerli değil.",
                        "Tamam"
                    );
                    return;
                }

                var confirm = await Application.Current.MainPage.DisplayAlert(
                    confirmTitle,
                    confirmMessage,
                    "Evet, Kabul Ediyorum",
                    "Hayır"
                );

                if (!confirm) return;

                IsLoading = true;

                var result = await _transactionService.AcceptNegotiatedPriceAsync(
                    transaction.TransactionId,
                    _currentUserId
                );

                if (result.Success)
                {
                    // ✅ ANLIK GÜNCELLEME
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        var outgoing = OutgoingOffers.FirstOrDefault(t => t.TransactionId == transaction.TransactionId);
                        if (outgoing != null)
                        {
                            var index = OutgoingOffers.IndexOf(outgoing);
                            outgoing.IsNegotiating = false;
                            outgoing.Status = TransactionStatus.Accepted;
                            
                            // QuotedPrice'ı güncelle
                            if (transaction.Type == ProductType.Satis)
                            {
                                outgoing.QuotedPrice = transaction.CounterOfferBySeller ?? transaction.Price;
                            }
                            else if (transaction.Type == ProductType.Takas)
                            {
                                outgoing.QuotedPrice = transaction.CounterCashByOwner ?? 0;
                            }
                            
                            outgoing.UpdatedAt = DateTime.UtcNow;
                            OutgoingOffers[index] = outgoing;
                            
                            Debug.WriteLine($"✅ Pazarlık onaylandı (Alıcı): {transaction.TransactionId}");
                        }
                    });

                    await Application.Current.MainPage.DisplayAlert(
                        "✅ Başarılı",
                        transaction.Type == ProductType.Satis 
                            ? "Fiyat üzerinde anlaştınız! Satıcı son onayını verecek." 
                            : "Ek nakit tutarı üzerinde anlaştınız! Sahip son onayını verecek.",
                        "Harika!"
                    );
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

        // SATIŞ Pazarlık Metodu - Fiyat Teklifi (Alıcı)
        [RelayCommand]
        private async Task ProposePriceAsync(Transaction transaction)
        {
            if (transaction == null || transaction.BuyerId != _currentUserId) return;
            if (Application.Current?.MainPage == null) return;

            try
            {
                // ✅ SADECE ALICININ TEKLİFİNİ GÖSTER
                var currentPriceText = transaction.Type == ProductType.Satis 
                    ? $"📦 Ürün Fiyatı: {transaction.Price:N2}₺\n\n"
                    : "";

                var yourOfferText = transaction.ProposedPriceByBuyer.HasValue
                    ? $"💰 Sizin Teklifiniz: {transaction.ProposedPriceByBuyer:N2}₺\n"
                    : "";

                var counterOfferText = transaction.CounterOfferBySeller.HasValue
                    ? $"🔄 Satıcının Karşı Teklifi: {transaction.CounterOfferBySeller:N2}₺\n\n"
                    : "\n";

                var roundInfo = transaction.NegotiationRoundCount > 0
                    ? $"📊 Pazarlık Turu: {transaction.NegotiationRoundCount}/{NegotiationRules.MaxNegotiationRounds}\n\n"
                    : "";

                var amountText = await Application.Current.MainPage.DisplayPromptAsync(
                    "💰 Fiyat Teklifi",
                    $"{currentPriceText}{yourOfferText}{counterOfferText}{roundInfo}Ne kadar teklif etmek istiyorsunuz?",
                    "Gönder",
                    "İptal",
                    placeholder: "Örn: 150",
                    keyboard: Keyboard.Numeric
                );

                if (string.IsNullOrWhiteSpace(amountText))
                    return;

                if (!decimal.TryParse(amountText, out var amount) || amount <= 0)
                {
                    await Application.Current.MainPage.DisplayAlert("Hata", "Geçerli bir fiyat girin (sıfırdan büyük)", "Tamam");
                    return;
                }

                // ✅ Aynı teklifi tekrar göndermesini engelle
                if (transaction.ProposedPriceByBuyer.HasValue && 
                    Math.Abs(transaction.ProposedPriceByBuyer.Value - amount) < 0.01m)
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "Bilgi", 
                        "Bu teklifi zaten gönderdiniz. Farklı bir tutar deneyin.", 
                        "Tamam"
                    );
                    return;
                }

                if (string.IsNullOrEmpty(_currentUserId)) return;

                IsLoading = true;
                var result = await _transactionService.ProposePriceForSaleAsync(
                    transaction.TransactionId,
                    amount,
                    _currentUserId
                );

                if (result.Success)
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "✅ Başarılı", 
                        $"Fiyat teklifiniz ({amount:N2}₺) satıcıya iletildi!", 
                        "Tamam"
                    );
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

        // ✅ YENİ: TAKAS için Ek Nakit Teklifi (Alıcı/Talep Eden)
        [RelayCommand]
        private async Task ProposeAdditionalCashAsync(Transaction transaction)
        {
            if (transaction == null || transaction.BuyerId != _currentUserId) return;
            if (Application.Current?.MainPage == null) return;

            try
            {
                // ✅ SADECE ALICININ TEKLİFİNİ GÖSTER
                var yourOfferText = transaction.AdditionalCashByRequester.HasValue
                    ? $"💰 Sizin Teklifiniz: {transaction.AdditionalCashByRequester:N2}₺\n"
                    : "";

                var counterOfferText = transaction.CounterCashByOwner.HasValue
                    ? $"🔄 Sahip'in Karşı Teklifi: {transaction.CounterCashByOwner:N2}₺\n\n"
                    : "\n";

                var roundInfo = transaction.NegotiationRoundCount > 0
                    ? $"📊 Pazarlık Turu: {transaction.NegotiationRoundCount}/{NegotiationRules.MaxNegotiationRounds}\n\n"
                    : "";

                var amountText = await Application.Current.MainPage.DisplayPromptAsync(
                    "💰 Ek Nakit Teklifi",
                    $"{yourOfferText}{counterOfferText}{roundInfo}Ne kadar ek nakit teklif etmek istiyorsunuz?\n(0 girebilirsiniz - sade takas)",
                    "Gönder",
                    "İptal",
                    placeholder: "Örn: 50",
                    keyboard: Keyboard.Numeric
                );

                if (string.IsNullOrWhiteSpace(amountText))
                    return;

                if (!decimal.TryParse(amountText, out var amount) || amount < 0)
                {
                    await Application.Current.MainPage.DisplayAlert("Hata", "Geçerli bir tutar girin (sıfır veya pozitif)", "Tamam");
                    return;
                }

                // ✅ Aynı teklifi tekrar göndermesini engelle
                if (transaction.AdditionalCashByRequester.HasValue && 
                    Math.Abs(transaction.AdditionalCashByRequester.Value - amount) < 0.01m)
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "Bilgi", 
                        "Bu teklifi zaten gönderdiniz. Farklı bir tutar deneyin.", 
                        "Tamam"
                    );
                    return;
                }

                if (string.IsNullOrEmpty(_currentUserId)) return;

                IsLoading = true;
                var result = await _transactionService.ProposeAdditionalCashAsync(
                    transaction.TransactionId,
                    amount,
                    _currentUserId
                );

                if (result.Success)
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "✅ Başarılı", 
                        $"Ek nakit teklifiniz ({amount:N2}₺) iletildi!", 
                        "Tamam"
                    );
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

        // SATIŞ Pazarlık Metodu - Karşı Teklif (Satıcı)
        [RelayCommand]
        private async Task SendCounterOfferAsync(Transaction transaction)
        {
            if (transaction == null || transaction.SellerId != _currentUserId) return;
            if (Application.Current?.MainPage == null) return;

            try
            {
                // ✅ SADECE SATICININ KARŞI TEKLİFİNİ GÖSTER
                var originalPriceText = $"📦 Ürün Fiyatınız: {transaction.Price:N2}₺\n\n";
                
                var buyerOfferText = transaction.ProposedPriceByBuyer.HasValue
                    ? $"💰 Alıcının Teklifi: {transaction.ProposedPriceByBuyer:N2}₺\n"
                    : "💰 Alıcı henüz teklif vermedi\n";

                var yourCounterText = transaction.CounterOfferBySeller.HasValue
                    ? $"🔄 Sizin Karşı Teklifiniz: {transaction.CounterOfferBySeller:N2}₺\n\n"
                    : "\n";

                var roundInfo = transaction.NegotiationRoundCount > 0
                    ? $"📊 Pazarlık Turu: {transaction.NegotiationRoundCount}/{NegotiationRules.MaxNegotiationRounds}\n\n"
                    : "";

                var amountText = await Application.Current.MainPage.DisplayPromptAsync(
                    "🔄 Karşı Teklif",
                    $"{originalPriceText}{buyerOfferText}{yourCounterText}{roundInfo}Karşı teklifiniz nedir?",
                    "Gönder",
                    "İptal",
                    placeholder: "Örn: 175",
                    keyboard: Keyboard.Numeric
                );

                if (string.IsNullOrWhiteSpace(amountText))
                    return;

                if (!decimal.TryParse(amountText, out var amount) || amount <= 0)
                {
                    await Application.Current.MainPage.DisplayAlert("Hata", "Geçerli bir fiyat girin (sıfırdan büyük)", "Tamam");
                    return;
                }

                // ✅ Aynı karşı teklifi tekrar göndermesini engelle
                if (transaction.CounterOfferBySeller.HasValue && 
                    Math.Abs(transaction.CounterOfferBySeller.Value - amount) < 0.01m)
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "Bilgi", 
                        "Bu karşı teklifi zaten gönderdiniz. Farklı bir tutar deneyin.", 
                        "Tamam"
                    );
                    return;
                }

                if (string.IsNullOrEmpty(_currentUserId)) return;

                IsLoading = true;
                var result = await _transactionService.SendCounterOfferForSaleAsync(
                    transaction.TransactionId,
                    amount,
                    _currentUserId
                );

                if (result.Success)
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "✅ Başarılı", 
                        $"Karşı teklifiniz ({amount:N2}₺) alıcıya iletildi!", 
                        "Tamam"
                    );
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

        #endregion
    }
}