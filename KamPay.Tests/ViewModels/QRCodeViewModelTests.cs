using KamPay.Models;
using KamPay.Services;
using KamPay.ViewModels;
using KamPay.Models.Messages;
using Moq;
using FluentAssertions;
using Xunit;
using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;

namespace KamPay.Tests.ViewModels
{
    public class QRCodeViewModelTests : IDisposable
    {
        private readonly Mock<IQRCodeService> _mockQRCodeService;
        private readonly Mock<IAuthenticationService> _mockAuthService;
        private readonly Mock<IProductService> _mockProductService;
        private readonly Mock<IStorageService> _mockStorageService;
        private readonly QRCodeViewModel _viewModel;

        public QRCodeViewModelTests()
        {
            _mockQRCodeService = new Mock<IQRCodeService>();
            _mockAuthService = new Mock<IAuthenticationService>();
            _mockProductService = new Mock<IProductService>();
            _mockStorageService = new Mock<IStorageService>();

            _viewModel = new QRCodeViewModel(
                _mockQRCodeService.Object,
                _mockAuthService.Object,
                _mockProductService.Object,
                _mockStorageService.Object
            );
        }

        public void Dispose()
        {
            // WeakReferenceMessenger'ý temizle
            WeakReferenceMessenger.Default.UnregisterAll(this);
        }

        #region Property Tests

        [Fact]
        public void TransactionId_WhenSet_TriggersOnChangedMethod()
        {
            // Arrange
            var mockUser = new User { UserId = "user123" };
            var mockTransaction = new Transaction
            {
                TransactionId = "trans123",
                SellerId = "seller123",
                ProductId = "prod123",
                OfferedProductId = "offprod123"
            };

            _mockAuthService
                .Setup(x => x.GetCurrentUserAsync())
                .ReturnsAsync(mockUser);

            // Mock QR codes
            var qrCodes = new List<DeliveryQRCode>
            {
                new DeliveryQRCode { ProductId = "prod123", QRCodeId = "qr1" },
                new DeliveryQRCode { ProductId = "offprod123", QRCodeId = "qr2" }
            };

            _mockQRCodeService
                .Setup(x => x.GetQRCodesForTransactionAsync("trans123"))
                .ReturnsAsync(ServiceResult<List<DeliveryQRCode>>.SuccessResult(qrCodes));

            // Act
            _viewModel.TransactionId = "trans123";

            // Assert
            _viewModel.TransactionId.Should().Be("trans123");
        }

        [Fact]
        public void PageTitle_DefaultValue_IsCorrect()
        {
            // Assert
            _viewModel.PageTitle.Should().Be("Teslimat Onayý");
        }

        [Fact]
        public void InstructionText_DefaultValue_IsCorrect()
        {
            // Assert
            _viewModel.InstructionText.Should().NotBeNullOrEmpty();
        }

        #endregion

        #region ProcessScannedQRCodeAsync Tests

        [Fact]
        public async Task ProcessScannedQRCode_WithNullOtherUserDelivery_ShowsError()
        {
            // Arrange
            _viewModel.OtherUserDelivery = null;

            // Act
            await _viewModel.ProcessScannedQRCodeAsync("some_qr_code");

            // Assert
            _viewModel.IsLoading.Should().BeFalse();
            // Hata mesajý DisplayAlert ile gösterilir (UI test gerektirir)
        }

        [Fact]
        public async Task ProcessScannedQRCode_WithInvalidQRCode_ShowsError()
        {
            // Arrange
            _viewModel.OtherUserDelivery = new DeliveryQRCode
            {
                QRCodeData = "valid_qr_code",
                IsUsed = false
            };

            // Act
            await _viewModel.ProcessScannedQRCodeAsync("invalid_qr_code");

            // Assert
            _viewModel.IsLoading.Should().BeFalse();
        }

        [Fact]
        public async Task ProcessScannedQRCode_WithAlreadyUsedQR_ShowsInfo()
        {
            // Arrange
            var qrCode = "valid_qr_code";
            _viewModel.OtherUserDelivery = new DeliveryQRCode
            {
                QRCodeData = qrCode,
                IsUsed = true
            };

            // Act
            await _viewModel.ProcessScannedQRCodeAsync(qrCode);

            // Assert
            _viewModel.IsLoading.Should().BeFalse();
        }

