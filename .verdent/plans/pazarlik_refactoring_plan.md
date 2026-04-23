# 🔄 KamPay Pazarlık Sistemi — Kapsamlı Refactoring Planı

> **Tarih:** 2026-04-23  
> **Kapsam:** Satış modülü pazarlık akışı — tüm olasılıklar

---

## 📋 Tespit Edilen Sorunlar

### 🔴 KRİTİK (Veri Bütünlüğü / İşlevsellik)

| # | Sorun | Kök Neden | Dosya(lar) |
|---|-------|-----------|------------|
| K1 | **Farklı ürünler aynı sohbette karışıyor** | `StartConversationForTransactionAsync` aynı iki kullanıcı arasında tek conversation oluşturuyor. Farklı ürünler için de aynı conversation ID kullanılıyor | `TransactionCrudService.cs:372-400` |
| K2 | **Karşı teklif fiyat validasyonu yanlış ürün fiyatını kullanıyor** | K1'in sonucu — aynı sohbette 2 ürün pazarlığı olunca `ValidateProposedPrice(proposedPrice, transaction.Price)` yanlış transaction'ın Price'ını kullanıyor | `TransactionNegotiationService.cs:70` |
| K3 | **Alıcı üst üste teklif gönderebiliyor** | `ProposePriceForSaleAsync` önceki teklifle aynı olup olmadığını kontrol etmiyor. Sıra kontrolü yok (alıcı-satıcı dönüşümlü mü?) | `TransactionNegotiationService.cs:37-127` |
| K4 | **Satıcı "Kabul Et" butonuna basınca karşı teklif gönderebiliyor** | `AcceptNegotiatedPriceAsync` her iki tarafça çağrılabilir ama satıcının kabul etmesi farklı bir anlam taşımalı | `TransactionNegotiationService.cs:380-461` |

### 🟡 ORTA (UX / Mantık Akışı)

| # | Sorun | Kök Neden | Dosya(lar) |
|---|-------|-----------|------------|
| O1 | **"Satıcının onaylamasını bekleyin" hem satıcı hem alıcıda görünüyor** | `AcceptNegotiatedPriceAsync` her iki tarafa da bildirim gönderiyor, mesaj metni role göre ayrılmamış | `TransactionNegotiationService.cs:426-442` |
| O2 | **Teklif kabul/reddi için süre kontrolü yetersiz** | 48 saat timeout var ama UI'da gösterilmiyor, timeout olunca otomatik iptal yok | `NegotiationRules.cs:15` |
| O3 | **Çoklu alıcı senaryosu eksik** | Bir ürüne birden fazla alıcı teklif verebilir ama satıcı birini kabul ettiğinde diğerlerinin pazarlığı açık kalıyor | `TransactionCrudService.cs:192-276` |
| O4 | **İlan kısmında fiyat formatı bozuk** | Fiyat gösteriminde locale bazlı format tutarsızlığı | İlgili XAML/Converter dosyaları |

### 🟢 İYİLEŞTİRME (Mimari)

| # | Sorun | Kök Neden |
|---|-------|-----------|
| I1 | **Conversation modeli ürün bazlı değil** | `Conversation.ProductId` tek ürün tutuyor, çoklu ürün pazarlığı desteklenmiyor |
| I2 | **Chat'te ürün bazlı filtreleme yok** | Tüm mesajlar tek akışta, ürün bazlı gruplandırma eksik |

---

## 🏗️ Çözüm Mimarisi

### Temel Karar: Ürün-Bazlı Conversation (Product-Scoped Conversations)

Mevcut sistemde aynı iki kullanıcı arası **tek conversation** kullanılıyor. Bu, farklı ürünler için pazarlık yapıldığında karmaşa yaratıyor.

**Çözüm:** Her `(kullanıcı çifti + ürün)` kombinasyonu için **ayrı conversation** oluşturmak.

```
ÖNCE:  Alıcı A ↔ Satıcı B → 1 Conversation (tüm ürünler karışık)
SONRA: Alıcı A ↔ Satıcı B → Ürün X → Conversation 1
       Alıcı A ↔ Satıcı B → Ürün Y → Conversation 2
```

