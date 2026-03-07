# ?? Güvenlik Ýyileþtirmeleri Migrasyon Rehberi

## ?? Genel Bakýþ

Bu rehber, mevcut `FirebaseAuthService` kullanan kodunuzu yeni güvenlik mimarisine geçirmek için adým adým talimatlarý içerir.

---

## ? Yapýlmasý Gerekenler (Checklist)

### **1. Yeni Dosyalarý Projeye Ekle**
- [ ] `KamPay/Security/ISecurityAuditService.cs` oluþturuldu
- [ ] `KamPay/Security/FirebaseSecurityAuditService.cs` oluþturuldu
- [ ] `KamPay/Helpers/AdvancedRateLimiter.cs` oluþturuldu

### **2. MauiProgram.cs Güncellemeleri**
- [ ] `ISecurityAuditService` DI kaydý eklendi
- [ ] `FirebaseAuthService` constructor güncellemesi yapýldý

### **3. FirebaseAuthService Güncellemeleri**
- [ ] Constructor'a `ISecurityAuditService` parametresi eklendi
- [ ] `LoginAsync()` metoduna güvenlik kontrolleri eklendi
- [ ] `SendPasswordResetEmailAsync()` metoduna rate limiting eklendi
- [ ] `RegisterAsync()` metoduna spam önleme eklendi

### **4. Firebase Rules Güncellemeleri**
- [ ] `security_audit_logs` koleksiyonu için rule eklendi
- [ ] `account_locks` koleksiyonu için rule eklendi

### **5. Test ve Doðrulama**
- [ ] Brute force attack senaryosu test edildi
- [ ] Password reset spam senaryosu test edildi
- [ ] Account lockout mekanizmasý test edildi
- [ ] Security audit logs doðru kaydediliyor

---

## ?? Adým Adým Migrasyon

### **Adým 1: MauiProgram.cs Güncelleme**

**?? Konum:** `KamPay/MauiProgram.cs`

**?? ESKÝ KOD:**
```csharp
// Servislerin DI kaydý
builder.Services.AddSingleton(emailSettings);
builder.Services.AddSingleton(firebaseConfig);

builder.Services.AddSingleton<FirebaseClient>(sp =>
{
    var client = new FirebaseClient(Constants.FirebaseRealtimeDbUrl);
    System.Diagnostics.Debug.WriteLine($"? FirebaseClient oluþturuldu: {Constants.FirebaseRealtimeDbUrl}");
    return client;
});

builder.Services.AddSingleton<FirebaseAuthProvider>(sp =>
{
    var config = sp.GetRequiredService<FirebaseConfigSettings>();
    var provider = new FirebaseAuthProvider(new FirebaseConfig(config.ApiKey));
    System.Diagnostics.Debug.WriteLine($"? FirebaseAuthProvider oluþturuldu");
    return provider;
});

builder.Services.AddSingleton<IEmailService, EmailService>();
builder.Services.AddSingleton<IUserProfileService, FirebaseUserProfileService>();

// ?? Firebase Authentication Service
builder.Services.AddSingleton<IAuthenticationService, FirebaseAuthService>();
```

**? YENÝ KOD:**
```csharp
// Servislerin DI kaydý
builder.Services.AddSingleton(emailSettings);
builder.Services.AddSingleton(firebaseConfig);

builder.Services.AddSingleton<FirebaseClient>(sp =>
{
    var client = new FirebaseClient(Constants.FirebaseRealtimeDbUrl);
    System.Diagnostics.Debug.WriteLine($"? FirebaseClient oluþturuldu: {Constants.FirebaseRealtimeDbUrl}");
    return client;
});

builder.Services.AddSingleton<FirebaseAuthProvider>(sp =>
{
    var config = sp.GetRequiredService<FirebaseConfigSettings>();
    var provider = new FirebaseAuthProvider(new FirebaseConfig(config.ApiKey));
    System.Diagnostics.Debug.WriteLine($"? FirebaseAuthProvider oluþturuldu");
    return provider;
});

builder.Services.AddSingleton<IEmailService, EmailService>();
builder.Services.AddSingleton<IUserProfileService, FirebaseUserProfileService>();

// ? YENÝ: Security Audit Service - ÖNCE kaydet
builder.Services.AddSingleton<ISecurityAuditService, FirebaseSecurityAuditService>();
System.Diagnostics.Debug.WriteLine("? ISecurityAuditService DI'ye kaydedildi");

// ?? Firebase Authentication Service - ISecurityAuditService baðýmlýlýðý ile
builder.Services.AddSingleton<IAuthenticationService, FirebaseAuthService>();
```

