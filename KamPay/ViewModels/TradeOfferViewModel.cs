using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Maui.Views;
using KamPay.Models;
using KamPay.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace KamPay.ViewModels
{
    [QueryProperty(nameof(ProductId), "productId")]
    public partial class TradeOfferViewModel : ObservableObject
    {
        private readonly IProductService _productService;
        private readonly ITransactionService _transactionService;
        private readonly IAuthenticationService _authService;

        [ObservableProperty]
        private string productId;

        [ObservableProperty]
        private Product targetProduct;

        [ObservableProperty]
        private ObservableCollection<Product> myProducts;

        [ObservableProperty]
        private Product selectedProduct;

        [ObservableProperty]
        private string offerMessage;

        [ObservableProperty]
        private bool isLoading;

        public TradeOfferViewModel(IProductService productService, ITransactionService transactionService, IAuthenticationService authService)
        {
            _productService = productService;
            _transactionService = transactionService;
            _authService = authService;
            MyProducts = new ObservableCollection<Product>();
        }

        async partial void OnProductIdChanged(string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                await LoadInitialDataAsync();
            }
        }

        private async Task LoadInitialDataAsync()
        {
            IsLoading = true;

            var targetProductResult = await _productService.GetProductByIdAsync(ProductId);
            if (targetProductResult.Success)
            {
                TargetProduct = targetProductResult.Data;
            }

            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser != null)
            {
                var myProductsResult = await _productService.GetUserProductsAsync(currentUser.UserId);
                if (myProductsResult.Success)
                {
                    MyProducts.Clear();
                    foreach (var product in myProductsResult.Data.Where(p => p.IsActive && !p.IsSold && p.ProductId != ProductId))
                    {
                        MyProducts.Add(product);
                    }
                }
            }
            IsLoading = false;
        }

        [RelayCommand]
        private async Task SubmitOfferAsync(Popup popup)
        {
            if (SelectedProduct == null)
            {
                await Application.Current.MainPage.DisplayAlert("Hata", "Lütfen takas için bir ürün seçin.", "Tamam");
                return;
            }

            IsLoading = true;
            var currentUser = await _authService.GetCurrentUserAsync();
            var result = await _transactionService.CreateTradeOfferAsync(TargetProduct, SelectedProduct.ProductId, OfferMessage, currentUser);
            await Application.Current.MainPage.DisplayAlert(result.Success ? "Baþarýlý" : "Hata", result.Message, "Tamam");
            IsLoading = false;

            if (popup != null)
            {
                await popup.CloseAsync();
            }
        }

        [RelayCommand]
        private async Task CancelAsync(Popup popup)
        {
            if (popup != null)
            {
                await popup.CloseAsync();
            }
        }
    }
}