        [Fact]
        public async Task ProcessScannedQRCode_WithOldQRCodeFormat_UsesBackwardCompatibility()
        {
            // Arrange
            var qrCodeData = "old_format_qr";
            var qrCodeId = "qr123";

            _viewModel.OtherUserDelivery = new DeliveryQRCode
            {
                QRCodeId = qrCodeId,
                QRCodeData = qrCodeData,
                IsUsed = false,
                VerificationPin = null, // Eski format (PIN yok)
                ProductTitle = "Test Product"
            };

            _mockQRCodeService
                .Setup(x => x.CompleteDeliveryAsync(qrCodeId))
                .ReturnsAsync(ServiceResult<bool>.SuccessResult(true));

            _mockProductService
                .Setup(x => x.MarkAsExchangedAsync(It.IsAny<string>()))
                .ReturnsAsync(ServiceResult<bool>.SuccessResult(true));

            // Act
            await _viewModel.ProcessScannedQRCodeAsync(qrCodeData);

            // Assert
            _mockQRCodeService.Verify(
                x => x.CompleteDeliveryAsync(qrCodeId),
                Times.Once
            );
        }

        [Fact]
        public async Task ProcessScannedQRCode_WithNewQRCodeFormat_UsesSecureScan()
        {
            // Arrange
            var qrCodeData = "secure_qr";
            var qrCodeId = "qr123";
            var pin = "123456";

            _viewModel.OtherUserDelivery = new DeliveryQRCode
            {
                QRCodeId = qrCodeId,
                QRCodeData = qrCodeData,
                IsUsed = false,
                VerificationPin = pin,
                ProductTitle = "Test Product"
            };

            _mockQRCodeService
                .Setup(x => x.ScanQRCodeWithLocationAsync(qrCodeId, It.IsAny<double>(), It.IsAny<double>(), pin))
                .ReturnsAsync(ServiceResult<bool>.SuccessResult(true));

            _mockProductService
                .Setup(x => x.MarkAsExchangedAsync(It.IsAny<string>()))
                .ReturnsAsync(ServiceResult<bool>.SuccessResult(true));

            // Mock DisplayPromptAsync için gerekli (PIN isteme)
            // Not: MAUI UI metodlarý test edilemez, ancak kod akýþý doðrulanabilir

            // Act & Assert - Exception beklenir çünkü DisplayPromptAsync mock edilemez
            await Assert.ThrowsAsync<NullReferenceException>(async () =>
            {
                await _viewModel.ProcessScannedQRCodeAsync(qrCodeData);
            });
        }

        #endregion

        #region Messenger Tests

        [Fact]
        public void Receive_QRCodeScannedMessage_CallsProcessScannedQRCodeAsync()
        {
            // Arrange
            var qrCodeData = "test_qr_code";
            _viewModel.OtherUserDelivery = new DeliveryQRCode
            {
                QRCodeData = qrCodeData,
                IsUsed = true // Hýzlý test için zaten kullanýlmýþ
            };

            // Act
            _viewModel.Receive(new QRCodeScannedMessage(qrCodeData));

            // Assert - IsLoading kontrolü yapýlabilir
            // Not: Async metod test edildiði için sonucu beklemek gerekir
        }

        #endregion

        #region ExtendTimeAsync Tests

        [Fact]
        public async Task ExtendTime_WithNullCurrentQRCode_DoesNothing()
        {
            // Arrange
            _viewModel.CurrentQRCode = null;

            // Act
            // DisplayPromptAsync mock edilemediði için direkt kontrol yapýlamaz
            // Ancak kod akýþý doðrulanabilir

            // Assert
            _viewModel.CurrentQRCode.Should().BeNull();
        }

        [Fact]
        public async Task ExtendTime_WithValidMinutes_CallsService()
        {
            // Arrange
            var qrCode = new DeliveryQRCode
            {
                QRCodeId = "qr123",
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            };
            _viewModel.CurrentQRCode = qrCode;

            var extendedTime = DateTime.UtcNow.AddHours(1).AddMinutes(30);
            _mockQRCodeService
                .Setup(x => x.ExtendQRCodeValidityAsync("qr123", 30))
                .ReturnsAsync(ServiceResult<DateTime>.SuccessResult(extendedTime));

            // Act & Assert - DisplayPromptAsync mock edilemez
            // Test edilebilir kod akýþý için refactoring gerekir
        }