---

### **Adým 2: FirebaseAuthService Constructor Güncelleme**

**?? Konum:** `KamPay/Services/FirebaseAuthService.cs`

**?? ESKÝ KOD:**
```csharp
public class FirebaseAuthService : IAuthenticationService
{
    private readonly FirebaseAuthProvider _authProvider;
    private readonly FirebaseClient _firebaseClient;
    private readonly IEmailService _emailService;
    private readonly IUserProfileService _userProfileService;

    public FirebaseAuthService(
        FirebaseAuthProvider authProvider,
        FirebaseClient firebaseClient,
        IEmailService emailService,
        IUserProfileService userProfileService)
    {
        _authProvider = authProvider ?? throw new ArgumentNullException(nameof(authProvider));
        _firebaseClient = firebaseClient ?? throw new ArgumentNullException(nameof(firebaseClient));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _userProfileService = userProfileService ?? throw new ArgumentNullException(nameof(userProfileService));
    }
}
```

**? YENÝ KOD:**
```csharp
using KamPay.Security; // ? YENÝ USING

public class FirebaseAuthService : IAuthenticationService
{
    private readonly FirebaseAuthProvider _authProvider;
    private readonly FirebaseClient _firebaseClient;
    private readonly IEmailService _emailService;
    private readonly IUserProfileService _userProfileService;
    private readonly ISecurityAuditService _securityAudit; // ? YENÝ

    public FirebaseAuthService(
        FirebaseAuthProvider authProvider,
        FirebaseClient firebaseClient,
        IEmailService emailService,
        IUserProfileService userProfileService,
        ISecurityAuditService securityAudit) // ? YENÝ PARAMETRE
    {
        _authProvider = authProvider ?? throw new ArgumentNullException(nameof(authProvider));
        _firebaseClient = firebaseClient ?? throw new ArgumentNullException(nameof(firebaseClient));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _userProfileService = userProfileService ?? throw new ArgumentNullException(nameof(userProfileService));
        _securityAudit = securityAudit ?? throw new ArgumentNullException(nameof(securityAudit)); // ? YENÝ
        
        System.Diagnostics.Debug.WriteLine("? FirebaseAuthService oluþturuldu (Security Audit ile)");
    }
}
```

---

### **Adým 3: LoginAsync() Metodunu Güncelle**

**?? Konum:** `KamPay/Services/FirebaseAuthService.cs` ? `LoginAsync()` metodu

**?? ESKÝ KOD (Baþlangýç):**
```csharp
public async Task<ServiceResult<AppUser>> LoginAsync(Models.LoginRequest request)
{
    try
    {
        var validation = ValidateLogin(request);
        if (!validation.IsValid)
            return ServiceResult<AppUser>.FailureResult("Giriþ bilgileri geçersiz", validation.Errors.ToArray());

        // 1?? Firebase Authentication ile giriþ yap
        try
        {
            _authLink = await _authProvider.SignInWithEmailAndPasswordAsync(
                request.Email.ToLower(),
                request.Password
            );
        }
        catch (FirebaseAuthException ex)
        {
            return ServiceResult<AppUser>.FailureResult("Giriþ baþarýsýz", GetFriendlyErrorMessage(ex));
        }
        // ...
    }
}
```

