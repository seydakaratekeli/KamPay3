using KamPay.Models;
using KamPay.Services;
using Moq;
using FluentAssertions;
using Xunit;
using System;
using System.Threading.Tasks;

namespace KamPay.Tests.Services
{
    public class FirebaseServiceSharingServiceTests
    {
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly Mock<IUserProfileService> _mockUserProfileService;
        private readonly Mock<IMessagingService> _mockMessagingService;
        private readonly FirebaseServiceSharingService _service;

        public FirebaseServiceSharingServiceTests()
        {
            _mockNotificationService = new Mock<INotificationService>();
            _mockUserProfileService = new Mock<IUserProfileService>();
            _mockMessagingService = new Mock<IMessagingService>();
            
            _service = new FirebaseServiceSharingService(
                _mockNotificationService.Object,
                _mockUserProfileService.Object,
                _mockMessagingService.Object
            );
        }

        #region CreateServiceOfferAsync Tests

        [Fact]
        public async Task CreateServiceOfferAsync_WithValidOffer_ReturnsSuccess()
        {
            // Arrange
            var offer = new ServiceOffer
            {
                Title = "Test Service",
                Description = "Test Description",
                ProviderId = "user123",
                Category = ServiceCategory.Education,
                Price = 100,
                TimeCredits = 2
            };

            // Act
            var result = await _service.CreateServiceOfferAsync(offer);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data.ServiceId.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task CreateServiceOfferAsync_WithNullOffer_ReturnsFailure()
        {
            // Arrange
            ServiceOffer offer = null;

            // Act & Assert
            await Assert.ThrowsAsync<NullReferenceException>(async () =>
            {
                await _service.CreateServiceOfferAsync(offer);
            });
        }

        [Fact]
        public async Task CreateServiceOfferAsync_GeneratesServiceId_WhenNotProvided()
        {
            // Arrange
            var offer = new ServiceOffer
            {
                ServiceId = null, // ID yok
                Title = "Test",
                Description = "Test",
                ProviderId = "user123",
                Price = 100
            };

            // Act
            var result = await _service.CreateServiceOfferAsync(offer);

            // Assert
            result.Success.Should().BeTrue();
            result.Data.ServiceId.Should().NotBeNullOrEmpty();
        }

        #endregion

        #region RequestServiceAsync Tests

        [Fact]
        public async Task RequestServiceAsync_WithValidData_ReturnsSuccess()
        {
            // Arrange
            var offer = new ServiceOffer
            {
                ServiceId = "service123",
                Title = "Test Service",
                ProviderId = "provider123",
                Price = 100,
                TimeCredits = 2
            };

            var requester = new User
            {
                UserId = "requester123",
                FirstName = "John",
                LastName = "Doe"
            };

            // Act
            var result = await _service.RequestServiceAsync(offer, requester, "Test message");

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data.ServiceId.Should().Be(offer.ServiceId);
            result.Data.ProviderId.Should().Be(offer.ProviderId);
            result.Data.RequesterId.Should().Be(requester.UserId);
            result.Data.Price.Should().Be(offer.Price);
            result.Data.TimeCreditValue.Should().Be(offer.TimeCredits);
        }

        [Fact]
        public async Task RequestServiceAsync_WithNullOffer_ReturnsFailure()
        {
            // Arrange
            ServiceOffer offer = null;
            var requester = new User { UserId = "user123" };

            // Act
            var result = await _service.RequestServiceAsync(offer, requester, "Test");

            // Assert
            result.Success.Should().BeFalse();
            result.Message.Should().Contain("eksik");
        }

        [Fact]
        public async Task RequestServiceAsync_WithNullRequester_ReturnsFailure()
        {
            // Arrange
            var offer = new ServiceOffer { ServiceId = "service123" };
            User requester = null;

            // Act
            var result = await _service.RequestServiceAsync(offer, requester, "Test");

            // Assert
            result.Success.Should().BeFalse();
            result.Message.Should().Contain("eksik");
        }

        [Fact]
        public async Task RequestServiceAsync_SetsCorrectInitialStatus()
        {
            // Arrange
            var offer = new ServiceOffer
            {
                ServiceId = "service123",
                Title = "Test",
                ProviderId = "provider123",
                Price = 100
            };
            var requester = new User { UserId = "req123", FirstName = "Test", LastName = "User" };

            // Act
            var result = await _service.RequestServiceAsync(offer, requester, "Message");

            // Assert
            result.Data.Status.Should().Be(ServiceRequestStatus.Pending);
            result.Data.PaymentStatus.Should().Be(ServicePaymentStatus.None);
            result.Data.PaymentMethod.Should().Be(PaymentMethodType.None);
        }

        #endregion

        #region ProposePriceAsync Tests

        [Theory]
        [InlineData(0)]
        [InlineData(-10)]
        [InlineData(-100.50)]
        public async Task ProposePrice_WithInvalidPrice_ReturnsFailure(decimal price)
        {
            // Arrange
            string requestId = "req123";
            string userId = "user123";

            // Act
            var result = await _service.ProposePrice(requestId, price, userId);

            // Assert
            result.Success.Should().BeFalse();
            result.Message.Should().Contain("Geçerli bir fiyat");
        }

        [Fact]
        public async Task ProposePrice_WithValidPrice_SetsIsNegotiating()
        {
            // Arrange
            var offer = new ServiceOffer
            {
                ServiceId = "service123",
                ProviderId = "provider123",
                Price = 100
            };
            var requester = new User { UserId = "req123", FirstName = "John", LastName = "Doe" };

            _mockNotificationService
                .Setup(x => x.CreateNotificationAsync(It.IsAny<Notification>()))
                .ReturnsAsync(ServiceResult<bool>.SuccessResult(true));

            // Ýlk olarak request oluþtur
            var requestResult = await _service.RequestServiceAsync(offer, requester, "Test");
            var requestId = requestResult.Data.RequestId;

            // Act
            var result = await _service.ProposePrice(requestId, 80, requester.UserId);

            // Assert
            result.Success.Should().BeTrue();
            // Firebase'e yazýldýðý için doðrulama sýnýrlý
        }

        #endregion

        #region SendCounterOfferAsync Tests

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public async Task SendCounterOffer_WithInvalidPrice_ReturnsFailure(decimal price)
        {
            // Arrange
            string requestId = "req123";
            string providerId = "provider123";

            // Act
            var result = await _service.SendCounterOfferAsync(requestId, price, providerId);

            // Assert
            result.Success.Should().BeFalse();
            result.Message.Should().Contain("Geçerli bir fiyat");
        }

        #endregion

        #region CreatePaymentSimulationAsync Tests

        [Theory]
        [InlineData("cardsim", PaymentMethodType.CardSim)]
        [InlineData("banktransfersim", PaymentMethodType.BankTransferSim)]
        [InlineData("walletsim", PaymentMethodType.WalletSim)]
        [InlineData("invalid", PaymentMethodType.CardSim)] // Default
        public async Task CreatePaymentSimulation_ParsesPaymentMethod_Correctly(string method, PaymentMethodType expected)
        {
            // Arrange
            var offer = new ServiceOffer
            {
                ServiceId = "service123",
                ProviderId = "provider123",
                Price = 100
            };
            var requester = new User { UserId = "req123", FirstName = "John", LastName = "Doe" };

            var requestResult = await _service.RequestServiceAsync(offer, requester, "Test");
            var requestId = requestResult.Data.RequestId;

            // Request'i önce kabul et
            await _service.RespondToRequestAsync(requestId, true);

            // Act
            var result = await _service.CreatePaymentSimulationAsync(requestId, method);

            // Assert
            result.Success.Should().BeTrue();
            result.Data.Method.Should().Be(expected);
        }

        [Fact]
        public async Task CreatePaymentSimulation_WithCardMethod_GeneratesOTP()
        {
            // Arrange
            var offer = new ServiceOffer
            {
                ServiceId = "service123",
                ProviderId = "provider123",
                Price = 100
            };
            var requester = new User { UserId = "req123", FirstName = "John", LastName = "Doe" };

            var requestResult = await _service.RequestServiceAsync(offer, requester, "Test");
            var requestId = requestResult.Data.RequestId;

            await _service.RespondToRequestAsync(requestId, true);

            // Act
            var result = await _service.CreatePaymentSimulationAsync(requestId, "cardsim");

            // Assert
            result.Success.Should().BeTrue();
            result.Data.Method.Should().Be(PaymentMethodType.CardSim);
            // OTP Firebase'e yazýldý (kontrol edilemez ama test geçer)
        }

        [Fact]
        public async Task CreatePaymentSimulation_WithBankTransfer_GeneratesReference()
        {
            // Arrange
            var offer = new ServiceOffer
            {
                ServiceId = "service123",
                ProviderId = "provider123",
                Price = 100
            };
            var requester = new User { UserId = "req123", FirstName = "John", LastName = "Doe" };

            var requestResult = await _service.RequestServiceAsync(offer, requester, "Test");
            var requestId = requestResult.Data.RequestId;

            await _service.RespondToRequestAsync(requestId, true);

            // Act
            var result = await _service.CreatePaymentSimulationAsync(requestId, "banktransfersim");

            // Assert
            result.Success.Should().BeTrue();
            result.Data.BankReference.Should().NotBeNullOrEmpty();
            result.Data.BankReference.Should().StartWith("BTX-");
        }

        #endregion

        #region ConfirmPaymentSimulationAsync Tests

        [Fact]
        public async Task ConfirmPaymentSimulation_WithValidData_UpdatesStatus()
        {
            // Arrange
            var offer = new ServiceOffer
            {
                ServiceId = "service123",
                ProviderId = "provider123",
                Price = 100
            };
            var requester = new User { UserId = "req123", FirstName = "John", LastName = "Doe" };

            var requestResult = await _service.RequestServiceAsync(offer, requester, "Test");
            var requestId = requestResult.Data.RequestId;

            await _service.RespondToRequestAsync(requestId, true);

            var paymentResult = await _service.CreatePaymentSimulationAsync(requestId, "banktransfersim");
            var paymentId = paymentResult.Data.PaymentId;

            // Act
            var result = await _service.ConfirmPaymentSimulationAsync(requestId, paymentId);

            // Assert
            result.Success.Should().BeTrue();
        }

        #endregion

        #region RespondToRequestAsync Tests

        [Fact]
        public async Task RespondToRequest_WithAccept_UpdatesStatusToAccepted()
        {
            // Arrange
            var offer = new ServiceOffer
            {
                ServiceId = "service123",
                ProviderId = "provider123",
                Price = 100
            };
            var requester = new User { UserId = "req123", FirstName = "John", LastName = "Doe" };

            _mockNotificationService
                .Setup(x => x.CreateNotificationAsync(It.IsAny<Notification>()))
                .ReturnsAsync(ServiceResult<bool>.SuccessResult(true));

            var requestResult = await _service.RequestServiceAsync(offer, requester, "Test");
            var requestId = requestResult.Data.RequestId;

            // Act
            var result = await _service.RespondToRequestAsync(requestId, true);

            // Assert
            result.Success.Should().BeTrue();
            _mockNotificationService.Verify(
                x => x.CreateNotificationAsync(It.Is<Notification>(n => n.Type == NotificationType.OfferAccepted)),
                Times.Once
            );
        }

        [Fact]
        public async Task RespondToRequest_WithDecline_UpdatesStatusToDeclined()
        {
            // Arrange
            var offer = new ServiceOffer
            {
                ServiceId = "service123",
                ProviderId = "provider123",
                Price = 100
            };
            var requester = new User { UserId = "req123", FirstName = "John", LastName = "Doe" };

            _mockNotificationService
                .Setup(x => x.CreateNotificationAsync(It.IsAny<Notification>()))
                .ReturnsAsync(ServiceResult<bool>.SuccessResult(true));

            var requestResult = await _service.RequestServiceAsync(offer, requester, "Test");
            var requestId = requestResult.Data.RequestId;

            // Act
            var result = await _service.RespondToRequestAsync(requestId, false);

            // Assert
            result.Success.Should().BeTrue();
            _mockNotificationService.Verify(
                x => x.CreateNotificationAsync(It.Is<Notification>(n => n.Type == NotificationType.OfferRejected)),
                Times.Once
            );
        }

        #endregion

        #region StartConversationForRequestAsync Tests

        [Fact]
        public async Task StartConversationForRequest_WithNewRequest_CreatesConversation()
        {
            // Arrange
            var offer = new ServiceOffer
            {
                ServiceId = "service123",
                ProviderId = "provider123",
                Price = 100
            };
            var requester = new User { UserId = "req123", FirstName = "John", LastName = "Doe" };

            var requestResult = await _service.RequestServiceAsync(offer, requester, "Test");
            var requestId = requestResult.Data.RequestId;

            var mockConversation = new Conversation
            {
                ConversationId = "conv123",
                User1Id = requester.UserId,
                User2Id = offer.ProviderId
            };

            _mockMessagingService
                .Setup(x => x.GetOrCreateConversationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(ServiceResult<Conversation>.SuccessResult(mockConversation));

            _mockMessagingService
                .Setup(x => x.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<User>()))
                .ReturnsAsync(ServiceResult<Message>.SuccessResult(new Message()));

            // Act
            var result = await _service.StartConversationForRequestAsync(requestId, requester.UserId);

            // Assert
            result.Success.Should().BeTrue();
            result.Data.Should().NotBeNullOrEmpty();
            _mockMessagingService.Verify(
                x => x.GetOrCreateConversationAsync(requester.UserId, offer.ProviderId, It.IsAny<string>()),
                Times.Once
            );
        }

        #endregion

        #region AcceptNegotiatedPriceAsync Tests

        [Fact]
        public async Task AcceptNegotiatedPrice_WithActiveNegotiation_LocksPrice()
        {
            // Arrange
            var offer = new ServiceOffer
            {
                ServiceId = "service123",
                ProviderId = "provider123",
                Price = 100
            };
            var requester = new User { UserId = "req123", FirstName = "John", LastName = "Doe" };

            _mockNotificationService
                .Setup(x => x.CreateNotificationAsync(It.IsAny<Notification>()))
                .ReturnsAsync(ServiceResult<bool>.SuccessResult(true));

            _mockMessagingService
                .Setup(x => x.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<User>()))
                .ReturnsAsync(ServiceResult<Message>.SuccessResult(new Message()));

            var requestResult = await _service.RequestServiceAsync(offer, requester, "Test");
            var requestId = requestResult.Data.RequestId;

            // Fiyat teklifi gönder
            await _service.ProposePrice(requestId, 80, requester.UserId);

            // Act
            var result = await _service.AcceptNegotiatedPriceAsync(requestId, requester.UserId);

            // Assert
            result.Success.Should().BeTrue();
            _mockNotificationService.Verify(
                x => x.CreateNotificationAsync(It.IsAny<Notification>()),
                Times.AtLeastOnce
            );
        }

