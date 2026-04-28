# 🔍 KamPay — Satış & Pazarlık Akış Analizi ve Düzeltme Planı

## Tespit Edilen 7 Kritik Sorun

---

### 🐛 SORUN 1: Liste Fiyatıyla Satın Alma Sonrası Pazarlık Hâlâ Mümkün

**Konum:** `ProductDetailViewModel.SendRequestAsync` → `TransactionCrudService.CreateRequestAsync`

**Detay:**
- Alıcı "Liste Fiyatıyla Al" seçtiğinde `CreateRequestAsync` çağrılıyor ve transaction `IsNegotiating = false` olarak oluşturuluyor.
- **AMA** Transaction modelinde `IsFixedPriceRequest` gibi bir bayrak YOK.
- Bu yüzden Chat ve Offers sayfalarında pazarlık butonları (`💰 Teklif Ver`, `Karşı Teklif`) hâlâ görünüyor.
- `CanNegotiateConverter` sadece `Status == Pending || IsNegotiating` kontrol ediyor — sabit fiyat ayrımı yapamıyor.

**Etkilenen Dosyalar:**
- [Transaction.cs](file:///c:/Users/seyda/source/repos/seydakaratekeli/KamPay3/KamPay/Models/Transactions/Transaction.cs)
- [TransactionCrudService.cs](file:///c:/Users/seyda/source/repos/seydakaratekeli/KamPay3/KamPay/Services/Transactions/TransactionCrudService.cs#L88-L132)
- [CanNegotiateConverter.cs](file:///c:/Users/seyda/source/repos/seydakaratekeli/KamPay3/KamPay/Converters/Negotiation/CanNegotiateConverter.cs)
- [ChatPage.xaml](file:///c:/Users/seyda/source/repos/seydakaratekeli/KamPay3/KamPay/Views/Messaging/ChatPage.xaml#L649-L664) — `💰` butonu

**Çözüm:**
1. `Transaction` modeline `bool IsFixedPriceRequest` property ekle
2. `CreateRequestAsync` içinde liste fiyatıyla alım → `IsFixedPriceRequest = true`
3. `CanNegotiateConverter` → `IsFixedPriceRequest == true` ise `false` dönsün
4. ChatPage.xaml satır 656: `💰` butonun `IsVisible` → `IsFixedPriceRequest` kontrolü ekle

---

### 🐛 SORUN 2: Satıcı Tarafında "Kabul Et / Reddet" Butonları Görünmüyor

**Konum:** [OffersPage.xaml](file:///c:/Users/seyda/source/repos/seydakaratekeli/KamPay3/KamPay/Views/Transactions/OffersPage.xaml#L328-L350) satır 328-350

**Detay:**
- `✓` (Kabul) ve `✕` (Reddet) butonları `IsVisible="{Binding Status, Converter={StaticResource IsPendingConverter}}"` ile kontrol ediliyor.
- Bu kısım doğru görünüyor — `Status == Pending` ise butonlar görünmeli.
- **ANCAK** bu butonlar sadece **Incoming Offers** template'inde var (satır 159-356). Outgoing Offers template'inde (satır 393-636) bu butonlar **YOK**.
- Yani satıcı "Gelen Teklifler" sekmesinde butonları görmeli.

**Olası Kök Neden:**
- Satıcının Incoming Offers listesi boş olabilir → `GetIncomingOffersAsync` listener'ı düzgün çalışmıyor olabilir.
- Veya `AcceptOfferCommand` ViewModel'de `RespondToOfferInternalAsync`'e yönleniyor, bu metod satır ~588'de `IsNegotiating` flag'ine göre dallanıyor.

**Çözüm:**
1. `OffersViewModel.RespondToOfferInternalAsync` → liste fiyatı talebi (`!IsNegotiating && QuotedPrice > 0`) dalını debug et
2. Incoming listener'ın satıcı ID'sine göre sorgu attığını doğrula
3. Eğer `Status == Pending && IsNegotiating == false && IsFixedPriceRequest == true` → doğrudan Kabul/Red akışı

---

### 🐛 SORUN 3: "Aktif İşlem Yüklenemedi" Hatası (Chat Sayfasında)

**Konum:** [ChatViewModel.cs](file:///c:/Users/seyda/source/repos/seydakaratekeli/KamPay3/KamPay/ViewModels/Messaging/ChatViewModel.cs#L946-L1040) — `ProposeOfferAsync` ve `AcceptOfferAsync`

**Detay:**
```
targetTransaction = ActiveTransactions.FirstOrDefault(t => t.TransactionId == message.RelatedTransactionId);
// Bulunamazsa:
var myOffersResult = await _transactionService.GetMyOffersAsync(_currentUser.UserId);
```
- `GetMyOffersAsync` → `BuyerId == currentUserId` ile sorgular
- **Satıcı** bu metodu çağırdığında → kendi transaction'ını bulamaz (çünkü BuyerId değil SellerId)
- Sonuç: `targetTransaction == null` → "Aktif işlem yüklenemedi" hatası

**Çözüm:**
1. Fallback sorgusunda `GetIncomingOffersAsync` (SellerId bazlı) de çağrılmalı
2. `ProposeOfferAsync` satır 957-962 arası:

```csharp
// Mevcut (HATALI — sadece alıcı):
var myOffersResult = await _transactionService.GetMyOffersAsync(_currentUser.UserId);

// DÜZELTİLMİŞ — alıcı + satıcı:
var myOffersResult = await _transactionService.GetMyOffersAsync(_currentUser.UserId);
if (targetTransaction == null)
{
    var incomingResult = await _transactionService.GetIncomingOffersAsync(_currentUser.UserId);
    if (incomingResult.Success && incomingResult.Data != null)
        targetTransaction = incomingResult.Data.FirstOrDefault(t => t.TransactionId == message.RelatedTransactionId);
}
```
3. Aynı düzeltme `AcceptOfferAsync` satır 1054-1059 için de yapılmalı

---

### 🐛 SORUN 4: Alıcı/Satıcı Rol Karışıklığı

**Konum:** Birden fazla dosyada tutarsız rol kontrolleri

**Detaylar:**

| Dosya | Metod | Sorun |
|-------|-------|-------|
| `OffersVM` satır 1097 | `AcceptNegotiatedPriceAsync` | Sadece **alıcı** kabul edebilir |
| `ChatVM` satır 1043 | `AcceptOfferAsync` | **Herkes** kabul edebilir → servisi çağırır |
| `TransactionNegotiationService` | `AcceptNegotiatedPriceAsync` | **İkisi de** kabul edebilir, farklı davranış |

- **OffersVM**: Alıcı satıcının karşı teklifini onaylar → `AcceptNegotiatedPriceAsync`
- **OffersVM**: Satıcı ise `RespondToOfferAsync(accept: true)` → doğrudan kabul
- **ChatVM**: Rol ayrımı YOK — herhangi bir kullanıcı "Kabul Et" dediğinde `AcceptNegotiatedPriceAsync` çağrılıyor

**Çözüm:**
1. `ChatViewModel.AcceptOfferAsync` içinde rol kontrolü ekle:
   - Satıcı "Kabul Et" → `RespondToOfferAsync(accept: true)` VEYA `AcceptNegotiatedPriceAsync` (satıcı dalı)
   - Alıcı "Kabul Et" → `AcceptNegotiatedPriceAsync` (alıcı dalı)
2. Her iki ViewModel'de de tutarlı rol mantığı uygula

---

### 🐛 SORUN 5: Liste Fiyatı Seçildiğinde "Pazarlık Yap" Butonu Pasif Olmalı

**Konum:** [ProductDetailPage.xaml](file:///c:/Users/seyda/source/repos/seydakaratekeli/KamPay3/KamPay/Views/Products/ProductDetailPage.xaml) — `CanNegotiateConverter` kullanımı

**Çözüm:**
1. `ProductDetailViewModel`'de `IsFixedPriceSelected` property ekle
2. Kullanıcı "Liste Fiyatıyla Al" seçeneğini seçtiğinde `IsFixedPriceSelected = true`
3. "Pazarlık Yap" butonuna `IsEnabled="{Binding IsFixedPriceSelected, Converter={StaticResource InvertedBoolConverter}}"` ekle
4. Opacity/visual feedback: seçildiğinde `0.4` opacity, seçilmediğinde `1.0`

---

### 🐛 SORUN 6: AcceptNegotiatedPriceAsync — Çift Yazma (Double Write)

**Konum:** [TransactionNegotiationService.cs](file:///c:/Users/seyda/source/repos/seydakaratekeli/KamPay3/KamPay/Services/Transactions/TransactionNegotiationService.cs) — `AcceptNegotiatedPriceAsync`

**Detay:**
- Satıcı kabul ettiğinde:
  1. Transaction güncellenir → `PutAsync(transaction)` (1. yazma)
  2. Sonra `_crudService.RespondToOfferAsync(transactionId, accept: true)` çağrılır
  3. `RespondToOfferAsync` de `PutAsync(transaction)` yapar (2. yazma) + `MarkAsReserved` + QR kod oluşturma
- Bu iki ayrı yazma **race condition** ve **duplikasyon** riski taşır

**Çözüm:**
- Satıcı kabul akışında tek bir atomik güncelleme yap
- `RespondToOfferAsync` çağrısını kaldır, QR kod oluşturmayı doğrudan `AcceptNegotiatedPriceAsync` içinde yap

---

### 🐛 SORUN 7: Chat'te Genel Sohbet vs Pazarlık Sohbet Ayrımı Belirsiz

**Konum:** [ChatPage.xaml](file:///c:/Users/seyda/source/repos/seydakaratekeli/KamPay3/KamPay/Views/Messaging/ChatPage.xaml#L194-L291)

**Detay:**
- Mevcut chip yapısı: "Tüm Mesajlar" + dinamik transaction chip'leri
- Ancak "Genel Sohbet" (ürün hakkında soru sorma) ve "Pazarlık" (transaction bazlı) arasında net bir ayrım yok
- Tüm mesajlar aynı conversation'da, sadece `RelatedTransactionId` ile filtreleniyor

**Mevcut Durum Yeterli Mi?**
- Evet, chip yapısı zaten "Tüm Mesajlar" ve "Ürün Bazlı" filtreleme sağlıyor
- Eksik olan: kullanıcıya hangi chip'in ne anlama geldiğinin açıklanması

**İyileştirme:**
- "Tüm Mesajlar" → "💬 Genel Sohbet" olarak yeniden adlandır
- Transaction chip'lerine durum bilgisi ekle (📦 Ürün Adı — "Pazarlık Devam")

---

## 📋 Uygulama Sırası (Öncelik)

| Sıra | Sorun | Öncelik | Tahmini Süre |
|------|-------|---------|--------------|
| 1 | SORUN 1 — `IsFixedPriceRequest` flag ekle | 🔴 Kritik | 30 dk |
| 2 | SORUN 3 — "Aktif işlem yüklenemedi" fix | 🔴 Kritik | 20 dk |
| 3 | SORUN 2 — Satıcı Kabul/Red butonları | 🔴 Kritik | 25 dk |
| 4 | SORUN 4 — Rol tutarlılığı | 🟡 Yüksek | 30 dk |
| 5 | SORUN 5 — Pazarlık butonu pasif | 🟡 Yüksek | 15 dk |
| 6 | SORUN 6 — Double write kaldır | 🟠 Orta | 25 dk |
| 7 | SORUN 7 — Chat chip isimlendirme | 🟢 Düşük | 10 dk |

---

## Dokunulacak Dosyalar (Özet)

```
Models/Transactions/Transaction.cs              → IsFixedPriceRequest property
Services/Transactions/TransactionCrudService.cs  → CreateRequestAsync flag set
Services/Transactions/TransactionNegotiationService.cs → AcceptNegotiatedPriceAsync düzeltme
ViewModels/Messaging/ChatViewModel.cs            → ProposeOfferAsync + AcceptOfferAsync fallback
ViewModels/Transactions/OffersViewModel.cs       → Rol tutarlılığı
Converters/Negotiation/CanNegotiateConverter.cs  → IsFixedPriceRequest kontrolü
Views/Messaging/ChatPage.xaml                    → Buton visibility + chip isimleri
Views/Products/ProductDetailPage.xaml            → Pazarlık butonu disable
```