**? YENÝ KOD (Güvenlik Kontrolleri ile):**
```csharp
public async Task<ServiceResult<AppUser>> LoginAsync(Models.LoginRequest request)
{
    try
    {
        var validation = ValidateLogin(request);
        if (!validation.IsValid)
            return ServiceResult<AppUser>.FailureResult("Giriþ bilgileri geçersiz", validation.Errors.ToArray());

        string safeEmail = request.Email.ToLower();

        // ?? YENÝ: 1. ÖNCE Hesap Kilidi Kontrolü
        var lockStatus = await _securityAudit.IsAccountLockedAsync(safeEmail);
        if (lockStatus.IsLocked)
        {
            var remainingTime = lockStatus.UnlockTime.HasValue 
                ? lockStatus.UnlockTime.Value - DateTime.UtcNow 
                : TimeSpan.Zero;
            
            var message = remainingTime.TotalMinutes > 60
                ? $"?? Hesabýnýz güvenlik nedeniyle kilitlenmiþtir.\n\n" +
                  $"Kalan Süre: {Math.Ceiling(remainingTime.TotalHours)} saat\n\n" +
                  $"Sebep: {lockStatus.Reason ?? "Çok fazla baþarýsýz giriþ denemesi"}"
                : $"?? Hesabýnýz geçici olarak kilitlenmiþtir.\n\n" +
                  $"Kalan Süre: {Math.Ceiling(remainingTime.TotalMinutes)} dakika\n\n" +
                  $"Sebep: {lockStatus.Reason ?? "Çok fazla baþarýsýz giriþ denemesi"}";
            
            return ServiceResult<AppUser>.FailureResult("Hesap Kilitli", message);
        }

        // ?? YENÝ: 2. Geliþmiþ Rate Limiting (IP + Email)
        var ipAddress = GetDeviceIpAddress(); 
        var rateLimitResult = SecureRateLimiters.Login.CheckRequest(safeEmail, ipAddress);
        
        if (!rateLimitResult.IsAllowed)
        {
            await _securityAudit.LogSuspiciousActivityAsync(
                null,
                "Login Rate Limit Exceeded",
                $"Rate limit aþýldý: {safeEmail} (IP: {ipAddress})",
                SuspicionLevel.High
            );

            if (rateLimitResult.IsBanned)
            {
                await _securityAudit.LockAccountTemporarilyAsync(
                    safeEmail,
                    TimeSpan.FromHours(24),
                    "Çok fazla rate limit ihlali"
                );
            }

            return ServiceResult<AppUser>.FailureResult("Çok fazla deneme", rateLimitResult.Message);
        }

        // 1?? Firebase Authentication ile giriþ yap
        try
        {
            _authLink = await _authProvider.SignInWithEmailAndPasswordAsync(
                safeEmail,
                request.Password
            );
        }
        catch (FirebaseAuthException ex)
        {
            // ?? YENÝ: 3. Baþarýsýz giriþ logu kaydet
            await _securityAudit.LogFailedLoginAttemptAsync(
                safeEmail, 
                ipAddress, 
                GetDeviceInfo()
            );

            // ?? YENÝ: 4. Otomatik hesap kilitleme kontrolü
            var shouldLock = await _securityAudit.ShouldLockAccountAsync(safeEmail);
            if (shouldLock.ShouldLock)
            {
                await _securityAudit.LockAccountTemporarilyAsync(
                    safeEmail,
                    shouldLock.LockDuration,
                    shouldLock.Reason
                );

                return ServiceResult<AppUser>.FailureResult(
                    "Hesap Kilitlendi",
                    $"?? Güvenlik nedeniyle hesabýnýz {shouldLock.LockDuration.TotalMinutes} dakika süreyle kilitlenmiþtir.\n\n{shouldLock.Reason}"
                );
            }

            return ServiceResult<AppUser>.FailureResult("Giriþ baþarýsýz", GetFriendlyErrorMessage(ex));
        }

        // ... mevcut kod devam ediyor (email verification, user fetch vs.)

        // ?? YENÝ: 5. Baþarýlý giriþ logu kaydet (en sonda)
        await _securityAudit.LogSuccessfulLoginAsync(
            user.UserId,
            user.Email,
            ipAddress,
            GetDeviceInfo()
        );

        // ... mevcut kod (session save, messaging vs.)
    }
}
```

---

### **Adým 4: Helper Methods Ekle**

**?? Konum:** `KamPay/Services/FirebaseAuthService.cs` ? `#region Helper Methods` içine

