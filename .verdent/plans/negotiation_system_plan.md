# KamPay Pazarlık Sistemi — Production-Level Yeniden Tasarım Planı

> **Tarih:** 2026-05-04 | **Rol:** Senior Full-Stack Mühendis & Sistem Mimarı

---

## 1. Problem Analizi

### Kullanıcı Senaryosu
1. Satıcı ürün ekler (365₺)
2. Alıcı teklif gönderir (350₺)
3. Satıcı karşı teklif gönderir (355₺)
4. **BUG:** Eski teklif (350₺) hala aktif görünür, "Kabul Et" / "Karşı Teklif" butonları eski tekliflerde de gösterilmeye devam eder

### Root Cause Analizi

**Ana Sorun: Flat veri modeli — Offer Chain yokluğu**

Mevcut `Transaction` modeli tüm pazarlık state'ini **iki skaler field** ile tutuyor:

```
ProposedPriceByBuyer  → decimal? (tek değer)
CounterOfferBySeller  → decimal? (tek değer)
```

Bu tasarım **sadece 1 tur** pazarlığı destekler. Karşı teklif geldiğinde eski teklif **overwrite** edilmez, iki alan paralel yaşar. UI her ikisini de "aktif" olarak gösterir.

**Katkıda bulunan faktörler:**

| # | Sorun | Dosya | Etki |
|---|-------|-------|------|
| RC-1 | Offer zinciri yok, teklifler skaler field | `Transaction.cs:69-72` | Eski teklifler pasif yapılamıyor |
| RC-2 | `IsNegotiating` boolean — state machine yok | `Transaction.cs:93` | Teklifin kime ait olduğu belli değil |
| RC-3 | UI buton visibility sadece `IsPending + IsNegotiating` kontrolü | `OffersPage.xaml:314-323` | Sıra kimin olduğuna bakmıyor |
| RC-4 | `CanAcceptNegotiationConverter` sadece `CounterOfferBySeller.HasValue` kontrol ediyor | `CanAcceptNegotiationConverter.cs:20` | Her iki taraf her zaman kabul edebiliyor |
| RC-5 | Firebase PutAsync ile **tüm transaction** overwrite — race condition | `TransactionNegotiationService.cs:108-111` | Eşzamanlı işlemlerde veri kaybı |

---

## 2. Domain Model Tasarımı

### 2.1 Offer Entity (YENİ)

```csharp
public class NegotiationOffer
{
    public string OfferId { get; set; } = Guid.NewGuid().ToString();
    public string TransactionId { get; set; } = "";
    
    // Kim, ne zaman, ne kadar
    public string ProposerId { get; set; } = "";
    public string ProposerName { get; set; } = "";
    public ProposerRole Role { get; set; } // Buyer veya Seller
    public decimal Amount { get; set; }
    
    // State
    public OfferStatus Status { get; set; } = OfferStatus.Active;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }
    
    // Zincir
    public string? ParentOfferId { get; set; } // Hangi teklife karşılık?
    public int RoundNumber { get; set; }
}

public enum OfferStatus
{
    Active,      // Yanıt bekliyor
    Superseded,  // Yeni teklif geldi, bu artık geçersiz
    Accepted,    // Kabul edildi
    Rejected,    // Reddedildi
    Expired      // Süre doldu
}

public enum ProposerRole { Buyer, Seller }
```

### 2.2 Transaction State Machine

```
         ┌──────────┐
         │  Created  │
         └────┬─────┘
              │ Alıcı teklif gönderir
              ▼
    ┌─────────────────┐
    │    Pending       │◄─────────────────────┐
    │  (İlk teklif)    │                      │
    └────┬───────┬────┘                      │
         │       │                            │
    Accept│  Counter│Offer                     │
         │       │                            │
         ▼       ▼                            │
  ┌──────────┐  ┌──────────────┐              │
  │ Accepted │  │ Negotiating  │──Counter──────┘
  └──────────┘  │ (Pazarlık)   │
                └──┬───┬───┬──┘
              Accept│  Reject│  Expire│
                   ▼       ▼       ▼
            ┌──────────┐ ┌────────┐ ┌─────────┐
            │ Accepted │ │Rejected│ │Cancelled│
            └──────────┘ └────────┘ └─────────┘
```

