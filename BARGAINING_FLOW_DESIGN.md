# Pazarlık Sistemi Akış Tasarımı (Bargaining System Flow Design)

## İçindekiler
1. [Genel Bakış](#genel-bakış)
2. [Pazarlık Senaryoları](#pazarlık-senaryoları)
3. [Durum Diyagramları](#durum-diyagramları)
4. [İş Mantığı Kuralları](#iş-mantığı-kuralları)
5. [Akış Detayları](#akış-detayları)

---

## Genel Bakış

KamPay uygulamasında iki tür pazarlık senaryosu bulunmaktadır:

### 1. **SATIŞ (Sale) Pazarlığı**
- Alıcı, satıcının belirlediği fiyattan farklı bir fiyat teklif eder
- Satıcı teklife karşı teklif verebilir veya kabul/red edebilir
- Anlaşma sağlandığında, anlaşılan fiyat üzerinden işlem devam eder

### 2. **TAKAS (Trade/Exchange) Pazarlığı**
- Alıcı, kendi ürünü ile satıcının ürününü takas etmek ister
- Ürünler arasında değer farkı varsa, "ek nakit" pazarlığı yapılabilir
- Anlaşma sağlandığında, ek nakit tutarı belirlenir ve işlem devam eder

---

## Pazarlık Senaryoları

### Senaryo 1: SATIŞ Pazarlığı - Başarılı Anlaşma

```
DURUM: Satıcı Ali bir telefonu 1000₺'ye listeliyor

ADIM 1: Teklif Oluşturma (Transaction.Status = Pending)
├─ Alıcı Ayşe, ürünü görür ve "Teklif Ver" butonuna tıklar
├─ Sistem yeni bir Transaction kaydı oluşturur
│  ├─ ProductId: "phone-123"
│  ├─ SellerId: "ali-456"
│  ├─ BuyerId: "ayse-789"
│  ├─ Type: ProductType.Satis
│  ├─ Price: 1000₺ (orijinal)
│  ├─ Status: Pending
│  ├─ IsNegotiating: false
│  └─ CreatedAt: 2024-12-28 12:00
└─ Ali'ye bildirim gönderilir: "Ayşe teklifinizi bekliyor"

ADIM 2: Alıcının İlk Fiyat Teklifi
├─ Ayşe, teklifinde "Pazarlık Başlat" butonuna tıklar
├─ Popup açılır: "Fiyat teklifiniz nedir?"
├─ Ayşe 850₺ girer
├─ Sistem güncellemesi:
│  ├─ ProposedPriceByBuyer: 850₺
│  ├─ IsNegotiating: true
│  ├─ LastNegotiationDate: DateTime.UtcNow
│  └─ NegotiationNotes: "Alıcı 850₺ teklif etti"
├─ Ali'ye bildirim: "Ayşe 850₺ teklif etti"
└─ ConversationId'de sistem mesajı: "💰 Fiyat Teklifi: 850₺ (Orijinal: 1000₺)"

ADIM 3: Satıcının Karşı Teklifi
├─ Ali bildirimi görür, teklife bakar
├─ Seçenekler:
│  ├─ [A] "Kabul Et" → Direkt 850₺'ye anlaşma (ADIM 5'e git)
│  ├─ [B] "Karşı Teklif Ver" → Pazarlık devam eder (ADIM 3b)
│  └─ [C] "Reddet" → Pazarlık sona erer (Transaction.Status = Rejected)
├─ Ali [B]'yi seçer ve 950₺ karşı teklif verir
├─ Sistem güncellemesi:
│  ├─ CounterOfferBySeller: 950₺
│  ├─ LastNegotiationDate: DateTime.UtcNow
│  └─ NegotiationNotes += "Satıcı 950₺ karşı teklif verdi"
├─ Ayşe'ye bildirim: "Ali 950₺ karşı teklif verdi"
└─ ConversationId'de sistem mesajı: "💰 Karşı Teklif: 950₺"

ADIM 4: Alıcının Karşı Teklife Yanıtı
├─ Ayşe karşı teklifi görür
├─ Seçenekler:
│  ├─ [A] "Kabul Et" → 950₺'ye anlaşma (ADIM 5'e git)
│  ├─ [B] "Yeni Teklif Ver" → Pazarlık devam eder (örn: 900₺)
│  └─ [C] "Vazgeç" → Pazarlık iptal edilir
├─ Ayşe [A]'yı seçer (950₺'yi kabul eder)
└─ AcceptNegotiatedPriceAsync() çağrılır

ADIM 5: Anlaşma ve Finalizasyon
├─ Sistem güncellemesi:
│  ├─ QuotedPrice: 950₺ (anlaşılan fiyat kilitlendi)
│  ├─ Price: 950₺ (işlem fiyatı güncellendi)
│  ├─ IsNegotiating: false (pazarlık sonlandı)
│  ├─ NegotiationNotes += "Anlaşılan tutar: 950₺"
│  └─ UpdatedAt: DateTime.UtcNow
├─ Her iki tarafa bildirim:
│  ├─ Ali'ye: "Ayşe 950₺ fiyatı kabul etti"
│  └─ Ayşe'ye: "950₺ fiyatı kabul ettiniz"
├─ ConversationId'de sistem mesajı: "✅ Anlaşma Sağlandı: 950₺"
└─ Sonraki Adımlar:
   ├─ Transaction.Status hala Pending (satıcının onayı bekleniyor)
   ├─ Satıcı "Kabul Et" derse → Status = Accepted → Ödeme adımına geçilir
   └─ Ödeme tamamlanınca → Status = Completed

SONUÇ: ✅ Başarılı anlaşma - 950₺ üzerinden satış gerçekleşecek
```

---

### Senaryo 2: TAKAS Pazarlığı - Ek Nakit ile Anlaşma

```
DURUM: Satıcı Mehmet bir laptop listeliyor (ProductId: laptop-456)
       Alıcı Zeynep bir tablet'e sahip (ProductId: tablet-789)

ADIM 1: Takas Teklifi Oluşturma
├─ Zeynep, Mehmet'in laptop'unu görür
├─ "Takas Teklifi Ver" butonuna tıklar
├─ Kendi ürünlerinden "tablet-789"'u seçer
├─ İsteğe bağlı mesaj yazar: "Tablet + nakit takas yapalım mı?"
├─ Sistem yeni Transaction kaydı oluşturur:
│  ├─ ProductId: "laptop-456" (Mehmet'in ürünü)
│  ├─ OfferedProductId: "tablet-789" (Zeynep'in ürünü)
│  ├─ SellerId: "mehmet-111"
│  ├─ BuyerId: "zeynep-222"
│  ├─ Type: ProductType.Takas
│  ├─ Status: Pending
│  ├─ IsNegotiating: false
│  ├─ OfferMessage: "Tablet + nakit takas yapalım mı?"
│  └─ CreatedAt: DateTime.UtcNow
└─ Mehmet'e bildirim: "Zeynep tablet-789 ile takas teklif ediyor"

ADIM 2: Talep Edenin Ek Nakit Teklifi
├─ Zeynep, "Ek Nakit Teklif Et" butonuna tıklar
├─ 200₺ ek nakit teklif eder
├─ Sistem güncellemesi:
│  ├─ AdditionalCashByRequester: 200₺
│  ├─ IsNegotiating: true
│  ├─ LastNegotiationDate: DateTime.UtcNow
│  └─ NegotiationNotes: "Talep eden 200₺ ek nakit teklif etti"
├─ Mehmet'e bildirim: "Zeynep 200₺ ek nakit teklif etti"
└─ Konuşmada: "🔄 [Laptop-Tablet Takası] 💰 Ek Nakit: 200₺"

ADIM 3: Sahibin Karşı Nakit Teklifi
├─ Mehmet teklifi değerlendirir
├─ 300₺ ek nakit ister (karşı teklif)
├─ Sistem güncellemesi:
│  ├─ CounterCashByOwner: 300₺
│  ├─ LastNegotiationDate: DateTime.UtcNow
│  └─ NegotiationNotes += "Sahip 300₺ ek nakit istedi"
├─ Zeynep'e bildirim: "Mehmet 300₺ ek nakit istiyor"
└─ Konuşmada: "💰 Karşı Teklif: 300₺"

ADIM 4: Talep Edenin Final Teklifi
├─ Zeynep 250₺ teklif eder (son teklif)
├─ Sistem güncellemesi:
│  ├─ AdditionalCashByRequester: 250₺ (güncellendi)
│  └─ LastNegotiationDate: DateTime.UtcNow
└─ Mehmet'e bildirim: "Zeynep 250₺ teklif etti"

ADIM 5: Anlaşma
├─ Mehmet 250₺'yi kabul eder
├─ AcceptNegotiatedPriceAsync() çağrılır
├─ Sistem güncellemesi:
│  ├─ QuotedPrice: 250₺ (anlaşılan ek nakit)
│  ├─ IsNegotiating: false
│  ├─ NegotiationNotes += "Anlaşılan ek nakit: 250₺"
│  └─ UpdatedAt: DateTime.UtcNow
├─ Her iki tarafa bildirim:
│  ├─ Mehmet'e: "Zeynep ile 250₺ ek nakit ile anlaştınız"
│  └─ Zeynep'e: "Mehmet ile 250₺ ek nakit ile anlaştınız"
└─ Sonraki Adımlar:
   ├─ Transaction.Status → Accepted (her iki taraf da onaylarsa)
   ├─ QR kodlar oluşturulur (teslimat için)
   └─ Ek 250₺ ödeme simülasyonu yapılır

SONUÇ: ✅ Başarılı takas anlaşması - Tablet + 250₺ ↔ Laptop
```

---

### Senaryo 3: Pazarlık Reddedilmesi

```
DURUM: Satış pazarlığında anlaşma sağlanamıyor

ADIM 1-3: Yukarıdaki gibi pazarlık başlar
├─ Alıcı: 850₺ teklif eder
├─ Satıcı: 950₺ karşı teklif verir
└─ Alıcı: 870₺ tekrar teklif eder

ADIM 4: Satıcı Red Kararı Verir
├─ Satıcı "Reddet" butonuna tıklar
├─ Onay pop-up: "Bu teklifi reddetmek istediğinize emin misiniz?"
├─ Satıcı "Evet" der
├─ Sistem güncellemesi:
│  ├─ Status: Rejected
│  ├─ IsNegotiating: false
│  ├─ NegotiationNotes += "Pazarlık reddedildi - anlaşma sağlanamadı"
│  └─ UpdatedAt: DateTime.UtcNow
├─ Alıcıya bildirim: "Satıcı teklifinizi reddetti"
└─ Transaction artık kapalı, yeni pazarlık açılamaz

SONUÇ: ❌ Anlaşma sağlanamadı - İşlem sonlandı
```

---

## Durum Diyagramları

### Transaction Durum Geçişleri

```
┌─────────────────────────────────────────────────────────────────┐
│              Transaction Status State Machine                    │
└─────────────────────────────────────────────────────────────────┘

                    ┌─────────────┐
              ┌────▶│  PENDING    │◀────┐
              │     │ (Teklif     │     │
              │     │  Bekliyor)  │     │
              │     └──────┬──────┘     │
              │            │            │
              │            │ Pazarlık   │ Yeni Teklif
              │            │ Başlar     │
              │            ▼            │
              │     ┌─────────────┐     │
              │     │ PENDING +   │─────┘
              │     │IsNegotiating│
              │     │   = true    │
              │     └──────┬──────┘
              │            │
              │            │ Anlaşma
              │            │ Sağlandı
              │            ▼
              │     ┌─────────────┐
              │     │ PENDING +   │
              │     │IsNegotiating│
              │     │   = false   │
         Red  │     │(Fiyat       │     Kabul
              │     │ Kilitli)    │
              │     └──────┬──────┘
              │            │
              │            ▼
              │     ┌─────────────┐
              └─────│  ACCEPTED   │
                    │ (Onaylandı) │
                    └──────┬──────┘
                           │
                           │ Ödeme/Teslimat
                           │ Tamamlandı
                           ▼
                    ┌─────────────┐
                    │  COMPLETED  │
                    │(Tamamlandı) │
                    └─────────────┘

         İptal Durumları:
         ┌─────────────┐
         │  REJECTED   │ (Satıcı reddetti)
         └─────────────┘
         ┌─────────────┐
         │  CANCELLED  │ (Kullanıcı iptal etti)
         └─────────────┘
```

### Pazarlık Akış Diyagramı

```
┌─────────────────────────────────────────────────────────────────┐
│                Pazarlık (Negotiation) Flow                       │
└─────────────────────────────────────────────────────────────────┘

    [Transaction Oluşturuldu]
             │
             ▼
    ┌────────────────┐
    │ IsNegotiating  │
    │   = false      │───────┐
    └────────┬───────┘       │
             │                │ Direkt Kabul/Red
             │                │ (Pazarlıksız)
             ▼                │
    ┌────────────────┐        │
    │ Taraf Teklif   │        │
    │ Verir          │        │
    └────────┬───────┘        │
             │                │
             ▼                │
    ┌────────────────┐        │
    │ IsNegotiating  │        │
    │   = true       │        │
    └────────┬───────┘        │
             │                │
             ▼                │
    ┌─────────────────┐       │
    │ Karşı Taraf     │       │
    │ Değerlendirir   │       │
    └────────┬────────┘       │
             │                │
      ┌──────┴──────┐         │
      │             │         │
      ▼             ▼         │
 ┌─────────┐  ┌──────────┐   │
 │Karşı    │  │ Kabul Et │   │
 │Teklif   │  │          │   │
 └────┬────┘  └────┬─────┘   │
      │            │         │
      │            ▼         │
      │    ┌──────────────┐  │
      │    │AcceptPrice   │  │
      │    │IsNegotiating │◀─┘
      │    │  = false     │
      │    └──────┬───────┘
      │           │
      └───────────┤
                  │
                  ▼
         ┌────────────────┐
         │ Anlaşılan Fiyat│
         │ Kilitlendi     │
         └────────┬───────┘
                  │
                  ▼
         ┌────────────────┐
         │ Status =       │
         │ Accepted       │
         │ (Satıcı onayı) │
         └────────┬───────┘
                  │
                  ▼
         ┌────────────────┐
         │ Ödeme/Teslimat │
         │ Süreci         │
         └────────────────┘
```

---

## İş Mantığı Kuralları

### Pazarlık Kuralları

1. **Yetki Kontrolü**
   - Sadece alıcı (BuyerId) fiyat teklif edebilir
   - Sadece satıcı (SellerId) karşı teklif verebilir
   - Her iki taraf da anlaşmayı kabul edebilir

2. **Durum Kontrolü**
   - Pazarlık sadece `Status = Pending` durumunda başlatılabilir
   - `Status = Accepted` veya sonrası durumlarda pazarlık kapalıdır

3. **Fiyat Kuralları**
   - SATIŞ için: Teklif edilen fiyat > 0 olmalı
   - TAKAS için: Ek nakit >= 0 olabilir (0₺ ek nakit = sade takas)

4. **Anlaşma Mekanizması**
   - `AcceptNegotiatedPriceAsync()` çağrıldığında:
     - SATIŞ: `AgreedAmount` = `CounterOfferBySeller ?? ProposedPriceByBuyer`
     - TAKAS: `AgreedAmount` = `CounterCashByOwner ?? AdditionalCashByRequester`
   - Anlaşılan tutar `QuotedPrice`'a atanır ve kilitlenir
   - `IsNegotiating = false` yapılır

5. **Bildirimler**
   - Her teklif/karşı teklifte diğer tarafa bildirim gönderilir
   - ConversationId varsa, sistem mesajı olarak kaydedilir

### Veri Bütünlüğü

```csharp
// Transaction modeli - Pazarlık alanları
public class Transaction
{
    // SATIŞ Pazarlığı
    public decimal? ProposedPriceByBuyer { get; set; }      // Alıcının teklifi
    public decimal? CounterOfferBySeller { get; set; }      // Satıcının karşı teklifi
    
    // TAKAS Pazarlığı
    public decimal? AdditionalCashByRequester { get; set; } // Talep edenin ek nakit teklifi
    public decimal? CounterCashByOwner { get; set; }        // Sahibin istediği ek nakit
    
    // Ortak Alanlar
    public bool IsNegotiating { get; set; }                 // Pazarlık aktif mi?
    public DateTime? LastNegotiationDate { get; set; }      // Son pazarlık tarihi
    public string NegotiationNotes { get; set; }            // Pazarlık geçmişi
    public string ConversationId { get; set; }              // Mesajlaşma ID'si
    
    // Hesaplanan Değer
    public decimal AgreedAmount => /* Logic */;             // Anlaşılan tutar
}
```

---

## Akış Detayları

### Pazarlık Başlatma

**Kod Akışı:**
```
1. Kullanıcı "Pazarlık Başlat" butonuna tıklar
2. ViewModel.ProposePriceCommand tetiklenir
3. DisplayPromptAsync() ile tutar sorulur
4. TransactionService.ProposePriceForSaleAsync() veya ProposeAdditionalCashAsync() çağrılır
5. Firebase güncellenir:
   - IsNegotiating = true
   - ProposedPrice... / AdditionalCash... = tutar
   - LastNegotiationDate = now
6. Bildirim gönderilir
7. Conversation'a sistem mesajı eklenir
```

### Karşı Teklif Verme

**Kod Akışı:**
```
1. Satıcı/Sahip bildirimi görür veya Offers sayfasında görür
2. ViewModel.SendCounterOfferCommand tetiklenir
3. DisplayPromptAsync() ile karşı tutar sorulur
4. TransactionService.SendCounterOfferForSaleAsync() veya SendCounterCashOfferAsync() çağrılır
5. Firebase güncellenir:
   - CounterOffer... / CounterCash... = tutar
   - LastNegotiationDate = now
6. Bildirim gönderilir
7. Conversation'a sistem mesajı eklenir
```

### Anlaşmayı Kabul Etme

**Kod Akışı:**
```
1. Kullanıcı "Anlaşmayı Kabul Et" butonuna tıklar
2. ViewModel.AcceptNegotiatedPriceCommand tetiklenir
3. DisplayAlert() ile onay sorulur
4. TransactionService.AcceptNegotiatedPriceAsync() çağrılır
5. Firebase güncellenir:
   - QuotedPrice = AgreedAmount (fiyat kilitlendi)
   - IsNegotiating = false (pazarlık sonlandı)
   - NegotiationNotes += "Anlaşılan tutar: X₺"
6. Her iki tarafa bildirim gönderilir
7. Conversation'a başarı mesajı eklenir
8. İşlem normal akışına devam eder (Seller approval → Payment → Delivery)
```

### Pazarlığı Reddetme

**Kod Akışı:**
```
1. Satıcı "Reddet" butonuna tıklar
2. ViewModel.RespondToOfferAsync(accept: false) çağrılır
3. DisplayAlert() ile onay sorulur
4. TransactionService.RespondToOfferAsync(transactionId, false) çağrılır
5. Firebase güncellenir:
   - Status = Rejected
   - IsNegotiating = false
   - UpdatedAt = now
6. Alıcıya bildirim gönderilir
7. Transaction sonlandırılır
```

---

## Pazarlık Sonrası Akış

### Anlaşma Sağlandıktan Sonra

```
┌─────────────────────────────────────────────────────────────────┐
│            Pazarlık Sonrası İşlem Akışı                         │
└─────────────────────────────────────────────────────────────────┘

    [Anlaşma Sağlandı]
    IsNegotiating = false
    QuotedPrice = AgreedAmount
             │
             ▼
    ┌────────────────┐
    │ Transaction    │
    │ Status:        │
    │ Pending        │───── Satıcı henüz "Kabul Et" demedi
    └────────┬───────┘
             │
             │ Satıcı "Kabul Et"
             │ (RespondToOfferAsync)
             ▼
    ┌────────────────┐
    │ Status:        │
    │ Accepted       │
    └────────┬───────┘
             │
             ├─── Eğer Type = Satis ise:
             │    ┌────────────────────┐
             │    │ Ödeme Süreci       │
             │    │ - SimulatePayment  │
             │    │ - OTP Validation   │
             │    └────────┬───────────┘
             │             │
             │             ▼
             │    ┌────────────────────┐
             │    │ PaymentStatus:Paid │
             │    │ Status: Completed  │
             │    └────────────────────┘
             │
             └─── Eğer Type = Takas ise:
                  ┌────────────────────┐
                  │ QR Kod Oluşturma   │
                  │ (Her iki ürün için)│
                  └────────┬───────────┘
                           │
                           ├─ Ek Nakit > 0 ise:
                           │  ┌─────────────────┐
                           │  │ Ödeme Simülasyonu│
                           │  │ (Ek nakit için) │
                           │  └─────────┬───────┘
                           │            │
                           ▼            ▼
                  ┌────────────────────┐
                  │ Teslimat Bekleniyor│
                  │ (QR Scan)          │
                  └────────┬───────────┘
                           │
                           │ Her iki QR tarandı
                           ▼
                  ┌────────────────────┐
                  │ Status: Completed  │
                  └────────────────────┘
```

### Bildirim ve Mesajlaşma Entegrasyonu

Pazarlık sürecinde her adımda:
1. **Firebase Notification** oluşturulur
2. **Conversation mesajı** (sistem mesajı) eklenir
3. Kullanıcılar anlık bilgilendirilir

**Örnek Bildirimler:**
- "Ali 850₺ teklif etti"
- "Zeynep 950₺ karşı teklif verdi"
- "Anlaşma sağlandı: 900₺"
- "Pazarlık reddedildi"

---

## Gelecek İyileştirmeler

### Önerilen Eklentiler

1. **Otomatik Zaman Aşımı**
   - Pazarlık 24 saat içinde cevap verilmezse otomatik iptal
   - `LastNegotiationDate` ile kontrol edilebilir

2. **Maksimum Tur Limiti**
   - Örneğin 5 tur pazarlıktan sonra otomatik sonlandırma
   - Sonsuz pazarlık döngüsünü önler

3. **Fiyat Aralığı Kontrolü**
   - Alıcı orijinal fiyatın %70'inden düşük teklif veremez
   - Satıcı orijinal fiyatın üstünde karşı teklif veremez

4. **Pazarlık Geçmişi**
   - Tüm teklif geçmişini görme
   - "Tarihçe" butonu ile detaylı görünüm

5. **Hızlı Kabul/Red**
   - Swipe left = Red
   - Swipe right = Kabul
   - Gesture-based UI

6. **Akıllı Öneri Sistemi**
   - Ortalama pazarlık oranlarına göre öneride bulunma
   - "Benzer ürünlerde %15 indirim yapıldı" gibi

---

## Özet

Bu dokümanda KamPay uygulamasının pazarlık sistemi:
- ✅ Detaylı senaryolarla açıklandı
- ✅ Durum diyagramları ile görselleştirildi
- ✅ İş mantığı kuralları belirlendi
- ✅ Kod akışları dokümante edildi
- ✅ Pazarlık sonrası süreç netleştirildi

**Mevcut Durum:** Sistem zaten çalışır durumda, temel pazarlık fonksiyonları implement edilmiş.

**Yapılması Gerekenler:** 
- Kullanıcı deneyimini iyileştirme (UI/UX)
- Ek güvenlik kontrolleri
- Gelişmiş özellikler (zaman aşımı, tur limiti, vb.)
