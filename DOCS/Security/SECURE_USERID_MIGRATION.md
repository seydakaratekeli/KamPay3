# KamPay — Güvenli Kullanýcý ID Eriþimi: Preferences ? IUserStateService Geçiþi

Bu belge, `current_user_id` deðerinin `Preferences` (þifresiz depolama) üzerinden okunmasýndan kaynaklanan güvenlik riskini ve uygulanan düzeltmeyi açýklar.

---

## Ýçindekiler

1. [Sorun](#sorun)
2. [Risk Analizi](#risk-analizi)
3. [Çözüm Mimarisi](#çözüm-mimarisi)
4. [Deðiþtirilen Dosyalar](#deðiþtirilen-dosyalar)
5. [Kod Deðiþiklikleri](#kod-deðiþiklikleri)
6. [Veri Akýþý: Öncesi ve Sonrasý](#veri-akýþý-öncesi-ve-sonrasý)

---

## Sorun

Aþaðýdaki converter sýnýflarý, mevcut kullanýcýnýn ID'sini okumak için `Preferences.Get("current_user_id", "")` çaðrýsýný kullanýyordu:

| Dosya | Etkilenen Sýnýflar |
|---|---|
| `Converters/ProductConverters.cs` | `MessageBubbleColorConverter`, `MessageBubbleAlignmentConverter`, `MessageTimeColorConverter`, `MessageTextColorConverter` |
| `Converters/NegotiationStatusTextConverter.cs` | `NegotiationStatusTextConverter` |

Bu yaklaþýmýn üç kritik sorunu vardý:

### 1. Þifresiz Depolama
`Preferences` API'si platform düzeyinde **þifreleme yapmaz**:
- **Android**: `SharedPreferences` — cihazda düz metin olarak okunabilir
- **iOS**: `NSUserDefaults` — uygulama sandbox dýþýnda yedeklenebilir

Kullanýcý ID'si kimlik doðrulama için doðrudan kullanýlmaz; ancak Firebase veritabaný sorgularýnda, mesaj sahipliði kontrolünde ve diðer güvenlik kararlarýnda kullanýlýr.

### 2. Senkronizasyon Eksikliði
`FirebaseAuthService.SaveUserSessionAsync()` tüm oturum verilerini `SecureStorage`'a yazýyordu. Ancak `Preferences`'taki `current_user_id` anahtarý **hiçbir yerde güncellenmiyordu**. Bu, converter'larýn her zaman boþ string okumasýna ve tüm mesaj balonlarýnýn yanlýþ hizalanmasýna neden oluyordu.

### 3. Kullanýcý Deðiþiminde Veri Kalýntýsý
`Preferences` temizlenmediði için farklý bir kullanýcý giriþ yaptýðýnda önceki kullanýcýnýn ID'si okunmaya devam edebiliyordu.

---

## Risk Analizi

| Risk | Düzey | Açýklama |
|---|---|---|
| Yanlýþ mesaj hizalama | ?? Yüksek | `current_user_id` boþ okunduðu için tüm mesajlar "gelen" olarak gösteriliyordu |
| Þifresiz kullanýcý ID depolanmasý | ?? Orta | Kullanýcý ID'si bir kimlik bilgisi deðil, ama yetki kontrollerinde kullanýlýyor |
| Kullanýcý deðiþiminde stale data | ?? Orta | Çýkýþ sonrasý eski ID kalýntý býrakabilir |
| Preferences/SecureStorage tutarsýzlýðý | ?? Yüksek | Ýki farklý kaynak, birbirinden baðýmsýz güncelleniyor |

---

## Çözüm Mimarisi

### Temel Fikir

Converter'lar `IValueConverter` arayüzünü implement eder ve **DI (Dependency Injection) alamaz**. Bu nedenle servis eriþimi için MAUI'nin `Application.Current` ? `MauiContext` ? `IServiceProvider` zinciri kullanýldý.

```
Converter.Convert()
    ??? ConverterHelpers.GetCurrentUserId()
            ??? Application.Current.Handler.MauiContext.Services
                    ??? IUserStateService.CurrentUserId
                            ??? _currentUser?.UserId  (bellek içi, þifreleme gerektirmez)
```

### Neden `IUserStateService`?

`IUserStateService`, uygulamanýn oturum durumunu tutan merkezi servistir:
- `SetUser(user)` — Giriþ sonrasý çaðrýlýr, `_currentUser`'ý günceller
- `ClearUser()` — Çýkýþ sonrasý çaðrýlýr, `_currentUser`'ý temizler
- Her iki durumda da `CurrentUserId` otomatik olarak doðru deðeri döndürür

---

## Deðiþtirilen Dosyalar

### `KamPay/Services/IUserStateService.cs`

```csharp
// YENÝ — eklenen property
string CurrentUserId { get; }
```

### `KamPay/Services/UserStateService.cs`

```csharp
// YENÝ — implement edilen property
public string CurrentUserId => _currentUser?.UserId ?? string.Empty;
```

### `KamPay/Converters/ProductConverters.cs`

**Yeni eklenen `ConverterHelpers` sýnýfý:**

```csharp
internal static class ConverterHelpers
{
    internal static string GetCurrentUserId()
    {
        try
        {
            var service = Application.Current?.Handler?.MauiContext?.Services
                .GetService(typeof(IUserStateService))
                as IUserStateService;
            return service?.CurrentUserId ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
```

**Deðiþtirilen 4 converter — `Preferences.Get` kaldýrýldý:**

```csharp
// ÖNCE
var currentUserId = Preferences.Get("current_user_id", string.Empty);

// SONRA
var currentUserId = ConverterHelpers.GetCurrentUserId();
```

Etkilenen sýnýflar:
- `MessageTimeColorConverter`
- `MessageTextColorConverter`
- `MessageBubbleAlignmentConverter`
- `MessageBubbleColorConverter`

### `KamPay/Converters/NegotiationStatusTextConverter.cs`

```csharp
// ÖNCE
var currentUserId = Preferences.Get("current_user_id", string.Empty);

// SONRA
var currentUserId = ConverterHelpers.GetCurrentUserId();
```

---

## Kod Deðiþiklikleri

### Önceki Durum (Riskli)

```csharp
public class MessageBubbleColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // ?? Þifresiz depodan okuma — her zaman boþ string döner
        var currentUserId = Preferences.Get("current_user_id", string.Empty);
        var senderId = value as string;

        return senderId == currentUserId
            ? Color.FromArgb("#4CAF50")  // Hiçbir zaman bu renge giremez
            : Color.FromArgb("#E0E0E0"); // Her mesaj bu renkte görünür
    }
}
```

### Sonraki Durum (Güvenli)

```csharp
internal static class ConverterHelpers
{
    // IUserStateService üzerinden bellek içi kullanýcý ID'si
    internal static string GetCurrentUserId()
    {
        try
        {
            var service = Application.Current?.Handler?.MauiContext?.Services
                .GetService(typeof(IUserStateService)) as IUserStateService;
            return service?.CurrentUserId ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}

public class MessageBubbleColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // ? Bellek içi, her zaman güncel, þifreleme gerektirmez
        var currentUserId = ConverterHelpers.GetCurrentUserId();
        var senderId = value as string;

        return senderId == currentUserId
            ? Color.FromArgb("#4CAF50") // Gönderilen mesaj — yeþil
            : Color.FromArgb("#E0E0E0"); // Gelen mesaj — gri
    }
}
```

---

## Veri Akýþý: Öncesi ve Sonrasý

### Önceki Akýþ

```
Kullanýcý Giriþ Yapar
    ??? FirebaseAuthService.SaveUserSessionAsync()
            ??? SecureStorage ? user_id, token, email  ?
            ??? Preferences   ? (HÝÇBÝR ÞEY YAZILMAZ)  ?

Converter Çalýþýr
    ??? Preferences.Get("current_user_id") ? ""  ?
            ??? TÜM MESAJLAR YANLIÞ HIZALANIYOR
```

### Sonraki Akýþ

```
Kullanýcý Giriþ Yapar
    ??? App.TryAutoLoginAsync()
            ??? userStateService.SetUser(user)
                    ??? _currentUser = user  ?

Converter Çalýþýr
    ??? ConverterHelpers.GetCurrentUserId()
            ??? IUserStateService.CurrentUserId
                    ??? _currentUser?.UserId  ? (her zaman güncel)
```

### Kullanýcý Deðiþimi

```
Kullanýcý Çýkýþ Yapar
    ??? FirebaseAuthService.LogoutAsync()
            ??? userStateService.ClearUser()
                    ??? _currentUser = null
                            ??? CurrentUserId = ""  ? (temizlendi)

Yeni Kullanýcý Giriþ Yapar
    ??? userStateService.SetUser(yeniKullanici)
            ??? CurrentUserId = yeniKullanici.UserId  ? (güncel)
```

---

## Ýlgili Dosyalar

- `KamPay/Services/IUserStateService.cs` — Arayüz
- `KamPay/Services/UserStateService.cs` — Uygulama
- `KamPay/Converters/ProductConverters.cs` — Mesaj converter'larý + `ConverterHelpers`
- `KamPay/Converters/NegotiationStatusTextConverter.cs` — Pazarlýk metni converter'ý
- `KamPay/Services/FirebaseAuthService.cs` — Oturum yönetimi (`SecureStorage`)

---

*Son güncelleme: KamPay devop branch — Güvenli Kullanýcý ID Eriþimi Refaktörü*
