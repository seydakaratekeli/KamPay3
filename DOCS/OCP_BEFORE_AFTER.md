# ?? OCP Refactoring: Before vs After

## ?? Problem: Ödeme Sistemi - SWITCH/IF Bloðu

---

## ? **BEFORE (OCP Ýhlali)**

```csharp
// FirebaseTransactionService.cs - CreatePaymentSimulationAsync()

// ?? SORUN: Yeni ödeme yöntemi eklemek için SWITCH bloðunu deðiþtirmen gerekiyor!
var payment = new PaymentDto
{
    Amount = amount,
    Currency = "TRY",
    Status = ServicePaymentStatus.Initiated,
    Method = method?.ToLower() switch
    {
        "cardsim" => PaymentMethodType.CardSim,
        "banktransfersim" or "eft" or "havale" => PaymentMethodType.BankTransferSim,
        "walletsim" => PaymentMethodType.WalletSim,
        _ => PaymentMethodType.CardSim
    }
};

// ?? SORUN: Her ödeme yöntemi için IF bloðu eklemen gerekiyor!
if (payment.Method == PaymentMethodType.CardSim)
{
    var otp = GenerateSecureOtp();
    await _firebaseClient
        .Child(Constants.TempOtpsCollection)
        .Child(payment.PaymentId)
        .PutAsync(new { Otp = otp, ExpiresAt = DateTime.UtcNow.AddMinutes(2) });
}

if (payment.Method == PaymentMethodType.BankTransferSim)
{
    payment.BankName = "Ziraat Bankasý";
    payment.BankReference = GenerateBankReference();
}

// ?? SORUN: Stripe, Iyzico, PayPal eklemek istersen?
// ? Tüm bu SWITCH ve IF bloklarýný deðiþtirmen gerekecek!
// ? Mevcut çalýþan kodu riske atacaksýn!
```

**Dezavantajlar**:
- ? Yeni ödeme yöntemi eklemek = Mevcut kodu deðiþtirmek
- ? IF/ELSE kalabalýðý ? Okunmasý zor, test edilmesi zor
- ? Her deðiþiklik risk ? Mevcut ödemeleri bozabilir
- ? Takým çalýþmasý zor ? Merge conflict riski

---

## ? **AFTER (OCP Uyumlu - Strategy + Factory Pattern)**

### 1. Interface Tanýmý
```csharp
// KamPay/Services/Payment/IPaymentProvider.cs

/// <summary>
/// ?? OCP PRENSÝBÝ: Yeni ödeme yöntemi eklerken bu interface'i implement et.
/// Mevcut kodu deðiþtirmene gerek kalmaz!
/// </summary>
public interface IPaymentProvider
{
    string ProviderName { get; }
    PaymentMethodType MethodType { get; }
    
    Task<ServiceResult<PaymentDto>> InitiatePaymentAsync(string transactionId, decimal amount);
    Task<ServiceResult<bool>> ValidatePaymentAsync(string paymentId, string? verificationData = null);
}
```

---

### 2. Concrete Implementations (Her Provider Kendi Dosyasýnda)

```csharp
// KamPay/Services/Payment/CardSimulationProvider.cs

/// <summary>
/// ?? KART SÝMÜLASYONU PROVIDER
/// OCP: Bu sýnýfý deðiþtirmeden yeni provider ekleyebilirsin
/// </summary>
public class CardSimulationProvider : IPaymentProvider
{
    public string ProviderName => "CardSim";
    public PaymentMethodType MethodType => PaymentMethodType.CardSim;

    public async Task<ServiceResult<PaymentDto>> InitiatePaymentAsync(string transactionId, decimal amount)
    {
        var payment = new PaymentDto
        {
            PaymentId = Guid.NewGuid().ToString(),
            Amount = amount,
            Method = MethodType
        };

        // ? Karta özel mantýk (OTP üretimi) sadece burada
        var otp = GenerateSecureOtp();
        await _firebaseClient
            .Child(Constants.TempOtpsCollection)
            .Child(payment.PaymentId)
            .PutAsync(new { Otp = otp, ExpiresAt = DateTime.UtcNow.AddMinutes(2) });

        return ServiceResult<PaymentDto>.SuccessResult(payment);
    }
}
```

