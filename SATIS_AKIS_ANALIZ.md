# 🔍 ÜRÜN SATIŞI AKIŞ ANALİZİ VE TESPİT EDİLEN SORUNLAR

## ✅ SENARYO 1: PAZARLIKSIZ SATIŞ (DOĞRUDAN TALEP)

### **Akış:**
1. ✅ **Alıcı → Ürün detayda "Satın Al" butonuna basar**
   - `ProductDetailViewModel.SendRequestAsync()` → `CreateRequestAsync()`
   - Transaction oluşturulur:
     - `QuotedPrice = product.Price` ✅ (doğru)
     - `IsNegotiating = false` ✅ (doğru)
     - `Status = Pending` ✅ (satıcı onayı bekliyor)
     - `PaymentStatus = Pending` ✅

2. ✅ **Satıcı → Gelen Teklifler'de "Onayla" butonuna basar**
   - `OffersViewModel.AcceptOfferAsync()` → `RespondToOfferAsync(accept=true)`
   - Kontrol: `!IsNegotiating && QuotedPrice > 0` → İlk blok çalışır
   - `Status = Accepted` olur
   - `IsReserved = true` (ürün rezerve edilir)
   - Alıcıya bildirim: "Ödeme yapabilirsiniz"

3. ✅ **Alıcı → Giden Teklifler'de "Ödeme Yap" butonuna basar**
   - `OffersViewModel.StartPaymentAsync()` → `PaymentPage`'e yönlendirir
   - Kontroller:
     - `Status == Accepted` ✅
     - `PaymentStatus != Paid` ✅
     - `!IsNegotiating` ✅

4. ✅ **Alıcı → Ödeme sayfasında ödeme yöntemini seçer**
   - `PaymentViewModel.StartPaymentAsync(method)` → `CreatePaymentSimulationAsync()`
   - PaymentDto oluşturulur, OTP/Havale bilgileri hazırlanır
   - Transaction güncellenir: `PaymentMethod = CardSim/BankTransferSim`

5. ✅ **Alıcı → OTP/Havale doğrulaması yapar**
   - `PaymentViewModel.ConfirmCardPaymentAsync()` → `ConfirmPaymentSimulationAsync()`
   - OTP doğrulanır
   - `PaymentStatus = Paid` olur
   - `CompleteTransactionInternalAsync()` çağrılır:
     - `Status = Completed`
     - `IsSold = true` (ürün satıldı olarak işaretlenir)
     - Puanlar verilir
     - Bildirimler gönderilir

---

## ✅ SENARYO 2: PAZARLIKLI SATIŞ

### **Akış:**
1. ✅ **Alıcı → Ürün detayda "Satın Al" butonuna basar**
   - `ProductDetailViewModel.SendRequestAsync()` → `CreateRequestAsync()`
   - Transaction oluşturulur: `QuotedPrice = product.Price`, `IsNegotiating = false`

2. ✅ **Alıcı → Ürün detayda "Fiyat Teklifi" butonuna basar**
   - `ProductDetailViewModel.ProposePriceAsync()` → `ProposePriceForSaleAsync()`
   - `ProposedPriceByBuyer = 150₺` (örnek)
   - `IsNegotiating = true` ✅
   - `NegotiationRoundCount = 1` ✅
   - `Status = Pending` KALIR (satıcı henüz onaylamadı)

3. ✅ **Satıcı → Gelen Teklifler'de "Karşı Teklif" butonuna basar**
   - `OffersViewModel.SendCounterOfferAsync()` → `SendCounterOfferForSaleAsync()`
   - `CounterOfferBySeller = 175₺`
   - `IsNegotiating = true` (kalır)
   - `NegotiationRoundCount = 2` ✅
   - Alıcıya bildirim gider

4. **🔄 Pazarlık Döngüsü (İsteğe Bağlı - Max 5 Tur)**
   - Alıcı → Tekrar `ProposePriceAsync()`
   - Satıcı → Tekrar `SendCounterOfferAsync()`
   - Her tur `NegotiationRoundCount++`
   - Limit: `NegotiationRules.MaxNegotiationRounds = 5`

