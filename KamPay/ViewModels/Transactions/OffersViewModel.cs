using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
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
using KamPay.Services.Auth;
using KamPay.Services.Transactions;

namespace KamPay.ViewModels
{
    public partial class OffersViewModel : ObservableObject, IDisposable
    {
        private readonly ITransactionService _transactionService;
        private readonly IAuthenticationService _authService;
        private readonly IUserStateService _userStateService;
        private IDisposable? _allOffersSubscription;
        private readonly FirebaseClient _firebaseClient;

        private readonly HashSet<string> _incomingIds = new();
        private readonly HashSet<string> _outgoingIds = new();
        private bool _initialLoadComplete = false;
        private bool _isInitialized = false;
        private string? _currentUserId;
        private CancellationTokenSource? _loadingTimeoutCts;
        private const int LoadingTimeoutMs = 5000;

        public ObservableRangeCollection<Transaction> IncomingOffers { get; } = new();
        public ObservableRangeCollection<Transaction> OutgoingOffers { get; } = new();

        [ObservableProperty] private bool isLoading = true;
        [ObservableProperty] private bool isRefreshing;
        [ObservableProperty] private bool isIncomingSelected = true;
        [ObservableProperty] private bool isOutgoingSelected = false;
        [ObservableProperty] private bool isSkeletonVisible = true;
        [ObservableProperty] private bool hasIncomingOffers = false;
        [ObservableProperty] private bool hasOutgoingOffers = false;

        public OffersViewModel(ITransactionService transactionService, IAuthenticationService authService, IUserStateService userStateService, FirebaseClient firebaseClient)
        {
            _transactionService = transactionService;
            _authService = authService;
            _userStateService = userStateService;
            _firebaseClient = firebaseClient;
            _userStateService.UserProfileChanged += OnUserProfileChanged;
            
            // âœ… PaymentCompleted mesajÄ±nÄ± dinle
            WeakReferenceMessenger.Default.Register<PaymentCompletedMessage>(this, (r, m) =>
            {
                _ = MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    Debug.WriteLine("ğŸ’° PaymentCompleted mesajÄ± alÄ±ndÄ±, offers yenileniyor...");
                    await RefreshOffersAsync();
                });
            });
            
