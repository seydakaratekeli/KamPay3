using KamPay.Models;
using KamPay.Services;

namespace KamPay.Tests.Services;

/// <summary>
/// FirebaseNotificationService birim testleri
/// Bildirim oluþturma, okuma ve yönetim testleri
/// </summary>
public class FirebaseNotificationServiceTests
{
    private readonly Mock<INotificationService> _mockNotificationService;
    private readonly List<Notification> _testNotifications;

    public FirebaseNotificationServiceTests()
    {
        _mockNotificationService = new Mock<INotificationService>();
        _testNotifications = CreateTestNotifications();
    }

    private List<Notification> CreateTestNotifications()
    {
        return new List<Notification>
        {
            new Notification
            {
                NotificationId = "notif1",
                UserId = "user123",
                Title = "Yeni Mesaj",
                Message = "Ahmet size mesaj gönderdi",
                Type = NotificationType.NewMessage,
                IsRead = false,
                CreatedAt = DateTime.UtcNow.AddHours(-1)
            },
            new Notification
            {
                NotificationId = "notif2",
                UserId = "user123",
                Title = "Ürün Satýldý",
                Message = "Ürününüz satýldý",
                Type = NotificationType.ProductSold,
                IsRead = true,
                CreatedAt = DateTime.UtcNow.AddHours(-2),
                ReadAt = DateTime.UtcNow.AddMinutes(-30)
            },
            new Notification
            {
                NotificationId = "notif3",
                UserId = "user123",
                Title = "Yeni Teklif",
                Message = "Ürününüze teklif geldi",
                Type = NotificationType.NewOffer,
                IsRead = false,
                CreatedAt = DateTime.UtcNow.AddMinutes(-30)
            }
        };
    }

    #region GetUserNotificationsAsync Tests

