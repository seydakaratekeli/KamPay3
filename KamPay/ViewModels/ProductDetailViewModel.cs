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
        // Gerekli tüm servisler
        private readonly IProductService _productService;
        private readonly IAuthenticationService _authService;
        private readonly IFavoriteService _favoriteService;
        private readonly IMessagingService _messagingService;
        private readonly ITransactionService _transactionService;
        private readonly IUserStateService _userStateService;
        // ✅ DIP FIX: FirebaseClient artık DI'den geliyor (new keyword kaldırıldı)
        private readonly FirebaseClient _firebaseClient;
        private IDisposable? _transactionListener;
        private string? _lastLoadedProductId;
        private bool _disposed = false;

        // Localization helper
        private static LocalizationResourceManager Res => LocalizationResourceManager.Instance;

        [ObservableProperty]
        private string productId = string.Empty;

        [ObservableProperty]
        private Product? product;

        [ObservableProperty]
        private bool isLoading;

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
            FirebaseClient firebaseClient) // ✅ DIP FIX: YENİ PARAMETRE
        {
            _productService = productService;
            _authService = authService;
            _favoriteService = favoriteService;
            _messagingService = messagingService;
            _transactionService = transactionService;
            _userStateService = userStateService;
            _firebaseClient = firebaseClient ?? throw new ArgumentNullException(nameof(firebaseClient)); // ✅ DIP FIX: DI'den inject
            
            // Kullanıcı profil değişikliklerini dinle
            _userStateService.UserProfileChanged += OnUserProfileChanged;
            
            System.Diagnostics.Debug.WriteLine("✅ ProductDetailViewModel oluşturuldu (DIP uyumlu - FirebaseClient DI'den)");
        }

        private void OnUserProfileChanged(object? sender, User updatedUser)
        {
            // Eğer gösterilen ürün bu kullanıcıya aitse güncelle
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

                        //  Aktif transaction'ı yükle
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
                            errorMsg += $"\n\nTeknik Hata Özeti: {string.Join("\n", result.Errors)}";
                        }
                        await Application.Current.MainPage.DisplayAlert("Detaylı API Hatası", errorMsg, "Tamam");
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

        //  Aktif transaction'ı yükle VE realtime listener başlat
        private async Task LoadActiveTransactionAsync(string currentUserId)
        {
            try
            {
                // Kullanıcının gönderdiği teklifleri kontrol et
                var myOffersResult = await _transactionService.GetMyOffersAsync(currentUserId);
                
                if (myOffersResult.Success && myOffersResult.Data != null)
                {
                    // Bu ürün için pending/accepted/negotiating durumda bir transaction var mı?
                    var existingTransaction = myOffersResult.Data
                        .FirstOrDefault(t => t.ProductId == ProductId && 
                                           (t.Status == TransactionStatus.Pending || 
                                            t.Status == TransactionStatus.Accepted ||
                                            t.IsNegotiating));
                    
                    if (existingTransaction != null)
                    {
                        ActiveTransaction = existingTransaction;
                        HasActiveTransaction = true;

                        // ✅ YENİ: Realtime listener başlat
                        StartTransactionListener(existingTransaction.TransactionId);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ LoadActiveTransaction hatası: {ex.Message}");
            }
        }

        // ✅ YENİ: Transaction için realtime listener
        private void StartTransactionListener(string transactionId)
        {
            // Önceki listener'ı durdur
            _transactionListener?.Dispose();

            System.Diagnostics.Debug.WriteLine($"🔥 Transaction listener başlatılıyor: {transactionId}");

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

                            // ActiveTransaction'ı güncelle
                            if (ActiveTransaction != null && ActiveTransaction.TransactionId == transactionId)
                            {
                                ActiveTransaction = updated;
                                OnPropertyChanged(nameof(ActiveTransaction));
                                System.Diagnostics.Debug.WriteLine($"✅ Transaction güncellendi: IsNegotiating={updated.IsNegotiating}, Status={updated.Status}");
                            }
                        });
                    },
                    error =>
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ Transaction listener hatası: {error.Message}");
                    });
        }

        [RelayCommand]
        private async Task ContactSellerAsync()
        {
            if (Product == null || IsLoading) return;

            try
            {
                IsLoading = true;
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null || currentUser.UserId == Product.UserId) return;

                var conversationResult = await _messagingService.GetOrCreateConversationAsync(currentUser.UserId, Product.UserId, Product.ProductId);

                if (conversationResult.Success)
                {
                    await Shell.Current.GoToAsync($"{nameof(ChatPage)}?conversationId={conversationResult.Data.ConversationId}");
                }
                else
                {
                    if (Application.Current?.MainPage != null)
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
                        // Kullanıcıya liste fiyatı mı yoksa pazarlık mı istediğini soralım
                        var action = await Application.Current.MainPage.DisplayActionSheet(
                            "Satın Alma Seçeneği",
                            Res["Cancel"] ?? "İptal",
                            null,
                            $"Liste Fiyatıyla Al ({Product.Price:N2}₺)",
                            "Fiyat Teklifi Ver (Pazarlık Yap)");

                        if (action == (Res["Cancel"] ?? "İptal") || action == null)
                        {
                            return;
                        }

                        decimal targetPrice = Product.Price;
                        bool isFixedPrice = true;

                        if (action == "Fiyat Teklifi Ver (Pazarlık Yap)")
                        {
                            var priceResult = await Application.Current.MainPage.DisplayPromptAsync(
                                "💰 Fiyat Teklifi",
                                $"'{Product.Title}' için teklifinizi girin (₺):",
                                Res["SendButton"] ?? "Gönder",
                                Res["Cancel"] ?? "İptal",
                                "Örn: 450",
                                keyboard: Keyboard.Numeric);

                            if (string.IsNullOrWhiteSpace(priceResult))
                            {
                                return;
                            }

                            if (!decimal.TryParse(priceResult.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out targetPrice) || targetPrice <= 0)
                            {
                                await Application.Current.MainPage.DisplayAlert(Res["Error"] ?? "Hata", "Geçerli bir tutar giriniz.", Res["Ok"] ?? "Tamam");
                                return;
                            }
                            isFixedPrice = false;
                        }

                        IsLoading = true;
                        // Satış için talebi oluştur
                        var saleResult = await _transactionService.CreateRequestAsync(Product, currentUser);
                        
                        if (saleResult.Success)
                        {
                            ActiveTransaction = saleResult.Data;
                            HasActiveTransaction = true;
                            
                            // Hemen konuşmayı başlat
                            var convResult = await _transactionService.StartConversationForTransactionAsync(ActiveTransaction.TransactionId, currentUser.UserId);
                            
                            if (convResult.Success)
                            {
                                // Seçilen fiyatı (liste fiyatı veya pazarlık) teklif olarak gönder
                                // isInitialRequest true ise "liste fiyatından almak istiyor", false ise "teklif verdi" yazar.
                                await _transactionService.ProposePriceForSaleAsync(ActiveTransaction.TransactionId, targetPrice, currentUser.UserId, isInitialRequest: isFixedPrice);
                                
                                // Durum bilgisini chate entegre et
                                string statusNote = isFixedPrice 
                                    ? "⏳ Satın alma talebi (liste fiyatı) oluşturuldu.\nLütfen satıcının onaylaması bekleniyor."
                                    : $"⏳ Pazarlık başlatıldı (Teklif: {targetPrice:N2}₺).\nLütfen satıcının teklifi değerlendirmesi bekleniyor.";

                                var systemMessageRequest = new SendMessageRequest
                                {
                                    ReceiverId = Product.UserId,
                                    Content = statusNote,
                                    Type = MessageType.System,
                                    ProductId = Product.ProductId
                                };
                                await _messagingService.SendMessageAsync(systemMessageRequest, currentUser);
                            }
                            
                            // Pop-up kaldırıldı, kullanıcıyı direkt konuşma penceresine yönlendir
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
                        // ✅ Bağış akışı (değişiklik yok)
                        var donationResult = await _transactionService.CreateRequestAsync(Product, currentUser);
                        
                        if (donationResult.Success)
                        {
                            ActiveTransaction = donationResult.Data;
                            HasActiveTransaction = true;
                            
                            var message = "✅ Bağış talebiniz gönderildi!\n\n" +
                                          "Ürün sahibinin onayını bekleyin. Onaylandıktan sonra teslimat için QR kod oluşturulacak.";
                            
                            await Application.Current.MainPage.DisplayAlert(
                                Res["Success"], 
                                message, 
                                Res["Ok"]
                            );
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

        //  Satıcıya mesaj gönder (Transaction üzerinden)
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
                    await Application.Current.MainPage.DisplayAlert(Res["Error"], $"Mesaj gönderilemedi: {ex.Message}", Res["Ok"]);
            }
            finally
            {
                IsLoading = false;
            }
        }

        //  Fiyat teklifi (Satış için - Alıcı) Artık Chat ekranına yönlendiriyor
        [RelayCommand]
        private async Task ProposePriceAsync()
        {
            if (ActiveTransaction == null || Product == null || Product.Type != ProductType.Satis) return;
            
            // "Pazarlık Yap" butonuna basıldığında direkt chat ekranına yönlendiriyoruz.
            await MessageSellerAsync();
        }

        //  Ek nakit teklifi (Takas için - Talep Eden)
        [RelayCommand]
        private async Task ProposeAdditionalCashAsync()
        {
            if (ActiveTransaction == null || Product == null || Product.Type != ProductType.Takas) return;

            try
            {
                // ✅ DÜZELTİLDİ: TALEP EDEN için net metin (sadece kendi teklifini ve sahibin karşı teklifini görür)
                var yourOfferText = ActiveTransaction.AdditionalCashByRequester.HasValue
                    ? $"💰 Sizin Teklifiniz: {ActiveTransaction.AdditionalCashByRequester:N2}₺\n"
                    : "";

                var counterOfferText = ActiveTransaction.CounterCashByOwner.HasValue
                    ? $"🔄 Sahip'in Karşı Teklifi: {ActiveTransaction.CounterCashByOwner:N2}₺\n\n"
                    : "\n";

                if (Application.Current?.MainPage == null) return;

                var result = await Application.Current.MainPage.DisplayPromptAsync(
                    "💰 Ek Nakit Teklifi",
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

        //  Anlaşılan fiyatı kabul et
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
            // Yükleniyorsa, kullanıcı kendi ürünüyse veya ürün null ise işlem yapma
            if (IsLoading || IsOwner || Product == null) return;

            try
            {
                IsLoading = true;

                // 1. OPTİMİK GÜNCELLEME:
                // Servis cevabını beklemeden UI'ı hemen güncelle
                if (IsFavorite)
                {
                    // Favoriden çıkarılıyor
                    IsFavorite = false;
                    Product.FavoriteCount = Math.Max(0, Product.FavoriteCount - 1);
                }
                else
                {
                    // Favoriye ekleniyor
                    IsFavorite = true;
                    Product.FavoriteCount++;
                }

                
                //  : UI'ın anlık değişmesi için Product nesnesinin değiştiğini bildiriyoruz
                OnPropertyChanged(nameof(Product));

                // 2. SERVİS İŞLEMİ (Arka Planda):
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

                    // Diğer sayfaları (Liste vb.) haberdar et
                    WeakReferenceMessenger.Default.Send(new FavoriteCountChangedMessage(Product));
                }
            }
            catch (Exception ex)
            {
                // 3. HATA DURUMU (ROLLBACK):
                // Eğer serviste hata olursa, yaptığımız değişikliği geri alıyoruz
                IsFavorite = !IsFavorite;
                Product.FavoriteCount = IsFavorite ? Product.FavoriteCount + 1 : Math.Max(0, Product.FavoriteCount - 1);

                OnPropertyChanged(nameof(Product)); // UI'ı tekrar düzelt

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
                    Text = $"{Product.Title}\n{Product.Description}\n{(Product.Type == ProductType.Satis ? $"{Product.Price:N2} ₺" : Product.Type == ProductType.Bagis ? "Ücretsiz" : "Takas")}\n\n{Res["SharedWithKamPay"]}"
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
                        .OnceAsync<Transaction>();
                        
                    var productTransactions = transactions
                        .Select(t => t.Object)
                        .Where(t => t.ProductId == ProductId && 
                                   (t.Status == TransactionStatus.Accepted || t.Status == TransactionStatus.Pending))
                        .ToList();

                    IsLoading = false;

                    if (productTransactions.Any())
                    {
                        var options = productTransactions.Select(t => $"{t.BuyerName}").Distinct().ToList();
                        options.Add("Uygulama dışından birine sattım");

                        var action = await Application.Current.MainPage.DisplayActionSheet(
                            "Bu ürünü kime sattınız?",
                            "İptal",
                            null,
                            options.ToArray()
                        );

                        if (action == "İptal" || string.IsNullOrEmpty(action)) 
                            return;

                        IsLoading = true;

                        if (action == "Uygulama dışından birine sattım")
                        {
                            await MarkProductAsSoldDirectlyAsync();
                        }
                        else
                        {
                            var selectedTransaction = productTransactions.FirstOrDefault(t => $"{t.BuyerName}" == action);
                            if (selectedTransaction != null)
                            {
                                // Güvenli Teslimat (QR Kod) sürecine yönlendir
                                await Shell.Current.GoToAsync($"QRCodeDisplayPage?transactionId={selectedTransaction.TransactionId}");
                            }
                        }
                        return;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Hata: {ex.Message}");
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

                var result = await _productService.MarkAsSoldAsync(ProductId);

                if (result.Success)
                {
                    if (Application.Current?.MainPage != null)
                        await Application.Current.MainPage.DisplayAlert(Res["Success"], Res["ProductMarkedAsSold"], Res["Ok"]);

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

                var result = await _productService.DeleteProductAsync(ProductId);

                if (result.Success)
                {
                    await Application.Current.MainPage.DisplayAlert(Res["Success"], Res["ProductDeleted"], Res["Ok"]);
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
                await Application.Current.MainPage.DisplayAlert(Res["Info"], Res["ReportReceived"], Res["Ok"]);
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

        //  Ürün fotoğrafını tam ekran göster
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

        //  Mevcut görseli tam ekran göster
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

    public class FavoriteCountChangedMessage : CommunityToolkit.Mvvm.Messaging.Messages.ValueChangedMessage<Product>
    {
        public FavoriteCountChangedMessage(Product value) : base(value) { }
    }
}