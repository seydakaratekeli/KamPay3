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
        private IDisposable _allOffersSubscription;
        private readonly FirebaseClient _firebaseClient;

        // Cache ve Durum Kontrolü
        private readonly HashSet<string> _incomingIds = new();
        private readonly HashSet<string> _outgoingIds = new();
        private bool _incomingInitialLoadComplete = false;
        private bool _outgoingInitialLoadComplete = false;

        // Yükleme kontrolü
        private bool _isInitialized = false;

        // 🔥 DÜZELTME 1: Kullanıcı ID'sini saklamak için değişken ekledik
        private string _currentUserId;

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

        // Boş durum mesajı
        [ObservableProperty]
        private string emptyMessage = "Teklifler yükleniyor...";

        public OffersViewModel(ITransactionService transactionService, IAuthenticationService authService)
        {
            _transactionService = transactionService;
            _authService = authService;
            _firebaseClient = new FirebaseClient(Constants.FirebaseRealtimeDbUrl);

            _ = InitializeAsync();
        }

        public async Task InitializeAsync()
        {
            if (_isInitialized) return;

            IsLoading = true;
            EmptyMessage = "Teklifler yükleniyor...";

            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser == null)
            {
                IsLoading = false;
                EmptyMessage = "Giriş yapmalısınız.";
                return;
            }

            // 🔥 DÜZELTME 2: Kullanıcı ID'sini kaydet
            _currentUserId = currentUser.UserId;

            StartListeningForOffers(_currentUserId);
            _isInitialized = true;

            // GÜVENLİK: 3 Saniye içinde veri gelmezse loading'i kapat
            await Task.Delay(3000);
            if (IsLoading)
            {
                IsLoading = false;
                UpdateEmptyMessage();
            }
        }

        private void StartListeningForOffers(string userId)
        {
            if (_allOffersSubscription != null) return;

            Console.WriteLine($"🔥 Offers listener başlatılıyor: {userId}");

            _allOffersSubscription = _firebaseClient
                .Child(Constants.TransactionsCollection)
                .AsObservable<Transaction>()
                .Where(e => e.Object != null)
                .Buffer(TimeSpan.FromMilliseconds(300))
                .Where(batch => batch.Any())
                .Subscribe(
                    events =>
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            try
                            {
                                ProcessOfferBatch(events, userId);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"❌ Offer batch hatası: {ex.Message}");
                            }
                            finally
                            {
                                IsLoading = false;
                                UpdateEmptyMessage();
                            }
                        });
                    },
                    error =>
                    {
                        Console.WriteLine($"❌ Firebase listener hatası: {error.Message}");
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            IsLoading = false;
                            EmptyMessage = "Bağlantı hatası.";
                        });
                    });
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

        private void UpdateEmptyMessage()
        {
            if (IsIncomingSelected)
            {
                EmptyMessage = IncomingOffers.Any() ? string.Empty : "Henüz gelen bir teklif yok.";
            }
            else
            {
                EmptyMessage = OutgoingOffers.Any() ? string.Empty : "Henüz yaptığınız bir teklif yok.";
            }
        }

        [RelayCommand]
        private async Task RefreshOffersAsync()
        {
            if (IsRefreshing) return;
            try
            {
                IsRefreshing = true;
                _allOffersSubscription?.Dispose();
                _allOffersSubscription = null;
                _incomingIds.Clear();
                _outgoingIds.Clear();

                IncomingOffers.Clear();
                OutgoingOffers.Clear();

                _isInitialized = false;
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser != null)
                {
                    _currentUserId = currentUser.UserId; // ID'yi güncelle
                    StartListeningForOffers(currentUser.UserId);
                }

                await Task.Delay(500);
                UpdateEmptyMessage();
            }
            catch (Exception ex) { Console.WriteLine($"❌ Refresh hatası: {ex.Message}"); }
            finally
            {
                IsRefreshing = false;
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void SelectIncoming()
        {
            IsIncomingSelected = true;
            IsOutgoingSelected = false;
            UpdateEmptyMessage();
        }

        [RelayCommand]
        private void SelectOutgoing()
        {
            IsIncomingSelected = false;
            IsOutgoingSelected = true;
            UpdateEmptyMessage();
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
            try
            {
                var result = await _transactionService.RespondToOfferAsync(transaction.TransactionId, accept);
                if (result.Success) await Application.Current.MainPage.DisplayAlert("Başarılı", $"Teklif {(accept ? "kabul edildi" : "reddedildi")}.", "Tamam");
                else await Application.Current.MainPage.DisplayAlert("Hata", result.Message, "Tamam");
            }
            catch (Exception ex) { await Application.Current.MainPage.DisplayAlert("Hata", ex.Message, "Tamam"); }
        }

        [RelayCommand]
        private async Task CompletePaymentAsync(Transaction transaction)
        {
            if (transaction == null) return;

            if (transaction.Type != ProductType.Satis ||
                transaction.Status != TransactionStatus.Accepted ||
                transaction.PaymentStatus != PaymentStatus.Pending)
            {
                await Application.Current.MainPage.DisplayAlert("Bilgi", "Bu işlem için ödeme yapılamaz.", "Tamam");
                return;
            }

            var confirm = await Application.Current.MainPage.DisplayAlert("Ödeme Simülasyonu",
                $"'{transaction.ProductTitle}' ürünü için ödemeyi tamamlamak üzeresiniz. Devam etmek istiyor musunuz?",
                "Evet, Tamamla", "Hayır");

            if (!confirm) return;

            IsLoading = true;
            try
            {
                if (_transactionService is FirebaseTransactionService firebaseService)
                {
                    var currentUser = await _authService.GetCurrentUserAsync();
                    if (currentUser != null)
                    {
                        var result = await firebaseService.CompletePaymentAsync(transaction.TransactionId, currentUser.UserId);
                        if (result.Success) await Application.Current.MainPage.DisplayAlert("Başarılı", "Ödeme tamamlandı.", "Tamam");
                        else await Application.Current.MainPage.DisplayAlert("Hata", result.Message, "Tamam");
                    }
                }
            }
            catch (Exception ex) { await Application.Current.MainPage.DisplayAlert("Hata", ex.Message, "Tamam"); }
            finally { IsLoading = false; }
        }

        [RelayCommand]
        private async Task ConfirmDonationReceivedAsync(Transaction transaction)
        {
            if (transaction == null) return;

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
                        if (result.Success) await Application.Current.MainPage.DisplayAlert("Başarılı", "Bağış alındı.", "Tamam");
                        else await Application.Current.MainPage.DisplayAlert("Hata", result.Message, "Tamam");
                    }
                }
            }
            catch (Exception ex) { await Application.Current.MainPage.DisplayAlert("Hata", ex.Message, "Tamam"); }
            finally { IsLoading = false; }
        }

        public void StopListening()
        {
            _allOffersSubscription?.Dispose();
            _allOffersSubscription = null;
        }

        // 🔥 DÜZELTME 3: ResumeListening artık _currentUserId kullanıyor
        public void ResumeListening()
        {
            if (_allOffersSubscription == null && !string.IsNullOrEmpty(_currentUserId))
            {
                StartListeningForOffers(_currentUserId);
            }
        }

        public void Dispose()
        {
            Console.WriteLine("🧹 OffersViewModel dispose ediliyor...");
            _allOffersSubscription?.Dispose();
            _allOffersSubscription = null;
            _incomingIds.Clear();
            _outgoingIds.Clear();
            _isInitialized = false;
        }
    }
}