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
        private readonly IPriceQuoteService _priceQuoteService;
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
            IPriceQuoteService priceQuoteService)
        {
            _productService = productService;
            _authService = authService;
            _favoriteService = favoriteService;
            _messagingService = messagingService;
            _transactionService = transactionService;
            _userStateService = userStateService;
            _priceQuoteService = priceQuoteService;
            
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
                    await _productService.IncrementViewCountAsync(ProductId); // Servis hazırsa bunu aç

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
                        var result = await _transactionService.CreateRequestAsync(Product, currentUser);
                        await Application.Current.MainPage.DisplayAlert(result.Success ? Res["Success"] : Res["Error"], result.Message, Res["Ok"]);
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
                    // Not: Yukarıda IsFavorite'ı değiştirdiğimiz için ters mantık kurmuyoruz.
                    // Şu anki durum neyse serviste de onu yapmaya çalışıyoruz.

                    // Ancak bir önceki adımda durumu değiştirdiğimiz için:
                    // Eğer şu an True ise -> Ekleme işlemi yapılmıştır.
                    // Eğer şu an False ise -> Çıkarma işlemi yapılmıştır.

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

        /// <summary>
        /// Fiyat teklifi ver (Dolap tarzı pazarlık)
        /// </summary>
        [RelayCommand]
        private async Task MakeOfferAsync()
        {
            if (Product == null || IsLoading) return;

            // Sadece satılık ürünler için teklif verilebilir
            if (Product.Type != ProductType.Satis)
            {
                await Application.Current.MainPage.DisplayAlert(
                    Res["Error"], 
                    "Sadece satılık ürünler için fiyat teklifi verebilirsiniz.", 
                    Res["Ok"]);
                return;
            }

            // Kullanıcı kendi ürününe teklif veremez
            if (IsOwner)
            {
                await Application.Current.MainPage.DisplayAlert(
                    Res["Error"], 
                    "Kendi ürününüze teklif veremezsiniz.", 
                    Res["Ok"]);
                return;
            }

            // Ürün rezerve veya satılmışsa teklif verilemez
            if (Product.IsReserved || Product.IsSold)
            {
                await Application.Current.MainPage.DisplayAlert(
                    Res["Error"], 
                    "Bu ürün artık müsait değil.", 
                    Res["Ok"]);
                return;
            }

            try
            {
                IsLoading = true;
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    await Application.Current.MainPage.DisplayAlert(Res["Error"], Res["LoginRequired"], Res["Ok"]);
                    return;
                }

                // Teklif fiyatı iste
                var priceStr = await Application.Current.MainPage.DisplayPromptAsync(
                    "Fiyat Teklifi Ver 💰",
                    $"Ürün fiyatı: {Product.Price:N2} ₺\n\nTeklif etmek istediğiniz fiyatı girin:",
                    "Gönder",
                    "İptal",
                    placeholder: "Örn: " + (Product.Price * 0.8m).ToString("N0"),
                    keyboard: Keyboard.Numeric);

                if (string.IsNullOrEmpty(priceStr))
                    return;

                if (!decimal.TryParse(priceStr, out var offerPrice) || offerPrice <= 0)
                {
                    await Application.Current.MainPage.DisplayAlert(Res["Error"], "Geçerli bir fiyat girin.", Res["Ok"]);
                    return;
                }

                // Mantıklı fiyat kontrolü
                if (offerPrice >= Product.Price)
                {
                    var confirm = await Application.Current.MainPage.DisplayAlert(
                        "Dikkat",
                        $"Teklifiniz ({offerPrice:N2} ₺) ürün fiyatından ({Product.Price:N2} ₺) yüksek veya eşit. Devam etmek istiyor musunuz?",
                        "Evet",
                        "Hayır");
                    
                    if (!confirm)
                        return;
                }

                // Mesaj ekle (isteğe bağlı)
                var message = await Application.Current.MainPage.DisplayPromptAsync(
                    "Mesaj Ekle",
                    "Teklifinizle birlikte bir mesaj eklemek ister misiniz? (İsteğe bağlı)",
                    "Gönder",
                    "Atla",
                    placeholder: "Örn: Merhaba, bu ürünle ilgileniyorum...");

                // Teklifi oluştur
                var request = new CreateQuoteRequest
                {
                    QuoteType = PriceQuoteType.Product,
                    ReferenceId = Product.ProductId,
                    QuotedPrice = offerPrice,
                    Message = message ?? string.Empty
                };

                var result = await _priceQuoteService.CreateQuoteAsync(currentUser.UserId, request);

                if (result.IsValid)
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "Başarılı! 🎉",
                        $"Teklifiniz ({offerPrice:N2} ₺) satıcıya gönderildi. Satıcının cevabını bekleyebilirsiniz.",
                        Res["Ok"]);
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert(
                        Res["Error"],
                        string.Join("\n", result.Errors),
                        Res["Ok"]);
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert(
                    Res["Error"], 
                    $"Teklif gönderilirken hata: {ex.Message}", 
                    Res["Ok"]);
            }
            finally
            {
                IsLoading = false;
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