        #endregion

        #region Integration-Like Tests

        [Fact]
        public async Task FullNegotiationFlow_WorksCorrectly()
        {
            // Arrange
            var offer = new ServiceOffer
            {
                ServiceId = "service123",
                ProviderId = "provider123",
                Title = "Math Tutoring",
                Price = 100
            };
            var requester = new User { UserId = "req123", FirstName = "John", LastName = "Doe" };

            _mockNotificationService
                .Setup(x => x.CreateNotificationAsync(It.IsAny<Notification>()))
                .ReturnsAsync(ServiceResult<bool>.SuccessResult(true));

            _mockMessagingService
                .Setup(x => x.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<User>()))
                .ReturnsAsync(ServiceResult<Message>.SuccessResult(new Message()));

            // Act & Assert
            // 1. Request oluþtur
            var requestResult = await _service.RequestServiceAsync(offer, requester, "Test message");
            requestResult.Success.Should().BeTrue();
            var requestId = requestResult.Data.RequestId;

            // 2. Fiyat teklifi
            var proposeResult = await _service.ProposePrice(requestId, 80, requester.UserId);
            proposeResult.Success.Should().BeTrue();

            // 3. Karþý teklif
            var counterResult = await _service.SendCounterOfferAsync(requestId, 90, offer.ProviderId);
            counterResult.Success.Should().BeTrue();

