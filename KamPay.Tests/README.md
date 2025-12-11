# KamPay Birim Testleri

Bu proje, KamPay uygulamasının birim testlerini içerir.

## 📦 Test Framework'leri

- **xUnit** - Test framework'ü
- **Moq** - Mock nesneler için
- **FluentAssertions** - Okunabilir assertion'lar için
- **Coverlet** - Code coverage için

## 🚀 Test Çalıştırma

### Visual Studio'dan
1. **Test Explorer** penceresini açın (Test > Test Explorer veya Ctrl+E, T)
2. **Run All** butonuna tıklayın
3. Sonuçları Test Explorer'da görün

### Komut Satırından

```bash
# Tüm testleri çalıştır
dotnet test

# Detaylı çıktı ile çalıştır
dotnet test --logger "console;verbosity=detailed"

# Sadece belirli bir sınıfı test et
dotnet test --filter FullyQualifiedName~FirebaseAuthServiceTests

# Sadece belirli bir metodu test et
dotnet test --filter FullyQualifiedName~ValidateRegistration_WithValidData_ReturnsSuccess

# Test coverage raporu oluştur
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura

# HTML coverage raporu oluştur (ReportGenerator gerekli)
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura
reportgenerator -reports:coverage.cobertura.xml -targetdir:coveragereport -reporttypes:Html
```

## 📊 Test Kategorileri

### Services Tests
- **FirebaseAuthServiceTests** (15 test)
  - Kayıt validasyonu
  - Giriş validasyonu
  - XSS koruması
  - SQL Injection koruması
  
- **FirebaseProductServiceTests** (12 test)
  - Ürün validasyonu
  - Fiyat kontrolleri
  - Kategori kontrolleri
  - Görsel sayısı kontrolü

- **FirebaseServiceSharingServiceTests** (30+ test) ⭐ YENİ
  - Hizmet oluşturma
  - Hizmet talebi
  - Fiyat pazarlığı (ProposePrice, SendCounterOffer, AcceptNegotiatedPrice)
  - Mesajlaşma (StartConversation)
  - Ödeme simülasyonu (Card, BankTransfer, Wallet)
  - Talep yanıtlama (Accept/Decline)
  - Full negotiation flow integration

- **FirebaseNotificationServiceTests** (12 test)
  - Bildirim oluşturma
  - Bildirim okuma
  - Toplu işlemler

- **FirebaseMessagingServiceTests** (10 test)
  - Mesaj gönderme
  - Konuşma yönetimi
  - Okundu işaretleme

- **UserStateServiceTests** (25+ test) ⭐ YENİ
  - Kullanıcı yenileme (RefreshCurrentUser)
  - Profil güncelleme (UpdateUserProfile)
  - Bulk update operasyonları
  - Event handling (UserProfileChanged)
  - CurrentUser property management
  - ClearUser functionality

### ViewModels Tests
- **LoginViewModelTests** (8 test)
  - Giriş işlemleri
  - Hata yönetimi
  - Property güncellemeleri
  
- **ProductListViewModelTests** (13 test)
  - Filtreleme mantığı
  - Sıralama seçenekleri
  - Kategori seçimi

- **QRCodeViewModelTests** (30+ test) ⭐ YENİ
  - QR kod tarama (ProcessScannedQRCode)
  - PIN doğrulama (güvenli QR kodlar)
  - Backward compatibility (eski QR kodlar)
  - Süre uzatma (ExtendTime)
  - Teslimat iptali (CancelDelivery)
  - Fotoğraf yükleme (Phase 2 features)
  - Timer yönetimi (UpdateTimeRemaining)
  - Messenger integration (QRCodeScannedMessage)

- **ServiceSharingViewModelTests** (10 test)
  - Hizmet listesi
  - Filtreleme
  
- **ServiceRequestsViewModelTests** (12 test)
  - Talep yönetimi
  - Durum güncellemeleri

- **GoodDeedBoardViewModelTests** (8 test)
  - İyilik tahtası gönderileri
  
- **SurpriseBoxViewModelTests** (10 test)
  - Sürpriz kutu mantığı

### Helpers Tests
- **InputSanitizerTests** (25 test)
  - XSS tespiti
  - SQL Injection tespiti
  - Email validasyonu
  - URL validasyonu
  - Text sanitization
  
- **ImageValidatorTests** (13 test)
  - Dosya uzantısı kontrolü
  - Dosya boyutu formatlama
  - MIME type kontrolü
  - Güvenli dosya adı oluşturma

- **NetworkHelperTests** (10 test)
  - Retry mekanizması
  - Hata yönetimi
  
- **RateLimiterTests** (8 test)
  - Rate limiting mantığı
  - Spam koruması

## 📈 Test Coverage Hedefleri

| Kategori | Hedef | Mevcut | Durum |
|----------|-------|--------|-------|
| Services | %80+ | ~85% | ✅ |
| ViewModels | %70+ | ~75% | ✅ |
| Helpers | %90+ | ~92% | ✅ |
| Models | %50+ | ~55% | ✅ |

## 🎯 Test Pattern'leri

### AAA Pattern (Arrange-Act-Assert)
```csharp
[Fact]
public void TestMethod_Scenario_ExpectedBehavior()
{
    // Arrange - Test için gerekli nesneleri hazırla
    var request = new LoginRequest { ... };
    
    // Act - Test edilecek metodu çalıştır
    var result = _service.Login(request);
    
    // Assert - Sonuçları doğrula
    result.Should().BeTrue();
}
```