**? YENÝ METHODLAR:**
```csharp
#region Helper Methods

// ...mevcut metodlar...

/// <summary>
/// ?? Cihaz IP adresini simüle eder (gerçek üretimde HttpContext'ten alýnmalý)
/// </summary>
private string GetDeviceIpAddress()
{
    // TODO: Gerçek üretimde:
    // - ASP.NET Core: HttpContext.Connection.RemoteIpAddress
    // - MAUI: Platform-specific API kullanarak gerçek IP al
    return "127.0.0.1"; // Simülasyon
}

/// <summary>
/// ?? Cihaz bilgisini alýr
/// </summary>
private string GetDeviceInfo()
{
    try
    {
        return $"{DeviceInfo.Platform} {DeviceInfo.Version} ({DeviceInfo.Model})";
    }
    catch
    {
        return "Unknown Device";
    }
}

#endregion
```

---

### **Adým 5: LoginViewModel Güncelleme (Opsiyonel - Zaten OK)**

**?? Konum:** `KamPay/ViewModels/LoginViewModel.cs`

**? MEVCUT KOD ZATEN DOÐRU:**
```csharp
private async Task LoginAsync()
{
    try
    {
        // ...

        // ? Rate Limiting Kontrolü ZATEN VAR (eski RateLimiters kullanýyor)
        var limitCheck = RateLimiters.Login.CheckLimit(Email);
        if (!limitCheck.IsAllowed)
        {
            ErrorMessage = limitCheck.Message;
            return;
        }

        var request = new LoginRequest
        {
            Email = Email,
            Password = Password,
            RememberMe = RememberMe
        };

        var result = await _authService.LoginAsync(request);

        if (result.Success)
        {
            // ? Baþarýlý giriþte rate limit'i sýfýrla
            RateLimiters.Login.Reset(Email);
            
            // ...
        }
        else
        {
            // ? Hata mesajlarýný göster
            ErrorMessage = result.Message ?? "Giriþ baþarýsýz";
        }
    }
    // ...
}
```

**?? NOT:** `LoginViewModel` hala eski `RateLimiters.Login` kullanýyor. Ýsterseniz `SecureRateLimiters.Login` ile deðiþtirebilirsiniz, ama **zorunlu deðil** çünkü `FirebaseAuthService` içinde zaten yeni rate limiter kullanýlýyor.

---

### **Adým 6: Firebase Security Rules Güncelleme**

**?? Konum:** Firebase Console ? Realtime Database ? Rules

**?? ESKÝ RULES:**
```json
{
  "rules": {
    "users": {
      ".read": "auth != null",
      ".write": "auth != null"
    },
    "products": {
      ".read": true,
      ".write": "auth != null"
    }
  }
}
```

**? YENÝ RULES (Güvenlik koleksiyonlarý ekle):**
```json
{
  "rules": {
    "users": {
      ".read": "auth != null",
      ".write": "auth != null"
    },
    "products": {
      ".read": true,
      ".write": "auth != null"
    },
    
    // ? YENÝ: Security Audit Logs
    "security_audit_logs": {
      ".read": "auth != null && root.child('users').child(auth.uid).child('IsAdmin').val() == true",
      ".write": "auth != null",
      ".indexOn": ["Email", "IpAddress", "EventType", "Timestamp"]
    },
    
    // ? YENÝ: Account Locks
    "account_locks": {
      ".read": "auth != null",
      ".write": "auth != null",
      ".indexOn": ["Email", "IsLocked", "UnlockAt"]
    }
  }
}
```

---

### **Adým 7: Build ve Test**

#### **7.1. Clean Build**
```bash
dotnet clean
dotnet build
```

#### **7.2. Test Senaryosu 1: Login Rate Limiting**
```
1. Uygulamayý çalýþtýr
2. Yanlýþ þifre ile 3 kez giriþ yap
3. Beklenen: 3. denemede "Çok fazla deneme" mesajý
4. Beklenen: 4. denemede hesap 60 dakika kilitlensin
5. Firebase Console'da kontrol et:
   - account_locks/{email} kaydý var mý?
   - security_audit_logs içinde 3 adet FailedLogin event var mý?
```

