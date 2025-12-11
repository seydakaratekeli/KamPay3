using KamPay.Helpers;
using KamPay.Models;
using KamPay.Services;

namespace KamPay.Tests.Services;

/// <summary>
/// FirebaseAuthService birim testleri
/// Kayýt, giriþ, validasyon ve güvenlik kontrolleri test edilir
/// </summary>
public class FirebaseAuthServiceTests
{
    private readonly Mock<IEmailService> _mockEmailService;
    private readonly FirebaseAuthService _authService;

    public FirebaseAuthServiceTests()
    {
        _mockEmailService = new Mock<IEmailService>();
        _authService = new FirebaseAuthService(_mockEmailService.Object);
    }

    #region ValidateRegistration Tests

    [Fact]
    public void ValidateRegistration_WithValidData_ReturnsSuccess()
    {
        // Arrange
        var request = new RegisterRequest
        {
            FirstName = "Ahmet",
            LastName = "Yýlmaz",
            Email = "ahmet.yilmaz@bartin.edu.tr",
            Password = "Test123456",
            PasswordConfirm = "Test123456"
        };

        // Act
        var result = _authService.ValidateRegistration(request);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("", "Yýlmaz", "ahmet@bartin.edu.tr", "Test123456", "Test123456")] // Boþ ad
    [InlineData("A", "Yýlmaz", "ahmet@bartin.edu.tr", "Test123456", "Test123456")] // Çok kýsa ad
    [InlineData("Ahmet", "", "ahmet@bartin.edu.tr", "Test123456", "Test123456")] // Boþ soyad
    [InlineData("Ahmet", "Y", "ahmet@bartin.edu.tr", "Test123456", "Test123456")] // Çok kýsa soyad
    public void ValidateRegistration_WithInvalidName_ReturnsError(
        string firstName, string lastName, string email, string password, string passwordConfirm)
    {
        // Arrange
        var request = new RegisterRequest
        {
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            Password = password,
            PasswordConfirm = passwordConfirm
        };

        // Act
        var result = _authService.ValidateRegistration(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("")] // Boþ email
    [InlineData("invalidmail")] // Geçersiz format
    [InlineData("test@gmail.com")] // Yanlýþ domain
    [InlineData("test@bartin.com")] // Eksik domain
    public void ValidateRegistration_WithInvalidEmail_ReturnsError(string email)
    {
        // Arrange
        var request = new RegisterRequest
        {
            FirstName = "Ahmet",
            LastName = "Yýlmaz",
            Email = email,
            Password = "Test123456",
            PasswordConfirm = "Test123456"
        };

        // Act
        var result = _authService.ValidateRegistration(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("e-posta") || e.Contains("email"));
    }

    [Theory]
    [InlineData("")] // Boþ þifre
    [InlineData("123")] // Çok kýsa
    [InlineData("test1234")] // Büyük harf yok
    [InlineData("TEST1234")] // Küçük harf yok
    [InlineData("TestTest")] // Rakam yok
    public void ValidateRegistration_WithInvalidPassword_ReturnsError(string password)
    {
        // Arrange
        var request = new RegisterRequest
        {
            FirstName = "Ahmet",
            LastName = "Yýlmaz",
            Email = "ahmet@bartin.edu.tr",
            Password = password,
            PasswordConfirm = password
        };

        // Act
        var result = _authService.ValidateRegistration(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Þifre") || e.Contains("þifre"));
    }

    [Fact]
    public void ValidateRegistration_WithMismatchedPasswords_ReturnsError()
    {
        // Arrange
        var request = new RegisterRequest
        {
            FirstName = "Ahmet",
            LastName = "Yýlmaz",
            Email = "ahmet@bartin.edu.tr",
            Password = "Test123456",
            PasswordConfirm = "Different123"
        };

        // Act
        var result = _authService.ValidateRegistration(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Þifreler eþleþmiyor");
    }

    [Theory]
    [InlineData("<script>alert('xss')</script>", "Yýlmaz")] // XSS ad
    [InlineData("Ahmet", "<img src=x>")] // XSS soyad
    [InlineData("'; DROP TABLE users--", "Yýlmaz")] // SQL Injection
    public void ValidateRegistration_WithDangerousContent_ReturnsError(string firstName, string lastName)
    {
        // Arrange
        var request = new RegisterRequest
        {
            FirstName = firstName,
            LastName = lastName,
            Email = "test@bartin.edu.tr",
            Password = "Test123456",
            PasswordConfirm = "Test123456"
        };

        // Act
        var result = _authService.ValidateRegistration(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("geçersiz karakterler"));
    }

    #endregion

    #region ValidateLogin Tests

    [Fact]
    public void ValidateLogin_WithValidData_ReturnsSuccess()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "test@bartin.edu.tr",
            Password = "Test123456"
        };

        // Act
        var result = _authService.ValidateLogin(request);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("", "Test123456")] // Boþ email
    [InlineData("test@bartin.edu.tr", "")] // Boþ þifre
    [InlineData("", "")] // Her ikisi de boþ
    public void ValidateLogin_WithMissingCredentials_ReturnsError(string email, string password)
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = email,
            Password = password
        };

        // Act
        var result = _authService.ValidateLogin(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    #endregion

    #region Helper Method Tests

    [Fact]
    public void IsUserLoggedIn_WhenNoUserLoggedIn_ReturnsFalse()
    {
        // Act
        var result = _authService.IsUserLoggedIn();

        // Assert (yeni instance'da kullanýcý olmaz)
        result.Should().BeFalse();
    }

    #endregion
}