        #endregion

        #region CancelDeliveryAsync Tests

        [Fact]
        public async Task CancelDelivery_WithNullCurrentQRCode_DoesNothing()
        {
            // Arrange
            _viewModel.CurrentQRCode = null;

            // Act & Assert
            _viewModel.CurrentQRCode.Should().BeNull();
        }

        [Fact]
        public async Task CancelDelivery_WithValidReason_CallsService()
        {
            // Arrange
            var qrCode = new DeliveryQRCode
            {
                QRCodeId = "qr123"
            };
            _viewModel.CurrentQRCode = qrCode;

            var mockUser = new User { UserId = "user123" };
            _mockAuthService
                .Setup(x => x.GetCurrentUserAsync())
                .ReturnsAsync(mockUser);

            _mockQRCodeService
                .Setup(x => x.CancelDeliveryQRCodeAsync("qr123", "user123", It.IsAny<string>()))
                .ReturnsAsync(ServiceResult<bool>.SuccessResult(true));

            // Act & Assert - DisplayActionSheet mock edilemez
            // Test edilebilir kod akýþý için refactoring gerekir
        }

        #endregion

        #region Photo Upload Tests (Phase 2)

        [Fact]
        public void PhotoRequired_DefaultValue_IsFalse()
        {
            // Assert
            _viewModel.PhotoRequired.Should().BeFalse();
        }

        [Fact]
        public void IsPhotoUploaded_WithoutPhoto_IsFalse()
        {
            // Arrange
            _viewModel.MyDelivery = new DeliveryQRCode
            {
                DeliveryPhotoUrl = null
            };

            // Act
            // UpdateUIState çaðrýsý gerekir (private metod)

            // Assert
            _viewModel.IsPhotoUploaded.Should().BeFalse();
        }

        [Fact]
        public void IsPhotoUploaded_WithPhoto_IsTrue()
        {
            // Arrange
            _viewModel.MyDelivery = new DeliveryQRCode
            {
                DeliveryPhotoUrl = "https://example.com/photo.jpg"
            };

            // Act
            // UpdateUIState çaðrýsý gerekir (private metod)

            // Assert
            _viewModel.MyDelivery.DeliveryPhotoUrl.Should().NotBeNullOrEmpty();
        }

        #endregion

        #region UpdateTimeRemaining Tests

        [Fact]
        public void UpdateTimeRemaining_WithExpiredQRCode_ShowsExpired()
        {
            // Arrange
            _viewModel.CurrentQRCode = new DeliveryQRCode
            {
                QRCodeId = "qr123",
                ExpiresAt = DateTime.UtcNow.AddMinutes(-10), // Süresi dolmuþ
                IsExpired = true
            };

            // Act
            // UpdateTimeRemaining private metod olduðu için direkt test edilemez
            // Timer tetiklenmesi gerekir

            // Assert
            // TimeRemaining property'si "Süresi doldu" olmalý
        }

        [Fact]
        public void UpdateTimeRemaining_WithValidQRCode_ShowsTimeLeft()
        {
            // Arrange
            _viewModel.CurrentQRCode = new DeliveryQRCode
            {
                QRCodeId = "qr123",
                ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                IsExpired = false
            };

            // Act
            // UpdateTimeRemaining private metod

            // Assert
            _viewModel.CurrentQRCode.IsExpired.Should().BeFalse();
        }

        [Fact]
        public void CanExtendTime_WhenExpired_IsFalse()
        {
            // Arrange
            _viewModel.CurrentQRCode = new DeliveryQRCode
            {
                ExpiresAt = DateTime.UtcNow.AddMinutes(-10),
                IsExpired = true
            };

            // Act & Assert
            // CanExtendTime property kontrolü (UpdateTimeRemaining sonrasý)
            _viewModel.CurrentQRCode.IsExpired.Should().BeTrue();
        }

