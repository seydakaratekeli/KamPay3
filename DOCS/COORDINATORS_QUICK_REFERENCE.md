# ?? Koordinatör Servisler - Hýzlý Referans

## ?? Tüm Koordinatörler

| Koordinatör | Interface | Sorumluluk | Baðýmlýlýklar | DI Kaydý |
|------------|-----------|------------|---------------|----------|
| **CacheCoordinator** | `ICacheCoordinator` | Cache orkestrasyon ve strateji yönetimi | `IProductCacheService` | ? Singleton |
| **ValidationCoordinator** | `IValidationCoordinator` | Validation strateji orkestrasyon | `IProductService` | ? Singleton |
| **NotificationCoordinator** | `INotificationCoordinator` | Bildirim orkestrasyon ve batch iþlemler | `INotificationService` | ? Singleton |
| **TransactionOrchestrator** | `ITransactionOrchestrator` | Transaction workflow orkestrasyon | `ITransactionService`, `INotificationService`, `IProductService`, `IUserProfileService` | ? Singleton |
| **ProductCreationCoordinator** | `IProductCreationCoordinator` | Ürün oluþturma süreci orkestrasyon | `IProductService`, `IStorageService`, `IValidationCoordinator` | ? Singleton |
| **ProductImageCoordinator** | `IProductImageCoordinator` | Ürün görsel yönetimi orkestrasyon | `IStorageService` | ? Singleton |
| **MessageMediaCoordinator** | `IMessageMediaCoordinator` | Mesaj medya yönetimi orkestrasyon | `IStorageService`, `IMessagingService` | ? Singleton |

---

## ?? Koordinatör Kategorileri

### 1?? **Veri Yönetimi Koordinatörleri**

#### CacheCoordinator
```csharp
// Kullaným
var products = await _cacheCoordinator.GetOrSetAsync(
    "products", 
    () => _productService.GetAllProductsAsync()
);
```

**Özellikler:**
- ? Cache hit/miss tracking
- ? Pattern-based invalidation
- ? Cache statistics
- ? Automatic cleanup

---

### 2?? **Ýþ Mantýðý Koordinatörleri**

#### ValidationCoordinator
```csharp
// Kullaným
var result = _validationCoordinator.ValidateProduct(request);
if (!result.IsValid) {
    // Hata mesajlarýný göster
}
```

**Doðrulama Tipleri:**
- ? Product (ürün)
- ? Transaction (iþlem)
- ? User (kullanýcý)
- ? ServiceRequest (hizmet)
- ? CustomerRequest (müþteri talebi)
- ? ProviderProposal (profesyonel teklif)
- ? Message (mesaj)
- ? Negotiation (pazarlýk)
- ? Email, Password, Phone

---

#### TransactionOrchestrator
```csharp
// Kullaným - Satýþ
await _transactionOrchestrator.CreateSaleTransactionAsync(product, buyer, proposedPrice);

// Kullaným - Takas
await _transactionOrchestrator.CreateTradeTransactionAsync(product, offeredProductId, message, buyer);

// Kullaným - Baðýþ
await _transactionOrchestrator.CreateDonationTransactionAsync(product, receiver);
```

**Workflow Adýmlarý:**
1. Uygunluk kontrolü
2. Transaction oluþturma
3. Ürün rezerve etme
4. Bildirim gönderme
5. Pazarlýk yönetimi
6. Ödeme iþleme
7. Tamamlama ve cleanup

---

### 3?? **Ýletiþim Koordinatörleri**

#### NotificationCoordinator
```csharp
// Toplu bildirim
await _notificationCoordinator.SendBatchNotificationsAsync(
    userIds, 
    "Baþlýk", 
    "Mesaj", 
    NotificationType.SystemNotice
);

// Transaction bildirimi
await _notificationCoordinator.SendTransactionNotificationsAsync(
    transaction, 
    "accepted"
);
```

**Bildirim Tipleri:**
- ? Batch notifications
- ? Transaction-based notifications
- ? Scheduled notifications
- ? Priority notifications
- ? User preference-based notifications

---

### 4?? **Medya Yönetimi Koordinatörleri**

#### ProductImageCoordinator
```csharp
// Görsel yükleme
await _productImageCoordinator.UploadProductImagesAsync(
    productId, 
    imagePaths
);

// Görsel silme
await _productImageCoordinator.DeleteProductImagesAsync(productId);
```

**Özellikler:**
- ? Multi-image upload
- ? Image compression
- ? Thumbnail generation
- ? Batch delete

---

#### MessageMediaCoordinator
```csharp
// Mesaj görseli gönder
await _messageMediaCoordinator.SendImageMessageAsync(
    conversationId,
    sender,
    imagePath
);
```