**Yeni Transaction Status:**

```csharp
public enum TransactionStatus
{
    Pending,      // İlk teklif yapıldı
    Negotiating,  // Pazarlık devam ediyor (YENİ)
    Accepted,     // Anlaşıldı
    Rejected,     // Reddedildi
    Completed,    // Ödeme/teslimat tamamlandı
    Cancelled,    // İptal/süre doldu
    Expired       // Pazarlık süresi doldu (YENİ)
}
```

---

## 3. Pazarlık Akışı — Tüm Senaryolar

### Senaryo 1: Teklif → Karşı Teklif → Karşı Teklif Zinciri
```
Alıcı: 350₺ teklif → Offer(Active, Round=1, ParentId=null)
  → Önceki offer yok, direkt Active
Satıcı: 355₺ karşı teklif → Offer(Active, Round=2, Parent=offer1)
  → offer1.Status = Superseded
Alıcı: 352₺ yeni teklif → Offer(Active, Round=3, Parent=offer2)
  → offer2.Status = Superseded
```

### Senaryo 2: Teklif Kabul
```
Son aktif offer → Status = Accepted
Transaction.Status = Accepted
Transaction.QuotedPrice = offer.Amount
```

### Senaryo 3: Teklif Reddi
```
Son aktif offer → Status = Rejected
Transaction.Status = Rejected
```

### Senaryo 4: Aynı anda iki teklif (Race Condition)
```
Çözüm: Firebase transaction + LastActionBy + server timestamp
İki teklif aynı anda gelirse → ikinci teklif "sıra sizde değil" hatası alır
```

### Senaryo 5: Kendi teklifine işlem yapma
```
Guard: offer.ProposerId == currentUserId → "Kendi teklifinize işlem yapamazsınız"
```

### Senaryo 6: Ürün satıldıktan sonra teklif
```
Guard: Product.IsReserved || Product.IsSold → "Bu ürün artık satışta değil"
Mevcut: AutoRejectOtherPendingOffersAsync zaten var ✅
```

### Senaryo 7: Uzun pazarlık zinciri
```
Mevcut: MaxNegotiationRounds = 10 ✅
Mevcut: NegotiationTimeoutHours = 48 ✅
```

### Senaryo 8: Eşzamanlı aksiyon (Race Condition)
```
Sorun: Read-Modify-Write pattern, PutAsync tüm objeyi yazıyor
Çözüm: Firebase PatchAsync ile sadece değişen field'ları güncelle
+ optimistic locking (UpdatedAt kontrolü)
```

---

## 4. Edge Case Analizi

| # | Edge Case | Mevcut Durum | Çözüm |
|---|-----------|-------------|-------|
| E-1 | İki kullanıcı aynı anda karşı teklif | ❌ Son yazan kazanır (data loss) | PatchAsync + LastActionBy guard |
| E-2 | Alıcı üst üste teklif | ✅ LastActionBy ile engelleniyor | Mevcut yeterli |
| E-3 | Stale UI state | ❌ Eski teklif butonları görünür | Offer chain + aktif offer kontrolü |
| E-4 | Duplicate teklif (aynı tutar) | ✅ ViewModel'de kontrol var | Mevcut yeterli |
| E-5 | Pazarlık sürerken ürün silme | ❌ Kontrol yok | Product delete → auto-reject tüm pending |
| E-6 | Network kesintisi sırasında teklif | ❌ Hata sonrası UI senkron değil | Retry + pessimistic UI update |
| E-7 | Conversation silinmiş ama transaction aktif | ⚠️ Kısmi kontrol var | ConversationId null check eklenmeli |
| E-8 | Aynı ürüne birden fazla alıcıdan pazarlık | ✅ AutoReject var ama sadece kabul sonrası | Kabul öncesi de uyarı göster |

---

## 5. Mimari Çözüm

### 5.1 Genel Mimari

