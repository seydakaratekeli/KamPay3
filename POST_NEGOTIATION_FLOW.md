# Pazarlık Tamamlandıktan Sonraki Akış (Post-Negotiation Flow)

## İçindekiler
1. [Genel Bakış](#genel-bakış)
2. [Anlaşma Sonrası Durum Geçişleri](#anlaşma-sonrası-durum-geçişleri)
3. [SATIŞ İçin Ödeme Akışı](#satiş-için-ödeme-akışı)
4. [TAKAS İçin Teslimat Akışı](#takas-için-teslimat-akışı)
5. [Bildirim ve İletişim](#bildirim-ve-iletişim)
6. [Hata Durumları ve İptal Senaryoları](#hata-durumları-ve-iptal-senaryoları)

---

## Genel Bakış

Pazarlık başarıyla tamamlandığında (`AcceptNegotiatedPriceAsync()` çağrıldığında), işlem şu duruma gelir:

```
Transaction Durumu:
├─ Status: Pending (Hala satıcının genel onayı bekleniyor)
├─ IsNegotiating: false (Pazarlık artık kapalı)
├─ QuotedPrice: [Anlaşılan Fiyat] (Kilitlendi)
├─ NegotiationNotes: [Detaylı Özet]
└─ UpdatedAt: [DateTime.UtcNow]
```

Bu noktadan sonra:
1. **Satıcı "Kabul Et" veya "Reddet" kararı verir**
2. Kabul edilirse → **Ödeme/Teslimat sürecine geçilir**
3. Reddedilirse → **İşlem iptal olur**

---

## Anlaşma Sonrası Durum Geçişleri

### Akış Diyagramı

```
┌─────────────────────────────────────────────────────────────────┐
│            Pazarlık Tamamlandıktan Sonraki Akış                 │
└─────────────────────────────────────────────────────────────────┘

    [AcceptNegotiatedPrice Çağrıldı]
    IsNegotiating = false
    QuotedPrice = Anlaşılan Fiyat
             │
             ▼
    ┌────────────────────┐
    │ Transaction        │
    │ Status: Pending    │
    │ (Satıcının Onayı   │
    │  Bekleniyor)       │
    └────────┬───────────┘
             │
             ├────────────────────┐
             │                    │
             ▼                    ▼
    ┌─────────────────┐   ┌──────────────────┐
    │ Satıcı          │   │ Satıcı           │
    │ "KABUL ET"      │   │ "REDDET"         │
    └────────┬────────┘   └────────┬─────────┘
             │                     │
             │                     ▼
             │            ┌──────────────────┐
             │            │ Status: Rejected │
             │            │ İşlem İptal      │
             │            └──────────────────┘
             │
             ▼
    ┌────────────────────┐
    │ Status: Accepted   │
    │ (Onaylandı)        │
    └────────┬───────────┘
             │
             ├─────────────────────────────┐
             │                             │
             ▼                             ▼
    ┌────────────────────┐       ┌────────────────────┐
    │ Type = Satis       │       │ Type = Takas       │
    └────────┬───────────┘       └────────┬───────────┘
             │                             │
             ▼                             ▼
    ┌────────────────────┐       ┌────────────────────┐
    │ ÖDEME SÜRECİ       │       │ TESLIMAT SÜRECİ    │
    │ (Payment Flow)     │       │ (QR Code Delivery) │
    └────────┬───────────┘       └────────┬───────────┘
             │                             │
             ▼                             ▼
    ┌────────────────────┐       ┌────────────────────┐
    │ Status: Completed  │       │ Status: Completed  │
    └────────────────────┘       └────────────────────┘
```

---

## SATIŞ İçin Ödeme Akışı

Satış işlemleri için pazarlık tamamlandıktan sonra **ödeme simülasyonu** yapılır.

### Adım 1: Satıcı Teklifi Kabul Eder

```csharp
// OffersPage.xaml.cs veya OffersViewModel.cs
await _transactionService.RespondToOfferAsync(transactionId, accept: true);

// Sonuç:
// - Transaction.Status = Accepted
// - Ürün rezerve edilir (IsReserved = true)
// - Alıcıya bildirim gönderilir: "Teklifiniz kabul edildi!"
```

### Adım 2: Alıcı Ödeme Sayfasına Gider

```
Alıcı Akışı:
1. "Teklifiniz kabul edildi" bildirimi alır
2. Offers sayfasında "Ödeme Yap" butonu görünür
3. PaymentPage'e yönlendirilir
```

### Adım 3: Ödeme Yöntemi Seçimi

```
PaymentPage:
├─ Seçenek 1: Kredi Kartı (Simülasyon)
│  ├─ OTP oluşturulur (6 haneli)
│  ├─ Ekranda gösterilir (test için)
│  └─ Kullanıcı OTP'yi girer ve onaylar
│
└─ Seçenek 2: Havale/EFT (Simülasyon)
   ├─ Banka bilgileri gösterilir
   ├─ Referans kodu gösterilir
   └─ Kullanıcı "Ödeme Yaptım" butonuna tıklar
```

### Adım 4: Ödeme Simülasyonu Başlatılır

```csharp
// PaymentViewModel.cs
var paymentResult = await _transactionService.CreatePaymentSimulationAsync(
    transactionId, 
    method: "cardsim" // veya "banktransfersim"
);

// Firebase'e kaydedilir:
// - payments/{paymentId}
// - tempotp/{paymentId} (sadece kredi kartı için, 2 dakika TTL)
```

### Adım 5: Ödeme Onaylanır

```csharp
// Kredi Kartı için:
await _transactionService.ConfirmPaymentSimulationAsync(
    transactionId, 
    paymentId, 
    otp: "123456"
);

// Havale/EFT için:
await _transactionService.ConfirmPaymentSimulationAsync(
    transactionId, 
    paymentId, 
    otp: null
);

// Sonuç:
// - Transaction.PaymentStatus = Paid
// - Transaction.Status = Completed
// - Ürün "Sold" işaretlenir
// - Satıcı ve alıcıya puan verilir
// - Bildirimler gönderilir
```

### Kod Örneği: CompletePaymentAsync

```csharp
public async Task<ServiceResult<Transaction>> CompletePaymentAsync(
    string transactionId, 
    string buyerId)
{
    var transaction = await GetTransactionAsync(transactionId);
    
    if (transaction == null) 
        return ServiceResult<Transaction>.FailureResult("İşlem bulunamadı");
    
    if (transaction.BuyerId != buyerId)
        return ServiceResult<Transaction>.FailureResult("Yetkiniz yok");
    
    if (transaction.Status != TransactionStatus.Accepted)
        return ServiceResult<Transaction>.FailureResult("İşlem henüz onaylanmadı");
    
    // Ödeme durumunu güncelle
    transaction.PaymentStatus = PaymentStatus.Paid;
    transaction.PaymentCompletedAt = DateTime.UtcNow;
    transaction.Status = TransactionStatus.Completed;
    transaction.UpdatedAt = DateTime.UtcNow;
    
    await SaveTransactionAsync(transaction);
    
    // Ürünü "Satıldı" işaretle
    await _productService.MarkAsSoldAsync(transaction.ProductId);
    
    // Puanları ver
    await _userProfileService.AddPointsAsync(transaction.SellerId, 10); // Satıcıya
    await _userProfileService.AddPointsAsync(transaction.BuyerId, 5);   // Alıcıya
    
    // Bildirimleri gönder
    await SendCompletionNotificationsAsync(transaction);
    
    return ServiceResult<Transaction>.SuccessResult(transaction);
}
```

---

## TAKAS İçin Teslimat Akışı

Takas işlemleri için pazarlık tamamlandıktan sonra **QR kod tabanlı teslimat** yapılır.

### Adım 1: Satıcı Teklifi Kabul Eder

```csharp
await _transactionService.RespondToOfferAsync(transactionId, accept: true);

// Sonuç:
// - Transaction.Status = Accepted
// - Her iki ürün de rezerve edilir
// - İki adet güvenli QR kod oluşturulur (birer taraf için)
```

### Adım 2: QR Kodlar Oluşturulur

```csharp
// FirebaseTransactionService.cs - RespondToOfferAsync içinde

if (transaction.Type == ProductType.Takas)
{
    // Satıcının ürünü için QR kod
    var qrCode1 = await _qrCodeService.GenerateSecureDeliveryQRCodeAsync(
        transactionId,
        transaction.ProductId,
        transaction.ProductTitle,
        transaction.SellerId,
        transaction.BuyerId,
        validityMinutes: 60
    );
    
    // Alıcının ürünü için QR kod
    var qrCode2 = await _qrCodeService.GenerateSecureDeliveryQRCodeAsync(
        transactionId,
        transaction.OfferedProductId,
        transaction.OfferedProductTitle,
        transaction.BuyerId,
        transaction.SellerId,
        validityMinutes: 60
    );
    
    transaction.DeliveryQRCodes.Add(qrCode1);
    transaction.DeliveryQRCodes.Add(qrCode2);
}
```

### Adım 3: Ek Nakit Ödemesi (Varsa)

Eğer pazarlık sonunda ek nakit anlaşması yapıldıysa (`QuotedPrice > 0`):

```csharp
// Ek nakit için ödeme simülasyonu
if (transaction.QuotedPrice > 0)
{
    await _transactionService.CreatePaymentSimulationAsync(
        transactionId,
        method: "cardsim" // veya "banktransfersim"
    );
    
    // Kullanıcı ödemeyi tamamlar
    await _transactionService.ConfirmPaymentSimulationAsync(
        transactionId,
        paymentId,
        otp
    );
}
```

### Adım 4: QR Kodları Tarama (Teslimat)

Her iki taraf da QR kodlarını tarar:

```csharp
// QR kod tarama ekranı (QRScannerPage)
await _qrCodeService.ValidateAndUseQRCodeAsync(scannedCode);

// Doğrulama:
// - QR kod geçerli mi?
// - Süresi dolmamış mı? (validityMinutes)
// - Doğru kişi mi tarıyor? (RecipientId kontrolü)
// - Daha önce kullanılmamış mı? (IsUsed = false)

// Başarılı tarama sonrası:
// - DeliveryQRCode.IsUsed = true
// - DeliveryQRCode.UsedAt = DateTime.UtcNow
```

### Adım 5: Her İki QR Tarandığında İşlem Tamamlanır

```csharp
// FirebaseTransactionService.cs

if (transaction.DeliveryQRCodes.All(qr => qr.IsUsed))
{
    // Her iki QR da tarandı, takas tamamlandı
    transaction.Status = TransactionStatus.Completed;
    transaction.UpdatedAt = DateTime.UtcNow;
    
    await SaveTransactionAsync(transaction);
    
    // Ürünleri "Takas Edildi" işaretle
    await _productService.MarkAsExchangedAsync(transaction.ProductId);
    await _productService.MarkAsExchangedAsync(transaction.OfferedProductId);
    
    // Puanları ver
    await _userProfileService.AddPointsAsync(transaction.SellerId, 15);
    await _userProfileService.AddPointsAsync(transaction.BuyerId, 15);
    
    // Bildirimleri gönder
    await SendCompletionNotificationsAsync(transaction);
}
```

---

## Bildirim ve İletişim

### Pazarlık Tamamlandığında

**Anlaşmayı Kabul Eden Kişiye:**
```
📊 Pazarlık İstatistikleri
━━━━━━━━━━━━━━━━━━━━
🔄 Tur Sayısı: 3
⏱️ Süre: 15 dakika
💰 Orijinal Fiyat: 1000.00₺
✅ Anlaşılan Fiyat: 850.00₺
📉 İndirim: %15.0
👤 Kabul Eden: Alıcı
📅 Tarih: 28.12.2024 14:30
```

**Diğer Tarafa Bildirim:**
```
Başlık: "Fiyat Anlaşması"
Mesaj: "'{ProductTitle}' için 850.00₺ fiyatı kabul edildi."
```

**Conversation Mesajı:**
```
📦 [Telefon - Satış]
✅ Anlaşma Sağlandı: 850.00₺
```

### Satıcı Teklifi Kabul Ettiğinde

**Alıcıya Bildirim:**
```
Başlık: "Teklifin Kabul Edildi!"
Mesaj: "{SellerName}, '{ProductTitle}' ürünü için yaptığın teklifi kabul etti."
ActionUrl: "OffersPage"
```

### Ödeme Tamamlandığında

**Satıcıya Bildirim:**
```
Başlık: "Ödeme Alındı!"
Mesaj: "{BuyerName}, '{ProductTitle}' için ödemeyi tamamladı."
```

**Alıcıya Bildirim:**
```
Başlık: "İşlem Tamamlandı"
Mesaj: "'{ProductTitle}' satın alımınız başarıyla tamamlandı!"
```

### QR Kod Tarandığında

**Her Tarama Sonrası:**
```
Başlık: "QR Kod Tarandı"
Mesaj: "{ScannerName}, '{ProductTitle}' için QR kodunu taradı."
```

**İşlem Tamamlandığında:**
```
Başlık: "Takas Tamamlandı!"
Mesaj: "'{ProductTitle}' ↔ '{OfferedProductTitle}' takası başarıyla tamamlandı!"
```

---

## Hata Durumları ve İptal Senaryoları

### Senaryo 1: Pazarlık Tamamlandı Ama Satıcı Reddetti

```
[AcceptNegotiatedPrice Çağrıldı]
IsNegotiating = false
QuotedPrice = 850₺
     │
     ▼
[Satıcı "Reddet" Tıkladı]
     │
     ▼
┌─────────────────────┐
│ Status: Rejected    │
│ İşlem İptal Edildi  │
└─────────────────────┘
     │
     ▼
[Bildirimler]
├─ Alıcıya: "Teklifiniz reddedildi"
└─ Ürün rezervasyonu kaldırılır
```

**Kod:**
```csharp
await _transactionService.RespondToOfferAsync(transactionId, accept: false);

// Sonuç:
// - Transaction.Status = Rejected
// - IsNegotiating = false
// - Ürün tekrar listeye döner (IsReserved = false)
```

### Senaryo 2: Ödeme Simülasyonu Başarısız

```
[Ödeme Başlatıldı]
     │
     ▼
[OTP Geçersiz / Süresi Dolmuş]
     │
     ▼
┌─────────────────────┐
│ PaymentStatus:      │
│ Failed              │
└─────────────────────┘
     │
     ▼
[Hata Mesajı]
"OTP geçersiz veya süresi dolmuş. 
Lütfen tekrar deneyin."
     │
     ▼
[Kullanıcı Tekrar Deneyebilir]
- CreatePaymentSimulationAsync() 
  tekrar çağrılabilir
```

### Senaryo 3: QR Kod Süresi Doldu

```
[QR Kod Oluşturuldu]
ValidityMinutes = 60
     │
     │ (60+ dakika geçti)
     ▼
[Kullanıcı QR Taramaya Çalışır]
     │
     ▼
┌─────────────────────┐
│ ❌ Hata             │
│ "QR kod süresi      │
│  dolmuş"            │
└─────────────────────┘
     │
     ▼
[Yeni QR Oluşturulmalı]
- Yönetici veya satıcı 
  yeni QR kod oluşturabilir
```

### Senaryo 4: Kullanıcı İptal Etti

```
[Transaction Status: Accepted]
Ödeme/Teslimat bekleniyor
     │
     ▼
[Kullanıcı "İptal Et" Tıkladı]
     │
     ▼
┌─────────────────────┐
│ Status: Cancelled   │
│ İşlem İptal Edildi  │
└─────────────────────┘
     │
     ▼
[Rollback İşlemleri]
├─ Ürün rezervasyonu kaldırılır
├─ Ödeme iade edilir (varsa)
└─ Her iki tarafa bildirim gönderilir
```

---

## Özet: Pazarlık Sonrası Tüm Süreç

### Başarılı SATIŞ Akışı

```
Pazarlık → Anlaşma → Satıcı Kabul → Ödeme → Tamamlandı
   (3 tur)   (850₺)    (Accepted)   (Paid)  (Completed)
```

**Toplam Süre:** ~5-15 dakika  
**Kullanıcı Etkileşimi:** 3-4 adım  
**Bildirim Sayısı:** 4-5 bildirim

### Başarılı TAKAS Akışı

```
Pazarlık → Anlaşma → Satıcı Kabul → QR Kodlar → Her İki QR Tarandı → Tamamlandı
   (2 tur)   (250₺)    (Accepted)   (Oluşturuldu)  (Teslimat OK)    (Completed)
```

**Toplam Süre:** ~30-60 dakika (teslimat zamanına bağlı)  
**Kullanıcı Etkileşimi:** 4-6 adım  
**Bildirim Sayısı:** 5-7 bildirim

---

## Gelecek İyileştirmeler

1. **Otomatik Hatırlatmalar**
   - Ödeme 24 saat içinde yapılmazsa hatırlatıcı
   - QR kod süresi dolmak üzereyken uyarı

2. **Daha Esnek QR Kod Yönetimi**
   - Süresi dolan QR kodları yenileyebilme
   - Manuel QR kod oluşturma

3. **Detaylı İstatistikler**
   - Pazarlık başarı oranları
   - Ortalama anlaşma süresi
   - En çok pazarlık yapılan kategoriler

4. **Kullanıcı Geri Bildirimleri**
   - İşlem sonrası değerlendirme
   - Diğer kullanıcıya puan verme

---

Bu dokümantasyon, pazarlık tamamlandıktan sonraki tüm süreci detaylı olarak açıklamaktadır. Her adımda ne olduğu, hangi bildirimlerin gönderildiği ve olası hata durumları belirtilmiştir.