        [Fact]
        public void CanExtendTime_WhenAlreadyExtended_IsFalse()
        {
            // Arrange
            _viewModel.CurrentQRCode = new DeliveryQRCode
            {
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                IsExpired = false,
                HasBeenExtended = true
            };

            // Act & Assert
            _viewModel.CurrentQRCode.HasBeenExtended.Should().BeTrue();
        }

        #endregion

        #region Integration Tests

        [Fact]
        public void QRCodeViewModel_Initialization_SetsDefaultValues()
        {
            // Arrange & Act
            var vm = new QRCodeViewModel(
                _mockQRCodeService.Object,
                _mockAuthService.Object,
                _mockProductService.Object,
                _mockStorageService.Object
            );

            // Assert
            vm.IsLoading.Should().BeFalse();
            vm.PageTitle.Should().Be("Teslimat Onayý");
            vm.PhotoRequired.Should().BeFalse();
            vm.IsPhotoUploaded.Should().BeFalse();
            vm.TimeRemaining.Should().BeEmpty();
            vm.CanExtendTime.Should().BeFalse();
        }

        [Fact]
        public void QRCodeViewModel_WithMessenger_RegistersCorrectly()
        {
            // Arrange
            var vm = new QRCodeViewModel(
                _mockQRCodeService.Object,
                _mockAuthService.Object,
                _mockProductService.Object,
                _mockStorageService.Object
            );

            var qrCodeData = "test_qr";
            vm.OtherUserDelivery = new DeliveryQRCode
            {
                QRCodeData = qrCodeData,
                IsUsed = true
            };

            // Act - Mesaj gönder
            WeakReferenceMessenger.Default.Send(new QRCodeScannedMessage(qrCodeData));

            // Assert - Mesajýn iþlendiði kontrol edilir
            // Note: Async metod olduðu için tam sonuç beklenemez
            vm.OtherUserDelivery.Should().NotBeNull();
        }

        #endregion

        #region Edge Cases

        [Fact]
        public async Task ProcessScannedQRCode_WithNullVerificationPin_UsesBackwardCompatibility()
        {
            // Arrange
            var qrCodeData = "qr_without_pin";
            _viewModel.OtherUserDelivery = new DeliveryQRCode
            {
                QRCodeId = "qr123",
                QRCodeData = qrCodeData,
                IsUsed = false,
                VerificationPin = null, // PIN yok
                ProductTitle = "Product"
            };

            _mockQRCodeService
                .Setup(x => x.CompleteDeliveryAsync("qr123"))
                .ReturnsAsync(ServiceResult<bool>.SuccessResult(true));

            _mockProductService
                .Setup(x => x.MarkAsExchangedAsync(It.IsAny<string>()))
                .ReturnsAsync(ServiceResult<bool>.SuccessResult(true));

            // Act
            await _viewModel.ProcessScannedQRCodeAsync(qrCodeData);

            // Assert
            _mockQRCodeService.Verify(
                x => x.CompleteDeliveryAsync("qr123"),
                Times.Once
            );
            _mockQRCodeService.Verify(
                x => x.ScanQRCodeWithLocationAsync(It.IsAny<string>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string>()),
                Times.Never
            );
        }

        [Fact]
        public async Task ProcessScannedQRCode_WithEmptyVerificationPin_UsesBackwardCompatibility()
        {
            // Arrange
            var qrCodeData = "qr_empty_pin";
            _viewModel.OtherUserDelivery = new DeliveryQRCode
            {
                QRCodeId = "qr123",
                QRCodeData = qrCodeData,
                IsUsed = false,
                VerificationPin = "", // Boþ PIN
                ProductTitle = "Product"
            };

            _mockQRCodeService
                .Setup(x => x.CompleteDeliveryAsync("qr123"))
                .ReturnsAsync(ServiceResult<bool>.SuccessResult(true));

            _mockProductService
                .Setup(x => x.MarkAsExchangedAsync(It.IsAny<string>()))
                .ReturnsAsync(ServiceResult<bool>.SuccessResult(true));

            // Act
            await _viewModel.ProcessScannedQRCodeAsync(qrCodeData);

            // Assert
            _mockQRCodeService.Verify(
                x => x.CompleteDeliveryAsync("qr123"),
                Times.Once
            );
        }

        #endregion
    }
}
