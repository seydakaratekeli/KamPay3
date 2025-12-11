using KamPay.Models;
using KamPay.Services;
using KamPay.ViewModels;

namespace KamPay.Tests.ViewModels;

/// <summary>
/// SurpriseBoxViewModel birim testleri
/// Sürpriz kutu açma, puan yönetimi ve ödül kazanma testleri
/// </summary>
public class SurpriseBoxViewModelTests
{
    private readonly Mock<ISurpriseBoxService> _mockSurpriseBoxService;
    private readonly Mock<IAuthenticationService> _mockAuthService;
    private readonly Mock<IUserProfileService> _mockUserProfileService;
    private readonly User _testUser;

    public SurpriseBoxViewModelTests()
    {
        _mockSurpriseBoxService = new Mock<ISurpriseBoxService>();
        _mockAuthService = new Mock<IAuthenticationService>();
        _mockUserProfileService = new Mock<IUserProfileService>();

        _testUser = new User
        {
            UserId = "user123",
            Email = "test@bartin.edu.tr",
            FirstName = "Test",
            LastName = "User"
        };

        _mockAuthService
            .Setup(x => x.GetCurrentUserAsync())
            .ReturnsAsync(_testUser);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_InitializesProperties()
    {
        // Arrange & Act
        var viewModel = CreateViewModelWithPoints(150);

        // Assert
        viewModel.Should().NotBeNull();
        viewModel.UserPoints.Should().BeGreaterThan(0);
        viewModel.IsLoading.Should().BeFalse();
    }

    #endregion

    #region CanRedeem Tests

    [Fact]
    public void CanRedeem_WithSufficientPoints_ReturnsTrue()
    {
        // Arrange
        var viewModel = CreateViewModelWithPoints(150);

        // Act & Assert
        viewModel.CanRedeem.Should().BeTrue();
    }

    [Fact]
    public void CanRedeem_WithInsufficientPoints_ReturnsFalse()
    {
        // Arrange
        var viewModel = CreateViewModelWithPoints(50);

        // Act & Assert
        viewModel.CanRedeem.Should().BeFalse();
    }

    [Fact]
    public void CanRedeem_WithExactly100Points_ReturnsTrue()
    {
        // Arrange
        var viewModel = CreateViewModelWithPoints(100);

        // Act & Assert
        viewModel.CanRedeem.Should().BeTrue();
    }

    [Fact]
    public void CanRedeem_WhenLoading_ReturnsFalse()
    {
        // Arrange
        var viewModel = CreateViewModelWithPoints(150);
        viewModel.IsLoading = true;

        // Act & Assert
        viewModel.CanRedeem.Should().BeFalse();
    }

    #endregion

    #region RedeemBoxAsync Tests

    [Fact]
    public async Task RedeemBoxAsync_WithSufficientPoints_ReturnsProduct()
    {
        // Arrange
        var viewModel = CreateViewModelWithPoints(150);
        var expectedProduct = new Product
        {
            ProductId = "product123",
            Title = "Test Ürün",
            Type = ProductType.Bagis
        };

        _mockSurpriseBoxService
            .Setup(x => x.RedeemSurpriseBoxAsync(_testUser.UserId))
            .ReturnsAsync(ServiceResult<Product>.SuccessResult(expectedProduct, "Tebrikler!"));

        // Act
        await viewModel.RedeemBoxCommand.ExecuteAsync(null);

        // Assert
        viewModel.RedemptionResult.Should().NotBeNull();
        viewModel.RedemptionResult.ProductId.Should().Be("product123");
        viewModel.SuccessMessage.Should().NotBeNullOrEmpty();
        viewModel.ErrorMessage.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task RedeemBoxAsync_WithInsufficientPoints_ShowsError()
    {
        // Arrange
        var viewModel = CreateViewModelWithPoints(50);

        _mockSurpriseBoxService
            .Setup(x => x.RedeemSurpriseBoxAsync(_testUser.UserId))
            .ReturnsAsync(ServiceResult<Product>.FailureResult(
                "Yetersiz Puan!",
                "Bu iþlem için 100 puana ihtiyacýnýz var"));

        // Act
        await viewModel.RedeemBoxCommand.ExecuteAsync(null);

        // Assert
        viewModel.RedemptionResult.Should().BeNull();
        viewModel.ErrorMessage.Should().Contain("Yetersiz");
        viewModel.SuccessMessage.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task RedeemBoxAsync_WhenLoading_DoesNotExecute()
    {
        // Arrange
        var viewModel = CreateViewModelWithPoints(150);
        viewModel.IsLoading = true;

        // Act
        await viewModel.RedeemBoxCommand.ExecuteAsync(null);

        // Assert
        _mockSurpriseBoxService.Verify(
            x => x.RedeemSurpriseBoxAsync(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task RedeemBoxAsync_SetsLoadingState()
    {
        // Arrange
        var viewModel = CreateViewModelWithPoints(150);
        bool loadingWasTrue = false;

        _mockSurpriseBoxService
            .Setup(x => x.RedeemSurpriseBoxAsync(_testUser.UserId))
            .Callback(() =>
            {
                loadingWasTrue = viewModel.IsLoading;
            })
            .ReturnsAsync(ServiceResult<Product>.SuccessResult(new Product()));

        // Act
        await viewModel.RedeemBoxCommand.ExecuteAsync(null);

        // Assert
        loadingWasTrue.Should().BeTrue();
        viewModel.IsLoading.Should().BeFalse(); // After completion
    }

    [Fact]
    public async Task RedeemBoxAsync_UpdatesPointsAfterSuccess()
    {
        // Arrange
        var viewModel = CreateViewModelWithPoints(150);
        var product = new Product { ProductId = "test123", Title = "Test" };

        _mockSurpriseBoxService
            .Setup(x => x.RedeemSurpriseBoxAsync(_testUser.UserId))
            .ReturnsAsync(ServiceResult<Product>.SuccessResult(product));

        // After redemption, points should be reduced
        _mockUserProfileService
            .Setup(x => x.GetUserStatsAsync(_testUser.UserId))
            .ReturnsAsync(ServiceResult<UserStats>.SuccessResult(new UserStats
            {
                UserId = _testUser.UserId,
                Points = 50 // 150 - 100 = 50
            }));

        // Act
        await viewModel.RedeemBoxCommand.ExecuteAsync(null);

        // Assert
        viewModel.RedemptionResult.Should().NotBeNull();
        _mockUserProfileService.Verify(
            x => x.GetUserStatsAsync(_testUser.UserId),
            Times.AtLeastOnce);
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_ClearsMessages()
    {
        // Arrange
        var viewModel = CreateViewModelWithPoints(150);
        viewModel.ErrorMessage = "Test error";
        viewModel.SuccessMessage = "Test success";
        viewModel.RedemptionResult = new Product();

        // Act
        viewModel.ResetCommand.Execute(null);

        // Assert
        viewModel.ErrorMessage.Should().BeNullOrEmpty();
        viewModel.SuccessMessage.Should().BeNullOrEmpty();
        viewModel.RedemptionResult.Should().BeNull();
    }

    #endregion

    #region RefreshAsync Tests

    [Fact]
    public async Task RefreshAsync_ReloadsPoints()
    {
        // Arrange
        var viewModel = CreateViewModelWithPoints(100);
        var initialPoints = viewModel.UserPoints;

        // Update mock to return different points
        _mockUserProfileService
            .Setup(x => x.GetUserStatsAsync(_testUser.UserId))
            .ReturnsAsync(ServiceResult<UserStats>.SuccessResult(new UserStats
            {
                UserId = _testUser.UserId,
                Points = 200 // Updated points
            }));

        // Act
        await viewModel.RefreshAsync();

        // Assert
        viewModel.UserPoints.Should().Be(200);
        viewModel.UserPoints.Should().NotBe(initialPoints);
    }

    #endregion

    #region RedemptionCompleted Event Tests

    [Fact]
    public async Task RedeemBoxAsync_FiresRedemptionCompletedEvent()
    {
        // Arrange
        var viewModel = CreateViewModelWithPoints(150);
        bool eventFired = false;
        bool eventSuccess = false;

        viewModel.RedemptionCompleted += (sender, success) =>
        {
            eventFired = true;
            eventSuccess = success;
        };

        _mockSurpriseBoxService
            .Setup(x => x.RedeemSurpriseBoxAsync(_testUser.UserId))
            .ReturnsAsync(ServiceResult<Product>.SuccessResult(new Product()));

        // Act
        await viewModel.RedeemBoxCommand.ExecuteAsync(null);

        // Assert
        eventFired.Should().BeTrue();
        eventSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task RedeemBoxAsync_FiresRedemptionCompletedEventOnFailure()
    {
        // Arrange
        var viewModel = CreateViewModelWithPoints(50);
        bool eventFired = false;
        bool eventSuccess = true;

        viewModel.RedemptionCompleted += (sender, success) =>
        {
            eventFired = true;
            eventSuccess = success;
        };

        _mockSurpriseBoxService
            .Setup(x => x.RedeemSurpriseBoxAsync(_testUser.UserId))
            .ReturnsAsync(ServiceResult<Product>.FailureResult("Yetersiz puan"));

        // Act
        await viewModel.RedeemBoxCommand.ExecuteAsync(null);

        // Assert
        eventFired.Should().BeTrue();
        eventSuccess.Should().BeFalse();
    }

    #endregion

    #region HasError Tests

    [Fact]
    public void HasError_WithErrorMessage_ReturnsTrue()
    {
        // Arrange
        var viewModel = CreateViewModelWithPoints(100);
        viewModel.ErrorMessage = "Test error";

        // Act & Assert
        viewModel.HasError.Should().BeTrue();
    }

    [Fact]
    public void HasError_WithoutErrorMessage_ReturnsFalse()
    {
        // Arrange
        var viewModel = CreateViewModelWithPoints(100);
        viewModel.ErrorMessage = "";

        // Act & Assert
        viewModel.HasError.Should().BeFalse();
    }

    #endregion

    #region SurpriseBox Model Tests

    [Fact]
    public void SurpriseBox_HasRequiredProperties()
    {
        // Arrange & Act
        var box = new SurpriseBox
        {
            BoxId = "box123",
            DonorId = "donor456",
            ProductId = "product789",
            ProductTitle = "Test Ürün",
            CreatedAt = DateTime.UtcNow
        };

        // Assert
        box.BoxId.Should().Be("box123");
        box.ProductTitle.Should().Be("Test Ürün");
        box.IsOpened.Should().BeFalse();
        box.RecipientId.Should().BeNull();
        box.OpenedAt.Should().BeNull();
    }

    #endregion

    #region Helper Methods

    private SurpriseBoxViewModel CreateViewModelWithPoints(int points)
    {
        _mockUserProfileService
            .Setup(x => x.GetUserStatsAsync(_testUser.UserId))
            .ReturnsAsync(ServiceResult<UserStats>.SuccessResult(new UserStats
            {
                UserId = _testUser.UserId,
                Points = points
            }));

        return new SurpriseBoxViewModel(
            _mockSurpriseBoxService.Object,
            _mockAuthService.Object,
            _mockUserProfileService.Object
        );
    }

    #endregion
}