            WeakReferenceMessenger.Default.Register<UserSessionChangedMessage>(this, (r, m) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (!m.Value) // Logout
                    {
                        StopListening();
                        IncomingOffers.Clear();
                        OutgoingOffers.Clear();
                        _incomingIds.Clear();
                        _outgoingIds.Clear();
                        _currentUserId = null;
                        _isInitialized = false;
                        _initialLoadComplete = false;
                    }
                    else // Login
                    {
                        _isInitialized = false;
                        _ = InitializeAsync();
                    }
                });
            });

            _ = InitializeAsync();
        }

        private void OnUserProfileChanged(object? sender, User updatedUser)
        {
            if (updatedUser == null) return;

            MainThread.InvokeOnMainThreadAsync(() =>
            {
                foreach (var offer in IncomingOffers.Where(o => o.BuyerId == updatedUser.UserId))
                    offer.BuyerName = updatedUser.FullName;
                
                foreach (var offer in OutgoingOffers.Where(o => o.SellerId == updatedUser.UserId))
                    offer.SellerName = updatedUser.FullName;
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
                Debug.WriteLine("âš ï¸ KullanÄ±cÄ± oturum aÃ§mamÄ±ÅŸ");
                return;
            }

            _currentUserId = currentUser.UserId;
            StartListeningForOffers(_currentUserId);
            _isInitialized = true;
        }

        private void StartListeningForOffers(string userId)
        {
            if (_allOffersSubscription != null) return;

            Debug.WriteLine($"ğŸ§ Offers listener baÅŸlatÄ±lÄ±yor: {userId}");

            _loadingTimeoutCts?.Cancel();
            _loadingTimeoutCts?.Dispose();
            _loadingTimeoutCts = new CancellationTokenSource();
            var timeoutToken = _loadingTimeoutCts.Token;

            _ = LoadInitialSnapshotAsync(userId, timeoutToken);

            Task.Delay(LoadingTimeoutMs, timeoutToken).ContinueWith(t =>
            {
                if (t.IsCanceled) return;

                MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (!_initialLoadComplete)
                    {
                        Debug.WriteLine("â³ Loading timeout - veri gelmedi.");
                        IsLoading = false;
                        IsSkeletonVisible = false;
                        IsRefreshing = false;
                        UpdateHasOffers();
                    }
                });
            }, TaskContinuationOptions.OnlyOnRanToCompletion);

            _allOffersSubscription = _firebaseClient
                .Child(Constants.TransactionsCollection)
                .AsObservable<Transaction>()
                .Where(e => e.Object != null)
                .Buffer(TimeSpan.FromMilliseconds(400))
                .Where(batch => batch.Any())
                .Subscribe(
                    events =>
                    {
                        MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            try
                            {
                                _loadingTimeoutCts?.Cancel();
                                ProcessOfferBatch(events, userId);

                                if (!_initialLoadComplete && ContainsRealOffer(events, userId))
                                {
                                    _initialLoadComplete = true;
                                    IsLoading = false;
                                    IsSkeletonVisible = false;
                                    IsRefreshing = false;
                                    Debug.WriteLine("âœ… Ä°lk gerÃ§ek realtime offer geldi â€” loading kapatÄ±ldÄ±.");
                                }

                                UpdateHasOffers();
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"âŒ Offer batch hatasÄ±: {ex.Message}");
                            }
                        });
                    },
                    error =>
                    {
                        Debug.WriteLine($"âŒ Firebase listener hatasÄ±: {error.Message}");
                        MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            IsLoading = false;
                            IsSkeletonVisible = false;
                            IsRefreshing = false;
                            UpdateHasOffers();
                        });
                    });
        }

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
                               !(s.Key != null && s.Key.StartsWith("service_")))
                    .Select(s =>
                    {
                        var transaction = s.Object;
                        transaction.TransactionId = s.Key;
                        return transaction;
                    })
                    .OrderByDescending(t => t.CreatedAt)
                    .ToList();

                // ✅ FAZ 7: Süresi dolmuş pazarlıkları arka planda iptal et (UI'ı bloke etmez)
                _ = Task.Run(() => CheckAndExpireNegotiationsAsync(userOffers));

                if (!userOffers.Any())
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        if (!_initialLoadComplete)
                        {
                            _initialLoadComplete = true;
                            IsLoading = false;
                            IsSkeletonVisible = false;
                            IsRefreshing = false;
                            UpdateHasOffers();
                            Debug.WriteLine("✅ Snapshot yüklendi — teklif yok.");
                        }
                    });
                    return;
                }

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    var newIncoming = new List<Transaction>();
                    var newOutgoing = new List<Transaction>();

                    foreach (var transaction in userOffers)
                    {
                        if (transaction.SellerId == userId)
                        {
                            if (!_incomingIds.Contains(transaction.TransactionId))
                            {
                                newIncoming.Add(transaction);
                                _incomingIds.Add(transaction.TransactionId);
                            }
                        }
                        else if (transaction.BuyerId == userId)
                        {
                            if (!_outgoingIds.Contains(transaction.TransactionId))
                            {
                                newOutgoing.Add(transaction);
                                _outgoingIds.Add(transaction.TransactionId);
                            }
                        }
                    }

                    if (newIncoming.Any()) IncomingOffers.AddRange(newIncoming);
                    if (newOutgoing.Any()) OutgoingOffers.AddRange(newOutgoing);

                    if (!_initialLoadComplete)
                    {
                        _initialLoadComplete = true;
                        IsLoading = false;
                        IsSkeletonVisible = false;
                        IsRefreshing = false;
                        UpdateHasOffers();
                        Debug.WriteLine($"✅ Snapshot yüklendi — {userOffers.Count} teklif bulundu.");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ Snapshot yüklenirken hata: {ex.Message}");
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    IsLoading = false;
                    IsSkeletonVisible = false;
                    IsRefreshing = false;
                    UpdateHasOffers();
                });
            }
        }

        private bool ContainsRealOffer(IList<FirebaseEvent<Transaction>> events, string userId)
        {
            return events.Any(e =>
                e.Object != null &&
                !string.IsNullOrWhiteSpace(e.Key) &&
                !string.IsNullOrWhiteSpace(e.Object?.TransactionId) &&
                (e.Object.SellerId == userId || e.Object.BuyerId == userId) &&
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

                if (transaction.SellerId != userId && transaction.BuyerId != userId)
                    continue;

                if (!string.IsNullOrEmpty(transaction.TransactionId) && transaction.TransactionId.StartsWith("service_"))
                {
                    Debug.WriteLine($"âš ï¸ Hizmet transaction'Ä± atlanÄ±yor: {transaction.TransactionId}");
                    continue;
                }

                if (transaction.SellerId == userId)
                {
                    if (UpdateOfferInCollection(IncomingOffers, _incomingIds, transaction, e.EventType))
                    {
                        hasIncomingChanges = true;
                        Debug.WriteLine($"ğŸ”„ INCOMING gÃ¼ncellendi: {transaction.TransactionId}");
                        Debug.WriteLine($"   Status: {transaction.Status}, Payment: {transaction.PaymentStatus}");
                        Debug.WriteLine($"   IsNegotiating: {transaction.IsNegotiating}, QuotedPrice: {transaction.QuotedPrice}");
                    }
                }
                else if (transaction.BuyerId == userId)
                {
                    if (UpdateOfferInCollection(OutgoingOffers, _outgoingIds, transaction, e.EventType))
                    {
                        hasOutgoingChanges = true;
                        Debug.WriteLine($"ğŸ”„ OUTGOING gÃ¼ncellendi: {transaction.TransactionId}");
                        Debug.WriteLine($"   Status: {transaction.Status}, Payment: {transaction.PaymentStatus}");
                        Debug.WriteLine($"   IsNegotiating: {transaction.IsNegotiating}, QuotedPrice: {transaction.QuotedPrice}");
                    }
                }
            }

            if (hasIncomingChanges)
            {
                SortOffersInPlace(IncomingOffers);
                OnPropertyChanged(nameof(IncomingOffers));
                Debug.WriteLine($"âœ… IncomingOffers collection updated - Count: {IncomingOffers.Count}");
            }

            if (hasOutgoingChanges)
            {
                SortOffersInPlace(OutgoingOffers);
                OnPropertyChanged(nameof(OutgoingOffers));
                Debug.WriteLine($"âœ… OutgoingOffers collection updated - Count: {OutgoingOffers.Count}");
            }
        }

        private bool UpdateOfferInCollection(ObservableRangeCollection<Transaction> collection, HashSet<string> idTracker, Transaction transaction, FirebaseEventType eventType)
        {
            var existing = collection.FirstOrDefault(t => t.TransactionId == transaction.TransactionId);

            switch (eventType)
            {
                case FirebaseEventType.InsertOrUpdate:
                    if (existing != null)
                    {
                        var index = collection.IndexOf(existing);
                        
                        bool hasRealChange = 
                            existing.Status != transaction.Status ||
                            existing.PaymentStatus != transaction.PaymentStatus ||
                            existing.IsNegotiating != transaction.IsNegotiating ||
                            existing.QuotedPrice != transaction.QuotedPrice ||
                            existing.ProposedPriceByBuyer != transaction.ProposedPriceByBuyer ||
                            existing.CounterOfferBySeller != transaction.CounterOfferBySeller ||
                            existing.AdditionalCashByRequester != transaction.AdditionalCashByRequester ||
                            existing.CounterCashByOwner != transaction.CounterCashByOwner;

                        if (hasRealChange)
                        {
                            collection.RemoveAt(index);
                            collection.Insert(index, transaction);
                            Debug.WriteLine($"âœ… GÃœNCELLEME: {transaction.TransactionId} - Status: {transaction.Status}, Payment: {transaction.PaymentStatus}, Index: {index}");
                            return true;
                        }
                        
                        return false;
                    }
                    else if (!idTracker.Contains(transaction.TransactionId))
                    {
                        collection.Add(transaction);
                        idTracker.Add(transaction.TransactionId);
                        Debug.WriteLine($"âœ… YENÄ° EKLEME: {transaction.TransactionId}");
                        return true;
                    }
                    break;

                case FirebaseEventType.Delete:
                    if (existing != null)
                    {
                        collection.Remove(existing);
                        idTracker.Remove(transaction.TransactionId);
                        Debug.WriteLine($"âœ… SÄ°LÄ°NDÄ°: {transaction.TransactionId}");
                        return true;
                    }
                    break;
            }
            return false;
        }

        private void SortOffersInPlace(ObservableRangeCollection<Transaction> collection)
        {
            var sorted = collection.OrderByDescending(t => t.CreatedAt).ToList();
            for (int i = 0; i < sorted.Count; i++)
            {
                var currentIndex = collection.IndexOf(sorted[i]);
                if (currentIndex != i && currentIndex >= 0) collection.Move(currentIndex, i);
            }
        }

        private void UpdateHasOffers()
        {
            HasIncomingOffers = IncomingOffers.Any();
            HasOutgoingOffers = OutgoingOffers.Any();
            Debug.WriteLine($"ğŸ“Š UpdateHasOffers: Gelen={IncomingOffers.Count} (HasIncoming={HasIncomingOffers}), Giden={OutgoingOffers.Count} (HasOutgoing={HasOutgoingOffers})");
        }

        [RelayCommand]
        private async Task RefreshOffersAsync()
        {
            if (IsRefreshing) return;
            
            try
            {
                IsRefreshing = true;
                Debug.WriteLine("ğŸ”„ RefreshOffersAsync baÅŸladÄ± â€” IsRefreshing = true");
                
                // Cancel any ongoing loading timeout
                _loadingTimeoutCts?.Cancel();
                _loadingTimeoutCts?.Dispose();
                _loadingTimeoutCts = null;
                
                _allOffersSubscription?.Dispose();
                _allOffersSubscription = null;
                
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
                    
                    // âœ… FIX: Wait for initial load with max 5 second timeout
                    var maxWaitTime = 5000;
                    var waitedTime = 0;
                    var checkInterval = 200;
                    
                    while (!_initialLoadComplete && waitedTime < maxWaitTime)
                    {
                        await Task.Delay(checkInterval);
                        waitedTime += checkInterval;
                    }
                    
                    Debug.WriteLine($"âœ… Refresh beklemesi tamamlandÄ±: {waitedTime}ms, InitialLoadComplete: {_initialLoadComplete}");
                }
                else
                {
                    Debug.WriteLine("âš ï¸ Refresh: KullanÄ±cÄ± bulunamadÄ±");
                }
            }
            catch (Exception ex) 
            { 
                Debug.WriteLine($"âŒ Refresh hatasÄ±: {ex.Message}"); 
            }
            finally
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    IsRefreshing = false;
                    UpdateHasOffers();
                    Debug.WriteLine("âœ… Refresh tamamlandÄ±, IsRefreshing = false");
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

        private async Task UpdateOfferInUIAsync(Transaction transaction, ServiceResult<Transaction> result)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var existingIncoming = IncomingOffers.FirstOrDefault(t => t.TransactionId == transaction.TransactionId);
                if (existingIncoming != null && result.Data != null)
                {
                    var index = IncomingOffers.IndexOf(existingIncoming);
                    IncomingOffers.RemoveAt(index);
                    IncomingOffers.Insert(index, result.Data);
                    OnPropertyChanged(nameof(IncomingOffers));
                    Debug.WriteLine($"✅ UI manuel güncellendi: {transaction.TransactionId}");
                }

                var existingOutgoing = OutgoingOffers.FirstOrDefault(t => t.TransactionId == transaction.TransactionId);
                if (existingOutgoing != null && result.Data != null)
                {
                    var index = OutgoingOffers.IndexOf(existingOutgoing);
                    OutgoingOffers.RemoveAt(index);
                    OutgoingOffers.Insert(index, result.Data);
                    OnPropertyChanged(nameof(OutgoingOffers));
                    Debug.WriteLine($"✅ UI (Outgoing) manuel güncellendi: {transaction.TransactionId}");
                }
            });
        }

        private async Task RespondToOfferInternalAsync(Transaction transaction, bool accept)
        {
            if (transaction == null) return;
            if (Shell.Current?.CurrentPage == null) return; // âœ… FAZ1: Modernize edildi

            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    await Shell.Current.CurrentPage.DisplayAlertAsync("Hata", "Oturum bilgisi alÄ±namadÄ±.", "Tamam");
                    return;
                }

                if (transaction.SellerId != currentUser.UserId)
                {
                    await Shell.Current.CurrentPage.DisplayAlertAsync(
                        "âš ï¸ Yetkiniz Yok", 
                        "Sadece satÄ±cÄ±/Ã¼rÃ¼n sahibi bu teklifi onaylayabilir veya reddedebilir.", 
                        "Tamam"
                    );
                    return;
                }

                // âœ… 1. SATIÅ - PazarlÄ±ksÄ±z
                // ✅ BUG-1 FIX: Status==Pending kontrolü eklendi.
                // AcceptNegotiatedPriceAsync artık Status=Accepted yapmıyor;
                // pazarlık bitmişse (IsNeg=false) ama Status hala Pending ise buraya düşer.
                if (transaction.Type == ProductType.Satis && !transaction.IsNegotiating && transaction.QuotedPrice > 0
                    && transaction.Status == TransactionStatus.Pending)
                {
                    var message = accept
                        ? $"'{transaction.ProductTitle}' iÃ§in {transaction.BuyerName} tarafÄ±ndan gÃ¶nderilen satÄ±n alma talebini kabul ediyor musunuz?\n\nğŸ’° SatÄ±ÅŸ FiyatÄ±: {transaction.QuotedPrice:N2}â‚º"
                        : $"'{transaction.ProductTitle}' iÃ§in {transaction.BuyerName} tarafÄ±ndan gÃ¶nderilen satÄ±n alma talebini reddetmek istediÄŸinizden emin misiniz?";

                    var confirm = await Shell.Current.CurrentPage.DisplayAlertAsync(
                        accept ? "âœ… SatÄ±n Alma Talebini Onayla" : "âŒ SatÄ±n Alma Talebini Reddet",
                        message,
                        accept ? "Evet, Kabul Et" : "Evet, Reddet",
                        "VazgeÃ§"
                    );

                    if (!confirm) return;

                    IsLoading = true;

                    var result = await _transactionService.RespondToOfferAsync(transaction.TransactionId, accept);

                    if (result.Success)
                    {
                        Debug.WriteLine($"✅ Firebase'e yazıldı, listener güncelleyecek: {transaction.TransactionId}");

                        // ✅ FIX: Manuel UI güncelleme - Firebase listener beklemeye gerek yok
                        await UpdateOfferInUIAsync(transaction, result);

                        var successMessage = accept 
                            ? "Satın alma talebi kabul edildi! Alıcı ödeme yapabilir." 
                            : "Satın alma talebi reddedildi.";

                        await Shell.Current.CurrentPage.DisplayAlertAsync("✅ Başarılı", successMessage, "Tamam");
                    }
                    else
                    {
                        await Shell.Current.CurrentPage.DisplayAlertAsync("Hata", result.Message, "Tamam");
                    }
                    return;
                }

                // âœ… 2. PAZARLIK Devam Ediyor
                if (transaction.IsNegotiating)
                {
                    var agreedAmount = transaction.AgreedAmount;
                    var confirmMessage = transaction.Type == ProductType.Satis
                        ? $"PazarlÄ±k devam ediyor!\n\nğŸ’° Son Teklif: {agreedAmount:N2}â‚º\n\nBu fiyat Ã¼zerinde anlaÅŸtÄ±nÄ±z mÄ±? EÄŸer anlaÅŸtÄ±ysanÄ±z, Ã¶nce pazarlÄ±k kutusundaki 'âœ“' butonuna basarak fiyatÄ± onaylayÄ±n."
                        : $"Takas iÃ§in ek nakit pazarlÄ±ÄŸÄ± devam ediyor!\n\nğŸ’° Son Teklif: {agreedAmount:N2}â‚º\n\nBu tutar Ã¼zerinde anlaÅŸtÄ±nÄ±z mÄ±? EÄŸer anlaÅŸtÄ±ysanÄ±z, Ã¶nce pazarlÄ±k kutusundaki 'âœ“' butonuna basarak tutarÄ± onaylayÄ±n.";

                    await Shell.Current.CurrentPage.DisplayAlertAsync("âš ï¸ PazarlÄ±k Devam Ediyor", confirmMessage, "AnladÄ±m");
                    return;
                }

                // âœ… 3. SATIÅ - PazarlÄ±k SonrasÄ±
                bool hadNegotiation = transaction.ProposedPriceByBuyer.HasValue || transaction.CounterOfferBySeller.HasValue;
                
                if (transaction.Type == ProductType.Satis && hadNegotiation && !transaction.IsNegotiating)
                {
                    var agreedPrice = transaction.QuotedPrice > 0 ? transaction.QuotedPrice : transaction.Price;
                    
                    var message = accept
                        ? $"'{transaction.ProductTitle}' iÃ§in {transaction.BuyerName} ile pazarlÄ±k sonucu {agreedPrice:N2}â‚º Ã¼zerinde anlaÅŸtÄ±nÄ±z.\n\nBu fiyatla satÄ±ÅŸÄ± onaylÄ±yor musunuz?\n\nğŸ“¦ Orijinal Fiyat: {transaction.Price:N2}â‚º"
                        : $"'{transaction.ProductTitle}' iÃ§in pazarlÄ±ÄŸÄ± iptal etmek ve teklifi reddetmek istediÄŸinizden emin misiniz?";

                    if (transaction.ProposedPriceByBuyer.HasValue)
                        message += $"\nğŸ’° AlÄ±cÄ±nÄ±n Teklifi: {transaction.ProposedPriceByBuyer:N2}â‚º";
                    
                    if (transaction.CounterOfferBySeller.HasValue)
                        message += $"\nğŸ”„ Sizin KarÅŸÄ± Teklifiniz: {transaction.CounterOfferBySeller:N2}â‚º";

                    var confirm = await Shell.Current.CurrentPage.DisplayAlertAsync(
                        accept ? "âœ… PazarlÄ±k Sonucu OnayÄ±" : "âŒ Teklifi Reddet",
                        message,
                        accept ? "Evet, Onayla" : "Evet, Reddet",
                        "VazgeÃ§"
                    );

                    if (!confirm) return;

                    IsLoading = true;

                    var result = await _transactionService.RespondToOfferAsync(transaction.TransactionId, accept);

                    if (result.Success)
                    {
                        Debug.WriteLine($"✅ Firebase'e yazıldı, listener güncelleyecek: {transaction.TransactionId}");

                        // ✅ FIX: Manuel UI güncelleme - Firebase listener beklemeye gerek yok
                        await UpdateOfferInUIAsync(transaction, result);

                        var successMessage = accept 
                            ? "Pazarlık sonucu onaylandı! Alıcı ödeme yapabilir." 
                            : "Teklif reddedildi.";

                        await Shell.Current.CurrentPage.DisplayAlertAsync("✅ Başarılı", successMessage, "Tamam");
                    }
                    else
                    {
                        await Shell.Current.CurrentPage.DisplayAlertAsync("Hata", result.Message, "Tamam");
                    }
                    return;
                }

                // âœ… 4. TAKAS - PazarlÄ±ksÄ±z veya PazarlÄ±k SonrasÄ±
                if (transaction.Type == ProductType.Takas)
                {
                    var message = accept
                        ? $"'{transaction.ProductTitle}' â†” '{transaction.OfferedProductTitle}' takas teklifini kabul ediyor musunuz?"
                        : $"'{transaction.ProductTitle}' iÃ§in takas teklifini reddetmek istediÄŸinizden emin misiniz?";

                    // Ek nakit varsa gÃ¶ster
                    if (transaction.QuotedPrice > 0)
                    {
                        message += $"\n\nğŸ’° AnlaÅŸÄ±lan Ek Nakit: {transaction.QuotedPrice:N2}â‚º";
                    }

                    var confirm = await Shell.Current.CurrentPage.DisplayAlertAsync(
                        accept ? "âœ… Takas Teklifini Onayla" : "âŒ Takas Teklifini Reddet",
                        message,
                        accept ? "Evet, Kabul Et" : "Evet, Reddet",
                        "VazgeÃ§"
                    );

                    if (!confirm) return;

                    IsLoading = true;

                    var result = await _transactionService.RespondToOfferAsync(transaction.TransactionId, accept);

                    if (result.Success)
                    {
                        Debug.WriteLine($"✅ TAKAS onaylandı: {transaction.TransactionId}");

                        // ✅ FIX: Manuel UI güncelleme - Firebase listener beklemeye gerek yok
                        await UpdateOfferInUIAsync(transaction, result);

                        var successMessage = accept 
                            ? "Takas teklifi kabul edildi! QR kodlar oluşturuldu." 
                            : "Takas teklifi reddedildi.";

                        await Shell.Current.CurrentPage.DisplayAlertAsync("✅ Başarılı", successMessage, "Tamam");
                    }
                    else
                    {
                        await Shell.Current.CurrentPage.DisplayAlertAsync("Hata", result.Message, "Tamam");
                    }
                    return;
                }

                // âœ… 5. BAÄIÅ
                if (transaction.Type == ProductType.Bagis)
                {
                    var message = accept
                        ? $"'{transaction.ProductTitle}' baÄŸÄ±ÅŸ talebini kabul ediyor musunuz?\n\n{transaction.BuyerName} bu Ã¼rÃ¼nÃ¼ talep etti."
                        : $"'{transaction.ProductTitle}' iÃ§in baÄŸÄ±ÅŸ talebini reddetmek istediÄŸinizden emin misiniz?";

                    var confirm = await Shell.Current.CurrentPage.DisplayAlertAsync(
                        accept ? "âœ… BaÄŸÄ±ÅŸ Talebini Onayla" : "âŒ BaÄŸÄ±ÅŸ Talebini Reddet",
                        message,
                        accept ? "Evet, Kabul Et" : "Evet, Reddet",
                        "VazgeÃ§"
                    );

                    if (!confirm) return;

                    IsLoading = true;

                    var result = await _transactionService.RespondToOfferAsync(transaction.TransactionId, accept);

                    if (result.Success)
                    {
                        Debug.WriteLine($"✅ BAĞIŞ onaylandı: {transaction.TransactionId}");

                        // ✅ FIX: Manuel UI güncelleme - Firebase listener beklemeye gerek yok
                        await UpdateOfferInUIAsync(transaction, result);

                        var successMessage = accept 
                            ? "Bağış talebi kabul edildi!" 
                            : "Bağış talebi reddedildi.";

                        await Shell.Current.CurrentPage.DisplayAlertAsync("✅ Başarılı", successMessage, "Tamam");
                    }
                    else
                    {
                        await Shell.Current.CurrentPage.DisplayAlertAsync("Hata", result.Message, "Tamam");
                    }
                    return;
                }

                // âœ… Buraya dÃ¼ÅŸmemeli artÄ±k
                await Shell.Current.CurrentPage.DisplayAlertAsync(
                    "Bilgi", 
                    "Bu iÅŸlem iÃ§in uygun akÄ±ÅŸ belirlenemedi. LÃ¼tfen sayfayÄ± yenileyip tekrar deneyin.", 
                    "Tamam"
                );
            }
            catch (Exception ex)
            {
                await Shell.Current.CurrentPage.DisplayAlertAsync("Hata", ex.Message, "Tamam");
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
            if (Shell.Current?.CurrentPage == null) return; // âœ… FAZ1: Modernize edildi

            if (transaction.Type == ProductType.Satis)
            {
                if (transaction.Status != TransactionStatus.Accepted)
                {
                    await Shell.Current.CurrentPage.DisplayAlertAsync("âš ï¸ Onay Bekleniyor", "SatÄ±cÄ±nÄ±n teklifi onaylamasÄ± gerekiyor. HenÃ¼z Ã¶deme yapÄ±lamaz.", "Tamam");
                    return;
                }

                if (transaction.PaymentStatus == PaymentStatus.Paid)
                {
                    await Shell.Current.CurrentPage.DisplayAlertAsync("âœ… Ã–deme TamamlandÄ±", "Bu iÅŸlem iÃ§in Ã¶deme zaten yapÄ±lmÄ±ÅŸ.", "Tamam");
                    return;
                }

                if (transaction.IsNegotiating)
                {
                    await Shell.Current.CurrentPage.DisplayAlertAsync("âš ï¸ PazarlÄ±k Devam Ediyor", "Ã–nce fiyat Ã¼zerinde anlaÅŸmanÄ±z gerekiyor.", "Tamam");
                    return;
                }

                var paymentMethod = await Shell.Current.CurrentPage.DisplayActionSheet(
                    "Ã–deme YÃ¶ntemi SeÃ§in",
                    "Ä°ptal",
                    null,
                    "Kart ile (Uygulama Ä°Ã§i)",
                    "Nakit / IBAN ile (Elden)"
                );

                if (paymentMethod == "Ä°ptal" || string.IsNullOrEmpty(paymentMethod)) return;

                if (paymentMethod == "Kart ile (Uygulama Ä°Ã§i)")
                {
                    await Shell.Current.CurrentPage.DisplayAlertAsync("Bilgi", "Kredi kartÄ± ile Ã¶deme sistemi yakÄ±nda geliÅŸtirilecektir. LÃ¼tfen Nakit / IBAN seÃ§eneÄŸini kullanÄ±n.", "Tamam");
                    return;
                }
                else if (paymentMethod == "Nakit / IBAN ile (Elden)")
                {
                    // KullanÄ±cÄ±yÄ± bilgilendir
                    await Shell.Current.CurrentPage.DisplayAlertAsync(
                        "Bilgilendirme", 
                        "Nakit veya IBAN ile Ã¶deme seÃ§eneÄŸinde, Ã¶deme sÃ¼reci doÄŸrudan kullanÄ±cÄ±lar arasÄ±nda gerÃ§ekleÅŸir. KamPay bu Ã¶deme sÃ¼recine teknik olarak dahil olmaz.\n\nEÄŸer Ã¶demeyi tamamladÄ±ysanÄ±z veya teslimat anÄ±nda yapacaksanÄ±z, lÃ¼tfen QR kod aÅŸamasÄ±na geÃ§erek teslimatÄ± gÃ¼venli bir ÅŸekilde onaylayÄ±n.", 
                        "AnladÄ±m, QR Koduna Git"
                    );

                    // Uygulama dÄ±ÅŸÄ± Ã¶deme seÃ§ildiÄŸi iÃ§in doÄŸrudan QR kod (gÃ¼venli fiziki/dijital onay) aÅŸamasÄ±na geÃ§ir.
                    await Shell.Current.GoToAsync($"QRCodeDisplayPage?transactionId={transaction.TransactionId}");
                }
            }
            else if (transaction.Type == ProductType.Takas || transaction.Type == ProductType.Bagis)
            {
                await Shell.Current.GoToAsync($"QRCodeDisplayPage?transactionId={transaction.TransactionId}");
            }
        }

        [RelayCommand]
        private async Task ConfirmDonationAsync(Transaction transaction)
        {
            if (transaction == null) return;
            if (Shell.Current?.CurrentPage == null) return; // âœ… FAZ1: Modernize edildi

            if (transaction.Type != ProductType.Bagis)
            {
                await Shell.Current.CurrentPage.DisplayAlertAsync("Bilgi", "Bu komut sadece baÄŸÄ±ÅŸ iÅŸlemleri iÃ§in geÃ§erlidir.", "Tamam");
                return;
            }

            if (transaction.Status != TransactionStatus.Accepted)
            {
                await Shell.Current.CurrentPage.DisplayAlertAsync("Bilgi", "Bu baÄŸÄ±ÅŸ henÃ¼z onaylanmamÄ±ÅŸ.", "Tamam");
                return;
            }

            if (transaction.PaymentStatus == PaymentStatus.Paid)
            {
                await Shell.Current.CurrentPage.DisplayAlertAsync("Bilgi", "Bu baÄŸÄ±ÅŸ zaten tamamlanmÄ±ÅŸ.", "Tamam");
                return;
            }

            var confirm = await Shell.Current.CurrentPage.DisplayAlertAsync(
                "BaÄŸÄ±ÅŸ OnayÄ±",
                $"'{transaction.ProductTitle}' Ã¼rÃ¼nÃ¼nÃ¼ teslim aldÄ±ÄŸÄ±nÄ±zÄ± onaylÄ±yor musunuz?\n\nBu iÅŸlem geri alÄ±namaz ve puanlar hesaplara eklenecektir.",
                "Evet, Teslim AldÄ±m",
                "HayÄ±r"
            );

            if (!confirm) return;

            IsLoading = true;
            try
            {
                if (string.IsNullOrEmpty(_currentUserId)) return;

                var result = await _transactionService.ConfirmDonationAsync(transaction.TransactionId, _currentUserId);

                    if (result.Success)
                    {
                        await Shell.Current.CurrentPage.DisplayAlertAsync("BaÅŸarÄ±lÄ±", "BaÄŸÄ±ÅŸ onaylandÄ±! PuanlarÄ±nÄ±z eklendi.", "Harika!");
                    }
                    else
                    {
                        await Shell.Current.CurrentPage.DisplayAlertAsync("Hata", result.Message, "Tamam");
                    }
            }
            catch (Exception ex)
            {
                await Shell.Current.CurrentPage.DisplayAlertAsync("Hata", $"BaÄŸÄ±ÅŸ onaylanÄ±rken hata oluÅŸtu: {ex.Message}", "Tamam");
            }
            finally
            {
                IsLoading = false;
            }
        }
        /// <summary>
        /// ✅ FAZ 7: Süresi dolmuş aktif pazarlıkları otomatik iptal eder.
        /// LoadInitialSnapshotAsync sonrası arka planda çalışır, UI'ı bloke etmez.
        /// Sadece IsNegotiating=true ve 48 saati geçmiş transaction'ları işler.
        /// </summary>
        private async Task CheckAndExpireNegotiationsAsync(List<Transaction> transactions)
        {
            try
            {
                var expiredNegotiations = transactions
                    .Where(t =>
                        t.IsNegotiating &&
                        t.Status == TransactionStatus.Pending &&
                        KamPay.Helpers.NegotiationRules.IsNegotiationExpired(t.NegotiationStartedAt))
                    .ToList();

                if (!expiredNegotiations.Any()) return;

                Debug.WriteLine($"⏰ FAZ 7: {expiredNegotiations.Count} süresi dolmuş pazarlık bulundu, iptal ediliyor...");

                foreach (var transaction in expiredNegotiations)
                {
                    try
                    {
                        // Pazarlığı durdur ama transaction'ı iptal etme — sadece IsNegotiating=false yap
                        // Status Pending kalır, taraflar hâlâ kabul/red yapabilir
                        transaction.IsNegotiating = false;
                        transaction.UpdatedAt = DateTime.UtcNow;
                        transaction.NegotiationNotes +=
                            (string.IsNullOrEmpty(transaction.NegotiationNotes) ? "" : "\n") +
                            $"⏰ Pazarlık süresi doldu ({KamPay.Helpers.NegotiationRules.NegotiationTimeoutHours} saat). " +
                            $"[{DateTime.UtcNow:dd.MM.yyyy HH:mm}]";

                        await _firebaseClient
                            .Child(Constants.TransactionsCollection)
                            .Child(transaction.TransactionId)
                            .PutAsync(transaction);

                        // Bildirim gönderme: _transactionService üzerinden yapılamıyorsa
                        // INotificationService'i OffersViewModel'e inject ederek kullanabilirsiniz.
                        // Şimdilik yalnızca Firebase güncellemesi yeterlidir;
                        // bildirim TransactionNegotiationService içinden zaten gönderildi.

                        Debug.WriteLine($"✅ FAZ 7: Süresi dolmuş pazarlık durduruldu: {transaction.TransactionId} ({transaction.ProductTitle})");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"⚠️ FAZ 7: Expire işlemi başarısız ({transaction.TransactionId}): {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ FAZ 7: CheckAndExpireNegotiationsAsync hatası: {ex.Message}");
            }
        }

        public void StopListening()
        {
            Debug.WriteLine("ğŸ›‘ Offers listener durduruluyor...");
            _loadingTimeoutCts?.Cancel();
            _allOffersSubscription?.Dispose();
            _allOffersSubscription = null;
        }

        public void ResumeListening()
        {
            if (_allOffersSubscription == null && !string.IsNullOrEmpty(_currentUserId))
            {
                Debug.WriteLine("â–¶ï¸ Offers listener devam ediyor...");
                StartListeningForOffers(_currentUserId);
            }
        }

        private bool _disposed = false;
        
        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                Debug.WriteLine("ğŸ§¹ OffersViewModel dispose ediliyor...");
                
                // âœ… EKLEME: Listener temizliÄŸi
                _allOffersSubscription?.Dispose();
                _allOffersSubscription = null;
                
                // âœ… EKLEME: Messenger unregister
                WeakReferenceMessenger.Default.Unregister<PaymentCompletedMessage>(this);
                
                // Timer temizliÄŸi
                _loadingTimeoutCts?.Cancel();
                _loadingTimeoutCts?.Dispose();
                
                // Event temizliÄŸi
                _userStateService.UserProfileChanged -= OnUserProfileChanged;
                
                // âœ… EKLEME: Collection temizliÄŸi
                IncomingOffers.Clear();
                OutgoingOffers.Clear();
                _incomingIds.Clear();
                _outgoingIds.Clear();
                
                _initialLoadComplete = false;
                _isInitialized = false;
                
                KamPay.Helpers.AppLogger.DebugLog("âœ… OffersViewModel resources disposed");
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âš ï¸ OffersViewModel dispose hatasÄ±: {ex.Message}");
            }
            finally
            {
                _disposed = true;
            }
        }

        #region ğŸ’° PAZARLIK KOMUTLARI

        [RelayCommand]
        private async Task MessagePartnerAsync(Transaction transaction)
        {
            if (transaction == null) return;
            if (Shell.Current?.CurrentPage == null) return; // âœ… FAZ1: Modernize edildi
            if (string.IsNullOrEmpty(_currentUserId)) return;

            try
            {
                IsLoading = true;

                var result = await _transactionService.StartConversationForTransactionAsync(transaction.TransactionId, _currentUserId);

                if (result.Success)
                {
                    KamPay.Helpers.AppLogger.DebugLog($"âœ… KonuÅŸma ID'si: {result.Data}");
                    await Shell.Current.GoToAsync($"{nameof(ChatPage)}?conversationId={result.Data}");
                }
                else
                {
                    await Shell.Current.CurrentPage.DisplayAlertAsync("Hata", result.Message, "Tamam");
                }
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âŒ MessagePartner hatasÄ±: {ex.Message}");
                await Shell.Current.CurrentPage.DisplayAlertAsync("Hata", ex.Message, "Tamam");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task AcceptNegotiatedPriceAsync(Transaction transaction)
        {
            if (transaction == null) return;
            if (Shell.Current?.CurrentPage == null) return; // âœ… FAZ1: Modernize edildi
            if (string.IsNullOrEmpty(_currentUserId)) return;

            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    await Shell.Current.CurrentPage.DisplayAlertAsync("Hata", "Oturum bilgisi alÄ±namadÄ±.", "Tamam");
                    return;
                }

                if (!transaction.IsNegotiating)
                {
                    await Shell.Current.CurrentPage.DisplayAlertAsync("Bilgi", "Åu anda aktif bir pazarlÄ±k bulunmuyor.", "Tamam");
                    return;
                }

                string confirmMessage = "";
                string confirmTitle = "";

                if (transaction.Type == ProductType.Satis)
                {
                    if (transaction.BuyerId != _currentUserId)
                    {
                        await Shell.Current.CurrentPage.DisplayAlertAsync("âš ï¸ Yetkiniz Yok", "Sadece alÄ±cÄ± bu karÅŸÄ± teklifi onaylayabilir.", "Tamam");
                        return;
                    }

                    if (!transaction.CounterOfferBySeller.HasValue)
                    {
                        await Shell.Current.CurrentPage.DisplayAlertAsync("Bilgi", "SatÄ±cÄ±nÄ±n karÅŸÄ± teklifi bekleniyor.", "Tamam");
                        return;
                    }

                    confirmTitle = "âœ… KarÅŸÄ± Teklifi Onayla";
                    confirmMessage = $"'{transaction.ProductTitle}' iÃ§in satÄ±cÄ±nÄ±n karÅŸÄ± teklifini kabul ediyor musunuz?\n\n" +
                                   $"ğŸ’° Sizin Teklifiniz: {transaction.ProposedPriceByBuyer:N2}â‚º\n" +
                                   $"ğŸ”„ SatÄ±cÄ±nÄ±n KarÅŸÄ± Teklifi: {transaction.CounterOfferBySeller:N2}â‚º\n\n" +
                                   $"âœ… Onaylanan Fiyat: {transaction.CounterOfferBySeller:N2}â‚º";
                }
                else if (transaction.Type == ProductType.Takas)
                {
                    if (transaction.BuyerId != _currentUserId)
                    {
                        await Shell.Current.CurrentPage.DisplayAlertAsync("âš ï¸ Yetkiniz Yok", "Sadece talep eden bu karÅŸÄ± teklifi onaylayabilir.", "Tamam");
                        return;
                    }

                    if (!transaction.CounterCashByOwner.HasValue)
                    {
                        await Shell.Current.CurrentPage.DisplayAlertAsync("Bilgi", "Sahip'in karÅŸÄ± teklifi bekleniyor.", "Tamam");
                        return;
                    }

                    confirmTitle = "âœ… KarÅŸÄ± Teklifi Onayla";
                    confirmMessage = $"'{transaction.ProductTitle}' iÃ§in sahip'in ek nakit karÅŸÄ± teklifini kabul ediyor musunuz?\n\n" +
                                   $"ğŸ’° Sizin Ek Nakit Teklifiniz: {transaction.AdditionalCashByRequester:N2}â‚º\n" +
                                   $"ğŸ”„ Sahip'in KarÅŸÄ± Teklifi: {transaction.CounterCashByOwner:N2}â‚º\n\n" +
                                   $"âœ… Onaylanan Ek Nakit: {transaction.CounterCashByOwner:N2}â‚º";
                }
                else
                {
                    await Shell.Current.CurrentPage.DisplayAlertAsync("Bilgi", "Bu iÅŸlem tÃ¼rÃ¼ iÃ§in pazarlÄ±k onayÄ± geÃ§erli deÄŸil.", "Tamam");
                    return;
                }

                var confirm = await Shell.Current.CurrentPage.DisplayAlertAsync(confirmTitle, confirmMessage, "Evet, Kabul Ediyorum", "HayÄ±r");

                if (!confirm) return;

                IsLoading = true;

                var result = await _transactionService.AcceptNegotiatedPriceAsync(transaction.TransactionId, _currentUserId);

                if (result.Success)
                {
                    Debug.WriteLine($"âœ… Firebase'e yazÄ±ldÄ±, listener gÃ¼ncelleyecek: {transaction.TransactionId}");

                    await Shell.Current.CurrentPage.DisplayAlertAsync(
                        "âœ… BaÅŸarÄ±lÄ±",
                        transaction.Type == ProductType.Satis 
                            ? "Fiyat Ã¼zerinde anlaÅŸtÄ±nÄ±z! SatÄ±cÄ± son onayÄ±nÄ± verecek." 
                            : "Ek nakit tutarÄ± Ã¼zerinde anlaÅŸtÄ±nÄ±z! Sahip son onayÄ±nÄ± verecek.",
                        "Harika!"
                    );
                }
                else
                {
                    await Shell.Current.CurrentPage.DisplayAlertAsync("Hata", result.Message, "Tamam");
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.CurrentPage.DisplayAlertAsync("Hata", ex.Message, "Tamam");
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
            if (Shell.Current?.CurrentPage == null) return; // âœ… FAZ1: Modernize edildi

            try
            {
                var currentPriceText = transaction.Type == ProductType.Satis ? $"ğŸ“¦ ÃœrÃ¼n FiyatÄ±: {transaction.Price:N2}â‚º\n\n" : "";
                var yourOfferText = transaction.ProposedPriceByBuyer.HasValue ? $"ğŸ’° Sizin Teklifiniz: {transaction.ProposedPriceByBuyer:N2}â‚º\n" : "";
                var counterOfferText = transaction.CounterOfferBySeller.HasValue ? $"ğŸ”„ SatÄ±cÄ±nÄ±n KarÅŸÄ± Teklifi: {transaction.CounterOfferBySeller:N2}â‚º\n\n" : "\n";
                var roundInfo = transaction.NegotiationRoundCount > 0 ? $"ğŸ“Š PazarlÄ±k Turu: {transaction.NegotiationRoundCount}/{NegotiationRules.MaxNegotiationRounds}\n\n" : "";

                var amountText = await Shell.Current.CurrentPage.DisplayPromptAsync(
                    "ğŸ’° Fiyat Teklifi",
                    $"{currentPriceText}{yourOfferText}{counterOfferText}{roundInfo}Ne kadar teklif etmek istiyorsunuz?",
                    "GÃ¶nder",
                    "Ä°ptal",
                    placeholder: "Ã–rn: 150",
                    keyboard: Keyboard.Numeric
                );

                if (string.IsNullOrWhiteSpace(amountText)) return;

                if (!decimal.TryParse(amountText.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var amount) || amount <= 0)
                {
                    await Shell.Current.CurrentPage.DisplayAlertAsync("Hata", "GeÃ§erli bir fiyat girin (sÄ±fÄ±rdan bÃ¼yÃ¼k)", "Tamam");
                    return;
                }

                if (transaction.ProposedPriceByBuyer.HasValue && Math.Abs(transaction.ProposedPriceByBuyer.Value - amount) < 0.01m)
                {
                    await Shell.Current.CurrentPage.DisplayAlertAsync("Bilgi", "Bu teklifi zaten gÃ¶nderdiniz. FarklÄ± bir tutar deneyin.", "Tamam");
                    return;
                }

                if (string.IsNullOrEmpty(_currentUserId)) return;

                IsLoading = true;
                var result = await _transactionService.ProposePriceForSaleAsync(transaction.TransactionId, amount, _currentUserId);

                if (result.Success)
                {
                    await Shell.Current.CurrentPage.DisplayAlertAsync("âœ… BaÅŸarÄ±lÄ±", $"Fiyat teklifiniz ({amount:N2}â‚º) satÄ±cÄ±ya iletildi!", "Tamam");
                }
                else
                {
                    await Shell.Current.CurrentPage.DisplayAlertAsync("Hata", result.Message, "Tamam");
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.CurrentPage.DisplayAlertAsync("Hata", ex.Message, "Tamam");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task ProposeAdditionalCashAsync(Transaction transaction)
        {
            if (transaction == null || transaction.BuyerId != _currentUserId) return;
            if (Shell.Current?.CurrentPage == null) return; // âœ… FAZ1: Modernize edildi

            try
            {
                var yourOfferText = transaction.AdditionalCashByRequester.HasValue ? $"ğŸ’° Sizin Teklifiniz: {transaction.AdditionalCashByRequester:N2}â‚º\n" : "";
                var counterOfferText = transaction.CounterCashByOwner.HasValue ? $"ğŸ”„ Sahip'in KarÅŸÄ± Teklifi: {transaction.CounterCashByOwner:N2}â‚º\n\n" : "\n";
                var roundInfo = transaction.NegotiationRoundCount > 0 ? $"ğŸ“Š PazarlÄ±k Turu: {transaction.NegotiationRoundCount}/{NegotiationRules.MaxNegotiationRounds}\n\n" : "";

                var amountText = await Shell.Current.CurrentPage.DisplayPromptAsync(
                    "ğŸ’° Ek Nakit Teklifi",
                    $"{yourOfferText}{counterOfferText}{roundInfo}Ne kadar ek nakit teklif etmek istiyorsunuz?\n(0 girebilirsiniz - sade takas)",
                    "GÃ¶nder",
                    "Ä°ptal",
                    placeholder: "Ã–rn: 50",
                    keyboard: Keyboard.Numeric
                );

                if (string.IsNullOrWhiteSpace(amountText)) return;

                if (!decimal.TryParse(amountText.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var amount) || amount < 0)
                {
                    await Shell.Current.CurrentPage.DisplayAlertAsync("Hata", "GeÃ§erli bir tutar girin (sÄ±fÄ±r veya pozitif)", "Tamam");
                    return;
                }

                if (transaction.AdditionalCashByRequester.HasValue && Math.Abs(transaction.AdditionalCashByRequester.Value - amount) < 0.01m)
                {
                    await Shell.Current.CurrentPage.DisplayAlertAsync("Bilgi", "Bu teklifi zaten gÃ¶nderdiniz. FarklÄ± bir tutar deneyin.", "Tamam");
                    return;
                }

                if (string.IsNullOrEmpty(_currentUserId)) return;

                IsLoading = true;
                var result = await _transactionService.ProposeAdditionalCashAsync(transaction.TransactionId, amount, _currentUserId);

                if (result.Success)
                {
                    await Shell.Current.CurrentPage.DisplayAlertAsync("âœ… BaÅŸarÄ±lÄ±", $"Ek nakit teklifiniz ({amount:N2}â‚º) iletildi!", "Tamam");
                }
                else
                {
                    await Shell.Current.CurrentPage.DisplayAlertAsync("Hata", result.Message, "Tamam");
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.CurrentPage.DisplayAlertAsync("Hata", ex.Message, "Tamam");
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
            if (Shell.Current?.CurrentPage == null) return; // âœ… FAZ1: Modernize edildi

            try
            {
                var originalPriceText = $"ğŸ“¦ ÃœrÃ¼n FiyatÄ±nÄ±z: {transaction.Price:N2}â‚º\n\n";
                var buyerOfferText = transaction.ProposedPriceByBuyer.HasValue ? $"ğŸ’° AlÄ±cÄ±nÄ±n Teklifi: {transaction.ProposedPriceByBuyer:N2}â‚º\n" : "ğŸ’° AlÄ±cÄ± henÃ¼z teklif vermedi\n";
                var yourCounterText = transaction.CounterOfferBySeller.HasValue ? $"ğŸ”„ Sizin KarÅŸÄ± Teklifiniz: {transaction.CounterOfferBySeller:N2}â‚º\n\n" : "\n";
                var roundInfo = transaction.NegotiationRoundCount > 0 ? $"ğŸ“Š PazarlÄ±k Turu: {transaction.NegotiationRoundCount}/{NegotiationRules.MaxNegotiationRounds}\n\n" : "";

                var amountText = await Shell.Current.CurrentPage.DisplayPromptAsync(
                    "ğŸ”„ KarÅŸÄ± Teklif",
                    $"{originalPriceText}{buyerOfferText}{yourCounterText}{roundInfo}KarÅŸÄ± teklifiniz nedir?",
                    "GÃ¶nder",
                    "Ä°ptal",
                    placeholder: "Ã–rn: 175",
                    keyboard: Keyboard.Numeric
                );

                if (string.IsNullOrWhiteSpace(amountText)) return;

                if (!decimal.TryParse(amountText.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var amount) || amount <= 0)
                {
                    await Shell.Current.CurrentPage.DisplayAlertAsync("Hata", "GeÃ§erli bir fiyat girin (sÄ±fÄ±rdan bÃ¼yÃ¼k)", "Tamam");
                    return;
                }

                if (transaction.CounterOfferBySeller.HasValue && Math.Abs(transaction.CounterOfferBySeller.Value - amount) < 0.01m)
                {
                    await Shell.Current.CurrentPage.DisplayAlertAsync("Bilgi", "Bu karÅŸÄ± teklifi zaten gÃ¶nderdiniz. FarklÄ± bir tutar deneyin.", "Tamam");
                    return;
                }

                if (string.IsNullOrEmpty(_currentUserId)) return;

                IsLoading = true;
                var result = await _transactionService.SendCounterOfferForSaleAsync(transaction.TransactionId, amount, _currentUserId);

                if (result.Success)
                {
                    await Shell.Current.CurrentPage.DisplayAlertAsync("âœ… BaÅŸarÄ±lÄ±", $"KarÅŸÄ± teklifiniz ({amount:N2}â‚º) alÄ±cÄ±ya iletildi!", "Tamam");
                }
                else
                {
                    await Shell.Current.CurrentPage.DisplayAlertAsync("Hata", result.Message, "Tamam");
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.CurrentPage.DisplayAlertAsync("Hata", ex.Message, "Tamam");
            }
            finally
            {
                IsLoading = false;
            }
        }

        #endregion
    }
}

