using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KamPay.Models;
using KamPay.Services;
using System.Collections.ObjectModel;

namespace KamPay.ViewModels
{
    [QueryProperty(nameof(ProductId), "productId")]
    public partial class EditProductViewModel : ObservableObject
    {
        private readonly IProductService _productService;
        private string _productId;

        [ObservableProperty]
        private string title;
        [ObservableProperty] private string description;
        [ObservableProperty] private Category selectedCategory;
        [ObservableProperty] private ProductCondition selectedCondition;
        [ObservableProperty] private ProductType selectedType;
        [ObservableProperty] private decimal price;
        [ObservableProperty] private string location;
        [ObservableProperty] private string exchangePreference;
        [ObservableProperty] private bool isLoading;
        [ObservableProperty] private string errorMessage;
        [ObservableProperty] private bool showPriceField;
        [ObservableProperty] private bool showExchangeField;

        public ObservableCollection<Category> Categories { get; } = new();
        public ObservableCollection<string> ImagePaths { get; } = new();
        public List<ProductCondition> Conditions => Enum.GetValues(typeof(ProductCondition)).Cast<ProductCondition>().ToList();
        public List<ProductType> ProductTypes => Enum.GetValues(typeof(ProductType)).Cast<ProductType>().ToList();

        public string ProductId
        {
            get => _productId;
            set
            {
                _productId = value;
                if (!string.IsNullOrEmpty(_productId))
                {
                    LoadProductForEdit();
                }
            }
        }

        public EditProductViewModel(IProductService productService)
        {
            _productService = productService;
        }

        private async void LoadProductForEdit()
        {
            IsLoading = true;
            await LoadCategoriesAsync();
            var result = await _productService.GetProductByIdAsync(ProductId);
            if (result.Success && result.Data != null)
            {
                var product = result.Data;
                Title = product.Title;
                Description = product.Description;
                SelectedCategory = Categories.FirstOrDefault(c => c.CategoryId == product.CategoryId) ?? Categories.FirstOrDefault();
                SelectedCondition = product.Condition;
                SelectedType = product.Type;
                Price = product.Price;
                Location = product.Location;
                ExchangePreference = product.ExchangePreference;

               
  ShowPriceField = product.Type == ProductType.Satis;
  ShowExchangeField = product.Type == ProductType.Takas;


ImagePaths.Clear();
foreach (var imageUrl in product.ImageUrls)
{
    ImagePaths.Add(imageUrl);
}
}
else
{
ErrorMessage = "Düzenlenecek ürün yüklenemedi.";
}
IsLoading = false;
}

partial void OnSelectedTypeChanged(ProductType value)
{
ShowPriceField = value == ProductType.Satis;
ShowExchangeField = value == ProductType.Takas;
if (value != ProductType.Satis) Price = 0;
}

private async Task LoadCategoriesAsync()
{
var result = await _productService.GetCategoriesAsync();
if (result.Success && result.Data != null)
{
Categories.Clear();
foreach (var cat in result.Data) Categories.Add(cat);
}
}

[RelayCommand]
private async Task SaveProductAsync()
{
IsLoading = true;
var request = new ProductRequest
{
Title = Title,
Description = Description,
CategoryId = SelectedCategory?.CategoryId,
Condition = SelectedCondition,
Type = SelectedType,
Price = Price,
Location = Location,
ExchangePreference = ExchangePreference,
ImagePaths = ImagePaths.ToList()
};

var result = await _productService.UpdateProductAsync(ProductId, request);

if (result.Success)
{
await Application.Current.MainPage.DisplayAlert("Baþarýlý", "Ürün güncellendi.", "Tamam");
await Shell.Current.GoToAsync("..");
}
else
{
ErrorMessage = result.Message;
}
IsLoading = false;
}

[RelayCommand]
private async Task CancelAsync()
{
await Shell.Current.GoToAsync("..");
}

[RelayCommand]
private async Task PickImagesAsync()
{
try
{
var photos = await MediaPicker.PickPhotoAsync(new MediaPickerOptions
{
    Title = "Ürün Görseli Seçin"
});

if (photos != null)
{
    // Maksimum görsel sayýsý kontrolü
    if (ImagePaths.Count >= 5)
    {
        await Application.Current.MainPage.DisplayAlert(
            "Uyarý",
            "En fazla 5 görsel ekleyebilirsiniz",
            "Tamam"
        );
        return;
    }

    // Görseli listeye ekle
    ImagePaths.Add(photos.FullPath);
}
}
catch (Exception ex)
{
ErrorMessage = $"Görsel seçilirken hata oluþtu: {ex.Message}";
}
}

[RelayCommand]
private void RemoveImage(string imagePath)
{
if (ImagePaths.Contains(imagePath))
{
ImagePaths.Remove(imagePath);
}
}



}
}