```
┌─────────────────────────────────────────────────┐
│  UI Layer (XAML + Converters)                    │
│  OffersPage.xaml → buton visibility              │
│  CanNegotiateConverter → aktif offer kontrolü    │
│  IsMyTurnConverter → sıra kontrolü               │
└──────────────┬──────────────────────────────────┘
               │ Data Binding (MVVM)
┌──────────────▼──────────────────────────────────┐
│  ViewModel Layer                                 │
│  OffersViewModel → komut yönlendirme             │
│  ChatViewModel.Negotiation → chat pazarlık       │
└──────────────┬──────────────────────────────────┘
               │ DI (Interface)
┌──────────────▼──────────────────────────────────┐
│  Service Layer                                   │
│  NegotiationService (YENİ — tek sorumluluk)      │
│   ├── CreateOffer()                              │
│   ├── CreateCounterOffer()                       │
│   ├── AcceptOffer()                              │
│   ├── RejectOffer()                              │
│   └── GetActiveOffer()                           │
│  TransactionNegotiationService (refactor)         │
└──────────────┬──────────────────────────────────┘
               │
┌──────────────▼──────────────────────────────────┐
│  Data Layer (Firebase Realtime DB)               │
│  transactions/{id}     → Transaction             │
│  negotiation_offers/{txId}/{offerId} → Offer     │
└─────────────────────────────────────────────────┘
```

### 5.2 Veri Akışı (Data Flow)

```
1. Alıcı "Teklif Ver" → ViewModel → NegotiationService.CreateOfferAsync()
2. Service:
   a. Transaction'ı oku
   b. Mevcut aktif offer varsa → Superseded yap
   c. Yeni offer oluştur (Active)
   d. Transaction güncelle (LastActionBy, RoundCount)
   e. PatchAsync ile atomik yaz
   f. Bildirim gönder
3. Firebase listener → ViewModel → UI otomatik güncellenir
4. UI: Sadece Status=Active olan offer için buton göster
```

---

## 6. Veritabanı Tasarımı

### 6.1 Firebase Yapısı

```
transactions/
  {transactionId}/
    ... (mevcut alanlar)
    CurrentActiveOfferId: "offer_xyz"     ← YENİ
    NegotiationStatus: "Negotiating"      ← YENİ (ayrı enum)

negotiation_offers/                        ← YENİ KOLEKSİYON
  {transactionId}/
    {offerId}/
      OfferId: "..."
      TransactionId: "..."
      ProposerId: "userId"
      ProposerName: "Ali"
      Role: "Buyer" | "Seller"
      Amount: 350.00
      Status: "Active" | "Superseded" | "Accepted" | "Rejected" | "Expired"
      CreatedAt: "2026-05-04T..."
      RespondedAt: null
      ParentOfferId: null | "prev_offer_id"
      RoundNumber: 1
```

### 6.2 Firebase Index Gereksinimleri

```json
{
  "negotiation_offers": {
    "$transactionId": {
      ".indexOn": ["Status", "CreatedAt", "ProposerId"]
    }
  }
}
```

### 6.3 Transaction Model Güncellemesi

```csharp
// Transaction.cs — EKLENECEK ALANLAR
public string? CurrentActiveOfferId { get; set; }

// KALDIRILACAK (Offer entity'sine taşınacak):
// ProposedPriceByBuyer → Offer.Amount (Role=Buyer)
// CounterOfferBySeller → Offer.Amount (Role=Seller)

// GERİYE UYUMLULUK: Migration sürecinde her iki alan da kalır
// Yeni kod Offer entity kullanır, eski veriler eski alanlardan okunur
```

---

## 7. API / Service Layer

### 7.1 INegotiationOfferService (YENİ)

```csharp
public interface INegotiationOfferService
{
    /// Yeni teklif oluştur (alıcı veya satıcı)
    Task<ServiceResult<NegotiationOffer>> CreateOfferAsync(
        string transactionId, decimal amount, string currentUserId);

    /// Aktif teklifi kabul et
    Task<ServiceResult<bool>> AcceptActiveOfferAsync(
        string transactionId, string currentUserId);

    /// Aktif teklifi reddet
    Task<ServiceResult<bool>> RejectActiveOfferAsync(
        string transactionId, string currentUserId);

    /// Transaction'ın aktif teklifini getir
    Task<NegotiationOffer?> GetActiveOfferAsync(string transactionId);

    /// Transaction'ın tüm teklif geçmişini getir
    Task<List<NegotiationOffer>> GetOfferHistoryAsync(string transactionId);
}
```