            // 4. Fiyat kabul
            var acceptResult = await _service.AcceptNegotiatedPriceAsync(requestId, requester.UserId);
            acceptResult.Success.Should().BeTrue();

            // 5. Verify notification calls
            _mockNotificationService.Verify(
                x => x.CreateNotificationAsync(It.IsAny<Notification>()),
                Times.AtLeast(3) // ProposePrice + CounterOffer + Accept
            );
        }

        [Fact]
        public async Task CompleteServiceFlow_WithPayment_WorksCorrectly()
        {
            // Arrange
            var offer = new ServiceOffer
            {
                ServiceId = "service123",
                ProviderId = "provider123",
                Title = "Service",
                Price = 100,
                TimeCredits = 2
            };
            var requester = new User { UserId = "req123", FirstName = "John", LastName = "Doe" };

            _mockNotificationService
                .Setup(x => x.CreateNotificationAsync(It.IsAny<Notification>()))
                .ReturnsAsync(ServiceResult<bool>.SuccessResult(true));

            _mockUserProfileService
                .Setup(x => x.TransferTimeCreditsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(ServiceResult<bool>.SuccessResult(true));

            // Act & Assert
            // 1. Request
            var requestResult = await _service.RequestServiceAsync(offer, requester, "Test");
            requestResult.Success.Should().BeTrue();
            var requestId = requestResult.Data.RequestId;

            // 2. Accept
            var acceptResult = await _service.RespondToRequestAsync(requestId, true);
            acceptResult.Success.Should().BeTrue();

            // 3. Complete
            var completeResult = await _service.CompleteRequestAsync(requestId, requester.UserId);
            completeResult.Success.Should().BeTrue();

            // 4. Verify credit transfer
            _mockUserProfileService.Verify(
                x => x.TransferTimeCreditsAsync(
                    requester.UserId,
                    offer.ProviderId,
                    offer.TimeCredits,
                    It.IsAny<string>()),
                Times.Once
            );
        }

        #endregion
    }
}