```csharp
// KamPay/Services/Payment/BankTransferSimulationProvider.cs

/// <summary>
/// ?? EFT/HAVALE SÝMÜLASYONU PROVIDER
/// OCP: CardSim'i deðiþtirmeden ayrý bir sýnýf
/// </summary>
public class BankTransferSimulationProvider : IPaymentProvider
{
    public string ProviderName => "BankTransferSim";
    public PaymentMethodType MethodType => PaymentMethodType.BankTransferSim;

    public async Task<ServiceResult<PaymentDto>> InitiatePaymentAsync(string transactionId, decimal amount)
    {
        var payment = new PaymentDto
        {
            Amount = amount,
            Method = MethodType,
            BankName = "Ziraat Bankasý",
            BankReference = GenerateBankReference() // ? EFT'ye özel mantýk sadece burada
        };

        return await Task.FromResult(ServiceResult<PaymentDto>.SuccessResult(payment));
    }
}
```

---

### 3. Factory Pattern (Provider Yönetimi)

```csharp
// KamPay/Services/Payment/PaymentProviderFactory.cs

/// <summary>
/// ?? FACTORY PATTERN
/// OCP: Yeni provider eklerken RegisterProvider() çaðýr, mevcut kodu deðiþtirme!
/// </summary>
public class PaymentProviderFactory : IPaymentProviderFactory
{
    private readonly Dictionary<string, IPaymentProvider> _providers = new();

    public void RegisterProvider(IPaymentProvider provider)
    {
        _providers[provider.ProviderName.ToLowerInvariant()] = provider;
    }

    public IPaymentProvider GetProvider(string methodName)
    {
        var key = methodName.ToLowerInvariant();
        
        // ? "banktransfersim", "eft", "havale" ? ayný provider
        key = key switch
        {
            "banktransfersim" or "eft" or "havale" => "banktransfersim",
            _ => key
        };

        if (!_providers.TryGetValue(key, out var provider))
        {
            throw new NotSupportedException($"Desteklenmeyen ödeme yöntemi: '{methodName}'");
        }

        return provider;
    }
}
```

---

### 4. DI Registration (MauiProgram.cs)

```csharp
// MauiProgram.cs

builder.Services.AddSingleton<IPaymentProviderFactory>(sp =>
{
    var factory = new PaymentProviderFactory();
    var firebaseClient = sp.GetRequiredService<FirebaseClient>();
    
    // ? Provider'larý kaydet - Yeni provider eklerken buraya ekle
    factory.RegisterProvider(new CardSimulationProvider(firebaseClient));
    factory.RegisterProvider(new BankTransferSimulationProvider(firebaseClient));
    
    // ?? GELECEK: Gerçek API entegrasyonlarý
    // factory.RegisterProvider(new StripePaymentProvider(apiKey, secretKey));
    // factory.RegisterProvider(new IyzicoPaymentProvider(apiKey, secretKey));
    
    return factory;
});
```

---

### 5. Kullaným (FirebaseTransactionService.cs)

```csharp
// FirebaseTransactionService.cs - CreatePaymentSimulationAsync()

// ? SWITCH YOK! Factory'den doðru provider'ý al
IPaymentProvider provider;
try
{
    provider = _paymentProviderFactory.GetProvider(method);
    System.Diagnostics.Debug.WriteLine($"? Provider seçildi: {provider.ProviderName}");
}
catch (NotSupportedException ex)
{
    return ServiceResult<PaymentDto>.FailureResult(ex.Message);
}

// ? Polimorfizm: Provider'ýn kendi InitiatePaymentAsync'ini çaðýr
var paymentResult = await provider.InitiatePaymentAsync(transactionId, amount);

// ? IF bloðu YOK! Her provider kendi doðrulama metodunu çalýþtýrýyor
// ConfirmPaymentSimulationAsync içinde:
var validationResult = await provider.ValidatePaymentAsync(paymentId, otp);
```

---

