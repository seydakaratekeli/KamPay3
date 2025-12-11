using KamPay.Models;
using KamPay.Services;

namespace KamPay.Tests.Services;

/// <summary>
/// FirebaseMessagingService birim testleri
/// Mesaj gönderme, konuþma oluþturma ve yönetim testleri
/// </summary>
public class FirebaseMessagingServiceTests
{
    private readonly Mock<IMessagingService> _mockMessagingService;
    private readonly Mock<INotificationService> _mockNotificationService;
    private readonly User _testSender;
    private readonly User _testReceiver;

    public FirebaseMessagingServiceTests()
    {
        _mockMessagingService = new Mock<IMessagingService>();
        _mockNotificationService = new Mock<INotificationService>();

        _testSender = new User
        {
            UserId = "sender123",
            Email = "sender@bartin.edu.tr",
            FirstName = "Ahmet",
            LastName = "Yýlmaz",
            ProfileImageUrl = "https://example.com/sender.jpg"
        };

        _testReceiver = new User
        {
            UserId = "receiver456",
            Email = "receiver@bartin.edu.tr",
            FirstName = "Ayþe",
            LastName = "Demir",
            ProfileImageUrl = "https://example.com/receiver.jpg"
        };
    }

    #region SendMessageAsync Tests

    [Fact]
    public async Task SendMessageAsync_WithValidRequest_SendsSuccessfully()
    {
        // Arrange
        var request = new SendMessageRequest
        {
            ReceiverId = _testReceiver.UserId,
            Content = "Merhaba!",
            Type = MessageType.Text
        };

        var expectedMessage = new Message
        {
            MessageId = "msg123",
            SenderId = _testSender.UserId,
            ReceiverId = _testReceiver.UserId,
            Content = "Merhaba!",
            Type = MessageType.Text
        };

        _mockMessagingService
            .Setup(x => x.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<User>()))
            .ReturnsAsync(ServiceResult<Message>.SuccessResult(expectedMessage, "Mesaj gönderildi"));

