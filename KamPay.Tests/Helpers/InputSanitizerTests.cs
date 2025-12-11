using KamPay.Helpers;

namespace KamPay.Tests.Helpers;

/// <summary>
/// InputSanitizer helper testleri
/// XSS, SQL Injection, Email validasyonu ve text sanitization testleri
/// </summary>
public class InputSanitizerTests
{
    #region ContainsDangerousContent Tests

    [Theory]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("<iframe src='evil.com'></iframe>")]
    [InlineData("javascript:alert(1)")]
    [InlineData("<svg onload=alert(1)>")]
    public void ContainsDangerousContent_WithXSSPatterns_ReturnsTrue(string input)
    {
        // Act
        var result = InputSanitizer.ContainsDangerousContent(input);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("'; DROP TABLE users--")]
    [InlineData("1' OR '1'='1")]
    [InlineData("admin'--")]
    [InlineData("' UNION SELECT * FROM passwords--")]
    public void ContainsDangerousContent_WithSQLInjectionPatterns_ReturnsTrue(string input)
    {
        // Act
        var result = InputSanitizer.ContainsDangerousContent(input);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("Normal metin")]
    [InlineData("Ahmet Yýlmaz")]
    [InlineData("Bu bir test açýklamasýdýr.")]
    [InlineData("123456")]
    public void ContainsDangerousContent_WithSafeContent_ReturnsFalse(string input)
    {
        // Act
        var result = InputSanitizer.ContainsDangerousContent(input);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ContainsDangerousContent_WithNullOrEmpty_ReturnsFalse(string input)
    {
        // Act
        var result = InputSanitizer.ContainsDangerousContent(input);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region IsValidEmail Tests

    [Theory]
    [InlineData("test@example.com")]
    [InlineData("user.name@domain.com")]
    [InlineData("test+tag@example.co.uk")]
    [InlineData("ahmet@bartin.edu.tr")]
    public void IsValidEmail_WithValidEmails_ReturnsTrue(string email)
    {
        // Act
        var result = InputSanitizer.IsValidEmail(email);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("@example.com")]
    [InlineData("test@")]
    [InlineData("test..name@example.com")]
    [InlineData("test name@example.com")]
    [InlineData("")]
    [InlineData(null)]
    public void IsValidEmail_WithInvalidEmails_ReturnsFalse(string email)
    {
        // Act
        var result = InputSanitizer.IsValidEmail(email);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region SanitizeText Tests

    [Theory]
    [InlineData("<script>alert('xss')</script>", "alert('xss')")]
    [InlineData("<b>Bold</b> text", "Bold text")]
    [InlineData("Normal text", "Normal text")]
    public void SanitizeText_RemovesHtmlTags(string input, string expected)
    {
        // Act
        var result = InputSanitizer.SanitizeText(input);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void SanitizeText_WithNull_ReturnsNull()
    {
        // Act
        var result = InputSanitizer.SanitizeText(null);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region IsValidLength Tests

    [Theory]
    [InlineData("Test", 1, 10, true)]
    [InlineData("Test", 5, 10, false)] // Çok kýsa
    [InlineData("Test", 1, 3, false)] // Çok uzun
    [InlineData("", 0, 10, true)] // Boþ izin verilirse
    [InlineData(null, 0, 10, true)] // Null izin verilirse
    public void IsValidLength_ChecksLengthCorrectly(string input, int min, int max, bool expected)
    {
        // Act
        var result = InputSanitizer.IsValidLength(input, min, max);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region NormalizeWhitespace Tests

    [Theory]
    [InlineData("   Multiple   Spaces   ", "Multiple Spaces")]
    [InlineData("Test\n\nNewlines", "Test Newlines")]
    [InlineData("  Leading and trailing  ", "Leading and trailing")]
    public void NormalizeWhitespace_RemovesExtraSpaces(string input, string expected)
    {
        // Act
        var result = InputSanitizer.NormalizeWhitespace(input);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void NormalizeWhitespace_WithNull_ReturnsNull()
    {
        // Act
        var result = InputSanitizer.NormalizeWhitespace(null);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region IsValidUrl Tests

    [Theory]
    [InlineData("https://www.example.com", true)]
    [InlineData("http://test.com", true)]
    [InlineData("ftp://files.example.com", true)]
    [InlineData("invalid-url", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidUrl_ValidatesUrlsCorrectly(string url, bool expected)
    {
        // Act
        var result = InputSanitizer.IsValidUrl(url);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region SanitizeUsername Tests

    [Theory]
    [InlineData("ahmet_yilmaz", "ahmet_yilmaz")]
    [InlineData("Ahmet Yýlmaz", "ahmet_yilmaz")]
    [InlineData("test@user#123", "test_user_123")]
    [InlineData("   user   ", "user")]
    public void SanitizeUsername_CleansUsername(string input, string expected)
    {
        // Act
        var result = InputSanitizer.SanitizeUsername(input);

        // Assert
        result.Should().Be(expected);
    }

    #endregion
}