### 7.2 CreateOfferAsync — İş Mantığı

```csharp
public async Task<ServiceResult<NegotiationOffer>> CreateOfferAsync(
    string transactionId, decimal amount, string currentUserId)
{
    // 1. VALIDATION
    var transaction = await GetTransaction(transactionId);
    if (transaction == null) return Fail("İşlem bulunamadı");
    if (transaction.IsFixedPriceRequest) return Fail("Pazarlık yapılamaz");
    if (transaction.Status != Pending && transaction.Status != Negotiating)
        return Fail("İşlem artık pazarlık aşamasında değil");

    // 2. YETKİ KONTROLÜ
    bool isBuyer = transaction.BuyerId == currentUserId;
    bool isSeller = transaction.SellerId == currentUserId;
    if (!isBuyer && !isSeller) return Fail("Yetkiniz yok");

    // 3. SIRA KONTROLÜ
    if (transaction.LastActionBy == currentUserId)
        return Fail("Karşı tarafın yanıtını beklemeniz gerekiyor");

    // 4. LİMİT KONTROLÜ
    var canContinue = NegotiationRules.CanContinueNegotiation(
        transaction.NegotiationRoundCount, transaction.NegotiationStartedAt);
    if (!canContinue.IsValid) return Fail(canContinue.ErrorMessage);

    // 5. FİYAT DOĞRULAMA
    if (isBuyer)
    {
        var priceCheck = NegotiationRules.ValidateProposedPrice(amount, transaction.Price);
        if (!priceCheck.IsValid) return Fail(priceCheck.ErrorMessage);
    }
    else
    {
        var activeOffer = await GetActiveOfferAsync(transactionId);
        var counterCheck = NegotiationRules.ValidateCounterOffer(
            amount, transaction.Price, activeOffer?.Amount);
        if (!counterCheck.IsValid) return Fail(counterCheck.ErrorMessage);
    }

    // 6. ESKİ AKTİF TEKLİFİ SUPERSEDED YAP
    var currentActive = await GetActiveOfferAsync(transactionId);
    if (currentActive != null)
    {
        currentActive.Status = OfferStatus.Superseded;
        currentActive.RespondedAt = DateTime.UtcNow;
        await UpdateOffer(transactionId, currentActive);
    }

    // 7. YENİ TEKLİF OLUŞTUR
    var offer = new NegotiationOffer
    {
        TransactionId = transactionId,
        ProposerId = currentUserId,
        ProposerName = isBuyer ? transaction.BuyerName : transaction.SellerName,
        Role = isBuyer ? ProposerRole.Buyer : ProposerRole.Seller,
        Amount = amount,
        Status = OfferStatus.Active,
        ParentOfferId = currentActive?.OfferId,
        RoundNumber = transaction.NegotiationRoundCount + 1
    };

    // 8. ATOMİK GÜNCELLEME (PatchAsync)
    var updates = new Dictionary<string, object>
    {
        [$"negotiation_offers/{transactionId}/{offer.OfferId}"] = offer,
        [$"transactions/{transactionId}/CurrentActiveOfferId"] = offer.OfferId,
        [$"transactions/{transactionId}/LastActionBy"] = currentUserId,
        [$"transactions/{transactionId}/NegotiationRoundCount"] = offer.RoundNumber,
        [$"transactions/{transactionId}/Status"] = TransactionStatus.Negotiating,
        [$"transactions/{transactionId}/IsNegotiating"] = true,
        [$"transactions/{transactionId}/UpdatedAt"] = DateTime.UtcNow
    };
    await _firebaseClient.Child("/").PatchAsync(updates);

    // 9. BİLDİRİM + MESAJ
    // ... (mevcut notification + conversation mesajı mantığı)

    return ServiceResult<NegotiationOffer>.SuccessResult(offer);
}
```

### 7.3 AcceptActiveOfferAsync

