using KamPay.Models;
using KamPay.Services;
using KamPay.ViewModels;

namespace KamPay.Tests.ViewModels;

/// <summary>
/// ServiceSharingViewModel birim testleri
/// Hizmet paylaþýmý, mesajlaþma ve filtreleme testleri
/// </summary>
public class ServiceSharingViewModelTests
{
    private readonly Mock<IServiceSharingService> _mockServiceService;
    private readonly Mock<IAuthenticationService> _mockAuthService;
    private readonly Mock<IUserProfileService> _mockUserProfileService;
    private readonly Mock<IUserStateService> _mockUserStateService;
    private readonly Mock<IMessagingService> _mockMessagingService;
    private readonly User _testUser;

    public ServiceSharingViewModelTests()
    {
        _mockServiceService = new Mock<IServiceSharingService>();
        _mockAuthService = new Mock<IAuthenticationService>();
        _mockUserProfileService = new Mock<IUserProfileService>();
        _mockUserStateService = new Mock<IUserStateService>();
        _mockMessagingService = new Mock<IMessagingService>();

        _testUser = new User
        {
            UserId = "user123",
            Email = "test@bartin.edu.tr",
            FirstName = "Test",
            LastName = "User",
            ProfileImageUrl = "https://example.com/profile.jpg"
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
        var viewModel = CreateViewModel();

        // Assert
        viewModel.Should().NotBeNull();
        viewModel.Services.Should().NotBeNull();
        viewModel.FilteredServices.Should().NotBeNull();
        viewModel.Categories.Should().NotBeNull();
        viewModel.Categories.Should().HaveCountGreaterThan(0);
    }

    #endregion

    #region Filter Tests

    [Fact]
    public void ApplyFilter_WithSearchText_FiltersServices()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.Services.Add(new ServiceOffer { Title = "Matematik Dersi", Description = "Ýlkokul" });
        viewModel.Services.Add(new ServiceOffer { Title = "Ýngilizce", Description = "Lise" });

        // Act
        viewModel.SearchText = "Matematik";
        viewModel.ApplyFilterCommand.Execute(null);

        // Assert
        viewModel.FilteredServices.Should().HaveCount(1);
        viewModel.FilteredServices.First().Title.Should().Contain("Matematik");
    }

    [Fact]
    public void ApplyFilter_WithCategory_FiltersCorrectly()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.Services.Add(new ServiceOffer { Title = "Ders 1", Category = ServiceCategory.Egitim });
        viewModel.Services.Add(new ServiceOffer { Title = "Tamir 1", Category = ServiceCategory.Teknik });

        // Act
        viewModel.FilterCategory = ServiceCategory.Egitim;
        viewModel.ApplyFilterCommand.Execute(null);