**Özellikler:**
- ? Image upload for messages
- ? Media validation
- ? Storage management

---

#### ProductCreationCoordinator
```csharp
// Ürün oluþturma
await _productCreationCoordinator.CreateProductWithImagesAsync(
    productRequest,
    currentUser,
    imagePaths
);
```

**Workflow:**
1. Validation
2. Image upload
3. Product creation
4. Notification
5. Cache invalidation

---

## ?? Kullaným Senaryolarý

### Senaryo 1: Ürün Ekleme
```csharp
public class AddProductViewModel
{
    private readonly IProductCreationCoordinator _creationCoordinator;
    private readonly IValidationCoordinator _validationCoordinator;

    public async Task SaveProductAsync()
    {
        // 1. Validate
        var validation = _validationCoordinator.ValidateProduct(request);
        if (!validation.IsValid) return;

        // 2. Create with images
        var result = await _creationCoordinator.CreateProductWithImagesAsync(
            request, currentUser, imagePaths
        );
    }
}
```

### Senaryo 2: Ýþlem Onaylama
```csharp
public class OffersViewModel
{
    private readonly ITransactionOrchestrator _orchestrator;
    private readonly INotificationCoordinator _notificationCoordinator;

    public async Task AcceptOfferAsync(string transactionId)
    {
        // 1. Ýþlemi onayla ve workflow'u baþlat
        var result = await _orchestrator.ApproveAndProcessTransactionAsync(
            transactionId, accept: true, currentUserId
        );

        // 2. Ek bildirimler gönder (opsiyonel)
        if (result.Success)
        {
            await _notificationCoordinator.SendPriorityNotificationAsync(
                otherUserId, "Teklif Kabul Edildi", "..."
            );
        }
    }
}
```

### Senaryo 3: Cache Yönetimi
```csharp
public class ProductListViewModel
{
    private readonly ICacheCoordinator _cacheCoordinator;
    private readonly IProductService _productService;

    public async Task LoadProductsAsync(bool forceRefresh = false)
    {
        if (forceRefresh)
        {
            await _cacheCoordinator.InvalidateAsync("products");
        }

        var result = await _cacheCoordinator.GetOrSetAsync(
            "products",
            () => _productService.GetAllProductsAsync(),
            TimeSpan.FromMinutes(5)
        );
    }
}
```

---

## ?? Performans Ýpuçlarý

### Cache Kullanýmý
```csharp
// ? YANLIÞ - Her seferinde Firebase'den çek
var products = await _productService.GetAllProductsAsync();

// ? DOÐRU - Cache koordinatörü kullan
var products = await _cacheCoordinator.GetOrSetAsync(
    "products", 
    () => _productService.GetAllProductsAsync()
);
```

### Validation Kullanýmý
```csharp
// ? YANLIÞ - Her ViewModel'de ayrý validation
if (string.IsNullOrEmpty(email)) { ... }
if (!IsValidEmail(email)) { ... }

// ? DOÐRU - Merkezi validation koordinatörü
var result = _validationCoordinator.ValidateEmail(email);
if (!result.IsValid) { ... }
```

### Notification Kullanýmý
```csharp
// ? YANLIÞ - Tek tek bildirim gönder
foreach (var userId in userIds)
{
    await _notificationService.CreateNotificationAsync(...);
}

// ? DOÐRU - Batch notification kullan
await _notificationCoordinator.SendBatchNotificationsAsync(
    userIds, title, message, type
);
```

---

## ?? Debugging Ýpuçlarý

### Cache Ýstatistiklerini Görüntüle
```csharp
var stats = await _cacheCoordinator.GetCacheStatsAsync();
Console.WriteLine($"Hit Rate: {stats.Data.HitRate}%");
Console.WriteLine($"Total Requests: {stats.Data.TotalRequests}");
```

### Validation Hatalarýný Logla
```csharp
var result = _validationCoordinator.ValidateProduct(request);
if (!result.IsValid)
{
    foreach (var error in result.Errors)
    {
        Debug.WriteLine($"Validation Error: {error}");
    }
}
```

### Transaction Workflow'u Ýzle
```csharp
// TransactionOrchestrator içinde Debug.WriteLine kullanýyor
// Output penceresinden takip edebilirsiniz
```

---

## ?? Daha Fazla Bilgi

- **SRP Detaylý Dokümantasyon**: `DOCS/SRP_COORDINATORS_COMPLETED.md`
- **Mimari Rehber**: `DOCS/ARCHITECTURE.md`
- **Güvenlik Rehberi**: `DOCS/SECURITY_SUMMARY.md`

---

**Son Güncelleme:** 2025-01-XX  
**Durum:** ? Tamamlandý ve Aktif