```csharp
public async Task<ServiceResult<bool>> AcceptActiveOfferAsync(
    string transactionId, string currentUserId)
{
    var transaction = await GetTransaction(transactionId);
    var activeOffer = await GetActiveOfferAsync(transactionId);

    if (activeOffer == null) return Fail("Aktif teklif yok");

    // KENDİ TEKLİFİNİ KABUL EDEMEZ
    if (activeOffer.ProposerId == currentUserId)
        return Fail("Kendi teklifinizi kabul edemezsiniz");

    // ROLE-AWARE KABUL (mevcut FAZ 3 mantığı korunur)
    bool isAcceptedByBuyer = transaction.BuyerId == currentUserId;

    activeOffer.Status = OfferStatus.Accepted;
    activeOffer.RespondedAt = DateTime.UtcNow;

    if (isAcceptedByBuyer)
    {
        // Alıcı kabul → Satıcının son onayı beklenir
        var updates = new Dictionary<string, object>
        {
            [$"negotiation_offers/{transactionId}/{activeOffer.OfferId}"] = activeOffer,
            [$"transactions/{transactionId}/IsNegotiating"] = false,
            [$"transactions/{transactionId}/QuotedPrice"] = activeOffer.Amount
        };
        await _firebaseClient.Child("/").PatchAsync(updates);
        // Satıcıya bildirim: "son onayını ver"
    }
    else
    {
        // Satıcı kabul → direkt Accepted, QR oluştur
        // RespondToOfferAsync akışını tetikle
    }

    return ServiceResult<bool>.SuccessResult(true);
}
```

---

## 8. UI Davranışı (KRİTİK)

### 8.1 Buton Visibility Matrisi

| Durum | Kabul (✓) | Reddet (✕) | Karşı Teklif (💰) | Teklif Ver (💰) | Kabul Et (✅) | Ödeme (💳) |
|-------|-----------|------------|-------------------|-----------------|--------------|------------|
| Pending, sıra satıcıda | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ |
| Pending, sıra alıcıda | ❌ | ❌ | ❌ (disable) | ✅ | ❌ | ❌ |
| Negotiating, sıra satıcıda | ❌ | ✅ | ✅ | ❌ | ✅* | ❌ |
| Negotiating, sıra alıcıda | ❌ | ❌ | ❌ | ✅ | ✅* | ❌ |
| Accepted | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |
| Rejected/Cancelled | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |

> *✅ = Karşı tarafın son teklifini kabul et butonu

### 8.2 Yeni CanNegotiateConverter Mantığı

```csharp
// ESKİ (Sorunlu):
return transaction.CounterOfferBySeller.HasValue && transaction.CounterOfferBySeller.Value > 0;

// YENİ:
var activeOffer = GetActiveOfferFromCache(transaction.CurrentActiveOfferId);
if (activeOffer == null) return false;

// Sadece karşı tarafın teklifini kabul edebilirsin
return activeOffer.ProposerId != currentUserId
    && activeOffer.Status == OfferStatus.Active;
```

### 8.3 Eski Teklif Gösterimi

```
✅ Aktif teklif → tam renk, butonlar aktif
⬜ Superseded teklif → soluk, "Bu teklif geçersiz" etiketi, buton yok
✅ Accepted teklif → yeşil çerçeve, "Kabul edildi" etiketi
❌ Rejected teklif → kırmızı çerçeve, "Reddedildi" etiketi
```

---

## 9. Implementasyon Planı (Fazlar)

### FAZ 1: Veri Modeli (Öncelik: KRİTİK)
- [ ] `NegotiationOffer` model sınıfı oluştur
- [ ] `OfferStatus`, `ProposerRole` enum'ları ekle
- [ ] `Transaction.cs`'e `CurrentActiveOfferId` alanı ekle
- [ ] `Constants.cs`'e `NegotiationOffersCollection` ekle
- [ ] Firebase rules'a `negotiation_offers` index ekle