        // Assert
        viewModel.FilteredServices.Should().HaveCount(1);
        viewModel.FilteredServices.First().Category.Should().Be(ServiceCategory.Egitim);
    }

    [Fact]
    public void ClearCategoryFilter_ResetsFilter()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.FilterCategory = ServiceCategory.Egitim;

        // Act
        viewModel.ClearCategoryFilterCommand.Execute(null);

        // Assert
        viewModel.FilterCategory.Should().BeNull();
    }

    #endregion

    #region Form Management Tests

    [Fact]
    public void OpenPostForm_ShowsForm()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        viewModel.OpenPostFormCommand.Execute(null);

        // Assert
        viewModel.IsPostFormVisible.Should().BeTrue();
    }

    [Fact]
    public void ClosePostForm_HidesForm()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.IsPostFormVisible = true;

        // Act
        viewModel.ClosePostFormCommand.Execute(null);

        // Assert
        viewModel.IsPostFormVisible.Should().BeFalse();
    }

    #endregion

    #region Create Service Tests

    [Fact]
    public async Task CreateServiceAsync_WithValidData_CreatesService()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.ServiceTitle = "Test Hizmet";
        viewModel.ServiceDescription = "Test açýklama";
        viewModel.ServicePrice = 100;
        viewModel.SelectedCategory = ServiceCategory.Egitim;

        _mockServiceService
            .Setup(x => x.CreateServiceOfferAsync(It.IsAny<ServiceOffer>()))
            .ReturnsAsync(ServiceResult<ServiceOffer>.SuccessResult(new ServiceOffer()));

        // Act
        await viewModel.CreateServiceCommand.ExecuteAsync(null);

        // Assert
        _mockServiceService.Verify(x => x.CreateServiceOfferAsync(It.IsAny<ServiceOffer>()), Times.Once);
        viewModel.ServiceTitle.Should().BeEmpty();
        viewModel.ServicePrice.Should().Be(0);
    }

    [Fact]
    public async Task CreateServiceAsync_WithEmptyTitle_ShowsError()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.ServiceTitle = "";
        viewModel.ServiceDescription = "Test";
        viewModel.ServicePrice = 100;

        // Act
        await viewModel.CreateServiceCommand.ExecuteAsync(null);

        // Assert
        _mockServiceService.Verify(x => x.CreateServiceOfferAsync(It.IsAny<ServiceOffer>()), Times.Never);
    }

    [Fact]
    public async Task CreateServiceAsync_WithInvalidPrice_ShowsError()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.ServiceTitle = "Test";
        viewModel.ServiceDescription = "Test açýklama";
        viewModel.ServicePrice = 0; // Invalid

        // Act
        await viewModel.CreateServiceCommand.ExecuteAsync(null);

        // Assert
        _mockServiceService.Verify(x => x.CreateServiceOfferAsync(It.IsAny<ServiceOffer>()), Times.Never);
    }

    #endregion

    #region Request Service Tests

    [Fact]
    public async Task RequestServiceAsync_WithValidOffer_SendsRequest()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var offer = new ServiceOffer
        {
            ServiceId = "service123",
            ProviderId = "provider456",
            Title = "Test Hizmet",
            Price = 100
        };

        _mockServiceService
            .Setup(x => x.RequestServiceAsync(It.IsAny<ServiceOffer>(), It.IsAny<User>(), It.IsAny<string>()))
            .ReturnsAsync(ServiceResult<ServiceRequest>.SuccessResult(new ServiceRequest(), "Talep gönderildi"));

        // Act
        await viewModel.RequestServiceCommand.ExecuteAsync(offer);

        // Assert
        _mockServiceService.Verify(
            x => x.RequestServiceAsync(It.Is<ServiceOffer>(o => o.ServiceId == "service123"), 
                It.IsAny<User>(), 
                It.IsAny<string>()), 
            Times.Once);
    }

    [Fact]
    public async Task RequestServiceAsync_WithOwnService_ShowsError()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var offer = new ServiceOffer
        {
            ServiceId = "service123",
            ProviderId = _testUser.UserId, // Own service
            Title = "Test Hizmet"
        };

        // Act
        await viewModel.RequestServiceCommand.ExecuteAsync(offer);

        // Assert
        _mockServiceService.Verify(x => x.RequestServiceAsync(It.IsAny<ServiceOffer>(), It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }

    #endregion

    #region MessageProvider Tests

    [Fact]
    public async Task MessageProviderAsync_WithValidOffer_OpensChat()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var offer = new ServiceOffer
        {
            ServiceId = "service123",
            ProviderId = "provider456",
            Title = "Test Hizmet"
        };

        var conversation = new Conversation
        {
            ConversationId = "conv123",
            User1Id = _testUser.UserId,
            User2Id = "provider456"
        };

        _mockMessagingService
            .Setup(x => x.GetOrCreateConversationAsync(
                _testUser.UserId,
                "provider456",
                "service123"))
            .ReturnsAsync(ServiceResult<Conversation>.SuccessResult(conversation));

        // Act
        await viewModel.MessageProviderCommand.ExecuteAsync(offer);

        // Assert
        _mockMessagingService.Verify(
            x => x.GetOrCreateConversationAsync(
                _testUser.UserId,
                "provider456",
                "service123"),
            Times.Once);
    }

    [Fact]
    public async Task MessageProviderAsync_WithOwnService_ShowsError()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var offer = new ServiceOffer
        {
            ServiceId = "service123",
            ProviderId = _testUser.UserId, // Own service
            Title = "Test Hizmet"
        };

        // Act
        await viewModel.MessageProviderCommand.ExecuteAsync(offer);

        // Assert
        _mockMessagingService.Verify(
            x => x.GetOrCreateConversationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    #endregion

    #region TimeCredits Tests

    [Fact]
    public void IncrementTimeCredits_IncreasesValue()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.TimeCredits = 5;

        // Act
        viewModel.IncrementTimeCreditsCommand.Execute(null);

        // Assert
        viewModel.TimeCredits.Should().Be(6);
    }

    [Fact]
    public void IncrementTimeCredits_DoesNotExceedMaximum()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.TimeCredits = 10; // Maximum

        // Act
        viewModel.IncrementTimeCreditsCommand.Execute(null);

        // Assert
        viewModel.TimeCredits.Should().Be(10);
    }

    [Fact]
    public void DecrementTimeCredits_DecreasesValue()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.TimeCredits = 5;

        // Act
        viewModel.DecrementTimeCreditsCommand.Execute(null);

        // Assert
        viewModel.TimeCredits.Should().Be(4);
    }

    [Fact]
    public void DecrementTimeCredits_DoesNotGoBelowMinimum()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.TimeCredits = 1; // Minimum

        // Act
        viewModel.DecrementTimeCreditsCommand.Execute(null);

        // Assert
        viewModel.TimeCredits.Should().Be(1);
    }

    #endregion

    #region Price Sort Tests

    [Fact]
    public void PriceSortOptions_ContainsAllOptions()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        var options = viewModel.PriceSortOptions;

        // Assert
        options.Should().NotBeNull();
        options.Should().HaveCount(3); // All, Ascending, Descending
    }

    #endregion

    #region ServiceOffer Model Tests

    [Fact]
    public void ServiceOffer_HasRequiredProperties()
    {
        // Arrange & Act
        var offer = new ServiceOffer
        {
            ServiceId = "service123",
            ProviderId = "provider456",
            Title = "Test Hizmet",
            Description = "Test açýklama",
            Category = ServiceCategory.Egitim,
            Price = 100,
            TimeCredits = 2,
            IsAvailable = true
        };

        // Assert
        offer.ServiceId.Should().Be("service123");
        offer.Title.Should().Be("Test Hizmet");
        offer.Category.Should().Be(ServiceCategory.Egitim);
        offer.Price.Should().Be(100);
        offer.IsAvailable.Should().BeTrue();
    }

    #endregion

    #region Helper Methods

    private ServiceSharingViewModel CreateViewModel()
    {
        return new ServiceSharingViewModel(
            _mockServiceService.Object,
            _mockAuthService.Object,
            _mockUserProfileService.Object,
            _mockUserStateService.Object,
            _mockMessagingService.Object
        );
    }

    #endregion
}
