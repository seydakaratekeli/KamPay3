using KamPay.Models;
using KamPay.Services;
using KamPay.ViewModels;

namespace KamPay.Tests.ViewModels;

/// <summary>
/// LoginViewModel birim testleri
/// Giriþ iþlemleri, validasyon ve hata yönetimi testleri
/// </summary>
public class LoginViewModelTests
{
    private readonly Mock<IAuthenticationService> _mockAuthService;
    private readonly LoginViewModel _viewModel;

    public LoginViewModelTests()
    {
        _mockAuthService = new Mock<IAuthenticationService>();
        _viewModel = new LoginViewModel(_mockAuthService.Object);
    }

    [Fact]
    public void Constructor_InitializesProperties()
    {
        // Assert
        _viewModel.Email.Should().BeNullOrEmpty();
        _viewModel.Password.Should().BeNullOrEmpty();
        _viewModel.RememberMe.Should().BeFalse();
        _viewModel.IsLoading.Should().BeFalse();
    }

    [Fact]
    public void ClearCredentials_ClearsAllFields()
    {
        // Arrange
        _viewModel.Email = "test@example.com";
        _viewModel.Password = "password123";
        _viewModel.ErrorMessage = "Bir hata";

        // Act
        _viewModel.ClearCredentials();

        // Assert
        _viewModel.Email.Should().BeEmpty();
        _viewModel.Password.Should().BeEmpty();
        _viewModel.ErrorMessage.Should().BeEmpty();
    }

    [Fact]
    public async Task LoginAsync_WithEmptyEmail_ShowsError()
    {
        // Arrange
        _viewModel.Email = "";
        _viewModel.Password = "Test123456";

        var validationResult = new ValidationResult();
        validationResult.AddError("E-posta alaný boþ býrakýlamaz");

        _mockAuthService
            .Setup(x => x.LoginAsync(It.IsAny<LoginRequest>()))
            .ReturnsAsync(ServiceResult<User>.FailureResult("Giriþ bilgileri geçersiz", validationResult.Errors.ToArray()));

        // Act
        await _viewModel.LoginCommand.ExecuteAsync(null);

        // Assert
        _viewModel.ErrorMessage.Should().NotBeEmpty();
        _viewModel.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_CallsAuthService()
    {
        // Arrange
        _viewModel.Email = "test@bartin.edu.tr";
        _viewModel.Password = "Test123456";
        _viewModel.RememberMe = true;

        var mockUser = new User
        {
            UserId = "123",
            Email = "test@bartin.edu.tr",
            FirstName = "Test",
            LastName = "User"
        };

        _mockAuthService
            .Setup(x => x.LoginAsync(It.Is<LoginRequest>(r =>
                r.Email == _viewModel.Email &&
                r.Password == _viewModel.Password &&
                r.RememberMe == true)))
            .ReturnsAsync(ServiceResult<User>.SuccessResult(mockUser, "Giriþ baþarýlý"));

        // Act
        await _viewModel.LoginCommand.ExecuteAsync(null);

        // Assert
        _mockAuthService.Verify(x => x.LoginAsync(It.IsAny<LoginRequest>()), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_WithFailedLogin_ShowsErrorMessage()
    {
        // Arrange
        _viewModel.Email = "wrong@bartin.edu.tr";
        _viewModel.Password = "WrongPassword";

        _mockAuthService
            .Setup(x => x.LoginAsync(It.IsAny<LoginRequest>()))
            .ReturnsAsync(ServiceResult<User>.FailureResult("Giriþ baþarýsýz", "E-posta veya þifre hatalý"));

        // Act
        await _viewModel.LoginCommand.ExecuteAsync(null);

        // Assert
        _viewModel.ErrorMessage.Should().Contain("E-posta veya þifre hatalý");
        _viewModel.IsLoading.Should().BeFalse();
    }

    [Fact]
    public void Email_WhenSet_UpdatesProperty()
    {
        // Arrange
        var email = "test@bartin.edu.tr";

        // Act
        _viewModel.Email = email;

        // Assert
        _viewModel.Email.Should().Be(email);
    }

    [Fact]
    public void Password_WhenSet_UpdatesProperty()
    {
        // Arrange
        var password = "SecurePassword123";

        // Act
        _viewModel.Password = password;

        // Assert
        _viewModel.Password.Should().Be(password);
    }

    [Fact]
    public void RememberMe_WhenToggled_UpdatesProperty()
    {
        // Arrange
        var initialValue = _viewModel.RememberMe;

        // Act
        _viewModel.RememberMe = !initialValue;

        // Assert
        _viewModel.RememberMe.Should().Be(!initialValue);
    }
}