Bu yaklaşım:
- ✅ Mevcut `Message.ProductId` ve `Message.RelatedTransactionId` alanlarıyla uyumlu
- ✅ Mesajlar doğal olarak ayrılmış olur
- ✅ `ValidateProposedPrice` doğru `transaction.Price` kullanır
- ✅ `MessagesPage`'de her conversation ürün adıyla gösterilir
- ✅ Minimal model değişikliği gerektirir

---

## 📝 Uygulama Adımları

### FAZ 1: Conversation Oluşturma Mantığını Düzelt (K1 + K2)

**Dosya:** [TransactionCrudService.cs](file:///c:/Users/seyda/source/repos/seydakaratekeli/KamPay3/KamPay/Services/Transactions/TransactionCrudService.cs#L336-L441)

**Mevcut sorun:** `StartConversationForTransactionAsync` iki kullanıcı arası mevcut bir conversation bulursa (herhangi bir ürün için) onu yeniden kullanıyor.

**Değişiklik:**

```diff
 // Mevcut: Sadece kullanıcı çiftine göre arama
-var existingWithOtherUser = existingConversations1
-    .FirstOrDefault(c => c.Object != null && c.Object.IsActive &&
-                        (c.Object.User2Id == otherUserId || c.Object.User1Id == otherUserId));
+// YENİ: Kullanıcı çifti + ProductId'ye göre arama
+var existingWithOtherUser = existingConversations1
+    .FirstOrDefault(c => c.Object != null && c.Object.IsActive &&
+                        (c.Object.User2Id == otherUserId || c.Object.User1Id == otherUserId) &&
+                        c.Object.ProductId == transaction.ProductId);
```

Aynı değişiklik ikinci sorgu için de yapılacak (satır ~384-391).

**Ayrıca:** Yeni conversation oluşturulurken `ProductId`, `ProductTitle`, `ProductThumbnail` set edilecek:

```diff
 var conversation = new Conversation
 {
     // ...mevcut alanlar...
+    ProductId = transaction.ProductId,
+    ProductTitle = transaction.ProductTitle,
+    ProductThumbnail = transaction.ProductThumbnailUrl,
 };
```

> [!IMPORTANT]
> Bu değişiklik mevcut conversation'ları etkilemez. Yeni oluşturulanlar ürün bazlı olacak. Mevcut boş `ProductId`'li conversation'lar eski mantıkla çalışmaya devam eder.

---

### FAZ 2: Üst Üste Teklif Gönderme Kontrolü (K3)

**Dosya:** [TransactionNegotiationService.cs](file:///c:/Users/seyda/source/repos/seydakaratekeli/KamPay3/KamPay/Services/Transactions/TransactionNegotiationService.cs#L37-L127)

**Sorun:** Alıcı art arda aynı veya farklı teklifler gönderebiliyor, sıra kontrolü yok.

**Çözüm — Sıra Kontrolü Ekle:**

`Transaction` modeline `LastActionBy` alanı ekle:

```csharp
// Transaction.cs'e eklenecek
public string LastActionBy { get; set; } = ""; // Son teklif/karşı teklif gönderen kullanıcı ID'si
```

`ProposePriceForSaleAsync` içinde kontrol:

```csharp
// Sıra kontrolü: Alıcı, son hareket zaten kendisindeyse tekrar teklif gönderemez
if (!isInitialRequest && transaction.LastActionBy == currentUserId)
    return ServiceResult<bool>.FailureResult(
        "Karşı tarafın yanıtını beklemeniz gerekiyor. Üst üste teklif gönderemezsiniz.");

// Teklif kaydedildikten sonra:
transaction.LastActionBy = currentUserId;
```

Aynı kontrol `SendCounterOfferForSaleAsync` için de uygulanacak.

---

### FAZ 3: Kabul Et Akışını Düzelt (K4 + O1)

**Dosya:** [TransactionNegotiationService.cs](file:///c:/Users/seyda/source/repos/seydakaratekeli/KamPay3/KamPay/Services/Transactions/TransactionNegotiationService.cs#L380-L461)

**Sorun 1:** Satıcı "Kabul Et"e basınca pazarlık durur ama satıcı aslında karşı teklif göndermek isteyebilir.  
**Sorun 2:** "Satıcının onaylamasını bekleyin" mesajı her iki tarafta da görünüyor.

**Çözüm:**

`AcceptNegotiatedPriceAsync` içinde **role-aware bildirimler**:

```diff
-await _notificationService.CreateNotificationAsync(new Notification
-{
-    UserId = transaction.SellerId,
-    Type = NotificationType.TransactionUpdate,
-    Title = "💰 Pazarlık Tamamlandı",
-    Message = $"... teklifinizi kabul etti. Teklifler sayfasından son onayınızı verin.",
-});
-
-await _notificationService.CreateNotificationAsync(new Notification
-{
-    UserId = transaction.BuyerId,
-    Type = NotificationType.TransactionUpdate,
-    Title = "✅ Fiyat Onaylandı",
-    Message = $"... Satıcının son onayı bekleniyor.",
-});
+// Role-aware bildirimler
+var isAcceptedByBuyer = transaction.BuyerId == currentUserId;
+
+if (isAcceptedByBuyer)
+{
+    // Alıcı kabul etti → Satıcıya "son onayını ver" bildirimi
+    await _notificationService.CreateNotificationAsync(new Notification
+    {
+        UserId = transaction.SellerId,
+        Title = "💰 Alıcı Fiyatı Kabul Etti",
+        Message = $"... Teklifler sayfasından son onayınızı verin.",
+    });
+    // Alıcıya "satıcıyı bekleyin" bildirimi
+    await _notificationService.CreateNotificationAsync(new Notification
+    {
+        UserId = transaction.BuyerId,
+        Title = "✅ Teklifiniz Onaylandı",
+        Message = $"... Satıcının son onayı bekleniyor.",
+    });
+}
+else
+{
+    // Satıcı kabul etti → her iki tarafa da "anlaşıldı" bildirimi
+    // Bu durumda satıcının AcceptNegotiatedPrice çağırması = direkt onay
+    // RespondToOfferAsync'e yönlendir
+}
```

**Satıcının "Kabul Et" davranışı:** Satıcı son teklifi kabul ederse, doğrudan `RespondToOfferAsync(transactionId, true)` çağrılmalı. `AcceptNegotiatedPriceAsync` sadece **alıcı** tarafından çağrılabilir olmalı veya satıcı kabul ettiğinde otomatik olarak `Accepted` statüsüne geçmeli.

---

### FAZ 4: Karşı Teklif Fiyat Validasyonunu Düzelt (K2)

**Dosya:** [TransactionNegotiationService.cs](file:///c:/Users/seyda/source/repos/seydakaratekeli/KamPay3/KamPay/Services/Transactions/TransactionNegotiationService.cs#L70)

**Mevcut:** `ValidateProposedPrice(proposedPrice, transaction.Price)` — Bu doğru çalışıyor çünkü her transaction kendi `Price` alanını taşıyor.

**Asıl sorun:** FAZ 1 çözüldüğünde bu sorun da çözülür. Çünkü aynı sohbetteki farklı ürünlerin karışması transaction düzeyinde değil, **UI düzeyinde** oluyor — yanlış transaction seçili kalıyor.

**Ek güvenlik:** `ChatViewModel.ProposeOfferAsync` ve `AcceptOfferAsync` içinde `targetTransaction.ProductId` ile aktif conversation'ın `ProductId`'sini cross-check et:

```csharp
// ChatViewModel'de ek güvenlik kontrolü
if (targetTransaction.ProductId != Conversation?.ProductId)
{
    await Application.Current.MainPage.DisplayAlert("Hata", 
        "Bu teklif farklı bir ürüne ait. Lütfen doğru sohbet penceresinden işlem yapın.", "Tamam");
    return;
}
```

---

### FAZ 5: Çoklu Alıcı Senaryosu (O3)

**Sorun:** Satıcı bir alıcıyla pazarlığı tamamlayıp ürünü sattığında, diğer alıcıların aktif pazarlıkları açık kalıyor.

**Çözüm:** `RespondToOfferAsync` içinde accept durumunda, aynı ürün için diğer pending transaction'ları otomatik reddet:

```csharp
// RespondToOfferAsync içinde, accept=true sonrası (satır ~226 civarı):
if (accept)
{
    // Aynı ürün için diğer pending transaction'ları otomatik reddet
    await AutoRejectOtherPendingOffersAsync(transaction.ProductId, transactionId);
}
```

**Yeni metot:**

```csharp
private async Task AutoRejectOtherPendingOffersAsync(string productId, string acceptedTransactionId)
{
    try
    {
        var allTransactions = await _firebaseClient
            .Child(Constants.TransactionsCollection)
            .OrderBy("ProductId")
            .EqualTo(productId)
            .OnceAsync<Transaction>();

        foreach (var t in allTransactions)
        {
            if (t.Key == acceptedTransactionId) continue;
            if (t.Object.Status != TransactionStatus.Pending) continue;

            t.Object.Status = TransactionStatus.Rejected;
            t.Object.IsNegotiating = false;
            t.Object.UpdatedAt = DateTime.UtcNow;
            t.Object.NegotiationNotes += "\n⚠️ Ürün başka bir alıcıya satıldı.";

            await _firebaseClient
                .Child(Constants.TransactionsCollection)
                .Child(t.Key)
                .PutAsync(t.Object);

            // Reddedilen alıcılara bildirim
            await _notificationService.CreateNotificationAsync(new Notification
            {
                UserId = t.Object.BuyerId,
                Type = NotificationType.OfferRejected,
                Title = "⚠️ Ürün Satıldı",
                Message = $"'{t.Object.ProductTitle}' başka bir alıcıya satıldı. Pazarlığınız kapandı.",
                ActionUrl = nameof(Views.OffersPage)
            });

            // İlgili conversation'a bilgi mesajı
            if (!string.IsNullOrEmpty(t.Object.ConversationId))
            {
                await AddSystemMessageAsync(t.Object.ConversationId,
                    $"⚠️ [{t.Object.ProductTitle}]\nBu ürün başka bir alıcıya satıldı. Pazarlık kapandı.");
            }
        }
    }
    catch (Exception ex)
    {
        AppLogger.DebugLog($"⚠️ AutoReject hatası: {ex.Message}");
    }
}
```

> [!TIP]
> Firebase'de `ProductId` üzerinde index gerekli. `Constants.cs` veya Firebase Console'da bu index'i oluşturun.

---

### FAZ 6: Fiyat Formatını Düzelt (O4)

**Sorun:** İlan listesinde fiyat formatı tutarsız.

**Kontrol edilecek dosyalar:**
- `Views/Products/ProductListPage.xaml` — fiyat label binding
- `Converters/Product/ProductPriceConverters.cs`

**Çözüm:** Tüm fiyat gösterimlerinde `{0:N2}` formatı + `₺` sembolü kullanıldığından emin ol. `CultureInfo.InvariantCulture` veya Türk locale (`tr-TR`) kullanarak tutarlılık sağla.

---

### FAZ 7: Pazarlık Süre Yönetimi (O2)

**Mevcut:** 48 saat timeout var ama sadece `CanContinueNegotiation` kontrolünde.

**İyileştirmeler:**

1. **UI'da kalan süreyi göster** — `NegotiationStatusTextConverter` içinde:
```csharp
if (transaction.NegotiationStartedAt.HasValue)
{
    var remaining = TimeSpan.FromHours(48) - (DateTime.UtcNow - transaction.NegotiationStartedAt.Value);
    if (remaining.TotalHours > 0)
        statusText += $"\n⏱️ Kalan: {remaining.Hours}s {remaining.Minutes}dk";
}
```

2. **Timeout olduğunda otomatik iptal** — `LoadActiveTransactionsAsync` veya OffersViewModel'de expire kontrolü.

---

## 🔀 Tüm Senaryo Akışları

### Senaryo 1: Basit Satış (Pazarlıksız)
```
Alıcı → "Satın Al" → Transaction(Pending, QuotedPrice=ListeFiyatı)
Satıcı → "Kabul Et" → Transaction(Accepted) + QR Kod
Alıcı → Ödeme → QR Teslimat → Transaction(Completed)
```

### Senaryo 2: Pazarlıklı Satış
```
Alıcı → Teklif (500₺) → ProposePriceForSaleAsync → LastActionBy=Alıcı
Satıcı → Karşı Teklif (700₺) → SendCounterOfferForSaleAsync → LastActionBy=Satıcı
Alıcı → Yeni Teklif (600₺) → ProposePriceForSaleAsync → LastActionBy=Alıcı
Satıcı → "Kabul Et" → AcceptNegotiatedPrice (satıcı) → Direkt Accepted + QR
  VEYA
Alıcı → "Kabul Et" → AcceptNegotiatedPrice (alıcı) → IsNegotiating=false, Status=Pending
Satıcı → "Onayla" → RespondToOfferAsync(accept=true) → Accepted + QR
```

### Senaryo 3: Aynı Satıcı, Farklı Ürünler, Aynı Alıcı
```
Alıcı A → Ürün X için teklif → Conversation_X oluşur (ProductId=X)
Alıcı A → Ürün Y için teklif → Conversation_Y oluşur (ProductId=Y)
MessagesPage'de iki ayrı sohbet görünür:
  - "Satıcı B — Ürün X"
  - "Satıcı B — Ürün Y"
```

### Senaryo 4: Aynı Ürün, Birden Fazla Alıcı
```
Alıcı A → Ürün X için teklif → Conversation_XA (A↔Satıcı)
Alıcı B → Ürün X için teklif → Conversation_XB (B↔Satıcı)
Satıcı A'nın teklifini kabul eder →
  - Transaction A → Accepted + QR
  - Transaction B → AutoReject ("Ürün başka alıcıya satıldı")
  - Ürün "Rezerve Edildi" olarak işaretlenir
```

### Senaryo 5: Pazarlık Zaman Aşımı
```
Alıcı → Teklif → 48 saat geçer →
CanContinueNegotiation → false →
"Pazarlık süresi doldu" mesajı
İsteğe bağlı: Otomatik iptal veya yeni pazarlık başlatma seçeneği
```

---

## 📁 Değiştirilecek Dosyalar Özeti

| Dosya | Değişiklik |
|-------|------------|
| `Models/Transactions/Transaction.cs` | `LastActionBy` alanı ekle |
| `Services/Transactions/TransactionCrudService.cs` | Conversation oluşturmada ProductId filtresi + AutoReject metodu |
| `Services/Transactions/TransactionNegotiationService.cs` | Sıra kontrolü, role-aware bildirimler, satıcı kabul akışı |
| `Helpers/NegotiationRules.cs` | Kalan süre hesaplama helper |
| `ViewModels/Messaging/ChatViewModel.cs` | ProductId cross-check güvenlik kontrolü |
| `Converters/Negotiation/NegotiationStatusTextConverter.cs` | Kalan süre gösterimi |
| İlgili XAML fiyat gösterim dosyaları | Format düzeltmesi |

---

## ⚠️ Ek Tavsiyeler

1. **Conversation'da ProductThumbnail göster** — `MessagesPage` listesinde her sohbetin yanında hangi ürün olduğu görsel olarak belli olsun
2. **Pazarlık geçmişi** — Tüm teklif/karşı teklif geçmişini bir liste olarak göster (sadece son değerler değil)
3. **"Pazarlık Yap" butonu** — ProductDetailPage'den doğrudan pazarlık başlatma seçeneği ekle
4. **Rate limiting** — Aynı ürüne kısa sürede çok fazla teklif gönderilmesini engelle
5. **Concurrent access** — İki alıcı aynı anda kabul ederse race condition oluşabilir; Firebase transaction kullan

> [!WARNING]
> FAZ 1 (Conversation ayrımı) diğer tüm fazların temelidir. Önce bu tamamlanmalıdır.
