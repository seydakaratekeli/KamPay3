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
    public partial class ProductDetailViewModel : ObservableObject
    {
        // Gerekli tüm servisleri tanımlıyoruz
        private readonly IProductService _productService;
        private readonly IAuthenticationService _authService;
        private readonly IFavoriteService _favoriteService;
        private readonly IMessagingService _messagingService;
        private readonly ITransactionService _transactionService;
        private string _lastLoadedProductId;

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

        public ObservableCollection<string> ProductImages { get; } = new();

        public ProductDetailViewModel(
            IProductService productService,
            IAuthenticationService authService,
            IFavoriteService favoriteService,
            IMessagingService messagingService,
            ITransactionService transactionService)
        {
            _productService = productService;
            _authService = authService;
            _favoriteService = favoriteService;
            _messagingService = messagingService;
            _transactionService = transactionService;
        }

        partial void OnProductIdChanged(string value)
        {
            if (!string.IsNullOrEmpty(value) && value != _lastLoadedProductId)
            {
                _lastLoadedProductId = value;
                _ = LoadProductAsync();
            }
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
                    await Application.Current.MainPage.DisplayAlert("Hata", "Ürün bulunamadı", "Tamam");
                    await Shell.Current.GoToAsync("..");
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Hata", $"Ürün yüklenirken hata oluştu: {ex.Message}", "Tamam");
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
                    await Application.Current.MainPage.DisplayAlert("Hata", conversationResult.Message, "Tamam");
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Hata", $"İletişim kurulamadı: {ex.Message}", "Tamam");
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
                await Application.Current.MainPage.DisplayAlert("Hata", "Bu işlem için giriş yapmalısınız.", "Tamam");
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
                        await Application.Current.MainPage.DisplayAlert(result.Success ? "Başarılı" : "Hata", result.Message, "Tamam");
                        break;
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

                await Application.Current.MainPage.DisplayAlert("Hata", "İşlem başarısız oldu: " + ex.Message, "Tamam");
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
                    Text = $"{Product.Title}\n{Product.Description}\n{Product.PriceText}\n\nKamPay ile paylaşıldı"
                });
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Hata", $"Paylaşılamadı: {ex.Message}", "Tamam");
            }
        }

        [RelayCommand]
        private async Task MarkAsSoldAsync()
        {
            if (Product == null) return;

            var confirm = await Application.Current.MainPage.DisplayAlert(
                "Onay",
                "Ürünü satıldı olarak işaretlemek istediğinize emin misiniz?",
                "Evet",
                "Hayır"
            );

            if (!confirm) return;

            try
            {
                IsLoading = true;

                var result = await _productService.MarkAsSoldAsync(ProductId);

                if (result.Success)
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "Başarılı",
                        "Ürün satıldı olarak işaretlendi",
                        "Tamam"
                    );

                    await LoadProductAsync();
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert("Hata", result.Message, "Tamam");
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Hata", $"İşlem başarısız: {ex.Message}", "Tamam");
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
                "Onay",
                "Ürünü silmek istediğinize emin misiniz? Bu işlem geri alınamaz.",
                "Evet, Sil",
                "İptal"
            );

            if (!confirm) return;

            try
            {
                IsLoading = true;

                var result = await _productService.DeleteProductAsync(ProductId);

                if (result.Success)
                {
                    await Application.Current.MainPage.DisplayAlert("Başarılı", "Ürün silindi", "Tamam");
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert("Hata", result.Message, "Tamam");
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Hata", $"Silme başarısız: {ex.Message}", "Tamam");
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
                "Şikayet Nedeni",
                "İptal",
                null,
                "Uygunsuz içerik",
                "Sahte ürün",
                "Yanıltıcı bilgi",
                "Diğer"
            );

            if (reason != null && reason != "İptal")
            {
                await Application.Current.MainPage.DisplayAlert("Bilgi", "Şikayetiniz alındı. İnceleme süreci başlatıldı.", "Tamam");
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
                await Application.Current.MainPage.DisplayAlert("Hata", $"Harita açılamadı: {ex.Message}", "Tamam");
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