#### **7.3. Test Senaryosu 2: Baþarýlý Giriþ**
```
1. Doðru þifre ile giriþ yap
2. Beklenen: Baþarýlý giriþ
3. Firebase Console'da kontrol et:
   - security_audit_logs içinde SuccessfulLogin event var mý?
   - account_locks/{email} kaydý silinmiþ mi?
```

#### **7.4. Test Senaryosu 3: Password Reset Spam**
```
1. Þifremi Unuttum'a týkla
2. 2 kez reset email gönder
3. Beklenen: 3. denemede "Çok fazla deneme" mesajý
4. Firebase Console'da kontrol et:
   - security_audit_logs içinde PasswordResetRequest eventleri var mý?
```

---

## ?? Sýk Karþýlaþýlan Hatalar ve Çözümleri

### **Hata 1: "ISecurityAuditService could not be resolved"**

**Sebep:** DI container'da `ISecurityAuditService` kaydý yok.

**Çözüm:**
```csharp
// MauiProgram.cs içinde MUTLAKA ekle:
builder.Services.AddSingleton<ISecurityAuditService, FirebaseSecurityAuditService>();
```

---

### **Hata 2: "Constructor parameter 'securityAudit' cannot be resolved"**

**Sebep:** `FirebaseAuthService` constructor'ýna `ISecurityAuditService` parametresi eklenmemiþ.

**Çözüm:**
```csharp
public FirebaseAuthService(
    FirebaseAuthProvider authProvider,
    FirebaseClient firebaseClient,
    IEmailService emailService,
    IUserProfileService userProfileService,
    ISecurityAuditService securityAudit) // ? EKLE
{
    _securityAudit = securityAudit ?? throw new ArgumentNullException(nameof(securityAudit));
}
```

---

### **Hata 3: "Firebase permission denied on security_audit_logs"**

**Sebep:** Firebase Security Rules güncel deðil.

**Çözüm:**
Firebase Console ? Realtime Database ? Rules sekmesinde yukarýdaki yeni rules'larý ekle.

---

### **Hata 4: "SecureRateLimiters.Login is not found"**

**Sebep:** `AdvancedRateLimiter.cs` dosyasý projeye eklenmemiþ.

**Çözüm:**
1. `KamPay/Helpers/AdvancedRateLimiter.cs` dosyasýný oluþtur
2. Ýçeriði kopyala/yapýþtýr
3. Rebuild

---

## ?? Migrasyon Sonrasý Kontrol Listesi

### **Kod Kontrolü:**
- [ ] `using KamPay.Security;` eklendi mi?
- [ ] Constructor'a `ISecurityAuditService` parametresi eklendi mi?
- [ ] `LoginAsync()` içinde tüm güvenlik kontrolleri var mý?
- [ ] Helper metodlar (`GetDeviceIpAddress`, `GetDeviceInfo`) eklendi mi?

### **Firebase Kontrolü:**
- [ ] Firebase Console'da `security_audit_logs` koleksiyonu oluþtu mu?
- [ ] Firebase Console'da `account_locks` koleksiyonu oluþtu mu?
- [ ] Security Rules güncellendi mi?

### **Test Kontrolü:**
- [ ] Brute force attack senaryosu çalýþýyor mu?
- [ ] Account lockout mekanizmasý çalýþýyor mu?
- [ ] Security audit logs doðru kaydediliyor mu?

---

## ?? Öðrenme Kaynaklarý

### **SOLID Prensipleri:**
- **Single Responsibility:** Her servis tek bir iþten sorumlu
- **Dependency Inversion:** Interface'ler üzerinden çalýþ
- **Open/Closed:** Yeni özellikler için açýk, deðiþiklik için kapalý

### **Security Best Practices:**
- OWASP Top 10 2021
- NIST Password Guidelines
- Firebase Security Documentation

---

## ?? Yardým ve Destek

**Sorularýnýz mý var?**
1. `DOCS/SECURITY_IMPROVEMENTS.md` dosyasýný okuyun
2. GitHub Issues'a yeni bir issue açýn
3. Proje dokümantasyonunu kontrol edin

---

**Son Güncelleme:** 2025-01-08  
**Versiyon:** 1.0.0  
**Migrasyon Süresi:** ~30 dakika
