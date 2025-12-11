using KamPay.Models;
using KamPay.Services;
using KamPay.ViewModels;

namespace KamPay.Tests.ViewModels;

/// <summary>
/// ServiceRequestsViewModel birim testleri
/// Hizmet talepleri, pazarlýk ve ödeme simülasyonu testleri
/// </summary>
public class ServiceRequestsViewModelTests
{
    private readonly Mock<IServiceSharingService> _mockServiceService;
    private readonly Mock<IAuthenticationService> _mockAuthService;
    private readonly Mock<IUserStateService> _mockUserStateService;
    private readonly User _testUser;

    public ServiceRequestsViewModelTests()
    {
        _mockServiceService = new Mock<IServiceSharingService>();
        _mockAuthService = new Mock<IAuthenticationService>();
        _mockUserStateService = new Mock<IUserStateService>();

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
    public void Constructor_InitializesCollections()
    {
        // Arrange & Act
        var viewModel = CreateViewModel();

        // Assert
        viewModel.Should().NotBeNull();
        viewModel.IncomingRequests.Should().NotBeNull();
        viewModel.OutgoingRequests.Should().NotBeNull();
        viewModel.PaymentMethods.Should().NotBeNull();
        viewModel.PaymentMethods.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public void Constructor_DefaultsToIncomingTab()
    {
        // Arrange & Act
        var viewModel = CreateViewModel();

        // Assert
        viewModel.IsIncomingSelected.Should().BeTrue();
        viewModel.IsOutgoingSelected.Should().BeFalse();
    }

    #endregion

    #region Tab Selection Tests

    [Fact]
    public void SelectIncoming_SwitchesToIncomingTab()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.IsOutgoingSelected = true;

        // Act
        viewModel.SelectIncomingCommand.Execute(null);

        // Assert
        viewModel.IsIncomingSelected.Should().BeTrue();
        viewModel.IsOutgoingSelected.Should().BeFalse();
    }

    [Fact]
    public void SelectOutgoing_SwitchesToOutgoingTab()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        viewModel.SelectOutgoingCommand.Execute(null);

        // Assert
        viewModel.IsIncomingSelected.Should().BeFalse();
        viewModel.IsOutgoingSelected.Should().BeTrue();
    }

    #endregion

    #region Accept/Decline Request Tests

    [Fact]
    public async Task AcceptRequestAsync_WithPendingRequest_AcceptsSuccessfully()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var request = new ServiceRequest
        {
            RequestId = "req123",
            Status = ServiceRequestStatus.Pending,
            ServiceTitle = "Test Hizmet"
        };

        _mockServiceService
            .Setup(x => x.RespondToRequestAsync("req123", true))
            .ReturnsAsync(ServiceResult<bool>.SuccessResult(true, "Talep kabul edildi"));

        // Act
        await viewModel.AcceptRequestCommand.ExecuteAsync(request);

