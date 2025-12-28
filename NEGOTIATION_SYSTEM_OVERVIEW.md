# Pazarlık Sistemi - Eksiksiz Genel Bakış

## 📋 İçindekiler
1. [Proje Özeti](#proje-özeti)
2. [Yapılan İyileştirmeler](#yapılan-iyileştirmeler)
3. [Temel Dosyalar ve Konumları](#temel-dosyalar-ve-konumları)
4. [Kullanım Senaryoları](#kullanım-senaryoları)
5. [Teknik Detaylar](#teknik-detaylar)
6. [Gelecek Öneriler](#gelecek-öneriler)

---

## Proje Özeti

KamPay uygulaması için pazarlık (negotiation) sisteminin tasarlanması ve geliştirilmesi tamamlanmıştır. Bu sistem, kullanıcıların ürün satışı ve takas işlemlerinde fiyat/değer üzerinde pazarlık yapmalarını sağlar.

### Desteklenen Pazarlık Tipleri

1. **SATIŞ Pazarlığı** 
   - Alıcı, listelenmiş fiyattan daha düşük bir fiyat teklif eder
   - Satıcı karşı teklif verebilir veya kabul/red edebilir
   - Anlaşma sağlandığında, belirlenen fiyat üzerinden satış gerçekleşir

2. **TAKAS Pazarlığı**
   - Alıcı, kendi ürünü ile satıcının ürününü takas etmek ister
   - Değer farkı varsa "ek nakit" pazarlığı yapılabilir
   - Anlaşma sağlandığında, ek nakit tutarı belirlenir ve takas gerçekleşir

---

## Yapılan İyileştirmeler

### 1. 📖 Kapsamlı Dokümantasyon

#### BARGAINING_FLOW_DESIGN.md
- **İçerik:** Detaylı pazarlık senaryoları, durum diyagramları, iş mantığı kuralları
- **Senaryolar:** 
  - Başarılı SATIŞ pazarlığı (Alıcı 850₺ teklif → Satıcı 950₺ karşı teklif → 950₺'de anlaşma)
  - Başarılı TAKAS pazarlığı (Laptop ↔ Tablet + 250₺ ek nakit)
  - Başarısız pazarlık (Anlaşma sağlanamadı)
- **Diyagramlar:** ASCII art ile durum makinesi, akış diyagramları

#### POST_NEGOTIATION_FLOW.md
- **İçerik:** Pazarlık sonrası ödeme ve teslimat süreçleri
- **Akışlar:**
  - SATIŞ için ödeme simülasyonu (Kredi Kartı, Havale/EFT)
  - TAKAS için QR kod tabanlı teslimat
  - Bildirim ve iletişim stratejisi
- **Hata Senaryoları:** OTP geçersiz, QR kod süresi doldu, kullanıcı iptal etti

#### Bu Dosya (NEGOTIATION_SYSTEM_OVERVIEW.md)
- Tüm sistemi bir araya getiren özet dokümantasyon

### 2. 🏗️ Model İyileştirmeleri

#### Transaction.cs
```csharp
// Yeni Alanlar
public DateTime? NegotiationStartedAt { get; set; }  // Pazarlık başlangıç zamanı
public int NegotiationRoundCount { get; set; } = 0;  // Kaç tur pazarlık yapıldı
```

#### ServiceOffer.cs (ServiceRequest)
```csharp
// Yeni Alanlar
public DateTime? NegotiationStartedAt { get; set; }
public int NegotiationRoundCount { get; set; } = 0;
public decimal AgreedPrice => CounterOfferByProvider ?? ProposedPriceByRequester ?? QuotedPrice ?? Price;
```

### 3. 🔒 İş Mantığı ve Doğrulama

#### NegotiationRules.cs (Yeni Dosya)
```csharp
// Sabitler
public const int MaxNegotiationRounds = 10;           // Maksimum 10 tur
public const int NegotiationTimeoutHours = 48;        // 48 saat içinde yanıt verilmeli
public const decimal MinOfferPercentage = 50m;        // En az %50 teklif edilebilir
public const decimal MaxCounterOfferPercentage = 100m; // En fazla %100 karşı teklif

// Metodlar
- CanContinueNegotiation()       // Pazarlık devam edebilir mi?
- ValidateProposedPrice()         // Teklif fiyatı geçerli mi?
- ValidateCounterOffer()          // Karşı teklif geçerli mi?
- ValidateAdditionalCash()        // Ek nakit geçerli mi?
- GetNegotiationSummary()         // Detaylı özet oluştur
- SuggestPrice()                  // Akıllı fiyat önerisi
```

### 4. 🔧 Service Güncellemeleri

#### FirebaseTransactionService.cs
Tüm pazarlık metodlarına eklenen özellikler:
- ✅ Tur sayısı takibi (`NegotiationRoundCount++`)
- ✅ Başlangıç zamanı kaydı (`NegotiationStartedAt`)
- ✅ Validasyon kontrolleri (`NegotiationRules.Validate*()`)
- ✅ Maksimum tur kontrolü
- ✅ Zaman aşımı kontrolü
- ✅ Detaylı anlaşma özeti

**Güncellenen Metodlar:**
- `ProposePriceForSaleAsync()` - Alıcının fiyat teklifi (SATIŞ)
- `SendCounterOfferForSaleAsync()` - Satıcının karşı teklifi (SATIŞ)
- `ProposeAdditionalCashAsync()` - Talep edenin ek nakit teklifi (TAKAS)
- `SendCounterCashOfferAsync()` - Sahibin ek nakit karşı teklifi (TAKAS)
- `AcceptNegotiatedPriceAsync()` - Anlaşmayı kabul etme (Ortak)

#### FirebaseServiceSharingService.cs
Benzer güncellemeler hizmet talepleri için uygulandı:
- `ProposePrice()` - Talep edenin fiyat teklifi
- `SendCounterOffer()` - Sağlayıcının karşı teklifi
- `AcceptNegotiatedPriceAsync()` - Anlaşmayı kabul etme

---

## Temel Dosyalar ve Konumları

```
KamPay3/
│
├── 📄 BARGAINING_FLOW_DESIGN.md          # Pazarlık akış tasarımı
├── 📄 POST_NEGOTIATION_FLOW.md           # Pazarlık sonrası akış
├── 📄 NEGOTIATION_SYSTEM_OVERVIEW.md     # Bu dosya (genel bakış)
│
└── KamPay/
    │
    ├── Models/
    │   ├── Transaction.cs                # İyileştirildi (NegotiationStartedAt, RoundCount)
    │   └── ServiceOffer.cs               # İyileştirildi (ServiceRequest için)
    │
    ├── Helpers/
    │   └── NegotiationRules.cs           # YENİ - Pazarlık kuralları ve validasyon
    │
    ├── Services/
    │   ├── FirebaseTransactionService.cs # Güncellendi (validasyon, tur takibi)
    │   └── FirebaseServiceSharingService.cs # Güncellendi (benzer özellikler)
    │
    ├── ViewModels/
    │   ├── OffersViewModel.cs            # Mevcut (pazarlık komutları)
    │   └── ServiceRequestsViewModel.cs   # Mevcut (servis pazarlık komutları)
    │
    └── Views/
        ├── OffersPage.xaml               # Mevcut (pazarlık UI)
        └── ServiceRequestsPage.xaml      # Mevcut (servis pazarlık UI)
```

---

## Kullanım Senaryoları

### Senaryo 1: SATIŞ Pazarlığı - Mobil Telefon

**Başlangıç Durumu:**
- Satıcı Ali bir iPhone 13'ü 10,000₺'ye listeler
- Alıcı Ayşe ürünü görür ve pazarlık etmek ister

**Adımlar:**

1️⃣ **Ayşe Teklif Verir**
```
Ayşe: "Teklif Ver" → 8,500₺ girer
Sistem Kontrolü:
  ✓ 8,500₺ > 5,000₺ (minimum %50)
  ✓ Tur sayısı: 1/10
  ✓ Zaman aşımı yok
Sonuç: Teklif kabul edildi
```

2️⃣ **Ali Karşı Teklif Verir**
```
Ali: Bildirim alır → "Karşı Teklif" → 9,500₺ girer
Sistem Kontrolü:
  ✓ 9,500₺ ≤ 10,000₺ (maksimum %100)
  ✓ 9,500₺ ≥ 8,500₺ (mantıklı karşı teklif)
  ✓ Tur sayısı: 2/10
Sonuç: Karşı teklif kabul edildi
```

3️⃣ **Ayşe Yeni Teklif Verir**
```
Ayşe: "Yeni Teklif" → 9,000₺ girer
Sistem Kontrolü:
  ✓ Geçerli teklif
  ✓ Tur sayısı: 3/10
Sonuç: Teklif kabul edildi
```

4️⃣ **Ali Anlaşmayı Kabul Eder**
```
Ali: "Anlaşmayı Kabul Et" tıklar
Sistem:
  - IsNegotiating = false
  - QuotedPrice = 9,000₺
  - NegotiationNotes = "📊 Pazarlık İstatistikleri..."
Sonuç: Pazarlık tamamlandı! Fiyat 9,000₺'de kilitlendi.
```

5️⃣ **Ali Teklifi Onaylar**
```
Ali: "Kabul Et" butonu
Sistem:
  - Status = Accepted
  - Ürün rezerve edilir
  - Ayşe'ye bildirim gönderilir
```

6️⃣ **Ayşe Ödeme Yapar**
```
Ayşe: PaymentPage'e gider
  → Kredi Kartı seçer
  → OTP alır ve girer
  → Ödeme tamamlanır

Sistem:
  - PaymentStatus = Paid
  - Status = Completed
  - Ürün "Satıldı" işaretlenir
  - Her iki tarafa puan verilir
```

**Sonuç:** ✅ Başarılı! Telefon 9,000₺'ye satıldı.  
**İstatistikler:**
- Pazarlık Turu: 3
- Süre: ~15 dakika
- İndirim: %10

---

### Senaryo 2: TAKAS Pazarlığı - Laptop ↔ Tablet

**Başlangıç Durumu:**
- Satıcı Mehmet bir Macbook Pro listeler
- Alıcı Zeynep bir iPad Pro'ya sahip ve takas yapmak ister

**Adımlar:**

1️⃣ **Zeynep Takas Teklifi Verir**
```
Zeynep: "Takas Teklifi Ver" → iPad Pro'yu seçer
Sistem:
  - Transaction oluşturulur
  - Type = Takas
  - OfferedProductId = iPad Pro'nun ID'si
  - Status = Pending
```

2️⃣ **Zeynep Ek Nakit Teklif Eder**
```
Zeynep: "Ek Nakit Teklif Et" → 2,000₺ girer
Sistem Kontrolü:
  ✓ 2,000₺ ≥ 0
  ✓ Tur sayısı: 1/10
Sonuç: Ek nakit teklifi kabul edildi
```

3️⃣ **Mehmet Karşı Teklif Verir**
```
Mehmet: "Karşı Teklif" → 3,000₺ ister
Sistem Kontrolü:
  ✓ 3,000₺ ≥ 0
  ✓ Tur sayısı: 2/10
Sonuç: Karşı teklif kabul edildi
```

4️⃣ **Zeynep Son Teklif Verir**
```
Zeynep: "Yeni Teklif" → 2,500₺ teklif eder
Sistem:
  ✓ Tur sayısı: 3/10
Sonuç: Teklif kabul edildi
```

5️⃣ **Mehmet Anlaşmayı Kabul Eder**
```
Mehmet: "Anlaşmayı Kabul Et"
Sistem:
  - IsNegotiating = false
  - QuotedPrice = 2,500₺ (ek nakit)
  - NegotiationNotes = "📊 Pazarlık İstatistikleri..."
```

6️⃣ **Mehmet Takası Onaylar**
```
Mehmet: "Kabul Et"
Sistem:
  - Status = Accepted
  - Her iki ürün rezerve edilir
  - QR kodlar oluşturulur (2 adet)
  - Zeynep'e bildirim gönderilir
```

7️⃣ **Zeynep Ek Nakit Ödeme Yapar**
```
Zeynep: 2,500₺ için ödeme simülasyonu
Sistem:
  - PaymentStatus = Paid
```

8️⃣ **Teslimat (QR Tarama)**
```
Buluşma noktasında:
  - Mehmet, Zeynep'in QR kodunu tarar (iPad Pro teslim)
  - Zeynep, Mehmet'in QR kodunu tarar (Macbook teslim)

Sistem:
  - Her iki QR.IsUsed = true
  - Status = Completed
  - Ürünler "Takas Edildi" işaretlenir
  - Her iki tarafa puan verilir
```

**Sonuç:** ✅ Başarılı! Macbook Pro ↔ iPad Pro + 2,500₺ takası tamamlandı.  
**İstatistikler:**
- Pazarlık Turu: 3
- Süre: ~20 dakika (pazarlık) + ~30 dakika (teslimat)
- Anlaşılan Ek Nakit: 2,500₺

---

## Teknik Detaylar

### Veri Yapısı

```csharp
// Transaction modeli - Tüm pazarlık bilgileri
{
    TransactionId: "tx-12345",
    Type: ProductType.Satis,  // veya Takas
    Status: TransactionStatus.Pending,
    
    // Taraflar
    SellerId: "user-ali",
    BuyerId: "user-ayse",
    
    // Pazarlık Bilgileri
    IsNegotiating: true,
    NegotiationStartedAt: "2024-12-28T12:00:00Z",
    NegotiationRoundCount: 3,
    LastNegotiationDate: "2024-12-28T12:15:00Z",
    
    // SATIŞ için
    ProposedPriceByBuyer: 8500,      // Ayşe'nin teklifi
    CounterOfferBySeller: 9500,       // Ali'nin karşı teklifi
    
    // TAKAS için (bu örnekte null)
    AdditionalCashByRequester: null,
    CounterCashByOwner: null,
    
    // Fiyatlandırma
    Price: 10000,           // Orijinal fiyat
    QuotedPrice: 9000,      // Anlaşılan fiyat (kilitli)
    
    // Notlar
    NegotiationNotes: "📊 Pazarlık İstatistikleri\n..."
}
```

### Validasyon Kuralları

| Kural | Değer | Açıklama |
|-------|-------|----------|
| Maksimum Tur | 10 | 10 tur sonra otomatik sonlandırma |
| Zaman Aşımı | 48 saat | 48 saat içinde yanıt verilmeli |
| Min Teklif | %50 | Orijinal fiyatın en az %50'si |
| Max Karşı Teklif | %100 | Orijinal fiyatı aşamaz |
| Ek Nakit | ≥ 0 | Negatif olamaz, 0 olabilir |

### Firebase Veri Yolu

```
firebase-db/
├── transactions/
│   └── {transactionId}/
│       ├── IsNegotiating: true/false
│       ├── NegotiationStartedAt: timestamp
│       ├── NegotiationRoundCount: number
│       └── ... (diğer alanlar)
│
├── serviceRequests/
│   └── {requestId}/
│       └── ... (benzer alanlar)
│
└── tempotp/                    # Ödeme OTP'leri (2 dk TTL)
    └── {paymentId}/
        ├── Otp: "123456"
        └── ExpiresAt: timestamp
```

---

## Gelecek Öneriler

### 1. 🤖 Otomasyonlar

**Otomatik Hatırlatıcılar:**
```csharp
// Background service
public async Task CheckExpiredNegotiations()
{
    var expiredTransactions = await GetTransactionsAsync(
        t => t.IsNegotiating && 
             t.LastNegotiationDate < DateTime.UtcNow.AddHours(-24)
    );
    
    foreach (var transaction in expiredTransactions)
    {
        // Hatırlatıcı gönder
        await SendReminderNotification(transaction);
    }
}
```

**Otomatik Sonlandırma:**
```csharp
// 48 saat içinde yanıt verilmezse otomatik iptal
if (NegotiationRules.IsNegotiationExpired(transaction.NegotiationStartedAt))
{
    transaction.Status = TransactionStatus.Cancelled;
    transaction.IsNegotiating = false;
    // Bildirimleri gönder
}
```

### 2. 📊 Analitik ve İstatistikler

**Kullanıcı Başına İstatistikler:**
```csharp
public class NegotiationStats
{
    public int TotalNegotiations { get; set; }
    public int SuccessfulNegotiations { get; set; }
    public decimal AverageDiscountPercentage { get; set; }
    public double AverageDurationMinutes { get; set; }
    public int AverageRoundsPerNegotiation { get; set; }
}
```

**Dashboard Metrikleri:**
- Günlük/Haftalık pazarlık sayısı
- Başarı oranı (Completed / Total)
- En popüler kategoriler
- Ortalama indirim oranları

### 3. 🎨 UI/UX İyileştirmeleri

**Pazarlık Geçmişi Görünümü:**
```xaml
<ListView ItemsSource="{Binding NegotiationHistory}">
    <ListView.ItemTemplate>
        <DataTemplate>
            <Frame>
                <StackLayout>
                    <Label Text="{Binding RoundNumber}" />
                    <Label Text="{Binding OfferedBy}" />
                    <Label Text="{Binding Amount}" />
                    <Label Text="{Binding CreatedAt}" />
                </StackLayout>
            </Frame>
        </DataTemplate>
    </ListView.ItemTemplate>
</ListView>
```

**Akıllı Fiyat Önerisi UI:**
```xaml
<Frame BackgroundColor="LightYellow">
    <StackLayout>
        <Label Text="💡 Akıllı Öneri" FontAttributes="Bold" />
        <Label Text="{Binding SuggestedPrice, StringFormat='Önerilen Fiyat: {0:N2}₺'}" />
        <Button Text="Bu Fiyatı Teklif Et" Command="{Binding AcceptSuggestionCommand}" />
    </StackLayout>
</Frame>
```

### 4. 🔔 Gelişmiş Bildirim Sistemi

**Push Notifications:**
```csharp
// Firebase Cloud Messaging entegrasyonu
await _fcmService.SendPushNotificationAsync(
    userId: transaction.SellerId,
    title: "Yeni Teklif!",
    body: $"{buyerName} {proposedPrice:N2}₺ teklif etti",
    data: new {
        type = "negotiation",
        transactionId = transaction.TransactionId
    }
);
```

**In-App Notifications:**
- Pazarlık güncellemeleri için gerçek zamanlı bildirimler
- "Typing..." göstergesi (karşı taraf cevap yazıyor)
- Okundu işaretleri

### 5. 🔒 Güvenlik İyileştirmeleri

**Rate Limiting:**
```csharp
// Kullanıcı başına dakikada maksimum 5 teklif
public class NegotiationRateLimiter
{
    private Dictionary<string, Queue<DateTime>> _userActions = new();
    
    public bool CanMakeOffer(string userId)
    {
        if (!_userActions.ContainsKey(userId))
            _userActions[userId] = new Queue<DateTime>();
        
        var queue = _userActions[userId];
        var now = DateTime.UtcNow;
        
        // Son 1 dakikadaki işlemleri temizle
        while (queue.Count > 0 && (now - queue.Peek()).TotalMinutes > 1)
            queue.Dequeue();
        
        return queue.Count < 5; // Max 5 işlem/dakika
    }
}
```

**Fraud Detection:**
```csharp
// Şüpheli aktivite tespiti
public async Task<bool> DetectSuspiciousActivity(Transaction transaction)
{
    // Çok hızlı teklif değişiklikleri
    if (transaction.NegotiationRoundCount > 5 && 
        transaction.LastNegotiationDate - transaction.NegotiationStartedAt < TimeSpan.FromMinutes(2))
    {
        await FlagForReview(transaction);
        return true;
    }
    
    // Anormal fiyat değişimleri
    // Bot davranışı
    // vb...
    
    return false;
}
```

### 6. 🌐 Çok Dil Desteği

**Localization:**
```csharp
// Resources/AppResources.tr.resx
NegotiationStarted = "Pazarlık başladı"
NegotiationCompleted = "Pazarlık tamamlandı"
OfferAccepted = "Teklif kabul edildi"

// Resources/AppResources.en.resx
NegotiationStarted = "Negotiation started"
NegotiationCompleted = "Negotiation completed"
OfferAccepted = "Offer accepted"
```

### 7. 🧪 Test Senaryoları

**Unit Tests:**
```csharp
[Test]
public void ValidateProposedPrice_BelowMinimum_ShouldFail()
{
    var result = NegotiationRules.ValidateProposedPrice(
        proposedPrice: 400,
        originalPrice: 1000
    );
    
    Assert.IsFalse(result.IsValid);
    Assert.IsTrue(result.ErrorMessage.Contains("Minimum: 500"));
}

[Test]
public void CanContinueNegotiation_MaxRoundsReached_ShouldFail()
{
    var result = NegotiationRules.CanContinueNegotiation(
        currentRounds: 10,
        negotiationStartedAt: DateTime.UtcNow
    );
    
    Assert.IsFalse(result.IsValid);
}
```

**Integration Tests:**
```csharp
[Test]
public async Task FullNegotiationFlow_Success()
{
    // 1. Transaction oluştur
    var transaction = await CreateTransaction();
    
    // 2. Alıcı teklif ver
    await ProposePriceAsync(transaction.Id, 850);
    
    // 3. Satıcı karşı teklif ver
    await SendCounterOfferAsync(transaction.Id, 950);
    
    // 4. Alıcı kabul et
    await AcceptNegotiatedPriceAsync(transaction.Id);
    
    // 5. Doğrula
    var updated = await GetTransactionAsync(transaction.Id);
    Assert.IsFalse(updated.IsNegotiating);
    Assert.AreEqual(950, updated.QuotedPrice);
}
```

---

## 🎯 Özet

Pazarlık sistemi tamamen tasarlandı ve geliştirildi. Sistem:

✅ **Kapsamlı dokümantasyona** sahip  
✅ **Güçlü validasyon** kuralları içeriyor  
✅ **Detaylı takip** mekanizmaları mevcut  
✅ **Kullanıcı dostu** akışlar tasarlandı  
✅ **Güvenli ve ölçeklenebilir** altyapı  
✅ **Gelecek iyileştirmeler** için hazır  

Sistem şu anda tamamen çalışır durumda ve kullanıcılar hem satış hem de takas işlemlerinde pazarlık yapabilirler. Tüm kurallar, limitler ve doğrulamalar yerinde olup, sisteme eklenen yeni özelliklerle daha da güçlendirilebilir.

---

**Hazırlayan:** GitHub Copilot  
**Tarih:** 28 Aralık 2024  
**Versiyon:** 1.0
