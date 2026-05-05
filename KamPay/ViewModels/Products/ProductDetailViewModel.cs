using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using KamPay.Models;
using KamPay.Services;
using KamPay.Views;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Maui.Core;
using Firebase.Database;
using Firebase.Database.Query;
using Firebase.Database.Streaming;
using KamPay.Helpers;
using KamPay.Services.Auth;
using KamPay.Services.Products;
using KamPay.Services.Messaging;
using KamPay.Services.Transactions;
using KamPay.Models.EventMessages;
using KamPay.Models.EventMessages.ProductEvent;
namespace KamPay.ViewModels
{
    public class ShowTradeOfferPopupMessage
    {
        public Product TargetProduct { get; }
        public ShowTradeOfferPopupMessage(Product targetProduct)
        {
            TargetProduct = targetProduct;
        }
    }
    [QueryProperty(nameof(ProductId), "ProductId")]
    public partial class ProductDetailViewModel : ObservableObject, IDisposable
    {
        // Gerekli tÃ¼m servisler
        private readonly IProductService _productService;
        private readonly IAuthenticationService _authService;
        private readonly IFavoriteService _favoriteService;
        private readonly IMessagingService _messagingService;
        private readonly ITransactionService _transactionService;
        private readonly IUserStateService _userStateService;
        private readonly INegotiationOfferService _negotiationOfferService;
        // âœ… DIP FIX: FirebaseClient artÄ±k DI'den geliyor (new keyword kaldÄ±rÄ±ldÄ±)
        private readonly FirebaseClient _firebaseClient;
        private readonly INotificationService _notificationService;
        private IDisposable? _transactionListener;
        private string? _lastLoadedProductId;
        private bool _disposed = false;

        // Localization helper
        private static LocalizationResourceManager Res => LocalizationResourceManager.Instance;

        [ObservableProperty]
        private string productId = string.Empty;
        [ObservableProperty]
        private bool isFixedPriceSelected = false;
        [ObservableProperty]
        private Product? product;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private bool isRefreshing;

        [ObservableProperty]
        private bool isOwner;

        [ObservableProperty]
        private bool canContact;

        [ObservableProperty]
        private int currentImageIndex;

        [ObservableProperty]
        private bool isFavorite;

        //  Aktif transaction bilgisi
        [ObservableProperty]
        private Transaction? activeTransaction;

        [ObservableProperty]
        private bool hasActiveTransaction;

        // HasLocation property - checks if product has valid location
        public bool HasLocation => Product != null &&
                                   !string.IsNullOrEmpty(Product.Location) &&
                                   Product.Latitude.HasValue &&
                                   Product.Longitude.HasValue;

        public ObservableCollection<string> ProductImages { get; } = new();

