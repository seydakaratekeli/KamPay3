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
        // Gerekli tüm servisleri tanımlıyoruz
        private readonly IProductService _productService;
        private readonly IAuthenticationService _authService;
        private readonly IFavoriteService _favoriteService;
        private readonly IMessagingService _messagingService;
        private readonly ITransactionService _transactionService;
        private readonly IUserStateService _userStateService;
        private string _lastLoadedProductId;
        private bool _disposed = false;

        // Localization helper
        private static LocalizationResourceManager Res => LocalizationResourceManager.Instance;

        [ObservableProperty]
        private string productId;

        [ObservableProperty]
        private Product product;

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

        // 🔥 YENİ: Aktif transaction bilgisi
        [ObservableProperty]
        private Transaction activeTransaction;

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
            IUserStateService userStateService)
        {
            _productService = productService;
            _authService = authService;
            _favoriteService = favoriteService;
            _messagingService = messagingService;
            _transactionService = transactionService;
            _userStateService = userStateService;
            
            // Kullanıcı profil değişikliklerini dinle
            _userStateService.UserProfileChanged += OnUserProfileChanged;
        }

        private void OnUserProfileChanged(object sender, User updatedUser)
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
                }
                _disposed = true;
            }
        }

        partial void OnProductIdChanged(string value)
        {
            if (!string.IsNullOrEmpty(value) && value != _lastLoadedProductId)
            {
                _lastLoadedProductId = value;
                _ = LoadProductAsync();
            }
        }

        partial void OnProductChanged(Product value)
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

                        // 🔥 YENİ: Aktif transaction'ı yükle
                        await LoadActiveTransactionAsync(currentUser.UserId);
                    }
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert(Res["Error"], Res["ProductNotFound"], Res["Ok"]);
                    await Shell.Current.GoToAsync("..");
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert(Res["Error"], $"{Res["ProductLoadError"]}: {ex.Message}", Res["Ok"]);
            }
            finally
            {
                IsLoading = false;
            }
        }

        // 🔥 YENİ: Aktif transaction'ı yükle
        private async Task LoadActiveTransactionAsync(string currentUserId)
        {
            try
            {
                // Kullanıcının gönderdiği teklifleri kontrol et
                var myOffersResult = await _transactionService.GetMyOffersAsync(currentUserId);
                
                if (myOffersResult.Success && myOffersResult.Data != null)
                {
                    // Bu ürün için pending durumda bir transaction var mı?
                    var existingTransaction = myOffersResult.Data
                        .FirstOrDefault(t => t.ProductId == ProductId && 
                                           t.Status == TransactionStatus.Pending);
                    
                    if (existingTransaction != null)
                    {
                        ActiveTransaction = existingTransaction;
                        HasActiveTransaction = true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ LoadActiveTransaction hatası: {ex.Message}");
            }
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
                    await Application.Current.MainPage.DisplayAlert(Res["Error"], conversationResult.Message, Res["Ok"]);
                }
            }
            catch (Exception ex)
            {
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
                await Application.Current.MainPage.DisplayAlert(Res["Error"], Res["LoginRequired"], Res["Ok"]);
                return;
            }

            IsLoading = true;
            try
            {
                switch (Product.Type)
                {
                    case ProductType.Takas:
                        WeakReferenceMessenger.Default.Send(new ShowTradeOfferPopupMessage(Product));
                        break;

                    case ProductType.Satis:
                    case ProductType.Bagis:
                        // 🔥 YENİ: Transaction oluştur
                        var result = await _transactionService.CreateRequestAsync(Product, currentUser);
                        
                        if (result.Success)
                        {
                            ActiveTransaction = result.Data;
                            HasActiveTransaction = true;
                            
                            await Application.Current.MainPage.DisplayAlert(
                                Res["Success"], 
                                "Talebiniz gönderildi. Artık satıcıyla mesajlaşabilir ve fiyat pazarlığı yapabilirsiniz.", 
                                Res["Ok"]
                            );
                        }
                        else
                        {
                            await Application.Current.MainPage.DisplayAlert(Res["Error"], result.Message, Res["Ok"]);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert(Res["Error"], ex.Message, Res["Ok"]);
            }
            finally
            {
                IsLoading = false;
            }
        }

        // 🔥 YENİ: Satıcıya mesaj gönder (Transaction üzerinden)
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
                    await Application.Current.MainPage.DisplayAlert(Res["Error"], result.Message, Res["Ok"]);
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert(Res["Error"], $"Mesaj gönderilemedi: {ex.Message}", Res["Ok"]);
            }
            finally
            {
                IsLoading = false;
            }
        }

        // 🔥 YENİ: Fiyat teklifi (Satış için - Alıcı)
        [RelayCommand]
        private async Task ProposePriceAsync()
        {
            if (ActiveTransaction == null || Product.Type != ProductType.Satis) return;

            try
            {
                var currentPriceText = Product.Price > 0 
                    ? $"Mevcut Fiyat: {Product.Price:N2} ₺\n\n" 
                    : "";

                var proposedText = ActiveTransaction.ProposedPriceByBuyer.HasValue
                    ? $"Sizin Teklifiniz: {ActiveTransaction.ProposedPriceByBuyer:N2} ₺\n"
                    : "";

                var counterText = ActiveTransaction.CounterOfferBySeller.HasValue
                    ? $"Satıcının Karşı Teklifi: {ActiveTransaction.CounterOfferBySeller:N2} ₺\n\n"
                    : "";

                var result = await Application.Current.MainPage.DisplayPromptAsync(
                    "💰 Fiyat Teklifi",
                    $"{currentPriceText}{proposedText}{counterText}Teklif etmek istediğiniz fiyatı girin:",
                    "Gönder",
                    "İptal",
                    "Fiyat (TL)",
                    keyboard: Keyboard.Numeric
                );

                if (string.IsNullOrWhiteSpace(result)) return;

                if (!decimal.TryParse(result, out var proposedPrice) || proposedPrice <= 0)
                {
                    await Application.Current.MainPage.DisplayAlert(Res["Error"], "Geçerli bir fiyat girin", Res["Ok"]);
                    return;
                }

                IsLoading = true;
                var currentUser = await _authService.GetCurrentUserAsync();
                var proposeResult = await _transactionService.ProposePriceForSaleAsync(
                    ActiveTransaction.TransactionId,
                    proposedPrice,
                    currentUser.UserId
                );

                if (proposeResult.Success)
                {
                    await Application.Current.MainPage.DisplayAlert(Res["Success"], "Fiyat teklifiniz gönderildi", Res["Ok"]);
                    await LoadActiveTransactionAsync(currentUser.UserId);
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert(Res["Error"], proposeResult.Message, Res["Ok"]);
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert(Res["Error"], ex.Message, Res["Ok"]);
            }
            finally
            {
                IsLoading = false;
            }
        }

        // 🔥 YENİ: Ek nakit teklifi (Takas için - Talep Eden)
        [RelayCommand]
        private async Task ProposeAdditionalCashAsync()
        {
            if (ActiveTransaction == null || Product.Type != ProductType.Takas) return;

            try
            {
                var currentText = ActiveTransaction.AdditionalCashByRequester.HasValue
                    ? $"Sizin Teklifiniz: {ActiveTransaction.AdditionalCashByRequester:N2} ₺\n"
                    : "";

                var counterText = ActiveTransaction.CounterCashByOwner.HasValue
                    ? $"Satıcının İsteği: {ActiveTransaction.CounterCashByOwner:N2} ₺\n\n"
                    : "";

                var result = await Application.Current.MainPage.DisplayPromptAsync(
                    "💰 Ek Nakit Teklifi",
                    $"{currentText}{counterText}Takas için eklemek istediğiniz nakit tutarını girin (0 girebilirsiniz):",
                    "Gönder",
                    "İptal",
                    "Tutar (TL)",
                    keyboard: Keyboard.Numeric
                );

                if (string.IsNullOrWhiteSpace(result)) return;

                if (!decimal.TryParse(result, out var cashAmount) || cashAmount < 0)
                {
                    await Application.Current.MainPage.DisplayAlert(Res["Error"], "Geçerli bir tutar girin", Res["Ok"]);
                    return;
                }

                IsLoading = true;
                var currentUser = await _authService.GetCurrentUserAsync();
                var proposeResult = await _transactionService.ProposeAdditionalCashAsync(
                    ActiveTransaction.TransactionId,
                    cashAmount,
                    currentUser.UserId
                );

                if (proposeResult.Success)
                {
                    await Application.Current.MainPage.DisplayAlert(Res["Success"], "Nakit teklifiniz gönderildi", Res["Ok"]);
                    await LoadActiveTransactionAsync(currentUser.UserId);
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert(Res["Error"], proposeResult.Message, Res["Ok"]);
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert(Res["Error"], ex.Message, Res["Ok"]);
            }
            finally
            {
                IsLoading = false;
            }
        }

        // 🔥 YENİ: Anlaşılan fiyatı kabul et
        [RelayCommand]
        private async Task AcceptNegotiatedPriceAsync()
        {
            if (ActiveTransaction == null || !ActiveTransaction.IsNegotiating) return;

            try
            {
                var agreedAmount = ActiveTransaction.AgreedAmount;
                var message = Product.Type == ProductType.Satis
                    ? $"'{Product.Title}' ürünü için {agreedAmount:N2}₺ fiyatını kabul ediyor musunuz?"
                    : $"'{Product.Title}' takası için {agreedAmount:N2}₺ ek ödemeyi kabul ediyor musunuz?";

                var confirm = await Application.Current.MainPage.DisplayAlert(
                    "✅ Fiyat Onayı",
                    message,
                    "Evet, Kabul Ediyorum",
                    "İptal"
                );

                if (!confirm) return;

                IsLoading = true;
                var currentUser = await _authService.GetCurrentUserAsync();
                var acceptResult = await _transactionService.AcceptNegotiatedPriceAsync(
                    ActiveTransaction.TransactionId,
                    currentUser.UserId
                );

                if (acceptResult.Success)
                {
                    await Application.Current.MainPage.DisplayAlert(
                        Res["Success"], 
                        $"Anlaşma sağlandı: {agreedAmount:N2}₺", 
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

                // 1. OPTİMİSTİK GÜNCELLEME:
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

                // 🔥 ÖNEMLİ: UI'ın anlık değişmesi için Product nesnesinin değiştiğini bildiriyoruz
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
                    Text = $"{Product.Title}\n{Product.Description}\n{Product.PriceText}\n\n{Res["SharedWithKamPay"]}"
                });
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert(Res["Error"], $"{Res["CouldNotShare"]}: {ex.Message}", Res["Ok"]);
            }
        }

        [RelayCommand]
        private async Task MarkAsSoldAsync()
        {
            if (Product == null) return;

            var confirm = await Application.Current.MainPage.DisplayAlert(
                Res["Confirmation"],
                Res["ConfirmMarkAsSold"],
                Res["Yes"],
                Res["No"]
            );

            if (!confirm) return;

            try
            {
                IsLoading = true;

                var result = await _productService.MarkAsSoldAsync(ProductId);

                if (result.Success)
                {
                    await Application.Current.MainPage.DisplayAlert(
                        Res["Success"],
                        Res["ProductMarkedAsSold"],
                        Res["Ok"]
                    );

                    await LoadProductAsync();
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert(Res["Error"], result.Message, Res["Ok"]);
                }
            }
            catch (Exception ex)
            {
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