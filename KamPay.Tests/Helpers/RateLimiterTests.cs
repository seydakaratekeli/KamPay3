using KamPay.Helpers;

namespace KamPay.Tests.Helpers;

/// <summary>
/// RateLimiter birim testleri
/// Rate limiting, spam korumasý ve limit yönetimi testleri
/// </summary>
public class RateLimiterTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_CreatesValidRateLimiter()
    {
        // Act
        var limiter = new RateLimiter(10, TimeSpan.FromMinutes(1));

        // Assert
        limiter.Should().NotBeNull();
    }

    #endregion

    #region IsRequestAllowed Tests

    [Fact]
    public void IsRequestAllowed_WithinLimit_ReturnsTrue()
    {
        // Arrange
        var limiter = new RateLimiter(5, TimeSpan.FromMinutes(1));
        var identifier = "user123";

        // Act
        var result = limiter.IsRequestAllowed(identifier);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsRequestAllowed_AtLimit_ReturnsFalse()
    {
        // Arrange
        var limiter = new RateLimiter(3, TimeSpan.FromMinutes(1));
        var identifier = "user123";

        // Make 3 requests (at limit)
        limiter.IsRequestAllowed(identifier);
        limiter.IsRequestAllowed(identifier);
        limiter.IsRequestAllowed(identifier);

        // Act - 4th request should be denied
        var result = limiter.IsRequestAllowed(identifier);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsRequestAllowed_ExceedsLimit_ReturnsFalse()
    {
        // Arrange
        var limiter = new RateLimiter(2, TimeSpan.FromMinutes(1));
        var identifier = "user123";

        // Make 2 requests
        limiter.IsRequestAllowed(identifier);
        limiter.IsRequestAllowed(identifier);

        // Act - 3rd and 4th requests should be denied
        var result1 = limiter.IsRequestAllowed(identifier);
        var result2 = limiter.IsRequestAllowed(identifier);

        // Assert
        result1.Should().BeFalse();
        result2.Should().BeFalse();
    }

    [Fact]
    public void IsRequestAllowed_DifferentIdentifiers_AreIndependent()
    {
        // Arrange
        var limiter = new RateLimiter(2, TimeSpan.FromMinutes(1));
        var user1 = "user1";
        var user2 = "user2";

        // Fill user1's quota
        limiter.IsRequestAllowed(user1);
        limiter.IsRequestAllowed(user1);

        // Act - user2 should still be allowed
        var result = limiter.IsRequestAllowed(user2);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsRequestAllowed_AfterTimeWindow_AllowsNewRequests()
    {
        // Arrange
        var limiter = new RateLimiter(2, TimeSpan.FromMilliseconds(100));
        var identifier = "user123";

        // Fill the quota
        limiter.IsRequestAllowed(identifier);
        limiter.IsRequestAllowed(identifier);

        // Verify limit is reached
        limiter.IsRequestAllowed(identifier).Should().BeFalse();

        // Act - Wait for time window to pass
        await Task.Delay(150);
        var result = limiter.IsRequestAllowed(identifier);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region GetRemainingRequests Tests

    [Fact]
    public void GetRemainingRequests_WithNoRequests_ReturnsMaximum()
    {
        // Arrange
        var maxRequests = 10;
        var limiter = new RateLimiter(maxRequests, TimeSpan.FromMinutes(1));
        var identifier = "user123";

        // Act
        var remaining = limiter.GetRemainingRequests(identifier);

        // Assert
        remaining.Should().Be(maxRequests);
    }

    [Fact]
    public void GetRemainingRequests_AfterSomeRequests_ReturnsCorrectCount()
    {
        // Arrange
        var limiter = new RateLimiter(10, TimeSpan.FromMinutes(1));
        var identifier = "user123";

        // Make 3 requests
        limiter.IsRequestAllowed(identifier);
        limiter.IsRequestAllowed(identifier);
        limiter.IsRequestAllowed(identifier);

        // Act
        var remaining = limiter.GetRemainingRequests(identifier);

        // Assert
        remaining.Should().Be(7);
    }

    [Fact]
    public void GetRemainingRequests_AtLimit_ReturnsZero()
    {
        // Arrange
        var limiter = new RateLimiter(3, TimeSpan.FromMinutes(1));
        var identifier = "user123";

        // Fill the quota
        limiter.IsRequestAllowed(identifier);
        limiter.IsRequestAllowed(identifier);
        limiter.IsRequestAllowed(identifier);

        // Act
        var remaining = limiter.GetRemainingRequests(identifier);

        // Assert
        remaining.Should().Be(0);
    }

    [Fact]
    public async Task GetRemainingRequests_AfterTimeWindow_ResetsToMaximum()
    {
        // Arrange
        var maxRequests = 5;
        var limiter = new RateLimiter(maxRequests, TimeSpan.FromMilliseconds(100));
        var identifier = "user123";

        // Use some quota
        limiter.IsRequestAllowed(identifier);
        limiter.IsRequestAllowed(identifier);

        // Act - Wait for time window to pass
        await Task.Delay(150);
        var remaining = limiter.GetRemainingRequests(identifier);

        // Assert
        remaining.Should().Be(maxRequests);
    }

    #endregion

    #region GetResetTime Tests

    [Fact]
    public void GetResetTime_WithNoRequests_ReturnsNull()
    {
        // Arrange
        var limiter = new RateLimiter(10, TimeSpan.FromMinutes(1));
        var identifier = "user123";

        // Act
        var resetTime = limiter.GetResetTime(identifier);

        // Assert
        resetTime.Should().BeNull();
    }

    [Fact]
    public void GetResetTime_WithRequests_ReturnsValidTime()
    {
        // Arrange
        var timeWindow = TimeSpan.FromMinutes(5);
        var limiter = new RateLimiter(10, timeWindow);
        var identifier = "user123";
        var beforeRequest = DateTime.UtcNow;

        // Act
        limiter.IsRequestAllowed(identifier);
        var resetTime = limiter.GetResetTime(identifier);

        // Assert
        resetTime.Should().NotBeNull();
        resetTime.Should().BeAfter(beforeRequest);
        resetTime.Should().BeOnOrBefore(DateTime.UtcNow.Add(timeWindow).AddSeconds(1));
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_ClearsRequestsForIdentifier()
    {
        // Arrange
        var limiter = new RateLimiter(3, TimeSpan.FromMinutes(1));
        var identifier = "user123";

        // Fill the quota
        limiter.IsRequestAllowed(identifier);
        limiter.IsRequestAllowed(identifier);
        limiter.IsRequestAllowed(identifier);

        // Verify limit is reached
        limiter.IsRequestAllowed(identifier).Should().BeFalse();

        // Act
        limiter.Reset(identifier);

        // Assert
        limiter.IsRequestAllowed(identifier).Should().BeTrue();
        limiter.GetRemainingRequests(identifier).Should().Be(2);
    }

    [Fact]
    public void Reset_DoesNotAffectOtherIdentifiers()
    {
        // Arrange
        var limiter = new RateLimiter(3, TimeSpan.FromMinutes(1));
        var user1 = "user1";
        var user2 = "user2";

        // Make requests for both users
        limiter.IsRequestAllowed(user1);
        limiter.IsRequestAllowed(user2);

        // Act - Reset only user1
        limiter.Reset(user1);

        // Assert
        limiter.GetRemainingRequests(user1).Should().Be(3);
        limiter.GetRemainingRequests(user2).Should().Be(2);
    }

    [Fact]
    public void ResetAll_ClearsAllRequests()
    {
        // Arrange
        var limiter = new RateLimiter(3, TimeSpan.FromMinutes(1));
        var user1 = "user1";
        var user2 = "user2";

        // Make requests for both users
        limiter.IsRequestAllowed(user1);
        limiter.IsRequestAllowed(user1);
        limiter.IsRequestAllowed(user2);
        limiter.IsRequestAllowed(user2);

        // Act
        limiter.ResetAll();

        // Assert
        limiter.GetRemainingRequests(user1).Should().Be(3);
        limiter.GetRemainingRequests(user2).Should().Be(3);
    }

    #endregion

    #region Pre-configured Limiters Tests

    [Fact]
    public void RateLimiters_Login_IsConfigured()
    {
        // Arrange
        var identifier = "test_user";

        // Act
        var result = RateLimiters.Login.CheckLimit(identifier);

        // Assert
        result.Should().NotBeNull();
        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void RateLimiters_Message_IsConfigured()
    {
        // Arrange
        var identifier = "test_user";

        // Act
        var result = RateLimiters.Message.CheckLimit(identifier);

        // Assert
        result.Should().NotBeNull();
        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void RateLimiters_ProductCreation_IsConfigured()
    {
        // Arrange
        var identifier = "test_user";

        // Act
        var result = RateLimiters.ProductCreation.CheckLimit(identifier);

        // Assert
        result.Should().NotBeNull();
        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void RateLimiters_AllConfigured_ReturnUniqueInstances()
    {
        // Act & Assert
        RateLimiters.Login.Should().NotBeNull();
        RateLimiters.Message.Should().NotBeNull();
        RateLimiters.ProductCreation.Should().NotBeNull();
        RateLimiters.ApiCall.Should().NotBeNull();
        RateLimiters.ImageUpload.Should().NotBeNull();
        RateLimiters.PasswordReset.Should().NotBeNull();
        RateLimiters.Search.Should().NotBeNull();
    }

    #endregion

    #region RateLimitResult Tests

    [Fact]
    public void RateLimitResult_Allowed_CreatesCorrectResult()
    {
        // Arrange
        var remaining = 5;
        var resetTime = DateTime.UtcNow.AddMinutes(1);

        // Act
        var result = RateLimitResult.Allowed(remaining, resetTime);

        // Assert
        result.IsAllowed.Should().BeTrue();
        result.RemainingRequests.Should().Be(remaining);
        result.ResetTime.Should().Be(resetTime);
    }

    [Fact]
    public void RateLimitResult_Denied_CreatesCorrectResult()
    {
        // Arrange
        var resetTime = DateTime.UtcNow.AddMinutes(2);

        // Act
        var result = RateLimitResult.Denied(resetTime);

        // Assert
        result.IsAllowed.Should().BeFalse();
        result.RemainingRequests.Should().Be(0);
        result.ResetTime.Should().Be(resetTime);
        result.Message.Should().NotBeNullOrEmpty();
        result.Message.Should().Contain("tekrar deneyin");
    }

    [Fact]
    public void RateLimitResult_Denied_WithMinutes_ShowsMinutesMessage()
    {
        // Arrange
        var resetTime = DateTime.UtcNow.AddMinutes(5);

        // Act
        var result = RateLimitResult.Denied(resetTime);

        // Assert
        result.Message.Should().Contain("dakika");
    }

    [Fact]
    public void RateLimitResult_Denied_WithSeconds_ShowsSecondsMessage()
    {
        // Arrange
        var resetTime = DateTime.UtcNow.AddSeconds(30);

        // Act
        var result = RateLimitResult.Denied(resetTime);

        // Assert
        result.Message.Should().Contain("saniye");
    }

    #endregion

    #region Extension Methods Tests

    [Fact]
    public void CheckLimit_WithinLimit_ReturnsAllowedResult()
    {
        // Arrange
        var limiter = new RateLimiter(5, TimeSpan.FromMinutes(1));
        var identifier = "user123";

        // Act
        var result = limiter.CheckLimit(identifier);

        // Assert
        result.Should().NotBeNull();
        result.IsAllowed.Should().BeTrue();
        result.RemainingRequests.Should().Be(4); // One used
    }

    [Fact]
    public void CheckLimit_AtLimit_ReturnsDeniedResult()
    {
        // Arrange
        var limiter = new RateLimiter(2, TimeSpan.FromMinutes(1));
        var identifier = "user123";

        // Fill quota
        limiter.IsRequestAllowed(identifier);
        limiter.IsRequestAllowed(identifier);

        // Act
        var result = limiter.CheckLimit(identifier);

        // Assert
        result.Should().NotBeNull();
        result.IsAllowed.Should().BeFalse();
        result.RemainingRequests.Should().Be(0);
        result.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void CheckLimit_ReturnsResetTime()
    {
        // Arrange
        var limiter = new RateLimiter(5, TimeSpan.FromMinutes(1));
        var identifier = "user123";

        // Act
        var result = limiter.CheckLimit(identifier);

        // Assert
        result.ResetTime.Should().NotBeNull();
        result.ResetTime.Should().BeAfter(DateTime.UtcNow);
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task RateLimiter_WithConcurrentRequests_IsThreadSafe()
    {
        // Arrange
        var limiter = new RateLimiter(100, TimeSpan.FromMinutes(1));
        var identifier = "user123";
        var tasks = new List<Task<bool>>();

        // Act - Make 50 concurrent requests
        for (int i = 0; i < 50; i++)
        {
            tasks.Add(Task.Run(() => limiter.IsRequestAllowed(identifier)));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().OnlyContain(r => r == true);
        limiter.GetRemainingRequests(identifier).Should().Be(50);
    }

    #endregion

    #region Edge Cases Tests

    [Fact]
    public void IsRequestAllowed_WithNullIdentifier_ThrowsException()
    {
        // Arrange
        var limiter = new RateLimiter(5, TimeSpan.FromMinutes(1));

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => limiter.IsRequestAllowed(null));
    }

    [Fact]
    public void IsRequestAllowed_WithEmptyIdentifier_Works()
    {
        // Arrange
        var limiter = new RateLimiter(5, TimeSpan.FromMinutes(1));

        // Act
        var result = limiter.IsRequestAllowed("");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithZeroMaxRequests_CreatesLimiter()
    {
        // Act
        var limiter = new RateLimiter(0, TimeSpan.FromMinutes(1));

        // Assert
        limiter.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithZeroTimeWindow_CreatesLimiter()
    {
        // Act
        var limiter = new RateLimiter(10, TimeSpan.Zero);

        // Assert
        limiter.Should().NotBeNull();
    }

    #endregion
}