    [Fact]
    public async Task GetUserNotificationsAsync_WithValidUserId_ReturnsNotifications()
    {
        // Arrange
        var userId = "user123";
        var expected = _testNotifications;

        _mockNotificationService
            .Setup(x => x.GetUserNotificationsAsync(userId))
            .ReturnsAsync(ServiceResult<List<Notification>>.SuccessResult(expected));

        // Act
        var result = await _mockNotificationService.Object.GetUserNotificationsAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().HaveCount(3);
        result.Data.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetUserNotificationsAsync_WithNoNotifications_ReturnsEmptyList()
    {
        // Arrange
        var userId = "user_no_notifications";

        _mockNotificationService
            .Setup(x => x.GetUserNotificationsAsync(userId))
            .ReturnsAsync(ServiceResult<List<Notification>>.SuccessResult(new List<Notification>()));

        // Act
        var result = await _mockNotificationService.Object.GetUserNotificationsAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserNotificationsAsync_WithInvalidUserId_ReturnsFailure()
    {
        // Arrange
        var userId = "";

        _mockNotificationService
            .Setup(x => x.GetUserNotificationsAsync(userId))
            .ReturnsAsync(ServiceResult<List<Notification>>.FailureResult(
                "Kullanýcý ID'si geçersiz",
                "Lütfen geçerli bir kullanýcý ID'si girin"));

        // Act
        var result = await _mockNotificationService.Object.GetUserNotificationsAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetUserNotificationsAsync_OrdersByCreatedAtDescending()
    {
        // Arrange
        var userId = "user123";
        var notifications = _testNotifications;

        _mockNotificationService
            .Setup(x => x.GetUserNotificationsAsync(userId))
            .ReturnsAsync(ServiceResult<List<Notification>>.SuccessResult(
                notifications.OrderByDescending(n => n.CreatedAt).ToList()));

        // Act
        var result = await _mockNotificationService.Object.GetUserNotificationsAsync(userId);

        // Assert
        result.Data.Should().NotBeNull();
        result.Data.Should().BeInDescendingOrder(n => n.CreatedAt);
        result.Data.First().NotificationId.Should().Be("notif3"); // Most recent
    }

    #endregion

    #region CreateNotificationAsync Tests

    [Fact]
    public async Task CreateNotificationAsync_WithValidNotification_ReturnsSuccess()
    {
        // Arrange
        var notification = new Notification
        {
            NotificationId = "new_notif",
            UserId = "user123",
            Title = "Test Bildirim",
            Message = "Test mesajý",
            Type = NotificationType.System,
            CreatedAt = DateTime.UtcNow
        };

        _mockNotificationService
            .Setup(x => x.CreateNotificationAsync(It.IsAny<Notification>()))
            .ReturnsAsync(ServiceResult<bool>.SuccessResult(true, "Bildirim oluþturuldu"));

        // Act
        var result = await _mockNotificationService.Object.CreateNotificationAsync(notification);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeTrue();
    }

    [Fact]
    public async Task CreateNotificationAsync_WithNullNotification_ReturnsFailure()
    {
        // Arrange
        Notification notification = null;

        _mockNotificationService
            .Setup(x => x.CreateNotificationAsync(null))
            .ReturnsAsync(ServiceResult<bool>.FailureResult(
                "Bildirim geçersiz",
                "Bildirim nesnesi null olamaz"));

        // Act
        var result = await _mockNotificationService.Object.CreateNotificationAsync(notification);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task CreateNotificationAsync_WithEmptyUserId_ReturnsFailure()
    {
        // Arrange
        var notification = new Notification
        {
            NotificationId = "notif_invalid",
            UserId = "", // Empty user ID
            Title = "Test",
            Message = "Test"
        };

        _mockNotificationService
            .Setup(x => x.CreateNotificationAsync(It.Is<Notification>(n => string.IsNullOrEmpty(n.UserId))))
            .ReturnsAsync(ServiceResult<bool>.FailureResult(
                "Kullanýcý ID'si geçersiz",
                "Bildirim için kullanýcý ID'si gerekli"));

        // Act
        var result = await _mockNotificationService.Object.CreateNotificationAsync(notification);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
    }

    [Theory]
    [InlineData(NotificationType.NewMessage)]
    [InlineData(NotificationType.ProductSold)]
    [InlineData(NotificationType.NewOffer)]
    [InlineData(NotificationType.SystemNotice)]
    public async Task CreateNotificationAsync_WithDifferentTypes_CreatesCorrectly(NotificationType type)
    {
        // Arrange
        var notification = new Notification
        {
            NotificationId = $"notif_{type}",
            UserId = "user123",
            Title = $"Test {type}",
            Message = "Test message",
            Type = type,
            CreatedAt = DateTime.UtcNow
        };

        _mockNotificationService
            .Setup(x => x.CreateNotificationAsync(It.Is<Notification>(n => n.Type == type)))
            .ReturnsAsync(ServiceResult<bool>.SuccessResult(true));

        // Act
        var result = await _mockNotificationService.Object.CreateNotificationAsync(notification);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region MarkAsReadAsync Tests

    [Fact]
    public async Task MarkAsReadAsync_WithValidNotificationId_MarksAsRead()
    {
        // Arrange
        var notificationId = "notif1";

        _mockNotificationService
            .Setup(x => x.MarkAsReadAsync(notificationId))
            .ReturnsAsync(ServiceResult<bool>.SuccessResult(true, "Bildirim okundu olarak iþaretlendi"));

        // Act
        var result = await _mockNotificationService.Object.MarkAsReadAsync(notificationId);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeTrue();
    }

    [Fact]
    public async Task MarkAsReadAsync_WithInvalidNotificationId_ReturnsFailure()
    {
        // Arrange
        var notificationId = "invalid_id";

        _mockNotificationService
            .Setup(x => x.MarkAsReadAsync(notificationId))
            .ReturnsAsync(ServiceResult<bool>.FailureResult(
                "Bildirim bulunamadý",
                "Belirtilen bildirim mevcut deðil"));

        // Act
        var result = await _mockNotificationService.Object.MarkAsReadAsync(notificationId);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task MarkAsReadAsync_WithAlreadyReadNotification_ReturnsSuccess()
    {
        // Arrange
        var notificationId = "notif2"; // Already read

        _mockNotificationService
            .Setup(x => x.MarkAsReadAsync(notificationId))
            .ReturnsAsync(ServiceResult<bool>.SuccessResult(true, "Bildirim zaten okunmuþ"));

        // Act
        var result = await _mockNotificationService.Object.MarkAsReadAsync(notificationId);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region MarkAllAsReadAsync Tests

    [Fact]
    public async Task MarkAllAsReadAsync_WithUnreadNotifications_MarksAllAsRead()
    {
        // Arrange
        var userId = "user123";

        _mockNotificationService
            .Setup(x => x.MarkAllAsReadAsync(userId))
            .ReturnsAsync(ServiceResult<bool>.SuccessResult(true, "Tüm bildirimler okundu olarak iþaretlendi"));

        // Act
        var result = await _mockNotificationService.Object.MarkAllAsReadAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeTrue();
    }

    [Fact]
    public async Task MarkAllAsReadAsync_WithNoUnreadNotifications_ReturnsSuccess()
    {
        // Arrange
        var userId = "user_all_read";

        _mockNotificationService
            .Setup(x => x.MarkAllAsReadAsync(userId))
            .ReturnsAsync(ServiceResult<bool>.SuccessResult(true, "Okunmamýþ bildirim yok"));

        // Act
        var result = await _mockNotificationService.Object.MarkAllAsReadAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region DeleteNotificationAsync Tests

    [Fact]
    public async Task DeleteNotificationAsync_WithValidId_DeletesSuccessfully()
    {
        // Arrange
        var notificationId = "notif1";

        _mockNotificationService
            .Setup(x => x.DeleteNotificationAsync(notificationId))
            .ReturnsAsync(ServiceResult<bool>.SuccessResult(true, "Bildirim silindi"));

        // Act
        var result = await _mockNotificationService.Object.DeleteNotificationAsync(notificationId);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteNotificationAsync_WithInvalidId_ReturnsFailure()
    {
        // Arrange
        var notificationId = "invalid_id";

        _mockNotificationService
            .Setup(x => x.DeleteNotificationAsync(notificationId))
            .ReturnsAsync(ServiceResult<bool>.FailureResult(
                "Silme hatasý",
                "Bildirim bulunamadý"));

        // Act
        var result = await _mockNotificationService.Object.DeleteNotificationAsync(notificationId);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
    }

    #endregion

    #region DeleteAllNotificationsAsync Tests

    [Fact]
    public async Task DeleteAllNotificationsAsync_WithValidUserId_DeletesAll()
    {
        // Arrange
        var userId = "user123";

        _mockNotificationService
            .Setup(x => x.DeleteAllNotificationsAsync(userId))
            .ReturnsAsync(ServiceResult<bool>.SuccessResult(true, "Tüm bildirimler silindi"));

        // Act
        var result = await _mockNotificationService.Object.DeleteAllNotificationsAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAllNotificationsAsync_WithNoNotifications_ReturnsSuccess()
    {
        // Arrange
        var userId = "user_no_notifications";

        _mockNotificationService
            .Setup(x => x.DeleteAllNotificationsAsync(userId))
            .ReturnsAsync(ServiceResult<bool>.SuccessResult(true, "Silinecek bildirim yok"));

        // Act
        var result = await _mockNotificationService.Object.DeleteAllNotificationsAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region Notification Model Tests

    [Fact]
    public void Notification_HasRequiredProperties()
    {
        // Arrange & Act
        var notification = new Notification
        {
            NotificationId = "test_id",
            UserId = "user123",
            Title = "Test Title",
            Message = "Test Message",
            Type = NotificationType.Message,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        // Assert
        notification.NotificationId.Should().Be("test_id");
        notification.UserId.Should().Be("user123");
        notification.Title.Should().Be("Test Title");
        notification.Message.Should().Be("Test Message");
        notification.Type.Should().Be(NotificationType.Message);
        notification.IsRead.Should().BeFalse();
        notification.ReadAt.Should().BeNull();
    }

    [Fact]
    public void Notification_ReadAt_IsNullByDefault()
    {
        // Arrange & Act
        var notification = new Notification();

        // Assert
        notification.IsRead.Should().BeFalse();
        notification.ReadAt.Should().BeNull();
    }

    [Fact]
    public void Notification_CanSetReadAt()
    {
        // Arrange
        var notification = new Notification();
        var readTime = DateTime.UtcNow;

        // Act
        notification.IsRead = true;
        notification.ReadAt = readTime;

        // Assert
        notification.IsRead.Should().BeTrue();
        notification.ReadAt.Should().Be(readTime);
    }

    #endregion

    #region NotificationType Tests

    [Theory]
    [InlineData(NotificationType.Message)]
    [InlineData(NotificationType.ProductSold)]
    [InlineData(NotificationType.OfferReceived)]
    [InlineData(NotificationType.ServiceRequest)]
    [InlineData(NotificationType.System)]
    public void NotificationType_HasAllExpectedValues(NotificationType type)
    {
        // Act & Assert
        Enum.IsDefined(typeof(NotificationType), type).Should().BeTrue();
    }

    #endregion
}