5. ✅ **Alıcı → Satıcının karşı teklifini kabul eder**
   - `ProductDetailViewModel.AcceptNegotiatedPriceAsync()` VEYA `OffersViewModel.AcceptNegotiatedPriceAsync()`
   - ⚠️ **BU METOD SADECE ALICI TARAFINDAKİ ONAYDIR**
   - `QuotedPrice = CounterOfferBySeller` (örnek: 175₺) ✅
   - `IsNegotiating = false` ✅
   - **Status = Pending KALIR** ✅ (satıcı hala final onayı vermedi!)
   - Firebase'e kaydedilir ✅
   - Bildiri gider: "Satıcının son onayı bekleniyor"

6. ⚠️ **SORUN: Satıcı hala "Onayla" butonuna basmalı!**
   - `OffersViewModel.AcceptOfferAsync()` → `RespondToOfferAsync(accept=true)`
   - Kontrol bloğu:
     ```csharp
     bool hadNegotiation = transaction.ProposedPriceByBuyer.HasValue || transaction.CounterOfferBySeller.HasValue;
     
     if (hadNegotiation && !transaction.IsNegotiating)
     {
         // Pazarlık sonrası onay
     }
     ```
   - Bu blok çalışır, `Status = Accepted` olur
   - `IsReserved = true`

7. ✅ **Alıcı → Ödeme yapar** (Senaryo 1 ile aynı)

---

## 🔴 TESPİT EDİLEN SORUNLAR

### **1. ❌ Transaction Null Kontrolü Eksikliği**
**Konum:** `PaymentViewModel.cs` → `StartPaymentAsync()` metodu

**Sorun:**
```csharp
private async Task StartPaymentAsync(string method)
{
    // ❌ Transaction null olabilir!
    if (Transaction == null)
    {
        await Shell.Current.DisplayAlert(Res["Error"], "İşlem bilgisi yüklenemedi. Lütfen tekrar deneyin.", Res["Ok"]);
        return;
    }
    
    // ... devamı
}
```

`CreatePaymentSimulationAsync()` metodunda da `transactionId` null olabilir ama kontrol yok!

**Çözüm:**
```csharp
// FirebaseTransactionService.cs → CreatePaymentSimulationAsync() başında ekle:
if (string.IsNullOrWhiteSpace(transactionId))
{
    System.Diagnostics.Debug.WriteLine($"❌ TransactionId boş!");
    return ServiceResult<PaymentDto>.FailureResult("İşlem ID'si bulunamadı.");
}
```

---

### **2. ❌ Pazarlık Sonrası Onay Kontrolü Eksik**
**Konum:** `FirebaseTransactionService.cs` → `CreatePaymentSimulationAsync()`

**Sorun:** Ödeme başlatılırken `Status` ve `IsNegotiating` kontrolleri yapılmıyor!

**Senaryo:**
- Alıcı pazarlık yapar (`IsNegotiating = true`)
- Sonra karşı teklifi kabul eder (`IsNegotiating = false`, `Status = Pending`)
- **Satıcı henüz "Onayla" butonuna basmadan** alıcı ödeme sayfasına gidebilir!

**Çözüm:**
```csharp
// CreatePaymentSimulationAsync() içinde, ÜRÜN transaction'ı için ekle:
if (!isServicePayment)
{
    var productTransaction = transaction as Transaction;
    
    // Status kontrolü
    if (productTransaction.Status != TransactionStatus.Accepted)
    {
        return ServiceResult<PaymentDto>.FailureResult(
            "İşlem henüz satıcı tarafından onaylanmamış. Önce onay beklenmeli."
        );
    }

    // Pazarlık kontrolü
    if (productTransaction.IsNegotiating)
    {
        return ServiceResult<PaymentDto>.FailureResult(
            "Pazarlık devam ediyor. Önce fiyat üzerinde anlaşmanız gerekiyor."
        );
    }
}
```

---

### **3. ✅ AcceptNegotiatedPriceAsync Firebase Kaydı (ÇÖZÜLDÜ)**
**Konum:** `FirebaseTransactionService.cs` → `AcceptNegotiatedPriceAsync()`

