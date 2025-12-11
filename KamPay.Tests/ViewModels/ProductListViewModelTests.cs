using KamPay.Models;
using KamPay.Services;
using KamPay.ViewModels;

namespace KamPay.Tests.ViewModels;

/// <summary>
/// ProductListViewModel birim testleri
/// Ürün listeleme, filtreleme ve sýralama testleri
/// </summary>
public class ProductListViewModelTests
{
    private readonly Mock<IProductService> _mockProductService;
    private readonly Mock<IAuthenticationService> _mockAuthService;
    private readonly Mock<ICategoryService> _mockCategoryService;
    private readonly Mock<IUserStateService> _mockUserStateService;
    private readonly ProductListViewModel _viewModel;

    public ProductListViewModelTests()
    {
        _mockProductService = new Mock<IProductService>();
        _mockAuthService = new Mock<IAuthenticationService>();
        _mockCategoryService = new Mock<ICategoryService>();
        _mockUserStateService = new Mock<IUserStateService>();

        _viewModel = new ProductListViewModel(
            _mockProductService.Object,
            _mockAuthService.Object,
            _mockCategoryService.Object,
            _mockUserStateService.Object
        );
    }

    [Fact]
    public void Constructor_InitializesProperties()
    {
        // Assert
        _viewModel.Products.Should().NotBeNull();
        _viewModel.Categories.Should().NotBeNull();
        _viewModel.SelectedSortOption.Should().Be(ProductSortOption.Newest);
        _viewModel.IsLoading.Should().BeTrue(); // Baþlangýçta loading
    }

    [Fact]
    public void SearchText_WhenChanged_UpdatesProperty()
    {
        // Arrange
        var searchText = "Test Ürün";

        // Act
        _viewModel.SearchText = searchText;

        // Assert
        _viewModel.SearchText.Should().Be(searchText);
    }

    [Fact]
    public void ToggleFilterPanel_ChangesVisibility()
    {
        // Arrange
        var initialState = _viewModel.ShowFilterPanel;

        // Act
        _viewModel.ToggleFilterPanelCommand.Execute(null);

        // Assert
        _viewModel.ShowFilterPanel.Should().Be(!initialState);
    }

    [Fact]
    public void ClearFilters_ResetsAllFilters()
    {
        // Arrange
        _viewModel.SearchText = "Test";
        _viewModel.SelectedType = ProductType.Satis;

        // Act
        _viewModel.ClearFiltersCommand.Execute(null);

        // Assert
        _viewModel.SearchText.Should().BeEmpty();
        _viewModel.SelectedType.Should().BeNull();
        _viewModel.SelectedSortOption.Should().Be(ProductSortOption.Newest);
    }

    [Theory]
    [InlineData(ProductSortOption.Newest)]
    [InlineData(ProductSortOption.Oldest)]
    [InlineData(ProductSortOption.PriceAsc)]
    [InlineData(ProductSortOption.PriceDesc)]
    [InlineData(ProductSortOption.MostViewed)]
    [InlineData(ProductSortOption.MostFavorited)]
    public void SelectedSortOption_UpdatesCorrectly(ProductSortOption sortOption)
    {
        // Act
        _viewModel.SelectedSortOption = sortOption;

        // Assert
        _viewModel.SelectedSortOption.Should().Be(sortOption);
    }

    [Fact]
    public void SetProductType_WithValidType_UpdatesSelectedType()
    {
        // Act
        _viewModel.SetProductTypeCommand.Execute("Satis");

        // Assert
        _viewModel.SelectedType.Should().Be(ProductType.Satis);
    }

    [Fact]
    public void SetProductType_WithNull_ClearsSelectedType()
    {
        // Arrange
        _viewModel.SelectedType = ProductType.Takas;

        // Act
        _viewModel.SetProductTypeCommand.Execute(null);

        // Assert
        _viewModel.SelectedType.Should().BeNull();
    }

    [Fact]
    public void SetProductType_WithInvalidType_DoesNotUpdateSelectedType()
    {
        // Arrange
        var initialType = _viewModel.SelectedType;

        // Act
        _viewModel.SetProductTypeCommand.Execute("InvalidType");

        // Assert
        _viewModel.SelectedType.Should().Be(initialType);
    }

    [Theory]
    [InlineData(ProductType.Satis)]
    [InlineData(ProductType.Takas)]
    [InlineData(ProductType.Bagis)]
    public void SelectedType_UpdatesCorrectly(ProductType productType)
    {
        // Act
        _viewModel.SelectedType = productType;

        // Assert
        _viewModel.SelectedType.Should().Be(productType);
    }

    [Fact]
    public void SelectedCategory_UpdatesCorrectly()
    {
        // Arrange
        var category = new Category
        {
            CategoryId = "electronics",
            Name = "Elektronik"
        };

        // Act
        _viewModel.SelectedCategory = category;

        // Assert
        _viewModel.SelectedCategory.Should().Be(category);
    }

    [Fact]
    public void ApplyFilters_ClosesFilterPanel()
    {
        // Arrange
        _viewModel.ShowFilterPanel = true;

        // Act
        _viewModel.ApplyFiltersCommand.Execute(null);

        // Assert
        _viewModel.ShowFilterPanel.Should().BeFalse();
    }

    [Fact]
    public void HasUnreadNotifications_UpdatesCorrectly()
    {
        // Act
        _viewModel.HasUnreadNotifications = true;

        // Assert
        _viewModel.HasUnreadNotifications.Should().BeTrue();

        // Act
        _viewModel.HasUnreadNotifications = false;

        // Assert
        _viewModel.HasUnreadNotifications.Should().BeFalse();
    }
}