        public ProductDetailViewModel(
            IProductService productService,
            IAuthenticationService authService,
            IFavoriteService favoriteService,
            IMessagingService messagingService,
            ITransactionService transactionService,
            IUserStateService userStateService,
            FirebaseClient firebaseClient,
            INotificationService notificationService,
            INegotiationOfferService negotiationOfferService)
        {
            _productService = productService;
            _authService = authService;
            _favoriteService = favoriteService;
            _messagingService = messagingService;
            _transactionService = transactionService;
            _userStateService = userStateService;
            _firebaseClient = firebaseClient ?? throw new ArgumentNullException(nameof(firebaseClient)); // âœ… DIP FIX: DI'den inject
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _negotiationOfferService = negotiationOfferService ?? throw new ArgumentNullException(nameof(negotiationOfferService));

            // KullanÄ±cÄ± profil deÄŸiÅŸikliklerini dinle
            _userStateService.UserProfileChanged += OnUserProfileChanged;

            // Ürün güncelleme mesajını dinle
            WeakReferenceMessenger.Default.Register<ProductUpdatedMessage>(this, (r, m) =>
            {
                if (m.Value.ProductId == ProductId)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        Product = m.Value;
                        ProductImages.Clear();
                        if (m.Value.ImageUrls != null)
                        {
                            foreach (var url in m.Value.ImageUrls)
                                ProductImages.Add(url);
                        }
                        CurrentImageIndex = 0;
                        OnPropertyChanged(nameof(HasLocation));
                    });
                }
            });

            KamPay.Helpers.AppLogger.DebugLog("âœ… ProductDetailViewModel oluÅŸturuldu (DIP uyumlu - FirebaseClient DI'den)");
        }

        private void OnUserProfileChanged(object? sender, User updatedUser)
        {
            // EÄŸer gÃ¶sterilen Ã¼rÃ¼n bu kullanÄ±cÄ±ya aitse gÃ¼ncelle
            if (Product != null && updatedUser != null && Product.UserId == updatedUser.UserId)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Product.UserName = updatedUser.FullName;
                    Product.UserPhotoUrl = updatedUser.ProfileImageUrl;
                    OnPropertyChanged(nameof(Product));
                });
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _userStateService.UserProfileChanged -= OnUserProfileChanged;
                    _transactionListener?.Dispose();
                    _transactionListener = null;
                    WeakReferenceMessenger.Default.UnregisterAll(this);
                }
                _disposed = true;
            }
        }

        partial void OnProductIdChanged(string? value)
        {
            if (!string.IsNullOrEmpty(value) && value != _lastLoadedProductId)
            {
                _lastLoadedProductId = value;
                _ = LoadProductAsync();
            }
        }

        partial void OnProductChanged(Product? value)
        {
            OnPropertyChanged(nameof(HasLocation));
        }

        [RelayCommand]
        private async Task LoadProductAsync()
        {
            try
            {
                IsLoading = true;

                var result = await _productService.GetProductByIdAsync(ProductId);

                if (result.Success && result.Data != null)
                {
                    Product = result.Data;
                    await _productService.IncrementViewCountAsync(ProductId);

                    ProductImages.Clear();
                    if (Product.ImageUrls != null && Product.ImageUrls.Any())
                    {
                        foreach (var imageUrl in Product.ImageUrls)
                        {
                            ProductImages.Add(imageUrl);
                        }
                    }

                    var currentUser = await _authService.GetCurrentUserAsync();
                    if (currentUser != null)
                    {
                        IsOwner = Product.UserId == currentUser.UserId;
                        CanContact = !IsOwner && Product.IsActive && !Product.IsSold;

                        var favResult = await _favoriteService.IsFavoriteAsync(currentUser.UserId, ProductId);
                        IsFavorite = favResult.Success && favResult.Data;

                        //  Aktif transaction'Ä± yÃ¼kle
                        await LoadActiveTransactionAsync(currentUser.UserId);
                    }
                }
                else
                {
                    if (Application.Current?.MainPage != null)
                    {
                        var errorMsg = string.IsNullOrEmpty(result.Message) ? Res["ProductNotFound"].ToString() : result.Message;
                        if (result.Errors != null && result.Errors.Any())
                        {
                            errorMsg += $"\n\nTeknik Hata Ã–zeti: {string.Join("\n", result.Errors)}";
                        }
                        await Application.Current.MainPage.DisplayAlert("DetaylÄ± API HatasÄ±", errorMsg, "Tamam");
                    }
                    await Shell.Current.GoToAsync("..");
                }
            }
            catch (Exception ex)
            {
                if (Application.Current?.MainPage != null)
                    await Application.Current.MainPage.DisplayAlert(Res["Error"], $"{Res["ProductLoadError"]}: {ex.Message}", Res["Ok"]);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task RefreshProductAsync()
        {
            IsRefreshing = true;
            try
            {
                _lastLoadedProductId = null; // Guard'ı sıfırla
                await LoadProductAsync();
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        //  Aktif transaction'Ä± yÃ¼kle VE realtime listener baÅŸlat
        private async Task LoadActiveTransactionAsync(string currentUserId)
        {
            try
            {
                // KullanÄ±cÄ±nÄ±n gÃ¶nderdiÄŸi teklifleri kontrol et
                var myOffersResult = await _transactionService.GetMyOffersAsync(currentUserId);

                if (myOffersResult.Success && myOffersResult.Data != null)
                {
                    // Bu Ã¼rÃ¼n iÃ§in pending/accepted/negotiating durumda bir transaction var mÄ±?
                    var existingTransaction = myOffersResult.Data
                        .FirstOrDefault(t => t.ProductId == ProductId &&
                                           (t.Status == TransactionStatus.Pending ||
                                            t.Status == TransactionStatus.Accepted ||
                                            t.IsNegotiating));

                    if (existingTransaction != null)
                    {
                        ActiveTransaction = existingTransaction;
                        HasActiveTransaction = true;

                        // âœ… YENÄ°: Realtime listener baÅŸlat
                        StartTransactionListener(existingTransaction.TransactionId);
                    }
                }
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âš ï¸ LoadActiveTransaction hatasÄ±: {ex.Message}");
            }
        }

        // âœ… YENÄ°: Transaction iÃ§in realtime listener
        private void StartTransactionListener(string transactionId)
        {
            // Ã–nceki listener'Ä± durdur
            _transactionListener?.Dispose();

            KamPay.Helpers.AppLogger.DebugLog($"ğŸ”¥ Transaction listener baÅŸlatÄ±lÄ±yor: {transactionId}");

            _transactionListener = _firebaseClient
                .Child(Constants.TransactionsCollection)
                .Child(transactionId)
                .AsObservable<Transaction>()
                .Subscribe(
                    evt =>
                    {
                        if (evt.Object == null) return;

                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            var updated = evt.Object;
                            updated.TransactionId = transactionId;

                            // ActiveTransaction'Ä± gÃ¼ncelle
                            if (ActiveTransaction != null && ActiveTransaction.TransactionId == transactionId)
                            {
                                ActiveTransaction = updated;
                                OnPropertyChanged(nameof(ActiveTransaction));
                                KamPay.Helpers.AppLogger.DebugLog($"âœ… Transaction gÃ¼ncellendi: IsNegotiating={updated.IsNegotiating}, Status={updated.Status}");
                                KamPay.Helpers.AppLogger.DebugLog($"✅ Transaction güncellendi: IsNegotiating={updated.IsNegotiating}, Status={updated.Status}");
                            }
                        });
                    },
                    error =>
                    {
                        KamPay.Helpers.AppLogger.DebugLog($"❌ Transaction listener hatası: {error.Message}");
                    });
        }

        [RelayCommand]
        private async Task ContactSellerAsync()
        {
            if (Product == null || IsLoading) return;

            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null || currentUser.UserId == Product.UserId) return;

                if (Application.Current?.MainPage == null) return;

                // Kullanıcıya sohbet türünü sor
                var action = await Application.Current.MainPage.DisplayActionSheet(
                    "Sohbet Türü Seçin",
                    Res["Cancel"] ?? "İptal",
                    null,
                    "Genel Sohbet",
                    "Ürün Hakkında (Pazarlık/Detay)");

                if (action == (Res["Cancel"] ?? "İptal") || string.IsNullOrEmpty(action))
                {
                    return;
                }

                IsLoading = true;

                string conversationType = action == "Genel Sohbet" ? "General" : "Negotiation";
                string? productIdToPass = action == "Genel Sohbet" ? null : Product.ProductId;

                var conversationResult = await _messagingService.GetOrCreateConversationAsync(
                    currentUser.UserId, 
                    Product.UserId, 
                    productIdToPass, 
                    conversationType);

                if (conversationResult.Success)
                {
                    await Shell.Current.GoToAsync($"{nameof(ChatPage)}?conversationId={conversationResult.Data.ConversationId}");
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert(Res["Error"], conversationResult.Message, Res["Ok"]);
                }
            }
            catch (Exception ex)
            {
                if (Application.Current?.MainPage != null)
                    await Application.Current.MainPage.DisplayAlert(Res["Error"], $"{Res["ContactFailed"]}: {ex.Message}", Res["Ok"]);
            }
            finally
            {
                IsLoading = false;
            }
        }


        [RelayCommand]
        private async Task SendRequestAsync()
        {
            if (Product == null || IsLoading) return;
            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser == null)
            {
                if (Application.Current?.MainPage != null)
                    await Application.Current.MainPage.DisplayAlert(Res["Error"], Res["LoginRequired"], Res["Ok"]);
                return;
            }

            try
            {
                switch (Product.Type)
                {
                    case ProductType.Takas:
                        WeakReferenceMessenger.Default.Send(new ShowTradeOfferPopupMessage(Product));
                        break;

                    case ProductType.Satis:
                        // KullanÄ±cÄ±ya liste fiyatÄ± mÄ± yoksa pazarlÄ±k mÄ± istediÄŸini soralÄ±m
                        var action = await Application.Current.MainPage.DisplayActionSheet(
                            "SatÄ±n Alma SeÃ§eneÄŸi",
                            Res["Cancel"] ?? "Ä°ptal",
                            null,
                            $"Liste FiyatÄ±yla Al ({Product.Price:N2}â‚º)",
                            "Fiyat Teklifi Ver (PazarlÄ±k Yap)");

                        if (action == (Res["Cancel"] ?? "Ä°ptal") || action == null)
                        {
                            return;
                        }

                        decimal targetPrice = Product.Price;
                        bool isFixedPrice = true;

                        if (action == "Fiyat Teklifi Ver (PazarlÄ±k Yap)")
                        {
                            var priceResult = await Application.Current.MainPage.DisplayPromptAsync(
                                "ğŸ’° Fiyat Teklifi",
                                $"'{Product.Title}' iÃ§in teklifinizi girin (â‚º):",
                                Res["SendButton"] ?? "GÃ¶nder",
                                Res["Cancel"] ?? "Ä°ptal",
                                "Ã–rn: 450",
                                keyboard: Keyboard.Numeric);

                            if (string.IsNullOrWhiteSpace(priceResult))
                            {
                                return;
                            }

                            if (!decimal.TryParse(priceResult.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out targetPrice) || targetPrice <= 0)
                            {
                                await Application.Current.MainPage.DisplayAlert(Res["Error"] ?? "Hata", "GeÃ§erli bir tutar giriniz.", Res["Ok"] ?? "Tamam");
                                return;
                            }
                            isFixedPrice = false;
                        }
                        IsLoading = true;
                        // ✅ SORUN 1 FIX: isFixedPrice parametresini geçir
                        var saleResult = await _transactionService.CreateRequestAsync(
                            Product, currentUser, isFixedPriceRequest: isFixedPrice);

                        if (saleResult.Success)
                        {
                            ActiveTransaction = saleResult.Data;
                            HasActiveTransaction = true;

                            // ✅ SORUN 1 FIX: ActiveTransaction'a flag yansıt (servis zaten set etti ama local nesne için)
                            if (ActiveTransaction != null)
                                ActiveTransaction.IsFixedPriceRequest = isFixedPrice;
                            // Hemen konuÅŸmayÄ± baÅŸlat
                            var convResult = await _transactionService.StartConversationForTransactionAsync(ActiveTransaction.TransactionId, currentUser.UserId);

                            if (convResult.Success)
                            {
                                // SeÃ§ilen fiyatÄ± (liste fiyatÄ± veya pazarlÄ±k) teklif olarak gÃ¶nder
                                // isInitialRequest true ise "liste fiyatÄ±ndan almak istiyor", false ise "teklif verdi" yazar.
                                await _transactionService.ProposePriceForSaleAsync(ActiveTransaction.TransactionId, targetPrice, currentUser.UserId, isInitialRequest: isFixedPrice);

                                // Durum bilgisini chate entegre et
                                string statusNote = isFixedPrice
                                    ? "â³ SatÄ±n alma talebi (liste fiyatÄ±) oluÅŸturuldu.\nLÃ¼tfen satÄ±cÄ±nÄ±n onaylamasÄ± bekleniyor."
                                    : $"â³ PazarlÄ±k baÅŸlatÄ±ldÄ± (Teklif: {targetPrice:N2}â‚º).\nLÃ¼tfen satÄ±cÄ±nÄ±n teklifi deÄŸerlendirmesi bekleniyor.";

                                var systemMessageRequest = new SendMessageRequest
                                {
                                    ReceiverId = Product.UserId,
                                    Content = statusNote,
                                    Type = MessageType.System,
                                    ProductId = Product.ProductId,
                                    ConversationType = "Negotiation" // ✅ YENİ: Pazarlık sohbetine zorla
                                };
                                await _messagingService.SendMessageAsync(systemMessageRequest, currentUser);
                            }

                            // Pop-up kaldÄ±rÄ±ldÄ±, kullanÄ±cÄ±yÄ± direkt konuÅŸma penceresine yÃ¶nlendir
                            if (convResult.Success)
                            {
                                await Shell.Current.GoToAsync($"{nameof(ChatPage)}?conversationId={convResult.Data}");
                            }
                        }
                        else
                        {
                            if (Application.Current?.MainPage != null)
                                await Application.Current.MainPage.DisplayAlert(Res["Error"], saleResult.Message, Res["Ok"]);
                        }
                        break;

                    case ProductType.Bagis:
                        IsLoading = true;
                        // BUG-17 FIX: Bagis akisi - alert + chat yonlendirmesi eklendi
                        var donationResult = await _transactionService.CreateRequestAsync(Product, currentUser);

                        if (donationResult.Success)
                        {
                            ActiveTransaction = donationResult.Data;
                            HasActiveTransaction = true;

                            // Konusmayi baslat (satis/takas akisiyla tutarli)
                            var donationConvResult = await _transactionService.StartConversationForTransactionAsync(
                                ActiveTransaction.TransactionId, currentUser.UserId);

                            await Application.Current.MainPage.DisplayAlert(
                                Res["Success"],
                                "Bagis talebiniz gonderildi! Urun sahibiyle mesajlasarak teslimatı koordine edebilirsiniz.",
                                Res["Ok"]
                            );

                            // BUG-17: Chat ekranina yonlendir
                            if (donationConvResult.Success)
                            {
                                await Shell.Current.GoToAsync($"{nameof(ChatPage)}?conversationId={donationConvResult.Data}");
                            }
                        }
                        else
                        {
                            if (Application.Current?.MainPage != null)
                                await Application.Current.MainPage.DisplayAlert(Res["Error"], donationResult.Message, Res["Ok"]);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                if (Application.Current?.MainPage != null)
                    await Application.Current.MainPage.DisplayAlert(Res["Error"], ex.Message, Res["Ok"]);
            }
            finally
            {
                IsLoading = false;
            }
        }

        //  SatÄ±cÄ±ya mesaj gÃ¶nder (Transaction Ã¼zerinden)
        [RelayCommand]
        private async Task MessageSellerAsync()
        {
            if (ActiveTransaction == null || IsLoading) return;

            try
            {
                IsLoading = true;

                var result = await _transactionService.StartConversationForTransactionAsync(
                    ActiveTransaction.TransactionId,
                    ActiveTransaction.BuyerId
                );

                if (result.Success)
                {
                    await Shell.Current.GoToAsync($"{nameof(ChatPage)}?conversationId={result.Data}");
                }
                else
                {
                    if (Application.Current?.MainPage != null)
                        await Application.Current.MainPage.DisplayAlert(Res["Error"], result.Message, Res["Ok"]);
                }
            }
            catch (Exception ex)
            {
                if (Application.Current?.MainPage != null)
                    await Application.Current.MainPage.DisplayAlert(Res["Error"], $"Mesaj gÃ¶nderilemedi: {ex.Message}", Res["Ok"]);
            }
            finally
            {
                IsLoading = false;
            }
        }

        //  Fiyat teklifi (SatÄ±ÅŸ iÃ§in - AlÄ±cÄ±) ArtÄ±k Chat ekranÄ±na yÃ¶nlendiriyor
        [RelayCommand]
        private async Task ProposePriceAsync()
        {
            if (ActiveTransaction == null || Product == null || Product.Type != ProductType.Satis) return;

            // "PazarlÄ±k Yap" butonuna basÄ±ldÄ±ÄŸÄ±nda direkt chat ekranÄ±na yÃ¶nlendiriyoruz.
            await MessageSellerAsync();
        }

        //  Ek nakit teklifi (Takas iÃ§in - Talep Eden)
        [RelayCommand]
        private async Task ProposeAdditionalCashAsync()
        {
            if (ActiveTransaction == null || Product == null || Product.Type != ProductType.Takas) return;

            try
            {
                // âœ… DÃœZELTÄ°LDÄ°: TALEP EDEN iÃ§in net metin (sadece kendi teklifini ve sahibin karÅŸÄ± teklifini gÃ¶rÃ¼r)
                var yourOfferText = ActiveTransaction.AdditionalCashByRequester.HasValue
                    ? $"ğŸ’° Sizin Teklifiniz: {ActiveTransaction.AdditionalCashByRequester:N2}â‚º\n"
                    : "";

                var counterOfferText = ActiveTransaction.CounterCashByOwner.HasValue
                    ? $"ğŸ”„ Sahip'in KarÅŸÄ± Teklifi: {ActiveTransaction.CounterCashByOwner:N2}â‚º\n\n"
                    : "\n";

                if (Application.Current?.MainPage == null) return;

                var result = await Application.Current.MainPage.DisplayPromptAsync(
                    "ğŸ’° Ek Nakit Teklifi",
                    $"{yourOfferText}{counterOfferText}Yeni ek nakit teklifinizi girin:",
                    Res["SendButton"],
                    Res["Cancel"],
                    Res["AmountTL"],
                    keyboard: Keyboard.Numeric
                );

                if (string.IsNullOrWhiteSpace(result)) return;

                if (!decimal.TryParse(result.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var cashAmount) || cashAmount < 0)
                {
                    await Application.Current.MainPage.DisplayAlert(Res["Error"], Res["EnterValidAmount"], Res["Ok"]);
                    return;
                }

                IsLoading = true;
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null) return;

                var proposeResult = await _transactionService.ProposeAdditionalCashAsync(
                    ActiveTransaction.TransactionId,
                    cashAmount,
                    currentUser.UserId
                );

                if (proposeResult.Success)
                {
                    await Application.Current.MainPage.DisplayAlert(
                        Res["Success"],
                        Res["CashOfferSent"],
                        Res["Ok"]
                    );
                    await LoadActiveTransactionAsync(currentUser.UserId);
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert(Res["Error"], proposeResult.Message, Res["Ok"]);
                }
            }
            catch (Exception ex)
            {
                if (Application.Current?.MainPage != null)
                    await Application.Current.MainPage.DisplayAlert(Res["Error"], ex.Message, Res["Ok"]);
            }
            finally
            {
                IsLoading = false;
            }
        }

        //  AnlaÅŸÄ±lan fiyatÄ± kabul et
        [RelayCommand]
        private async Task AcceptNegotiatedPriceAsync()
        {
            if (ActiveTransaction == null || Product == null || !ActiveTransaction.IsNegotiating) return;

            try
            {
                var agreedAmount = ActiveTransaction.AgreedAmount;
                var message = Product.Type == ProductType.Satis
                    ? string.Format(Res["AcceptPriceForProduct"], Product.Title, agreedAmount)
                    : string.Format(Res["AcceptAdditionalCashForTrade"], Product.Title, agreedAmount);

                if (Application.Current?.MainPage == null) return;

                var confirm = await Application.Current.MainPage.DisplayAlert(
                    Res["PriceConfirmation"],
                    message,
                    Res["YesIAccept"],
                    Res["Cancel"]
                );

                if (!confirm) return;

                IsLoading = true;
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null) return;

                var acceptResult = await _transactionService.AcceptNegotiatedPriceAsync(
                    ActiveTransaction.TransactionId,
                    currentUser.UserId
                );

                if (acceptResult.Success)
                {
                    await Application.Current.MainPage.DisplayAlert(
                        Res["Success"],
                        string.Format(Res["AgreedOnPrice"], agreedAmount),
                        Res["Ok"]
                    );
                    await LoadActiveTransactionAsync(currentUser.UserId);
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert(Res["Error"], acceptResult.Message, Res["Ok"]);
                }
            }
            catch (Exception ex)
            {
                if (Application.Current?.MainPage != null)
                    await Application.Current.MainPage.DisplayAlert(Res["Error"], ex.Message, Res["Ok"]);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task ToggleFavoriteAsync()
        {
            // YÃ¼kleniyorsa, kullanÄ±cÄ± kendi Ã¼rÃ¼nÃ¼yse veya Ã¼rÃ¼n null ise iÅŸlem yapma
            if (IsLoading || IsOwner || Product == null) return;

            try
            {
                IsLoading = true;

                // 1. OPTÄ°MÄ°K GÃœNCELLEME:
                // Servis cevabÄ±nÄ± beklemeden UI'Ä± hemen gÃ¼ncelle
                if (IsFavorite)
                {
                    // Favoriden Ã§Ä±karÄ±lÄ±yor
                    IsFavorite = false;
                    Product.FavoriteCount = Math.Max(0, Product.FavoriteCount - 1);
                }
                else
                {
                    // Favoriye ekleniyor
                    IsFavorite = true;
                    Product.FavoriteCount++;
                }


                //  : UI'Ä±n anlÄ±k deÄŸiÅŸmesi iÃ§in Product nesnesinin deÄŸiÅŸtiÄŸini bildiriyoruz
                OnPropertyChanged(nameof(Product));

                // 2. SERVÄ°S Ä°ÅLEMÄ° (Arka Planda):
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser != null)
                {
                    if (IsFavorite)
                    {
                        await _favoriteService.AddToFavoritesAsync(currentUser.UserId, ProductId);
                    }
                    else
                    {
                        await _favoriteService.RemoveFromFavoritesAsync(currentUser.UserId, ProductId);
                    }

                    // DiÄŸer sayfalarÄ± (Liste vb.) haberdar et
                    WeakReferenceMessenger.Default.Send(new FavoriteCountChangedMessage(Product));
                }
            }
            catch (Exception ex)
            {
                // 3. HATA DURUMU (ROLLBACK):
                // EÄŸer serviste hata olursa, yaptÄ±ÄŸÄ±mÄ±z deÄŸiÅŸikliÄŸi geri alÄ±yoruz
                IsFavorite = !IsFavorite;
                Product.FavoriteCount = IsFavorite ? Product.FavoriteCount + 1 : Math.Max(0, Product.FavoriteCount - 1);

                OnPropertyChanged(nameof(Product)); // UI'Ä± tekrar dÃ¼zelt

                if (Application.Current?.MainPage != null)
                    await Application.Current.MainPage.DisplayAlert(Res["Error"], Res["OperationFailed"] + ": " + ex.Message, Res["Ok"]);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task ShareProductAsync()
        {
            if (Product == null) return;
            try
            {
                await Share.RequestAsync(new ShareTextRequest
                {
                    Title = Product.Title,
                    Text = $"{Product.Title}\n{Product.Description}\n{(Product.Type == ProductType.Satis ? $"{Product.Price:N2} â‚º" : Product.Type == ProductType.Bagis ? "Ãœcretsiz" : "Takas")}\n\n{Res["SharedWithKamPay"]}"
                });
            }
            catch (Exception ex)
            {
                if (Application.Current?.MainPage != null)
                    await Application.Current.MainPage.DisplayAlert(Res["Error"], $"{Res["CouldNotShare"]}: {ex.Message}", Res["Ok"]);
            }
        }

        [RelayCommand]
        private async Task MarkAsSoldAsync()
        {
            if (Product == null || Application.Current?.MainPage == null) return;

            if (Product.Type == ProductType.Satis)
            {
                try
                {
                    IsLoading = true;
                    var transactions = await _firebaseClient
                        .Child(Constants.TransactionsCollection)
                        .OrderBy("ProductId")
                        .EqualTo(ProductId)
                        .OnceAsync<Transaction>();

                    var productTransactions = transactions
                        .Select(t => t.Object)
                        .Where(t => t.Status == TransactionStatus.Accepted ||
                                    t.Status == TransactionStatus.Pending ||
                                    t.Status == TransactionStatus.Negotiating)
                        .ToList();

                    IsLoading = false;

                    if (productTransactions.Any())
                    {
                        var options = productTransactions.Select(t => $"{t.BuyerName}").Distinct().ToList();
                        options.Add("Uygulama dÄ±ÅŸÄ±ndan birine sattÄ±m");

                        var action = await Application.Current.MainPage.DisplayActionSheet(
                            "Bu Ã¼rÃ¼nÃ¼ kime sattÄ±nÄ±z?",
                            "Ä°ptal",
                            null,
                            options.ToArray()
                        );

                        if (action == "Ä°ptal" || string.IsNullOrEmpty(action))
                            return;

                        IsLoading = true;

                        if (action == "Uygulama dÄ±ÅŸÄ±ndan birine sattÄ±m")
                        {
                            await MarkProductAsSoldDirectlyAsync();
                        }
                        else
                        {
                            var selectedTransaction = productTransactions.FirstOrDefault(t => $"{t.BuyerName}" == action);
                            if (selectedTransaction != null)
                            {
                                // GÃ¼venli Teslimat (QR Kod) sÃ¼recine yÃ¶nlendir
                                await Shell.Current.GoToAsync($"QRCodeDisplayPage?transactionId={selectedTransaction.TransactionId}");
                            }
                        }
                        return;
                    }
                }
                catch (Exception ex)
                {
                    KamPay.Helpers.AppLogger.DebugLog($"Hata: {ex.Message}");
                }
                finally
                {
                    IsLoading = false;
                }
            }

            var confirm = await Application.Current.MainPage.DisplayAlert(
                Res["Confirmation"],
                Res["ConfirmMarkAsSold"],
                Res["Yes"],
                Res["No"]
            );

            if (!confirm) return;
            await MarkProductAsSoldDirectlyAsync();
        }

        private async Task MarkProductAsSoldDirectlyAsync()
        {
            try
            {
                IsLoading = true;

                await CancelProductTransactionsAsync(
                    "Urun Satildi",
                    "Uzerinde teklif verdiginiz '{0}' urunu uygulama disinda satildi.");

                var result = await _productService.MarkAsSoldAsync(ProductId);

                if (result.Success)
                {
                    if (Application.Current?.MainPage != null)
                        await Application.Current.MainPage.DisplayAlert(Res["Success"], Res["ProductMarkedAsSold"], Res["Ok"]);

                    // Güncel ürün verisini çek ve listeye bildir
                    var soldResult = await _productService.GetProductByIdAsync(ProductId);
                    if (soldResult.Success && soldResult.Data != null)
                    {
                        WeakReferenceMessenger.Default.Send(new ProductUpdatedMessage(soldResult.Data));
                    }

                    await LoadProductAsync();
                }
                else
                {
                    if (Application.Current?.MainPage != null)
                        await Application.Current.MainPage.DisplayAlert(Res["Error"], result.Message, Res["Ok"]);
                }
            }
            catch (Exception ex)
            {
                if (Application.Current?.MainPage != null)
                    await Application.Current.MainPage.DisplayAlert(Res["Error"], $"{Res["OperationFailed"]}: {ex.Message}", Res["Ok"]);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task CancelProductTransactionsAsync(string notificationTitle, string notificationMessageFormat)
        {
            try
            {
                var pendingTransactions = await _firebaseClient
                    .Child(Constants.TransactionsCollection)
                    .OrderBy("ProductId")
                    .EqualTo(ProductId)
                    .OnceAsync<Transaction>();

                foreach (var txEntry in pendingTransactions)
                {
                    var tx = txEntry.Object;
                    if (tx == null)
                        continue;

                    tx.TransactionId = txEntry.Key;

                    if (tx.Status != TransactionStatus.Pending &&
                        tx.Status != TransactionStatus.Negotiating &&
                        tx.Status != TransactionStatus.Accepted)
                    {
                        continue;
                    }

                    if (tx.IsNegotiating || tx.Status == TransactionStatus.Negotiating)
                        await _negotiationOfferService.ExpireActiveOfferAsync(tx.TransactionId);

                    var notes = tx.NegotiationNotes +
                        (string.IsNullOrEmpty(tx.NegotiationNotes) ? "" : "\n") +
                        $"Urun kapatildigi icin islem iptal edildi. [{DateTime.UtcNow:dd.MM.yyyy HH:mm}]";

                    var updates = new Dictionary<string, object>
                    {
                        [$"{Constants.TransactionsCollection}/{tx.TransactionId}/Status"] = TransactionStatus.Cancelled,
                        [$"{Constants.TransactionsCollection}/{tx.TransactionId}/IsNegotiating"] = false,
                        [$"{Constants.TransactionsCollection}/{tx.TransactionId}/UpdatedAt"] = DateTime.UtcNow,
                        [$"{Constants.TransactionsCollection}/{tx.TransactionId}/NegotiationNotes"] = notes
                    };

                    await ApplyMultiPathUpdatesAsync(updates);

                    await _notificationService.CreateNotificationAsync(new Notification
                    {
                        UserId = tx.BuyerId,
                        Type = NotificationType.OfferRejected,
                        Title = notificationTitle,
                        Message = string.Format(notificationMessageFormat, tx.ProductTitle),
                        ActionUrl = nameof(Views.OffersPage)
                    });
                }
            }
            catch (Exception cancelEx)
            {
                KamPay.Helpers.AppLogger.DebugLog($"Bekleyen teklifler iptal edilirken hata: {cancelEx.Message}");
            }
        }

        private async Task ApplyMultiPathUpdatesAsync(Dictionary<string, object> updates)
        {
            foreach (var update in updates)
            {
                var pathParts = update.Key
                    .Split('/', StringSplitOptions.RemoveEmptyEntries);

                if (pathParts.Length == 0)
                    continue;

                var node = _firebaseClient.Child(pathParts[0]);
                foreach (var part in pathParts.Skip(1))
                {
                    node = node.Child(part);
                }

                await node.PutAsync(update.Value);
            }
        }

        [RelayCommand]
        private async Task EditProductAsync()
        {
            if (Product == null) return;
            await Shell.Current.GoToAsync($"{nameof(EditProductPage)}?productId={ProductId}");
        }

        [RelayCommand]
        private async Task DeleteProductAsync()
        {
            if (Product == null) return;

            if (Application.Current?.MainPage == null) return;

            var confirm = await Application.Current.MainPage.DisplayAlert(
                Res["Confirmation"],
                Res["ConfirmDeleteProduct"],
                Res["YesDelete"],
                Res["Cancel"]
            );

            if (!confirm) return;

            try
            {
                IsLoading = true;

                await CancelProductTransactionsAsync(
                    "Urun Silindi",
                    "Uzerinde teklif verdiginiz '{0}' urunu yayindan kaldirildi.");

                var result = await _productService.DeleteProductAsync(ProductId);

                if (result.Success)
                {
                    WeakReferenceMessenger.Default.Send(new ProductDeletedMessage(ProductId));
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert(Res["Error"], result.Message, Res["Ok"]);
                }
            }
            catch (Exception ex)
            {
                if (Application.Current?.MainPage != null)
                    await Application.Current.MainPage.DisplayAlert(Res["Error"], $"{Res["DeleteFailed"]}: {ex.Message}", Res["Ok"]);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task ReportProductAsync()
        {
            if (Product == null) return;

            if (Application.Current?.MainPage == null) return;

            var reason = await Application.Current.MainPage.DisplayActionSheet(
                Res["ReportReason"],
                Res["Cancel"],
                null,
                Res["InappropriateContent"],
                Res["FakeProduct"],
                Res["MisleadingInfo"],
                Res["Other"]
            );

            if (reason != null && reason != Res["Cancel"])
            {
                try
                {
                    IsLoading = true;
                    var currentUser = await _authService.GetCurrentUserAsync();
                    if (currentUser != null)
                    {
                        var reportId = Guid.NewGuid().ToString();
                        var reportData = new
                        {
                            ReportId = reportId,
                            ProductId = Product.ProductId,
                            ProductTitle = Product.Title,
                            ReportedUserId = Product.UserId,
                            ReporterUserId = currentUser.UserId,
                            ReporterName = currentUser.FullName,
                            Reason = reason,
                            ReportedAt = DateTime.UtcNow,
                            Status = "Pending"
                        };

                        await _firebaseClient
                            .Child("reports")
                            .Child(reportId)
                            .PutAsync(reportData);
                    }

                    await Application.Current.MainPage.DisplayAlert(Res["Info"], Res["ReportReceived"], Res["Ok"]);
                }
                catch (Exception ex)
                {
                    KamPay.Helpers.AppLogger.DebugLog($"Şikayet kaydedilirken hata: {ex.Message}");
                    await Application.Current.MainPage.DisplayAlert(Res["Error"], "Şikayetiniz gönderilirken bir hata oluştu.", Res["Ok"]);
                }
                finally
                {
                    IsLoading = false;
                }
            }
        }

        [RelayCommand]
        private void PreviousImage()
        {
            if (ProductImages.Count == 0) return;

            CurrentImageIndex--;
            if (CurrentImageIndex < 0)
            {
                CurrentImageIndex = ProductImages.Count - 1;
            }
        }

        [RelayCommand]
        private void NextImage()
        {
            if (ProductImages.Count == 0) return;

            CurrentImageIndex++;
            if (CurrentImageIndex >= ProductImages.Count)
            {
                CurrentImageIndex = 0;
            }
        }

        //  ÃœrÃ¼n fotoÄŸrafÄ±nÄ± tam ekran gÃ¶ster
        [RelayCommand]
        private async Task ViewProductImageAsync(string imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl)) return;

            try
            {
                var imagesList = ProductImages.ToList();
                var index = ProductImages.IndexOf(imageUrl);
                if (index < 0) index = 0;

                var parameters = new Dictionary<string, object>
                {
                    { "images", imagesList },
                    { "index", index }
                };

                await Shell.Current.GoToAsync("ImageViewerPage", parameters);
            }
            catch (Exception)
            {
                // Fallback to single image if something goes wrong
                await Shell.Current.GoToAsync($"ImageViewerPage?photoUrl={Uri.EscapeDataString(imageUrl)}");
            }
        }

        //  Mevcut gÃ¶rseli tam ekran gÃ¶ster
        [RelayCommand]
        private async Task ViewCurrentImageAsync()
        {
            if (ProductImages.Count == 0 || CurrentImageIndex < 0 || CurrentImageIndex >= ProductImages.Count)
                return;

            var currentImageUrl = ProductImages[CurrentImageIndex];
            await ViewProductImageAsync(currentImageUrl);
        }

        [RelayCommand]
        private async Task OpenLocationAsync()
        {
            if (Product == null || Product.Latitude == null || Product.Longitude == null) return;

            try
            {
                var location = new Location(Product.Latitude.Value, Product.Longitude.Value);
                var options = new MapLaunchOptions { Name = Product.Location };

                await Map.OpenAsync(location, options);
            }
            catch (Exception ex)
            {
                if (Application.Current?.MainPage != null)
                    await Application.Current.MainPage.DisplayAlert(Res["Error"], $"{Res["MapOpenFailed"]}: {ex.Message}", Res["Ok"]);
            }
        }

        [RelayCommand]
        private async Task GoBackAsync()
        {
            await Shell.Current.GoToAsync("..");
        }
    }

}