### Theory ile Parametrik Testler
```csharp
[Theory]
[InlineData("", "Test123456")] // Boş email
[InlineData("test@bartin.edu.tr", "")] // Boş şifre
public void Login_WithMissingCredentials_ReturnsError(string email, string password)
{
    // Test implementation
}
```

### Moq ile Mock Nesneler
```csharp
var mockService = new Mock<IAuthenticationService>();
mockService
    .Setup(x => x.LoginAsync(It.IsAny<LoginRequest>()))
    .ReturnsAsync(ServiceResult<User>.SuccessResult(mockUser));
```

### FluentAssertions
```csharp
result.Should().BeTrue();
result.Errors.Should().BeEmpty();
result.Data.Should().NotBeNull();
result.Message.Should().Contain("başarılı");
```

## 📝 Yeni Test Ekleme

### 1. Test Sınıfı Oluşturma
```csharp
namespace KamPay.Tests.Services;

public class NewServiceTests
{
    private readonly Mock<IDependency> _mockDependency;
    private readonly NewService _service;

    public NewServiceTests()
    {
        _mockDependency = new Mock<IDependency>();
        _service = new NewService(_mockDependency.Object);
    }

    [Fact]
    public void TestMethod_Scenario_ExpectedResult()
    {
        // Test implementation
    }
}
```

### 2. Test İsimlendirme Kuralları
- **Format**: `MethodName_Scenario_ExpectedBehavior`
- **Örnekler**:
  - `ValidateRegistration_WithValidData_ReturnsSuccess`
  - `Login_WithEmptyEmail_ReturnsError`
  - `AddProduct_WithTooManyImages_ThrowsException`
  - `ProposePrice_WithInvalidPrice_ReturnsFailure` (yeni)
  - `ProcessScannedQRCode_WithOldFormat_UsesBackwardCompatibility` (yeni)

### 3. Test Kategorileri
```csharp
// Trait kullanarak testleri kategorize edin
[Trait("Category", "Unit")]
[Trait("Category", "Integration")]
```

## 🔧 Troubleshooting

### Test Çalışmıyor
1. NuGet paketlerinin yüklü olduğunu kontrol edin: `dotnet restore`
2. Projeyi temizleyin: `dotnet clean`
3. Yeniden derleyin: `dotnet build`

### Mock Çalışmıyor
1. Mock setup'ının doğru olduğunu kontrol edin
2. `It.IsAny<T>()` veya spesifik değer kullanın
3. `.Verifiable()` ve `.Verify()` ile mock çağrısını doğrulayın

### Coverage Raporu Oluşmuyor
1. Coverlet paketinin yüklü olduğunu kontrol edin
2. Test projesinde `/p:CollectCoverage=true` kullanın

## 📚 Kaynaklar

- [xUnit Documentation](https://xunit.net/)
- [Moq Documentation](https://github.com/moq/moq4)
- [FluentAssertions Documentation](https://fluentassertions.com/)
- [.NET Testing Best Practices](https://docs.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)

## 👥 Katkıda Bulunma

1. Yeni test eklerken mevcut pattern'leri takip edin
2. Test coverage'ı artırmaya çalışın
3. Anlamlı test isimleri kullanın
4. AAA pattern'ini uygulayın
5. Parametrik testler için Theory kullanın

## 📊 Mevcut Test İstatistikleri

| Kategori | Test Sayısı | Durum |
|----------|-------------|-------|
| FirebaseAuthService | 15 | ✅ |
| FirebaseProductService | 12 | ✅ |
| FirebaseServiceSharingService | 30+ | ✅ ⭐ YENİ |
| FirebaseNotificationService | 12 | ✅ |
| FirebaseMessagingService | 10 | ✅ |
| UserStateService | 25+ | ✅ ⭐ YENİ |
| InputSanitizer | 25 | ✅ |
| ImageValidator | 13 | ✅ |
| NetworkHelper | 10 | ✅ |
| RateLimiter | 8 | ✅ |
| LoginViewModel | 8 | ✅ |
| ProductListViewModel | 13 | ✅ |
| QRCodeViewModel | 30+ | ✅ ⭐ YENİ |
| ServiceSharingViewModel | 10 | ✅ |
| ServiceRequestsViewModel | 12 | ✅ |
| GoodDeedBoardViewModel | 8 | ✅ |
| SurpriseBoxViewModel | 10 | ✅ |
| **TOPLAM** | **240+** | ✅ |

### ⭐ Yeni Eklenen Test Sınıfları (2025-12-02)

1. **FirebaseServiceSharingServiceTests** (30+ test)
   - Mesajlaşma ve pazarlık özellikleri için kapsamlı testler
   - Ödeme simülasyonu testleri
   - Integration test senaryoları

2. **QRCodeViewModelTests** (30+ test)
   - QR kod güvenlik testleri
   - PIN doğrulama
   - Backward compatibility testleri
   - Phase 2 fotoğraf özellikleri

3. **UserStateServiceTests** (25+ test)
   - Kullanıcı durumu yönetimi
   - Bulk update operasyonları
   - Event handling testleri

**Toplam Artış**: +85 yeni test eklendi! 🎉

---

**Son Güncelleme**: 2025-12-02  
**Versiyon**: 2.0.0  
**Proje**: KamPay3 - Bartın Üniversitesi
