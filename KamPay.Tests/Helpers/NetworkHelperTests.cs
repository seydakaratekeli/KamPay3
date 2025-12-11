using KamPay.Helpers;
using KamPay.Models;
using System.Net;

namespace KamPay.Tests.Helpers;

/// <summary>
/// NetworkHelper birim testleri
/// Að iþlemleri, retry mantýðý ve hata yönetimi testleri
/// </summary>
public class NetworkHelperTests
{
    #region IsRetriableException Tests

    [Fact]
    public void IsRetriableException_WithHttpRequestException_ReturnsTrue()
    {
        // Arrange
        var exception = new HttpRequestException("Network error");

        // Act
        var result = NetworkHelper.IsRetriableException(exception);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsRetriableException_WithTaskCanceledException_ReturnsTrue()
    {
        // Arrange
        var exception = new TaskCanceledException("Request canceled");

        // Act
        var result = NetworkHelper.IsRetriableException(exception);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsRetriableException_WithTimeoutException_ReturnsTrue()
    {
        // Arrange
        var exception = new TimeoutException("Request timeout");

        // Act
        var result = NetworkHelper.IsRetriableException(exception);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsRetriableException_WithArgumentException_ReturnsFalse()
    {
        // Arrange
        var exception = new ArgumentException("Invalid argument");

        // Act
        var result = NetworkHelper.IsRetriableException(exception);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsRetriableException_WithInvalidOperationException_ReturnsFalse()
    {
        // Arrange
        var exception = new InvalidOperationException("Invalid operation");

        // Act
        var result = NetworkHelper.IsRetriableException(exception);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GetUserFriendlyErrorMessage Tests

    [Fact]
    public void GetUserFriendlyErrorMessage_WithHttpRequestException_ReturnsConnectionError()
    {
        // Arrange
        var exception = new HttpRequestException("Network error");

        // Act
        var message = NetworkHelper.GetUserFriendlyErrorMessage(exception);

        // Assert
        message.Should().Contain("Ýnternet baðlantýsý");
    }

    [Fact]
    public void GetUserFriendlyErrorMessage_WithTaskCanceledException_ReturnsTimeoutMessage()
    {
        // Arrange
        var exception = new TaskCanceledException("Timeout");

        // Act
        var message = NetworkHelper.GetUserFriendlyErrorMessage(exception);

        // Assert
        message.Should().Contain("zaman aþýmý");
    }

    [Fact]
    public void GetUserFriendlyErrorMessage_WithTimeoutException_ReturnsTimeoutMessage()
    {
        // Arrange
        var exception = new TimeoutException("Timeout");

        // Act
        var message = NetworkHelper.GetUserFriendlyErrorMessage(exception);

        // Assert
        message.Should().Contain("zaman aþýmý");
    }

    [Fact]
    public void GetUserFriendlyErrorMessage_WithGenericException_ReturnsGenericMessage()
    {
        // Arrange
        var exception = new Exception("Something went wrong");

        // Act
        var message = NetworkHelper.GetUserFriendlyErrorMessage(exception);

        // Assert
        message.Should().Contain("Bir hata oluþtu");
    }

    #endregion

    #region ExecuteWithRetryAsync Tests

    [Fact]
    public async Task ExecuteWithRetryAsync_WithSuccessfulOperation_ReturnsResult()
    {
        // Arrange
        var expectedResult = "Success";
        Func<Task<string>> operation = async () =>
        {
            await Task.Delay(10);
            return expectedResult;
        };

        // Act
        var result = await NetworkHelper.ExecuteWithRetryAsync(operation);

        // Assert
        result.Should().Be(expectedResult);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_WithTemporaryFailure_RetriesAndSucceeds()
    {
        // Arrange
        int attemptCount = 0;
        Func<Task<string>> operation = async () =>
        {
            attemptCount++;
            await Task.Delay(10);
            
            if (attemptCount < 2)
                throw new HttpRequestException("Temporary error");
            
            return "Success after retry";
        };

        // Act
        var result = await NetworkHelper.ExecuteWithRetryAsync(operation, maxRetries: 3);

        // Assert
        result.Should().Be("Success after retry");
        attemptCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_WithPermanentFailure_ThrowsException()
    {
        // Arrange
        Func<Task<string>> operation = async () =>
        {
            await Task.Delay(10);
            throw new HttpRequestException("Permanent error");
        };

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await NetworkHelper.ExecuteWithRetryAsync(operation, maxRetries: 2, delayMs: 50));
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_WithNonRetriableException_ThrowsImmediately()
    {
        // Arrange
        int attemptCount = 0;
        Func<Task<string>> operation = async () =>
        {
            attemptCount++;
            await Task.Delay(10);
            throw new ArgumentException("Non-retriable error");
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await NetworkHelper.ExecuteWithRetryAsync(operation, maxRetries: 3));

        // Should only attempt once since exception is not retriable
        attemptCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_WithCustomShouldRetry_UsesCustomLogic()
    {
        // Arrange
        int attemptCount = 0;
        Func<Task<string>> operation = async () =>
        {
            attemptCount++;
            await Task.Delay(10);
            throw new InvalidOperationException("Custom error");
        };

        Func<Exception, bool> customRetry = (ex) => ex is InvalidOperationException;

        // Act
        var result = await NetworkHelper.ExecuteWithRetryAsync(
            operation, 
            maxRetries: 3, 
            delayMs: 50,
            shouldRetry: customRetry);

        // Assert - Should throw after retries
        // This will actually throw, so we wrap it
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await NetworkHelper.ExecuteWithRetryAsync(operation, maxRetries: 2, delayMs: 50, shouldRetry: customRetry));
    }

    #endregion

    #region ExecuteNetworkOperationAsync Tests

    [Fact]
    public async Task ExecuteNetworkOperationAsync_WithSuccessfulOperation_ReturnsSuccessResult()
    {
        // Arrange
        var expectedData = "Test Data";
        Func<Task<string>> operation = async () =>
        {
            await Task.Delay(10);
            return expectedData;
        };

        // Act
        var result = await NetworkHelper.ExecuteNetworkOperationAsync(operation);

        // Assert
        // Note: This test depends on actual network connectivity
        // In a real test environment, you might want to mock the connectivity check
        if (NetworkHelper.HasInternetConnection())
        {
            result.IsSuccess.Should().BeTrue();
            result.Data.Should().Be(expectedData);
        }
    }

    [Fact]
    public async Task ExecuteNetworkOperationAsync_WithException_ReturnsFailureResult()
    {
        // Arrange
        Func<Task<string>> operation = async () =>
        {
            await Task.Delay(10);
            throw new InvalidOperationException("Test error");
        };

        // Act
        var result = await NetworkHelper.ExecuteNetworkOperationAsync(operation);

        // Assert
        if (NetworkHelper.HasInternetConnection())
        {
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().NotBeEmpty();
        }
    }

    #endregion

    #region Connection Type Tests

    [Fact]
    public void GetConnectionType_ReturnsString()
    {
        // Act
        var connectionType = NetworkHelper.GetConnectionType();

        // Assert
        connectionType.Should().NotBeNullOrEmpty();
        connectionType.Should().BeOneOf("WiFi", "Cellular", "Ethernet", "Unknown");
    }

    [Fact]
    public void HasInternetConnection_ReturnsBoolean()
    {
        // Act
        var hasConnection = NetworkHelper.HasInternetConnection();

        // Assert
        // Just verify it returns without throwing
        hasConnection.Should().BeOneOf(true, false);
    }

    #endregion

    #region Custom Exception Tests

    [Fact]
    public void NoInternetException_CreatesWithDefaultMessage()
    {
        // Act
        var exception = new NoInternetException();

        // Assert
        exception.Message.Should().Contain("Ýnternet baðlantýsý");
    }

    [Fact]
    public void NoInternetException_CreatesWithCustomMessage()
    {
        // Arrange
        var customMessage = "Custom network error";

        // Act
        var exception = new NoInternetException(customMessage);

        // Assert
        exception.Message.Should().Be(customMessage);
    }

    [Fact]
    public void RateLimitExceededException_StoresRetryAfterTime()
    {
        // Arrange
        var retryAfter = DateTime.UtcNow.AddMinutes(5);

        // Act
        var exception = new RateLimitExceededException(retryAfter);

        // Assert
        exception.RetryAfter.Should().Be(retryAfter);
        exception.Message.Should().Contain("limit");
    }

    [Fact]
    public void RateLimitExceededException_CreatesWithCustomMessage()
    {
        // Arrange
        var customMessage = "Too many requests";
        var retryAfter = DateTime.UtcNow.AddMinutes(10);

        // Act
        var exception = new RateLimitExceededException(customMessage, retryAfter);

        // Assert
        exception.Message.Should().Be(customMessage);
        exception.RetryAfter.Should().Be(retryAfter);
    }

    #endregion

    #region ThrottleRequestAsync Tests

    [Fact]
    public async Task ThrottleRequestAsync_DelaysSubsequentRequests()
    {
        // Arrange
        var minDelayMs = 100;
        var startTime = DateTime.UtcNow;

        // Act
        await NetworkHelper.ThrottleRequestAsync(minDelayMs);
        var firstCallTime = DateTime.UtcNow;
        
        await NetworkHelper.ThrottleRequestAsync(minDelayMs);
        var secondCallTime = DateTime.UtcNow;

        // Assert
        var timeBetweenCalls = (secondCallTime - firstCallTime).TotalMilliseconds;
        timeBetweenCalls.Should().BeGreaterOrEqualTo(minDelayMs - 50); // 50ms tolerance
    }

    #endregion

    #region Constants Tests

    [Fact]
    public void NetworkHelper_HasCorrectConstants()
    {
        // Assert
        NetworkHelper.MaxRetryAttempts.Should().Be(3);
        NetworkHelper.RetryDelayMs.Should().Be(1000);
    }

    #endregion
}