        // Act
        var result = await _mockMessagingService.Object.SendMessageAsync(request, _testSender);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Content.Should().Be("Merhaba!");
    }

    [Fact]
    public async Task SendMessageAsync_WithEmptyContent_ReturnsFailure()
    {
        // Arrange
        var request = new SendMessageRequest
        {
            ReceiverId = _testReceiver.UserId,
            Content = "",
            Type = MessageType.Text
        };

        _mockMessagingService
            .Setup(x => x.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<User>()))
            .ReturnsAsync(ServiceResult<Message>.FailureResult(
                "Mesaj içeriði boþ olamaz",
                "Lütfen bir mesaj yazýn"));

        // Act
        var result = await _mockMessagingService.Object.SendMessageAsync(request, _testSender);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SendMessageAsync_WithNullSender_ReturnsFailure()
    {
        // Arrange
        var request = new SendMessageRequest
        {
            ReceiverId = _testReceiver.UserId,
            Content = "Test",
            Type = MessageType.Text
        };

        _mockMessagingService
            .Setup(x => x.SendMessageAsync(It.IsAny<SendMessageRequest>(), null))
            .ReturnsAsync(ServiceResult<Message>.FailureResult(
                "Gönderen geçersiz",
                "Kullanýcý bilgisi bulunamadý"));

        // Act
        var result = await _mockMessagingService.Object.SendMessageAsync(request, null);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task SendMessageAsync_WithImageType_RequiresImageUrl()
    {
        // Arrange
        var request = new SendMessageRequest
        {
            ReceiverId = _testReceiver.UserId,
            Content = "Fotoðraf",
            Type = MessageType.Image,
            ImageUrl = "" // Empty image URL
        };

        _mockMessagingService
            .Setup(x => x.SendMessageAsync(It.Is<SendMessageRequest>(r => 
                r.Type == MessageType.Image && string.IsNullOrEmpty(r.ImageUrl)), 
                It.IsAny<User>()))
            .ReturnsAsync(ServiceResult<Message>.FailureResult(
                "Görsel URL'i boþ olamaz",
                "Lütfen bir görsel seçin"));

        // Act
        var result = await _mockMessagingService.Object.SendMessageAsync(request, _testSender);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task SendMessageAsync_WithProductId_IncludesProductInfo()
    {
        // Arrange
        var productId = "product123";
        var request = new SendMessageRequest
        {
            ReceiverId = _testReceiver.UserId,
            Content = "Bu ürün hakkýnda bilgi alabilir miyim?",
            Type = MessageType.Text,
            ProductId = productId
        };

        var expectedMessage = new Message
        {
            MessageId = "msg123",
            SenderId = _testSender.UserId,
            ReceiverId = _testReceiver.UserId,
            Content = request.Content,
            Type = MessageType.Text,
            ProductId = productId,
            ProductTitle = "Test Ürün",
            ProductThumbnail = "https://example.com/product.jpg"
        };

        _mockMessagingService
            .Setup(x => x.SendMessageAsync(It.Is<SendMessageRequest>(r => r.ProductId == productId), 
                It.IsAny<User>()))
            .ReturnsAsync(ServiceResult<Message>.SuccessResult(expectedMessage));

        // Act
        var result = await _mockMessagingService.Object.SendMessageAsync(request, _testSender);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Data.ProductId.Should().Be(productId);
        result.Data.ProductTitle.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region GetConversationMessagesAsync Tests

    [Fact]
    public async Task GetConversationMessagesAsync_WithValidId_ReturnsMessages()
    {
        // Arrange
        var conversationId = "conv123";
        var messages = new List<Message>
        {
            new Message
            {
                MessageId = "msg1",
                ConversationId = conversationId,
                Content = "Merhaba",
                SentAt = DateTime.UtcNow.AddMinutes(-10)
            },
            new Message
            {
                MessageId = "msg2",
                ConversationId = conversationId,
                Content = "Nasýlsýn?",
                SentAt = DateTime.UtcNow.AddMinutes(-5)
            }
        };

        _mockMessagingService
            .Setup(x => x.GetConversationMessagesAsync(conversationId, It.IsAny<int>()))
            .ReturnsAsync(ServiceResult<List<Message>>.SuccessResult(messages));

        // Act
        var result = await _mockMessagingService.Object.GetConversationMessagesAsync(conversationId);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetConversationMessagesAsync_WithLimit_ReturnsLimitedMessages()
    {
        // Arrange
        var conversationId = "conv123";
        var limit = 10;
        var messages = Enumerable.Range(1, 5)
            .Select(i => new Message
            {
                MessageId = $"msg{i}",
                ConversationId = conversationId,
                Content = $"Message {i}"
            })
            .ToList();

        _mockMessagingService
            .Setup(x => x.GetConversationMessagesAsync(conversationId, limit))
            .ReturnsAsync(ServiceResult<List<Message>>.SuccessResult(messages));

        // Act
        var result = await _mockMessagingService.Object.GetConversationMessagesAsync(conversationId, limit);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().HaveCountLessOrEqualTo(limit);
    }

    #endregion

    #region GetUserConversationsAsync Tests

    [Fact]
    public async Task GetUserConversationsAsync_WithValidUserId_ReturnsConversations()
    {
        // Arrange
        var userId = _testSender.UserId;
        var conversations = new List<Conversation>
        {
            new Conversation
            {
                ConversationId = "conv1",
                User1Id = userId,
                User2Id = _testReceiver.UserId,
                LastMessage = "Son mesaj",
                LastMessageTime = DateTime.UtcNow
            }
        };

        _mockMessagingService
            .Setup(x => x.GetUserConversationsAsync(userId))
            .ReturnsAsync(ServiceResult<List<Conversation>>.SuccessResult(conversations));

        // Act
        var result = await _mockMessagingService.Object.GetUserConversationsAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetUserConversationsAsync_OrdersByLastMessageTime()
    {
        // Arrange
        var userId = _testSender.UserId;
        var conversations = new List<Conversation>
        {
            new Conversation
            {
                ConversationId = "conv1",
                LastMessageTime = DateTime.UtcNow.AddHours(-1)
            },
            new Conversation
            {
                ConversationId = "conv2",
                LastMessageTime = DateTime.UtcNow
            }
        };

        _mockMessagingService
            .Setup(x => x.GetUserConversationsAsync(userId))
            .ReturnsAsync(ServiceResult<List<Conversation>>.SuccessResult(
                conversations.OrderByDescending(c => c.LastMessageTime).ToList()));

        // Act
        var result = await _mockMessagingService.Object.GetUserConversationsAsync(userId);

        // Assert
        result.Data.Should().BeInDescendingOrder(c => c.LastMessageTime);
    }

    #endregion

    #region GetOrCreateConversationAsync Tests

    [Fact]
    public async Task GetOrCreateConversationAsync_WithExistingConversation_ReturnsExisting()
    {
        // Arrange
        var user1Id = _testSender.UserId;
        var user2Id = _testReceiver.UserId;

        var existingConversation = new Conversation
        {
            ConversationId = "existing_conv",
            User1Id = user1Id,
            User2Id = user2Id
        };

        _mockMessagingService
            .Setup(x => x.GetOrCreateConversationAsync(user1Id, user2Id, null))
            .ReturnsAsync(ServiceResult<Conversation>.SuccessResult(existingConversation));

        // Act
        var result = await _mockMessagingService.Object.GetOrCreateConversationAsync(user1Id, user2Id);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Data.ConversationId.Should().Be("existing_conv");
    }

    [Fact]
    public async Task GetOrCreateConversationAsync_WithNewUsers_CreatesNew()
    {
        // Arrange
        var user1Id = _testSender.UserId;
        var user2Id = _testReceiver.UserId;

        var newConversation = new Conversation
        {
            ConversationId = "new_conv_123",
            User1Id = user1Id,
            User2Id = user2Id,
            LastMessage = "Konuþma baþladý",
            IsActive = true
        };

        _mockMessagingService
            .Setup(x => x.GetOrCreateConversationAsync(user1Id, user2Id, null))
            .ReturnsAsync(ServiceResult<Conversation>.SuccessResult(newConversation));

        // Act
        var result = await _mockMessagingService.Object.GetOrCreateConversationAsync(user1Id, user2Id);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.IsActive.Should().BeTrue();
    }

    #endregion

    #region MarkMessagesAsReadAsync Tests

    [Fact]
    public async Task MarkMessagesAsReadAsync_UpdatesUnreadCount()
    {
        // Arrange
        var conversationId = "conv123";
        var readerUserId = _testReceiver.UserId;

        _mockMessagingService
            .Setup(x => x.MarkMessagesAsReadAsync(conversationId, readerUserId))
            .ReturnsAsync(ServiceResult<bool>.SuccessResult(true));

        // Act
        var result = await _mockMessagingService.Object.MarkMessagesAsReadAsync(conversationId, readerUserId);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeTrue();
    }

    #endregion

    #region GetTotalUnreadMessageCountAsync Tests

    [Fact]
    public async Task GetTotalUnreadMessageCountAsync_ReturnsCorrectCount()
    {
        // Arrange
        var userId = _testSender.UserId;
        var unreadCount = 5;

        _mockMessagingService
            .Setup(x => x.GetTotalUnreadMessageCountAsync(userId))
            .ReturnsAsync(ServiceResult<int>.SuccessResult(unreadCount));

        // Act
        var result = await _mockMessagingService.Object.GetTotalUnreadMessageCountAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be(unreadCount);
    }

    [Fact]
    public async Task GetTotalUnreadMessageCountAsync_WithNoUnread_ReturnsZero()
    {
        // Arrange
        var userId = _testSender.UserId;

        _mockMessagingService
            .Setup(x => x.GetTotalUnreadMessageCountAsync(userId))
            .ReturnsAsync(ServiceResult<int>.SuccessResult(0));

        // Act
        var result = await _mockMessagingService.Object.GetTotalUnreadMessageCountAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be(0);
    }

    #endregion

    #region DeleteConversationAsync Tests

    [Fact]
    public async Task DeleteConversationAsync_MarksAsInactive()
    {
        // Arrange
        var conversationId = "conv123";
        var userId = _testSender.UserId;

        _mockMessagingService
            .Setup(x => x.DeleteConversationAsync(conversationId, userId))
            .ReturnsAsync(ServiceResult<bool>.SuccessResult(true, "Konuþma silindi"));

        // Act
        var result = await _mockMessagingService.Object.DeleteConversationAsync(conversationId, userId);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region UpdateUserInfoInMessagesAsync Tests

    [Fact]
    public async Task UpdateUserInfoInMessagesAsync_UpdatesSenderAndReceiverNames()
    {
        // Arrange
        var userId = _testSender.UserId;
        var newName = "Ahmet Yeni Ýsim";
        var newPhotoUrl = "https://example.com/new.jpg";

        _mockMessagingService
            .Setup(x => x.UpdateUserInfoInMessagesAsync(userId, newName, newPhotoUrl))
            .ReturnsAsync(ServiceResult<bool>.SuccessResult(true, "5 mesaj güncellendi"));

        // Act
        var result = await _mockMessagingService.Object.UpdateUserInfoInMessagesAsync(userId, newName, newPhotoUrl);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region UpdateUserInfoInConversationsAsync Tests

    [Fact]
    public async Task UpdateUserInfoInConversationsAsync_UpdatesUserInfo()
    {
        // Arrange
        var userId = _testSender.UserId;
        var newName = "Ahmet Yýlmaz";
        var newPhotoUrl = "https://example.com/new.jpg";

        _mockMessagingService
            .Setup(x => x.UpdateUserInfoInConversationsAsync(userId, newName, newPhotoUrl))
            .ReturnsAsync(ServiceResult<bool>.SuccessResult(true, "3 konuþma güncellendi"));

        // Act
        var result = await _mockMessagingService.Object.UpdateUserInfoInConversationsAsync(userId, newName, newPhotoUrl);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region Message Model Tests

    [Fact]
    public void Message_HasRequiredProperties()
    {
        // Arrange & Act
        var message = new Message
        {
            MessageId = "msg123",
            ConversationId = "conv123",
            SenderId = _testSender.UserId,
            ReceiverId = _testReceiver.UserId,
            Content = "Test message",
            Type = MessageType.Text,
            SentAt = DateTime.UtcNow
        };

        // Assert
        message.MessageId.Should().Be("msg123");
        message.Content.Should().Be("Test message");
        message.Type.Should().Be(MessageType.Text);
        message.IsDelivered.Should().BeFalse();
        message.IsRead.Should().BeFalse();
    }

    [Theory]
    [InlineData(MessageType.Text)]
    [InlineData(MessageType.Image)]
    [InlineData(MessageType.System)]
    public void Message_SupportsAllTypes(MessageType type)
    {
        // Arrange & Act
        var message = new Message { Type = type };

        // Assert
        message.Type.Should().Be(type);
    }

    #endregion

    #region Conversation Model Tests

    [Fact]
    public void Conversation_GetOtherUserId_ReturnsCorrectUser()
    {
        // Arrange
        var conversation = new Conversation
        {
            User1Id = _testSender.UserId,
            User2Id = _testReceiver.UserId
        };

        // Act
        var otherUserId = conversation.GetOtherUserId(_testSender.UserId);

        // Assert
        otherUserId.Should().Be(_testReceiver.UserId);
    }

    [Fact]
    public void Conversation_GetUnreadCount_ReturnsCorrectCount()
    {
        // Arrange
        var conversation = new Conversation
        {
            User1Id = _testSender.UserId,
            User2Id = _testReceiver.UserId,
            UnreadCountUser1 = 3,
            UnreadCountUser2 = 5
        };

        // Act
        var unreadForUser1 = conversation.GetUnreadCount(_testSender.UserId);
        var unreadForUser2 = conversation.GetUnreadCount(_testReceiver.UserId);

        // Assert
        unreadForUser1.Should().Be(3);
        unreadForUser2.Should().Be(5);
    }

    #endregion
}
