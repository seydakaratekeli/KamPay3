using KamPay.Models;
using KamPay.Services;
using Moq;
using FluentAssertions;
using Xunit;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace KamPay.Tests.Services
{
    public class UserStateServiceTests
    {
        private readonly Mock<IAuthenticationService> _mockAuthService;
        private readonly Mock<IUserProfileService> _mockProfileService;
        private readonly Mock<IProductService> _mockProductService;
        private readonly Mock<IServiceSharingService> _mockServiceService;
        private readonly Mock<IGoodDeedService> _mockGoodDeedService;
        private readonly Mock<IMessagingService> _mockMessagingService;
        private readonly UserStateService _service;

        public UserStateServiceTests()
        {
            _mockAuthService = new Mock<IAuthenticationService>();
            _mockProfileService = new Mock<IUserProfileService>();
            _mockProductService = new Mock<IProductService>();
            _mockServiceService = new Mock<IServiceSharingService>();
            _mockGoodDeedService = new Mock<IGoodDeedService>();
            _mockMessagingService = new Mock<IMessagingService>();

            _service = new UserStateService(
                _mockAuthService.Object,
                _mockProfileService.Object,
                _mockProductService.Object,
                _mockServiceService.Object,
                _mockGoodDeedService.Object,
                _mockMessagingService.Object
            );
        }

        #region RefreshCurrentUserAsync Tests

        [Fact]
        public async Task RefreshCurrentUser_WithValidUser_ReturnsSuccess()
        {
            // Arrange
            var mockUser = new User
            {
                UserId = "user123",
                FirstName = "John",
                LastName = "Doe",
                Email = "john@example.com"
            };

            var mockProfile = new UserProfile
            {
                UserId = "user123",
                FirstName = "John",
                LastName = "Doe",
                Email = "john@example.com",
                ProfileImageUrl = "https://example.com/photo.jpg"
            };

            _mockAuthService
                .Setup(x => x.GetCurrentUserAsync())
                .ReturnsAsync(mockUser);

            _mockProfileService
                .Setup(x => x.GetUserProfileAsync("user123"))
                .ReturnsAsync(ServiceResult<UserProfile>.SuccessResult(mockProfile));

            // Act
            var result = await _service.RefreshCurrentUserAsync();

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data.UserId.Should().Be("user123");
            result.Data.FirstName.Should().Be("John");
            result.Data.ProfileImageUrl.Should().Be("https://example.com/photo.jpg");
        }

        [Fact]
        public async Task RefreshCurrentUser_WithNullUser_ReturnsFailure()
        {
            // Arrange
            _mockAuthService
                .Setup(x => x.GetCurrentUserAsync())
                .ReturnsAsync((User)null);

            // Act
            var result = await _service.RefreshCurrentUserAsync();

            // Assert
            result.Success.Should().BeFalse();
            result.Message.Should().Contain("oturum");
        }

        [Fact]
        public async Task RefreshCurrentUser_UpdatesCurrentUserProperty()
        {
            // Arrange
            var mockUser = new User
            {
                UserId = "user123",
                FirstName = "John",
                LastName = "Doe"
            };

            _mockAuthService
                .Setup(x => x.GetCurrentUserAsync())
                .ReturnsAsync(mockUser);

            _mockProfileService
                .Setup(x => x.GetUserProfileAsync("user123"))
                .ReturnsAsync(ServiceResult<UserProfile>.SuccessResult(new UserProfile()));

            // Act
            await _service.RefreshCurrentUserAsync();

            // Assert
            _service.CurrentUser.Should().NotBeNull();
            _service.CurrentUser.UserId.Should().Be("user123");
        }

        [Fact]
        public async Task RefreshCurrentUser_FiresUserProfileChangedEvent()
        {
            // Arrange
            var mockUser = new User
            {
                UserId = "user123",
                FirstName = "John",
                LastName = "Doe"
            };

            _mockAuthService
                .Setup(x => x.GetCurrentUserAsync())
                .ReturnsAsync(mockUser);

            _mockProfileService
                .Setup(x => x.GetUserProfileAsync("user123"))
                .ReturnsAsync(ServiceResult<UserProfile>.SuccessResult(new UserProfile()));

            User eventUser = null;
            _service.UserProfileChanged += (sender, user) => eventUser = user;

            // Act
            await _service.RefreshCurrentUserAsync();

            // Assert
            eventUser.Should().NotBeNull();
            eventUser.UserId.Should().Be("user123");
        }

        [Fact]
        public async Task RefreshCurrentUser_WithProfileServiceFailure_StillReturnsUser()
        {
            // Arrange
            var mockUser = new User
            {
                UserId = "user123",
                FirstName = "John",
                LastName = "Doe"
            };

            _mockAuthService
                .Setup(x => x.GetCurrentUserAsync())
                .ReturnsAsync(mockUser);

            _mockProfileService
                .Setup(x => x.GetUserProfileAsync("user123"))
                .ReturnsAsync(ServiceResult<UserProfile>.FailureResult("Profile error"));

            // Act
            var result = await _service.RefreshCurrentUserAsync();

            // Assert
            result.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data.UserId.Should().Be("user123");
        }

        #endregion

        #region UpdateUserProfileAsync Tests

        [Fact]
        public async Task UpdateUserProfile_WithNullCurrentUser_ReturnsFailure()
        {
            // Arrange
            _service.ClearUser();

            // Act
            var result = await _service.UpdateUserProfileAsync(firstName: "New Name");

            // Assert
            result.Success.Should().BeFalse();
            result.Message.Should().Contain("oturum");
        }

        [Fact]
        public async Task UpdateUserProfile_WithFirstName_UpdatesSuccessfully()
        {
            // Arrange
            var mockUser = new User
            {
                UserId = "user123",
                FirstName = "John",
                LastName = "Doe"
            };

            _mockAuthService
                .Setup(x => x.GetCurrentUserAsync())
                .ReturnsAsync(mockUser);

            _mockProfileService
                .Setup(x => x.GetUserProfileAsync("user123"))
                .ReturnsAsync(ServiceResult<UserProfile>.SuccessResult(new UserProfile()));

            await _service.RefreshCurrentUserAsync();

            _mockProfileService
                .Setup(x => x.UpdateUserProfileAsync("user123", "Jane", null, null, null))
                .ReturnsAsync(ServiceResult<bool>.SuccessResult(true));

            // Setup bulk update mocks
            SetupBulkUpdateMocks();

            // Act
            var result = await _service.UpdateUserProfileAsync(firstName: "Jane");

            // Assert
            result.Success.Should().BeTrue();
            _service.CurrentUser.FirstName.Should().Be("Jane");
        }

        [Fact]
        public async Task UpdateUserProfile_WithLastName_UpdatesSuccessfully()
        {
            // Arrange
            await SetupCurrentUser();

            _mockProfileService
                .Setup(x => x.UpdateUserProfileAsync("user123", null, "Smith", null, null))
                .ReturnsAsync(ServiceResult<bool>.SuccessResult(true));

            SetupBulkUpdateMocks();

            // Act
            var result = await _service.UpdateUserProfileAsync(lastName: "Smith");

            // Assert
            result.Success.Should().BeTrue();
            _service.CurrentUser.LastName.Should().Be("Smith");
        }

        [Fact]
        public async Task UpdateUserProfile_WithProfileImage_UpdatesSuccessfully()
        {
            // Arrange
            await SetupCurrentUser();

            var newImageUrl = "https://example.com/newphoto.jpg";

            _mockProfileService
                .Setup(x => x.UpdateUserProfileAsync("user123", null, null, null, newImageUrl))
                .ReturnsAsync(ServiceResult<bool>.SuccessResult(true));

            SetupBulkUpdateMocks();

            // Act
            var result = await _service.UpdateUserProfileAsync(profileImageUrl: newImageUrl);

            // Assert
            result.Success.Should().BeTrue();
            _service.CurrentUser.ProfileImageUrl.Should().Be(newImageUrl);
        }

        [Fact]
        public async Task UpdateUserProfile_WithUsername_UpdatesSuccessfully()
        {
            // Arrange
            await SetupCurrentUser();

            _mockProfileService
                .Setup(x => x.UpdateUserProfileAsync("user123", null, null, "newusername", null))
                .ReturnsAsync(ServiceResult<bool>.SuccessResult(true));

            SetupBulkUpdateMocks();

            // Act
            var result = await _service.UpdateUserProfileAsync(username: "newusername");

            // Assert
            result.Success.Should().BeTrue();
            _service.CurrentUser.Username.Should().Be("newusername");
        }

        [Fact]
        public async Task UpdateUserProfile_CallsAllBulkUpdateServices()
        {
            // Arrange
            await SetupCurrentUser();

            _mockProfileService
                .Setup(x => x.UpdateUserProfileAsync("user123", "Jane", null, null, null))
                .ReturnsAsync(ServiceResult<bool>.SuccessResult(true));

            SetupBulkUpdateMocks();

            // Act
            var result = await _service.UpdateUserProfileAsync(firstName: "Jane");

            // Assert
            result.Success.Should().BeTrue();

            _mockProductService.Verify(
                x => x.UpdateUserInfoInProductsAsync("user123", It.IsAny<string>(), It.IsAny<string>()),
                Times.Once
            );

            _mockServiceService.Verify(
                x => x.UpdateUserInfoInServicesAsync("user123", It.IsAny<string>(), It.IsAny<string>()),
                Times.Once
            );

            _mockGoodDeedService.Verify(
                x => x.UpdateUserInfoInPostsAsync("user123", It.IsAny<string>(), It.IsAny<string>()),
                Times.Once
            );

            _mockMessagingService.Verify(
                x => x.UpdateUserInfoInMessagesAsync("user123", It.IsAny<string>(), It.IsAny<string>()),
                Times.Once
            );

            _mockMessagingService.Verify(
                x => x.UpdateUserInfoInConversationsAsync("user123", It.IsAny<string>(), It.IsAny<string>()),
                Times.Once
            );
        }

        [Fact]
        public async Task UpdateUserProfile_FiresUserProfileChangedEvent()
        {
            // Arrange
            await SetupCurrentUser();

            _mockProfileService
                .Setup(x => x.UpdateUserProfileAsync("user123", "Jane", null, null, null))
                .ReturnsAsync(ServiceResult<bool>.SuccessResult(true));

            SetupBulkUpdateMocks();

            User eventUser = null;
            _service.UserProfileChanged += (sender, user) => eventUser = user;

            // Act
            await _service.UpdateUserProfileAsync(firstName: "Jane");

            // Assert
            eventUser.Should().NotBeNull();
            eventUser.FirstName.Should().Be("Jane");
        }

        [Fact]
        public async Task UpdateUserProfile_WithProfileServiceFailure_ReturnsFailure()
        {
            // Arrange
            await SetupCurrentUser();

            _mockProfileService
                .Setup(x => x.UpdateUserProfileAsync("user123", "Jane", null, null, null))
                .ReturnsAsync(ServiceResult<bool>.FailureResult("Update failed"));

            // Act
            var result = await _service.UpdateUserProfileAsync(firstName: "Jane");

            // Assert
            result.Success.Should().BeFalse();
            result.Message.Should().Contain("failed");
        }

        [Fact]
        public async Task UpdateUserProfile_WithBulkUpdateFailure_StillSucceeds()
        {
            // Arrange
            await SetupCurrentUser();

            _mockProfileService
                .Setup(x => x.UpdateUserProfileAsync("user123", "Jane", null, null, null))
                .ReturnsAsync(ServiceResult<bool>.SuccessResult(true));

            // Bir servis baþarýsýz olsa bile diðerleri devam etmeli
            _mockProductService
                .Setup(x => x.UpdateUserInfoInProductsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(ServiceResult<bool>.FailureResult("Product update failed"));

            _mockServiceService
                .Setup(x => x.UpdateUserInfoInServicesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(ServiceResult<bool>.SuccessResult(true));

            _mockGoodDeedService
                .Setup(x => x.UpdateUserInfoInPostsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(ServiceResult<bool>.SuccessResult(true));

            _mockMessagingService
                .Setup(x => x.UpdateUserInfoInMessagesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(ServiceResult<bool>.SuccessResult(true));

            _mockMessagingService
                .Setup(x => x.UpdateUserInfoInConversationsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(ServiceResult<bool>.SuccessResult(true));

            // Act
            var result = await _service.UpdateUserProfileAsync(firstName: "Jane");

            // Assert
            result.Success.Should().BeTrue();
            _service.CurrentUser.FirstName.Should().Be("Jane");
        }

        [Fact]
        public async Task UpdateUserProfile_WithMultipleFields_UpdatesAll()
        {
            // Arrange
            await SetupCurrentUser();

            _mockProfileService
                .Setup(x => x.UpdateUserProfileAsync(
                    "user123",
                    "Jane",
                    "Smith",
                    "janesmith",
                    "https://example.com/new.jpg"))
                .ReturnsAsync(ServiceResult<bool>.SuccessResult(true));

            SetupBulkUpdateMocks();

            // Act
            var result = await _service.UpdateUserProfileAsync(
                firstName: "Jane",
                lastName: "Smith",
                username: "janesmith",
                profileImageUrl: "https://example.com/new.jpg"
            );

            // Assert
            result.Success.Should().BeTrue();
            _service.CurrentUser.FirstName.Should().Be("Jane");
            _service.CurrentUser.LastName.Should().Be("Smith");
            _service.CurrentUser.Username.Should().Be("janesmith");
            _service.CurrentUser.ProfileImageUrl.Should().Be("https://example.com/new.jpg");
        }

        #endregion

        #region ClearUser Tests

        [Fact]
        public async Task ClearUser_RemovesCurrentUser()
        {
            // Arrange
            await SetupCurrentUser();
            _service.CurrentUser.Should().NotBeNull();

            // Act
            _service.ClearUser();

            // Assert
            _service.CurrentUser.Should().BeNull();
        }

        [Fact]
        public async Task ClearUser_FiresUserProfileChangedEvent()
        {
            // Arrange
            await SetupCurrentUser();

            User eventUser = new User { UserId = "not_null" };
            _service.UserProfileChanged += (sender, user) => eventUser = user;

            // Act
            _service.ClearUser();

            // Assert
            eventUser.Should().BeNull();
        }

        #endregion

        #region CurrentUser Property Tests

        [Fact]
        public void CurrentUser_DefaultValue_IsNull()
        {
            // Assert
            _service.CurrentUser.Should().BeNull();
        }

        [Fact]
        public async Task CurrentUser_AfterRefresh_IsNotNull()
        {
            // Arrange & Act
            await SetupCurrentUser();

            // Assert
            _service.CurrentUser.Should().NotBeNull();
        }

        #endregion

        #region UserProfileChanged Event Tests

        [Fact]
        public async Task UserProfileChanged_TriggersOnRefresh()
        {
            // Arrange
            var mockUser = new User { UserId = "user123", FirstName = "John", LastName = "Doe" };

            _mockAuthService
                .Setup(x => x.GetCurrentUserAsync())
                .ReturnsAsync(mockUser);

            _mockProfileService
                .Setup(x => x.GetUserProfileAsync("user123"))
                .ReturnsAsync(ServiceResult<UserProfile>.SuccessResult(new UserProfile()));

            int eventCount = 0;
            _service.UserProfileChanged += (sender, user) => eventCount++;

            // Act
            await _service.RefreshCurrentUserAsync();

            // Assert
            eventCount.Should().Be(1);
        }

        [Fact]
        public async Task UserProfileChanged_TriggersOnUpdate()
        {
            // Arrange
            await SetupCurrentUser();

            _mockProfileService
                .Setup(x => x.UpdateUserProfileAsync("user123", "Jane", null, null, null))
                .ReturnsAsync(ServiceResult<bool>.SuccessResult(true));

            SetupBulkUpdateMocks();

            int eventCount = 0;
            _service.UserProfileChanged += (sender, user) => eventCount++;

            // Act
            await _service.UpdateUserProfileAsync(firstName: "Jane");

            // Assert
            eventCount.Should().BeGreaterOrEqualTo(1);
        }

        [Fact]
        public void UserProfileChanged_TriggersOnClear()
        {
            // Arrange
            int eventCount = 0;
            User capturedUser = new User { UserId = "not_null" };
            _service.UserProfileChanged += (sender, user) =>
            {
                eventCount++;
                capturedUser = user;
            };

            // Act
            _service.ClearUser();

            // Assert
            eventCount.Should().Be(1);
            capturedUser.Should().BeNull();
        }

        #endregion

        #region Edge Cases

        [Fact]
        public async Task UpdateUserProfile_WithEmptyStrings_DoesNotUpdate()
        {
            // Arrange
            await SetupCurrentUser();
            var originalFirstName = _service.CurrentUser.FirstName;

            _mockProfileService
                .Setup(x => x.UpdateUserProfileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(ServiceResult<bool>.SuccessResult(true));

            SetupBulkUpdateMocks();

            // Act
            var result = await _service.UpdateUserProfileAsync(firstName: "   "); // Whitespace only

            // Assert
            result.Success.Should().BeTrue();
            _service.CurrentUser.FirstName.Should().Be(originalFirstName); // Deðiþmemeli
        }

        [Fact]
        public async Task RefreshCurrentUser_WithException_ReturnsFailure()
        {
            // Arrange
            _mockAuthService
                .Setup(x => x.GetCurrentUserAsync())
                .ThrowsAsync(new Exception("Network error"));

            // Act
            var result = await _service.RefreshCurrentUserAsync();

            // Assert
            result.Success.Should().BeFalse();
            result.Message.Should().Contain("yüklenemedi");
        }

        [Fact]
        public async Task UpdateUserProfile_WithException_ReturnsFailure()
        {
            // Arrange
            await SetupCurrentUser();

            _mockProfileService
                .Setup(x => x.UpdateUserProfileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _service.UpdateUserProfileAsync(firstName: "Jane");

            // Assert
            result.Success.Should().BeFalse();
            result.Message.Should().Contain("güncellenemedi");
        }

        #endregion

        #region Helper Methods

        private async Task SetupCurrentUser()
        {
            var mockUser = new User
            {
                UserId = "user123",
                FirstName = "John",
                LastName = "Doe",
                Username = "johndoe",
                ProfileImageUrl = "https://example.com/photo.jpg"
            };

            _mockAuthService
                .Setup(x => x.GetCurrentUserAsync())
                .ReturnsAsync(mockUser);

            _mockProfileService
                .Setup(x => x.GetUserProfileAsync("user123"))
                .ReturnsAsync(ServiceResult<UserProfile>.SuccessResult(new UserProfile
                {
                    UserId = "user123",
                    FirstName = "John",
                    LastName = "Doe"
                }));

            await _service.RefreshCurrentUserAsync();
        }

        private void SetupBulkUpdateMocks()
        {
            _mockProductService
                .Setup(x => x.UpdateUserInfoInProductsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(ServiceResult<bool>.SuccessResult(true));

            _mockServiceService
                .Setup(x => x.UpdateUserInfoInServicesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(ServiceResult<bool>.SuccessResult(true));

            _mockGoodDeedService
                .Setup(x => x.UpdateUserInfoInPostsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(ServiceResult<bool>.SuccessResult(true));

            _mockMessagingService
                .Setup(x => x.UpdateUserInfoInMessagesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(ServiceResult<bool>.SuccessResult(true));

            _mockMessagingService
                .Setup(x => x.UpdateUserInfoInConversationsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(ServiceResult<bool>.SuccessResult(true));
        }

        #endregion
    }
}