**Sorun (Önceden):** QuotedPrice güncellendi ama Firebase'e yazılmıyordu.

**Çözüm (Uygulandı):**
```csharp
// ✅ KRİTİK FİX: Firebase'e kaydet!
await _firebaseClient
    .Child(Constants.TransactionsCollection)
    .Child(transactionId)
    .PutAsync(transaction);
```

---

### **4. ⚠️ Pazarlık Sonrası Çift Onay Sistemi Karmaşık**
**Konum:** UI/UX Akışı

**Sorun:**
1. Alıcı → `AcceptNegotiatedPriceAsync()` (Fiyat üzerinde anlaştı)
2. Satıcı → `AcceptOfferAsync()` (Final onay)

Bu iki adımlı onay sistemi kullanıcı için kafa karıştırıcı olabilir.

**Öneriler:**
- ✅ **Mevcut Akışı Koru** (Güvenlik için iyi)
- Veya: Alıcı kabul edince otomatik olarak `Status = Accepted` yap (daha hızlı)

---

### **5. ❌ UI'da Buton Görünürlüğü Kontrolü**
**Konum:** OffersPage.xaml / Converters

**Sorun:** Hangi durumlarda hangi butonlar görünür, net değil.

**Beklenen Durum Matrisi:**

| Durum | IsNegotiating | Status | Alıcı Butonları | Satıcı Butonları |
|-------|---------------|--------|-----------------|------------------|
| **Pazarlıksız Talep** | false | Pending | - | Onayla / Reddet |
| **Pazarlıksız Onaylı** | false | Accepted | Ödeme Yap | - |
| **Pazarlık Devam** | true | Pending | Fiyat Teklifi, Kabul Et | Karşı Teklif |
| **Pazarlık Bitti, Onay Bekliyor** | false | Pending | - | Onayla |
| **Pazarlık Onaylandı** | false | Accepted | Ödeme Yap | - |
| **Ödeme Tamamlandı** | false | Completed | - | - |

**Gerekli Converters:**
- `CanPayConverter`: `Status==Accepted && PaymentStatus!=Paid && !IsNegotiating`
- `CanApproveAfterNegotiationConverter`: `!IsNegotiating && Status==Pending && (ProposedPrice != null || CounterOffer != null)`

---

## 📝 ÖNERİLEN DÜZELTMELowException Tasarımı**
- Status: Pending → Accepted → Completed
- IsNegotiating: false → true (pazarlık) → false (anlaşıldı) → (onay sonrası Accepted)

**Çözüm:** Mevcut akış mantıklı, sadece validasyonlar eklenmeli.

---

## 🛠️ YAPILACAK DÜZELTMELER

### **1. CreatePaymentSimulationAsync Güvenlik Kontrolleri**
```csharp
// TransactionId null kontrolü ekle
// Status != Accepted kontrolü ekle
// IsNegotiating == true kontrolü ekle
```

### **2. PaymentViewModel Null Kontrolü**
```csharp
// Transaction null ise uyarı ver, metodu durdur
```

### **3. UI Converter'lar Ekle**
```csharp
// CanPayConverter
// CanApproveAfterNegotiationConverter
// NegotiationStatusConverter
```

### **4. Test Senaryoları**
- ✅ Pazarlıksız satış
- ✅ Pazarlıklı satış (1 tur)
- ✅ Pazarlıklı satış (max tur)
- ⚠️ Pazarlık sonrası onaysız ödeme engelleme
- ⚠️ Null transaction kontrolü

---

## 📊 ÖZET

**Genel Durum:** Akış mantığı doğru, bazı kenar durumlar için validasyon eksik.

**Kritik Sorunlar:**
1. ❌ CreatePaymentSimulationAsync → Status ve IsNegotiating kontrolü eksik
2. ❌ PaymentViewModel → Transaction null kontrolü zayıf

**Orta Önem:**
3. ⚠️ UI Buton görünürlüğü converter'ları eksik

**Düşük Öncelik:**
4. ℹ️ Pazarlık sonrası çift onay sistemi karmaşık (ama mantıklı)

**Sonraki Adım:** Yukarıdaki 1 ve 2 numaralı sorunları düzelt, test et.