        // Assert
        _mockServiceService.Verify(x => x.RespondToRequestAsync("req123", true), Times.Once);
    }

    [Fact]
    public async Task DeclineRequestAsync_WithPendingRequest_DeclinesSuccessfully()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var request = new ServiceRequest
        {
            RequestId = "req123",
            Status = ServiceRequestStatus.Pending,
            ServiceTitle = "Test Hizmet"
        };

        _mockServiceService
            .Setup(x => x.RespondToRequestAsync("req123", false))
            .ReturnsAsync(ServiceResult<bool>.SuccessResult(true, "Talep reddedildi"));

        // Act
        await viewModel.DeclineRequestCommand.ExecuteAsync(request);

        // Assert
        _mockServiceService.Verify(x => x.RespondToRequestAsync("req123", false), Times.Once);
    }

    [Fact]
    public async Task AcceptRequestAsync_WithNonPendingRequest_DoesNothing()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var request = new ServiceRequest
        {
            RequestId = "req123",
            Status = ServiceRequestStatus.Accepted // Already accepted
        };

        // Act
        await viewModel.AcceptRequestCommand.ExecuteAsync(request);

        // Assert
        _mockServiceService.Verify(x => x.RespondToRequestAsync(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    #endregion

    #region Complete Request Tests

    [Fact]
    public async Task CompleteRequestAsync_WithAcceptedRequest_CompletesSuccessfully()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var request = new ServiceRequest
        {
            RequestId = "req123",
            Status = ServiceRequestStatus.Accepted,
            Price = 100,
            QuotedPrice = 100
        };

        _mockServiceService
            .Setup(x => x.SimulatePaymentAndCompleteAsync(
                "req123",
                _testUser.UserId,
                It.IsAny<PaymentMethodType>(),
                null))
            .ReturnsAsync(ServiceResult<bool>.SuccessResult(true, "Ödeme tamamlandý"));

        // Act
        await viewModel.CompleteRequestCommand.ExecuteAsync(request);

        // Assert
        _mockServiceService.Verify(
            x => x.SimulatePaymentAndCompleteAsync(
                "req123",
                _testUser.UserId,
                It.IsAny<PaymentMethodType>(),
                null),
            Times.Once);
    }

    [Fact]
    public async Task CompleteRequestAsync_WithNonAcceptedRequest_DoesNothing()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var request = new ServiceRequest
        {
            RequestId = "req123",
            Status = ServiceRequestStatus.Pending // Not accepted yet
        };

        // Act
        await viewModel.CompleteRequestCommand.ExecuteAsync(request);

        // Assert
        _mockServiceService.Verify(
            x => x.SimulatePaymentAndCompleteAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<PaymentMethodType>(),
                null),
            Times.Never);
    }

    #endregion

    #region Start Conversation Tests

    [Fact]
    public async Task StartConversationAsync_WithValidRequest_StartsConversation()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var request = new ServiceRequest
        {
            RequestId = "req123",
            RequesterId = _testUser.UserId,
            ProviderId = "provider456"
        };

        _mockServiceService
            .Setup(x => x.StartConversationForRequestAsync("req123", _testUser.UserId))
            .ReturnsAsync(ServiceResult<string>.SuccessResult("conv123", "Konuþma baþlatýldý"));

        // Act
        await viewModel.StartConversationCommand.ExecuteAsync(request);

        // Assert
        _mockServiceService.Verify(
            x => x.StartConversationForRequestAsync("req123", _testUser.UserId),
            Times.Once);
    }

    #endregion

    #region ProposePrice Tests

    [Fact]
    public async Task ProposePriceAsync_AsRequester_ProposesSuccessfully()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var request = new ServiceRequest
        {
            RequestId = "req123",
            RequesterId = _testUser.UserId,
            ProviderId = "provider456",
            ServiceTitle = "Test Hizmet",
            Price = 100
        };

        _mockServiceService
            .Setup(x => x.ProposePrice("req123", It.IsAny<decimal>(), _testUser.UserId))
            .ReturnsAsync(ServiceResult<bool>.SuccessResult(true, "Teklif gönderildi"));

        // Act
        await viewModel.ProposePriceCommand.ExecuteAsync(request);

        // Assert
        _mockServiceService.Verify(
            x => x.ProposePrice("req123", It.IsAny<decimal>(), _testUser.UserId),
            Times.Once);
    }

    [Fact]
    public async Task ProposePriceAsync_AsProvider_ShowsError()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var request = new ServiceRequest
        {
            RequestId = "req123",
            RequesterId = "requester456",
            ProviderId = _testUser.UserId // Current user is provider
        };

        // Act
        await viewModel.ProposePriceCommand.ExecuteAsync(request);

        // Assert
        _mockServiceService.Verify(
            x => x.ProposePrice(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>()),
            Times.Never);
    }

    #endregion

    #region SendCounterOffer Tests

    [Fact]
    public async Task SendCounterOfferAsync_AsProvider_SendsSuccessfully()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var request = new ServiceRequest
        {
            RequestId = "req123",
            RequesterId = "requester456",
            ProviderId = _testUser.UserId, // Current user is provider
            ServiceTitle = "Test Hizmet",
            Price = 100,
            ProposedPriceByRequester = 80
        };

        _mockServiceService
            .Setup(x => x.SendCounterOfferAsync("req123", It.IsAny<decimal>(), _testUser.UserId))
            .ReturnsAsync(ServiceResult<bool>.SuccessResult(true, "Karþý teklif gönderildi"));

        // Act
        await viewModel.SendCounterOfferCommand.ExecuteAsync(request);

        // Assert
        _mockServiceService.Verify(
            x => x.SendCounterOfferAsync("req123", It.IsAny<decimal>(), _testUser.UserId),
            Times.Once);
    }

    [Fact]
    public async Task SendCounterOfferAsync_AsRequester_ShowsError()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var request = new ServiceRequest
        {
            RequestId = "req123",
            RequesterId = _testUser.UserId, // Current user is requester
            ProviderId = "provider456"
        };

        // Act
        await viewModel.SendCounterOfferCommand.ExecuteAsync(request);

        // Assert
        _mockServiceService.Verify(
            x => x.SendCounterOfferAsync(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>()),
            Times.Never);
    }

    #endregion

    #region AcceptNegotiatedPrice Tests

    [Fact]
    public async Task AcceptNegotiatedPriceAsync_WithNegotiatingRequest_AcceptsSuccessfully()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var request = new ServiceRequest
        {
            RequestId = "req123",
            RequesterId = _testUser.UserId,
            ProviderId = "provider456",
            IsNegotiating = true,
            ProposedPriceByRequester = 80,
            CounterOfferByProvider = 90,
            ServiceTitle = "Test Hizmet"
        };

        _mockServiceService
            .Setup(x => x.AcceptNegotiatedPriceAsync("req123", _testUser.UserId))
            .ReturnsAsync(ServiceResult<bool>.SuccessResult(true, "Fiyat kabul edildi"));

        // Act
        await viewModel.AcceptNegotiatedPriceCommand.ExecuteAsync(request);

        // Assert
        _mockServiceService.Verify(
            x => x.AcceptNegotiatedPriceAsync("req123", _testUser.UserId),
            Times.Once);
    }

    [Fact]
    public async Task AcceptNegotiatedPriceAsync_WithoutNegotiation_DoesNothing()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var request = new ServiceRequest
        {
            RequestId = "req123",
            IsNegotiating = false // No active negotiation
        };

        // Act
        await viewModel.AcceptNegotiatedPriceCommand.ExecuteAsync(request);

        // Assert
        _mockServiceService.Verify(
            x => x.AcceptNegotiatedPriceAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    #endregion

    #region Payment Method Tests

    [Fact]
    public void SelectedPaymentMethod_DefaultsToCardSim()
    {
        // Arrange & Act
        var viewModel = CreateViewModel();

        // Assert
        viewModel.SelectedPaymentMethod.Should().Be(PaymentMethodType.CardSim);
    }

    [Fact]
    public void SelectedPaymentMethod_CanBeChanged()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        viewModel.SelectedPaymentMethod = PaymentMethodType.BankTransferSim;

        // Assert
        viewModel.SelectedPaymentMethod.Should().Be(PaymentMethodType.BankTransferSim);
    }

    [Fact]
    public void PaymentMethods_ContainsExpectedOptions()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        var methods = viewModel.PaymentMethods;

        // Assert
        methods.Should().Contain(p => p.Method == PaymentMethodType.CardSim);
        methods.Should().Contain(p => p.Method == PaymentMethodType.BankTransferSim);
    }

    #endregion

    #region ServiceRequest Model Tests

    [Fact]
    public void ServiceRequest_HasRequiredProperties()
    {
        // Arrange & Act
        var request = new ServiceRequest
        {
            RequestId = "req123",
            ServiceId = "service123",
            ServiceTitle = "Test Hizmet",
            RequesterId = "requester456",
            ProviderId = "provider789",
            Price = 100,
            Status = ServiceRequestStatus.Pending,
            RequestedAt = DateTime.UtcNow
        };

        // Assert
        request.RequestId.Should().Be("req123");
        request.ServiceTitle.Should().Be("Test Hizmet");
        request.Status.Should().Be(ServiceRequestStatus.Pending);
        request.Price.Should().Be(100);
    }

    [Fact]
    public void ServiceRequest_NegotiationProperties_AreOptional()
    {
        // Arrange & Act
        var request = new ServiceRequest();

        // Assert
        request.IsNegotiating.Should().BeFalse();
        request.ProposedPriceByRequester.Should().BeNull();
        request.CounterOfferByProvider.Should().BeNull();
        request.QuotedPrice.Should().BeNull();
    }

    #endregion

    #region Refresh Tests

    [Fact]
    public async Task RefreshRequestsAsync_ReloadsRequests()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        await viewModel.RefreshRequestsCommand.ExecuteAsync(null);

        // Assert
        viewModel.IsRefreshing.Should().BeFalse();
    }

    #endregion

    #region Helper Methods

    private ServiceRequestsViewModel CreateViewModel()
    {
        return new ServiceRequestsViewModel(
            _mockServiceService.Object,
            _mockAuthService.Object,
            _mockUserStateService.Object
        );
    }

    #endregion
}
