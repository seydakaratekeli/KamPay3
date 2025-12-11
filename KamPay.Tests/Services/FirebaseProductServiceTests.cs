using KamPay.Helpers;
using KamPay.Models;
using KamPay.Services;

namespace KamPay.Tests.Services;

/// <summary>
/// FirebaseProductService birim testleri
/// Ürün validasyonu, fiyat kontrolleri, güvenlik testleri
/// </summary>
public class FirebaseProductServiceTests
{
    private readonly Mock<IStorageService> _mockStorageService;
    private readonly FirebaseProductService _productService;

    public FirebaseProductServiceTests()
    {
        _mockStorageService = new Mock<IStorageService>();
        _productService = new FirebaseProductService(_mockStorageService.Object);
    }

    #region ValidateProduct Tests

    [Fact]
    public void ValidateProduct_WithValidData_ReturnsSuccess()
    {
        // Arrange
        var request = new ProductRequest
        {
            Title = "MacBook Pro 2021",
            Description = "Temiz, garantili laptop",
            CategoryId = "electronics",
            Condition = ProductCondition.LikeNew,
            Type = ProductType.Satis,
            Price = 15000
        };

        // Act
        var result = _productService.ValidateProduct(request);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")] // Boþ baþlýk
    [InlineData(null)] // Null baþlýk
    [InlineData("   ")] // Sadece boþluk
    public void ValidateProduct_WithInvalidTitle_ReturnsError(string title)
    {
        // Arrange
        var request = new ProductRequest
        {
            Title = title,
            Description = "Açýklama",
            CategoryId = "test",
            Type = ProductType.Satis,
            Price = 100
        };

        // Act
        var result = _productService.ValidateProduct(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("baþlýk") || e.Contains("baþlýðý"));
    }

    [Fact]
    public void ValidateProduct_WithTooLongTitle_ReturnsError()
    {
        // Arrange
        var longTitle = new string('A', Constants.MaxProductTitleLength + 1);
        var request = new ProductRequest
        {
            Title = longTitle,
            Description = "Açýklama",
            CategoryId = "test",
            Type = ProductType.Satis,
            Price = 100
        };

        // Act
        var result = _productService.ValidateProduct(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Baþlýk") && e.Contains("karakter"));
    }

    [Theory]
    [InlineData("")] // Boþ açýklama
    [InlineData(null)] // Null açýklama
    public void ValidateProduct_WithInvalidDescription_ReturnsError(string description)
    {
        // Arrange
        var request = new ProductRequest
        {
            Title = "Test Ürün",
            Description = description,
            CategoryId = "test",
            Type = ProductType.Satis,
            Price = 100
        };

        // Act
        var result = _productService.ValidateProduct(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("açýklama"));
    }

    [Fact]
    public void ValidateProduct_WithTooLongDescription_ReturnsError()
    {
        // Arrange
        var longDesc = new string('A', Constants.MaxProductDescriptionLength + 1);
        var request = new ProductRequest
        {
            Title = "Test",
            Description = longDesc,
            CategoryId = "test",
            Type = ProductType.Satis,
            Price = 100
        };

        // Act
        var result = _productService.ValidateProduct(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Açýklama") && e.Contains("karakter"));
    }

    [Theory]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("'; DROP TABLE products--")]
    public void ValidateProduct_WithDangerousContent_ReturnsError(string maliciousContent)
    {
        // Arrange
        var request = new ProductRequest
        {
            Title = maliciousContent,
            Description = "Test",
            CategoryId = "test",
            Type = ProductType.Satis,
            Price = 100
        };

        // Act
        var result = _productService.ValidateProduct(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("geçersiz karakterler"));
    }

    [Theory]
    [InlineData(0)] // Sýfýr fiyat
    [InlineData(-100)] // Negatif fiyat
    [InlineData(1000000)] // Çok yüksek fiyat
    public void ValidateProduct_WithInvalidPrice_ReturnsError(decimal price)
    {
        // Arrange
        var request = new ProductRequest
        {
            Title = "Test Ürün",
            Description = "Açýklama",
            CategoryId = "test",
            Type = ProductType.Satis,
            Price = price
        };

        // Act
        var result = _productService.ValidateProduct(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("fiyat") || e.Contains("Fiyat"));
    }

    [Theory]
    [InlineData("")] // Boþ kategori
    [InlineData(null)] // Null kategori
    public void ValidateProduct_WithMissingCategory_ReturnsError(string categoryId)
    {
        // Arrange
        var request = new ProductRequest
        {
            Title = "Test Ürün",
            Description = "Açýklama",
            CategoryId = categoryId,
            Type = ProductType.Satis,
            Price = 100
        };

        // Act
        var result = _productService.ValidateProduct(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Kategori"));
    }

    [Fact]
    public void ValidateProduct_WithTooManyImages_ReturnsError()
    {
        // Arrange
        var tooManyImages = Enumerable.Range(0, Constants.MaxProductImages + 1)
            .Select(i => $"image{i}.jpg")
            .ToList();

        var request = new ProductRequest
        {
            Title = "Test Ürün",
            Description = "Açýklama",
            CategoryId = "test",
            Type = ProductType.Satis,
            Price = 100,
            ImagePaths = tooManyImages
        };

        // Act
        var result = _productService.ValidateProduct(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("görsel"));
    }

    #endregion
}