## ?? **YENÝ PROVIDER EKLEMEK (ÖRNEK: Stripe)**

### ? **BEFORE** (Mevcut Kodu Deðiþtir)
```csharp
// ?? FirebaseTransactionService.cs'yi aç
Method = method?.ToLower() switch
{
    "cardsim" => PaymentMethodType.CardSim,
    "banktransfersim" => PaymentMethodType.BankTransferSim,
    "stripe" => PaymentMethodType.Stripe, // ? YENÝ SATIR EKLE
}

// ?? IF bloðu ekle
if (payment.Method == PaymentMethodType.Stripe)
{
    // Stripe API çaðrýsý...
}
```
**Sorunlar**:
- Mevcut kodu deðiþtiriyorsun ? Risk!
- Test tekrar çalýþtýrýlmalý (tüm IF/ELSE bloklarý)
- Merge conflict riski (baþka biri de ayný dosyayý deðiþtiriyorsa)

---

### ? **AFTER** (Yeni Sýnýf Ekle)
```csharp
// ?? Yeni dosya: KamPay/Services/Payment/StripePaymentProvider.cs

public class StripePaymentProvider : IPaymentProvider
{
    public string ProviderName => "Stripe";
    public PaymentMethodType MethodType => PaymentMethodType.Stripe;

    public async Task<ServiceResult<PaymentDto>> InitiatePaymentAsync(...)
    {
        // Stripe API çaðrýsý...
        var stripePayment = await _stripeApi.CreatePaymentIntentAsync(...);
        return ServiceResult<PaymentDto>.SuccessResult(payment);
    }
}

// ?? MauiProgram.cs'de sadece kayýt yap
factory.RegisterProvider(new StripePaymentProvider(apiKey, secretKey));
```
**Avantajlar**:
- ? Mevcut koda dokunmadýn (CardSim, BankTransferSim aynen çalýþýyor)
- ? Test sadece StripePaymentProvider için (izole)
- ? Merge conflict riski yok (yeni dosya oluþturuldu)
- ? Takým çalýþmasý kolay (herkes farklý provider'da çalýþabilir)

---

## ?? **KARÞILAÞTIRMA TABLOSU**

| Özellik | BEFORE (OCP Ýhlali) | AFTER (OCP Uyumlu) |
|---------|---------------------|---------------------|
| **Yeni Provider Eklemek** | Mevcut kodu deðiþtir (SWITCH/IF) | Yeni sýnýf yaz + Factory'ye kaydet |
| **Kod Karmaþýklýðý** | Tek dosyada tüm IF/ELSE bloklarý | Her provider ayrý dosyada |
| **Test Edilebilirlik** | Tüm provider'larý test etmelisin | Her provider izole test edilir |
| **Merge Conflict Riski** | Yüksek (herkes ayný dosyayý deðiþtiriyor) | Düþük (herkes farklý dosyada) |
| **Sürdürülebilirlik** | Kod kalabalýklaþýyor, okunmasý zorlaþýyor | Her provider tek sorumluluk (SRP) |
| **Risk** | Mevcut ödemeleri bozabilir | Mevcut kod hiç deðiþmez, risk yok |

---

## ?? **SONUÇ**

**BEFORE**: 
- Yeni ödeme yöntemi = Mevcut kodu deðiþtir = Risk
- IF/ELSE kalabalýðý = Okunmasý zor, test edilmesi zor

**AFTER**: 
- Yeni ödeme yöntemi = Yeni sýnýf yaz = Güvenli
- Her provider izole = Anlaþýlýr, test edilebilir, sürdürülebilir

---

## ?? **OCP PRENSÝBÝ ÖZETÝ**

> **"Kodlar geliþime AÇIK, deðiþime KAPALI olmalýdýr."**

- ? **Geliþime AÇIK**: Yeni özellik ekleyebilirsin (yeni provider sýnýfý)
- ? **Deðiþime KAPALI**: Mevcut çalýþan kodu deðiþtirmiyorsun (CardSim aynen çalýþýyor)

? **Interface + Strategy Pattern + Factory = OCP Uyumlu Kod**