### FAZ 2: Service Layer (Öncelik: KRİTİK)
- [ ] `INegotiationOfferService` interface oluştur
- [ ] `FirebaseNegotiationOfferService` implementasyonu
- [ ] `CreateOfferAsync` — atomic PatchAsync ile
- [ ] `AcceptActiveOfferAsync` — role-aware mantık
- [ ] `RejectActiveOfferAsync`
- [ ] `GetActiveOfferAsync` / `GetOfferHistoryAsync`
- [ ] `TransactionNegotiationService` refactor — yeni servise delegate et
- [ ] `MauiProgram.cs` DI kaydı

### FAZ 3: ViewModel Güncellemesi (Öncelik: YÜKSEK)
- [ ] `OffersViewModel` — yeni servis kullanımı
- [ ] `ChatViewModel.Negotiation` — yeni servis kullanımı
- [ ] Aktif offer cache mekanizması (UI performans)

### FAZ 4: UI / Converter Güncellemesi (Öncelik: YÜKSEK)
- [ ] `CanAcceptNegotiationConverter` — aktif offer kontrolü
- [ ] `IsMyTurnConverter` — offer bazlı sıra kontrolü
- [ ] `NegotiationStatusTextConverter` — offer geçmişi gösterimi
- [ ] `OffersPage.xaml` — buton visibility düzeltmesi
- [ ] Eski tekliflere "Geçersiz" etiketi

### FAZ 5: Geriye Uyumluluk (Öncelik: ORTA)
- [ ] Eski `ProposedPriceByBuyer` / `CounterOfferBySeller` verilerini migration
- [ ] Fallback: Offer koleksiyonu boşsa eski alanlardan oku
- [ ] `AgreedAmount` computed property güncelle

### FAZ 6: Edge Case & Test (Öncelik: ORTA)
- [ ] Race condition testi
- [ ] Süre dolmuş pazarlık testi
- [ ] Ürün silme sırasında aktif pazarlık testi
- [ ] Network kesintisi senaryosu

---

## 10. Performans ve Ölçeklenebilirlik

### Mevcut Sorunlar
1. **N+1 Query:** `AddNegotiationMessageAsync` her teklif mesajında TÜM mesajları çekip `IsActiveOffer=false` yapıyor
2. **Full Object Write:** `PutAsync` tüm transaction'ı yazıyor — race condition riski
3. **Client-side filtering:** Tüm transaction'lar client'a çekiliyor, filtreleme client'ta yapılıyor

### Çözümler

| Sorun | Çözüm | Etki |
|-------|-------|------|
| N+1 Query (mesajlar) | Offer koleksiyonunda `Status` index, sadece aktif offer'ı sorgula | ~%80 okuma azalması |
| Full Object Write | `PatchAsync` ile sadece değişen field'ları yaz | Race condition riski ↓ |
| Client-side filter | Firebase `OrderBy`+`EqualTo` ile server-side filter | Bant genişliği ↓ |
| Realtime listener | Mevcut `AsObservable` yeterli, offer koleksiyonu için de ekle | Anlık güncelleme |

### Ölçeklenebilirlik

- **1000+ eşzamanlı pazarlık:** Firebase Realtime DB yeterli (okuma: 200K/sn)
- **Offer geçmişi büyümesi:** Her transaction'da max 10 offer (MaxRounds) → sınırlı
- **Cache:** LiteDB'de aktif offer cache'i → offline destek

---

## 11. Özet Karar Matrisi

| Karar | Seçenek A | Seçenek B | **Seçilen** |
|-------|-----------|-----------|-------------|
| Offer depolama | Transaction içinde array | Ayrı koleksiyon | **Ayrı koleksiyon** (sorgulanabilirlik) |
| State yönetimi | Boolean flags | State machine enum | **State machine** (netlik) |
| Atomik yazma | PutAsync (full) | PatchAsync (partial) | **PatchAsync** (güvenlik) |
| Geriye uyumluluk | Big-bang migration | Gradual + fallback | **Gradual** (risk azaltma) |
| UI güncelleme | Manuel refresh | Realtime listener | **Realtime** (mevcut altyapı) |

> [!IMPORTANT]
> **Öncelik sırası:** FAZ 1 → FAZ 2 → FAZ 4 → FAZ 3 → FAZ 5 → FAZ 6
> FAZ 1+2+4 tamamlandığında ana bug çözülmüş olur.
