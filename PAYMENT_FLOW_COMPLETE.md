# ÖDEME AKIŞ DOKÜMANTASYONU - ÜRÜN vs HİZMET

## İçindekiler
1. [ÜRÜN SATIŞI İÇİN ÖDEME AKIŞI](#ürün-satışı-için-ödeme-akışi)
2. [HİZMET İÇİN ÖDEME AKIŞI](#hizmet-için-ödeme-akışi)
3. [FARKLAR VE BENZERLIKLER](#farklar-ve-benzerlikler)
4. [KOD SEVİYESİNDE AKIŞ](#kod-seviyesinde-akış)

---

## ÜRÜN SATIŞI İÇİN ÖDEME AKIŞI

### 📦 1. Ürün Satış Süreci (Transaction Tabanlı)

```
┌─────────────────────────────────────────────────────────────────┐
│                    ÜRÜN SATIŞI AKIŞI                             │
│              (Transaction Model Kullanılır)                      │
└─────────────────────────────────────────────────────────────────┘

ADIM 1: Teklif Gönderme (ProductDetailPage)
─────────────────────────────────────────────
Kullanıcı Akışı:
┌──────────────────────┐
│ ProductDetailPage    │
│ "Satın Al" Butonu    │
└──────┬───────────────┘
       │
       ▼
┌──────────────────────────────────────────┐
│ CreateRequestAsync()                     │
│ - Product bilgisi                        │
│ - User bilgisi                           │
│ → Transaction oluşturulur                │
│   Status: Pending                        │
│   PaymentStatus: Pending                 │
│   Type: Satis                            │
└──────┬───────────────────────────────────┘
       │
       ▼
Firebase: transactions/{transactionId}
       │
       ▼
Satıcıya Bildirim: "Yeni Satış Teklifi!"


ADIM 2: Pazarlık Süreci (Opsiyonel - OffersPage)
─────────────────────────────────────────────────
[Alıcı] → ProposePriceForSaleAsync()
   ↓
Transaction.ProposedPriceByBuyer = 150₺
Transaction.IsNegotiating = true
   ↓
[Satıcı] → SendCounterOfferForSaleAsync()
   ↓
Transaction.CounterOfferBySeller = 175₺
   ↓
[Her İki Taraf] → AcceptNegotiatedPriceAsync()
   ↓
Transaction.QuotedPrice = 175₺ (Anlaşılan fiyat)
Transaction.IsNegotiating = false
Transaction.Price = 175₺


ADIM 3: Satıcı Teklifi Kabul Eder (OffersPage - Gelen Teklifler)
──────────────────────────────────────────────────────────────────
Satıcı:
┌──────────────────────┐
│ OffersPage           │
│ "Kabul Et" Butonu    │
└──────┬───────────────┘
       │
       ▼
┌──────────────────────────────────────────┐
│ RespondToOfferAsync(accept: true)       │
│                                          │
│ Transaction Güncellenir:                 │
│ - Status: Accepted                       │
│ - UpdatedAt: DateTime.UtcNow             │
│                                          │
│ Ürün Rezerve Edilir:                     │
│ - Product.IsReserved = true              │
└──────┬───────────────────────────────────┘
       │
       ▼
Alıcıya Bildirim: "Teklifin Kabul Edildi!"
       │
       ▼
Alıcının Offers Sayfasında "Ödeme Yap" Butonu Görünür


ADIM 4: Alıcı Ödeme Sayfasına Gider (OffersPage - Giden Teklifler)
───────────────────────────────────────────────────────────────────
Alıcı:
┌──────────────────────┐
│ OffersPage           │
│ "Ödeme Yap" Butonu   │
└──────┬───────────────┘
       │
       ▼
┌──────────────────────────────────────────┐
│ CompletePaymentAsync()                   │
│                                          │
│ Kontroller:                              │
│ ✓ Type == Satis                          │
│ ✓ Status == Accepted                     │
│ ✓ PaymentStatus == Pending               │
│                                          │
│ Navigasyon:                              │
│ → Shell.GoToAsync(PaymentPage)           │
│   with Transaction parameter             │
└──────────────────────────────────────────┘


ADIM 5: Ödeme Yöntemi Seçimi (PaymentPage)
───────────────────────────────────────────
┌──────────────────────────────────────────┐
│ PaymentPage UI                           │
│                                          │
│ [Kredi Kartı]  [EFT/Havale]             │
└──────┬───────────────────────────────────┘
       │
       ▼
┌──────────────────────────────────────────┐
│ StartPaymentAsync(method)                │
│ - method = "cardsim" veya                │
│            "banktransfersim"             │
└──────┬───────────────────────────────────┘
       │
       ▼
┌──────────────────────────────────────────┐
│ TransactionService                       │
│ .CreatePaymentSimulationAsync()          │
│                                          │
│ 1. Transaction'ı al ve doğrula           │
│ 2. Tutarı belirle:                       │
│    amount = QuotedPrice > 0              │
│           ? QuotedPrice                  │
│           : Price                        │
│                                          │
│ 3. PaymentDto oluştur                    │
│    - PaymentId (GUID)                    │
│    - Amount                              │
│    - Currency: "TRY"                     │
│    - Status: Initiated                   │
│    - Method: CardSim/BankTransferSim     │
│                                          │
│ 4. KART ise → OTP oluştur                │
│    Firebase: tempotp/{paymentId}         │
│    - Otp: "123456" (6 hane)              │
│    - ExpiresAt: +2 dakika                │
│                                          │
│ 5. EFT ise → Referans oluştur            │
│    - BankName: "Ziraat Bankası"          │
│    - BankReference: "BTX-..."            │
│                                          │
│ 6. Transaction güncelle                  │
│    - PaymentMethod                       │
│    - PaymentSimulationId                 │
│    - PaymentStatus: Pending              │
└──────┬───────────────────────────────────┘
       │
       ▼
UI Güncellenir


ADIM 6A: Kredi Kartı Ödemesi
─────────────────────────────
┌──────────────────────────────────────────┐
│ UI: OTP Gösterimi                        │
│                                          │
│ ┌────────────────────────────────────┐  │
│ │ 📱 SİMÜLASYON KODU                │  │
│ │      123456                         │  │
│ │ Bu kodu aşağıya girin               │  │
│ └────────────────────────────────────┘  │
│                                          │
│ [Doğrulama Kodu: ______]                │
│ [✓ Ödemeyi Onayla]                      │
└──────┬───────────────────────────────────┘
       │ (Kullanıcı OTP girer)
       ▼
┌──────────────────────────────────────────┐
│ ConfirmCardPaymentAsync()                │
│                                          │
│ 1. Validasyon:                           │
│    - OTP 6 hane mi?                      │
│    - Sayısal mı?                         │
│                                          │
│ 2. Sanitization:                         │
│    - InputSanitizer.SanitizeText()       │
│                                          │
│ 3. Servis Çağrısı:                       │
│    ConfirmPaymentSimulationAsync()       │
└──────┬───────────────────────────────────┘
       │
       ▼
┌──────────────────────────────────────────┐
│ ConfirmPaymentSimulationAsync()          │
│                                          │
│ 1. Firebase'den OTP al                   │
│ 2. Süre kontrolü (< 2 dk)                │
│ 3. OTP eşleşme kontrolü                  │
│ 4. OTP'yi sil (tek kullanımlık)          │
│                                          │
│ 5. Transaction Güncelle:                 │
│    - PaymentStatus: Paid                 │
│    - PaymentCompletedAt: DateTime.UtcNow │
│                                          │
│ 6. Type == Satis ise:                    │
│    → CompleteTransactionInternalAsync()  │
└──────┬───────────────────────────────────┘
       │
       ▼
ADIM 7: İşlemi Tamamla


ADIM 6B: EFT/Havale Ödemesi
────────────────────────────
┌──────────────────────────────────────────┐
│ UI: Banka Bilgileri                      │
│                                          │
│ ┌────────────────────────────────────┐  │
│ │ 🏦 Havale/EFT Bilgileri            │  │
│ │                                    │  │
│ │ Banka: Ziraat Bankası              │  │
│ │ Tutar: 175.00 ₺                    │  │
│ │ Referans: BTX-20241228120000-ABC   │  │
│ └────────────────────────────────────┘  │
│                                          │
│ ⚠️ Önemli: Referans kodunu mutlaka       │
│    ödeme açıklamasına yazınız!           │
│                                          │
│ [✓ Ödemeyi Tamamladım]                  │
└──────┬───────────────────────────────────┘
       │ (Kullanıcı bankadan ödeme yapar)
       │ (Kullanıcı "Tamamladım" tıklar)
       ▼
┌──────────────────────────────────────────┐
│ ConfirmCardPaymentAsync()                │
│ (İsim "Card" ama EFT için de çalışır)   │
│                                          │
│ → ConfirmPaymentSimulationAsync()        │
│   - OTP kontrolü YOK                     │
│   - Direkt PaymentStatus: Paid           │
└──────┬───────────────────────────────────┘
       │
       ▼
ADIM 7: İşlemi Tamamla


ADIM 7: İşlemi Tamamla (Her İki Yöntem İçin)
─────────────────────────────────────────────
┌──────────────────────────────────────────┐
│ CompleteTransactionInternalAsync()       │
│                                          │
│ 1. Transaction Güncelle:                 │
│    - Status: Completed                   │
│    - UpdatedAt: DateTime.UtcNow          │
│    Firebase: transactions/{id}           │
│                                          │
│ 2. Ürünü Kapat:                          │
│    - Product.IsActive = false            │
│    - Product.IsSold = true               │
│    Firebase: products/{productId}        │
│                                          │
│ 3. Puanları Ver:                         │
│    - Satıcı: +10 puan                    │
│    - Alıcı: +5 puan                      │
│                                          │
│ 4. Bildirimler Gönder:                   │
│    Satıcıya:                             │
│    "Ürünün Satıldı!"                     │
│                                          │
│    Alıcıya:                              │
│    "Satın Alma Tamamlandı!"              │
└──────┬───────────────────────────────────┘
       │
       ▼
┌──────────────────────┐
│ Başarı Mesajı        │
│ "Ödeme Onaylandı"    │
│ → OffersPage         │
└──────────────────────┘
```

---

## HİZMET İÇİN ÖDEME AKIŞI

### 🛠️ 2. Hizmet Satış Süreci (ServiceRequest Tabanlı)

```
┌─────────────────────────────────────────────────────────────────┐
│                    HİZMET SATIŞI AKIŞI                           │
│           (ServiceRequest Model Kullanılır)                      │
└─────────────────────────────────────────────────────────────────┘

ADIM 1: Hizmet Talebi Gönderme (ServiceSharingPage)
────────────────────────────────────────────────────
Kullanıcı Akışı:
┌──────────────────────┐
│ ServiceSharingPage   │
│ "Hizmeti Talep Et"   │
└──────┬───────────────┘
       │
       ▼
┌──────────────────────────────────────────┐
│ RequestServiceAsync()                    │
│                                          │
│ ServiceRequest oluşturulur:              │
│ - RequestId: GUID                        │
│ - ServiceId                              │
│ - ServiceTitle                           │
│ - ProviderId (Hizmet Sağlayıcı)          │
│ - RequesterId (Talep Eden)               │
│ - Status: Pending                        │
│ - QuotedPrice: offer.Price               │
│ - Price: offer.Price                     │
│ - TimeCreditValue: offer.TimeCredits     │
│ - PaymentStatus: None                    │
│ - PaymentMethod: None                    │
└──────┬───────────────────────────────────┘
       │
       ▼
Firebase: servicerequests/{requestId}
       │
       ▼
Sağlayıcıya Bildirim: "Yeni Hizmet Talebi!"


ADIM 2: Pazarlık Süreci (ServiceRequestsPage)
──────────────────────────────────────────────
[Talep Eden] → ProposePriceAsync()
   ↓
ServiceRequest.ProposedPriceByRequester = 100₺
ServiceRequest.IsNegotiating = true
   ↓
[Sağlayıcı] → SendCounterOfferAsync()
   ↓
ServiceRequest.CounterOfferByProvider = 120₺
   ↓
[Her İki Taraf] → AcceptNegotiatedPriceAsync()
   ↓
ServiceRequest.QuotedPrice = 120₺
ServiceRequest.IsNegotiating = false


ADIM 3: Sağlayıcı Talebi Kabul Eder (ServiceRequestsPage)
──────────────────────────────────────────────────────────
Sağlayıcı:
┌──────────────────────┐
│ ServiceRequestsPage  │
│ "Kabul Et" Butonu    │
└──────┬───────────────┘
       │
       ▼
┌──────────────────────────────────────────┐
│ RespondToRequestAsync(accept: true)      │
│                                          │
│ ServiceRequest Güncellenir:              │
│ - Status: Accepted                       │
│ - UpdatedAt: DateTime.UtcNow             │
└──────┬───────────────────────────────────┘
       │
       ▼
Talep Edene Bildirim: "Hizmet Talebin Onaylandı!"


ADIM 4: Hizmet Sağlanır (Gerçek Hayatta)
─────────────────────────────────────────
[Sağlayıcı hizmeti verir]
   ↓
[Talep Eden memnun kalır]
   ↓
ServiceRequestsPage'e geri döner


ADIM 5: Hizmeti Tamamla Butonu (ServiceRequestsPage - Giden Talepler)
──────────────────────────────────────────────────────────────────────
⚠️ ÖNEMLİ: Burada iki farklı yöntem var:

YÖNTEM A: Eski Simülasyon (Tek Adım - Kullanılmıyor)
────────────────────────────────────────────────────
┌──────────────────────────────────────────┐
│ SimulatePaymentAndCompleteAsync()        │
│ (FirebaseServiceSharingService içinde)  │
│                                          │
│ 1. CreatePaymentSimulationAsync()        │
│ 2. Task.Delay(1500) - Simülasyon        │
│ 3. OTP otomatik al                       │
│ 4. ConfirmPaymentSimulationAsync()       │
│ 5. ServiceRequest.Status = Completed     │
│ 6. Bildirim gönder                       │
└──────────────────────────────────────────┘

❌ SORUN: Kullanıcı ara adımları görmüyor!


YÖNTEM B: Yeni Akış (Adım Adım - ÖNERİLEN)
───────────────────────────────────────────
Talep Eden:
┌──────────────────────┐
│ ServiceRequestsPage  │
│ "Hizmeti Tamamla"    │
└──────┬───────────────┘
       │
       ▼
┌──────────────────────────────────────────┐
│ CompleteServiceRequestAsync()            │
│ (ServiceRequestsViewModel içinde)       │
│                                          │
│ Kontroller:                              │
│ ✓ Status == Accepted                     │
│ ✓ RequesterId == currentUserId           │
│                                          │
│ QuotedPrice > 0 mı?                      │
└──────┬───────────────────────────────────┘
       │
       ├─── EVET (Ücretli Hizmet) ───┐
       │                              │
       │                              ▼
       │                   ┌──────────────────────────┐
       │                   │ ÖDEME SÜRECİ GEREKLİ    │
       │                   │                          │
       │                   │ 1. Transaction oluştur:  │
       │                   │    (ServiceRequest'ten)  │
       │                   │    - Type: Satis         │
       │                   │    - Price: QuotedPrice  │
       │                   │    - Status: Accepted    │
       │                   │    - PaymentStatus:      │
       │                   │      Pending             │
       │                   │                          │
       │                   │ 2. PaymentPage'e yönlen: │
       │                   │    GoToAsync(PaymentPage)│
       │                   └──────────┬───────────────┘
       │                              │
       │                              ▼
       │                   [ÜRÜN SATIŞI İLE AYNI ÖDEME AKIŞI]
       │                   (Yukarıdaki ADIM 5-7)
       │
       └─── HAYIR (Ücretsiz/Zaman Kredisi) ───┐
                                               │
                                               ▼
                                    ┌──────────────────────────┐
                                    │ KREDİ TRANSFERİ          │
                                    │                          │
                                    │ TransferTimeCreditsAsync()│
                                    │ - From: RequesterId      │
                                    │ - To: ProviderId         │
                                    │ - Amount: TimeCreditValue│
                                    └──────────┬───────────────┘
                                               │
                                               ▼
                                    ┌──────────────────────────┐
                                    │ TAMAMLA                  │
                                    │                          │
                                    │ - Status: Completed      │
                                    │ - Puan ver (her iki     │
                                    │   tarafa)                │
                                    │ - Bildirim gönder        │
                                    └──────────────────────────┘
```

---

## FARKLAR VE BENZERLIKLER

### 📊 Karşılaştırma Tablosu

| Özellik | ÜRÜN SATIŞI | HİZMET SATIŞI |
|---------|-------------|---------------|
| **Model** | `Transaction` | `ServiceRequest` |
| **Collection** | `transactions` | `servicerequests` |
| **Başlangıç** | ProductDetailPage | ServiceSharingPage |
| **Talep Metodu** | `CreateRequestAsync()` | `RequestServiceAsync()` |
| **Kabul Metodu** | `RespondToOfferAsync()` | `RespondToRequestAsync()` |
| **PaymentStatus** | `Pending` (başlangıçta) | `None` (başlangıçta) |
| **Ödeme Butonu** | OffersPage'de görünür | ServiceRequestsPage'de görünür |
| **Ödeme Sayfası** | PaymentPage | **PaymentPage (AYNI)** |
| **Ödeme Servisi** | `FirebaseTransactionService` | **Aynı servis kullanılmalı** |
| **Tamamlama** | Otomatik (ödeme sonrası) | Manuel (kullanıcı tıklar) |
| **Kredi Sistemi** | ❌ Yok | ✅ Var (TimeCreditValue) |
| **QR Kod Teslimat** | ✅ Takas için var | ❌ Yok |

### ✅ Benzerlikler

1. **Aynı Ödeme UI**: `PaymentPage.xaml` her ikisi için de kullanılır
2. **Aynı Ödeme Metodları**: Kredi Kartı (OTP) ve EFT/Havale
3. **Aynı Simülasyon Sistemi**: `CreatePaymentSimulationAsync()` + `ConfirmPaymentSimulationAsync()`
4. **Aynı Validasyonlar**: InputSanitizer, RateLimiter
5. **Aynı PaymentDto Modeli**: `PaymentDto`, `PaymentMethodType`, `ServicePaymentStatus`

### ❌ Farklar

1. **Model**: Transaction vs ServiceRequest
2. **Başlangıç Noktası**: ProductDetail vs ServiceSharing
3. **PaymentStatus Başlangıç**: `Pending` vs `None`
4. **Tamamlama Mekanizması**: 
   - Ürün: Ödeme yapılınca otomatik tamamlanır
   - Hizmet: Kullanıcı "Tamamla" butonuna tıklar
5. **Kredi Sistemi**: Hizmet için zaman kredisi var, ürün için yok

---

## KOD SEVİYESİNDE AKIŞ

### 🔧 Ürün Satışı - Kod Akışı

```csharp
// 1. TEKLIF OLUŞTURMA (ProductDetailViewModel.cs)
[RelayCommand]
private async Task SendRequestAsync()
{
    // Transaction oluştur
    var result = await _transactionService.CreateRequestAsync(Product, currentUser);
    
    // Firebase: transactions/{transactionId}
    // {
    //   TransactionId: "abc123",
    //   Type: ProductType.Satis,
    //   Status: TransactionStatus.Pending,
    //   PaymentStatus: PaymentStatus.Pending,
    //   Price: 100.00,
    //   ...
    // }
}

// 2. TEKLİFİ KABUL ETME (OffersViewModel.cs)
[RelayCommand]
private async Task AcceptOfferAsync(Transaction transaction)
{
    await _transactionService.RespondToOfferAsync(transaction.TransactionId, accept: true);
    
    // Transaction güncellenir:
    // Status: Accepted
    // Ürün rezerve edilir
}

// 3. ÖDEME SAYFASINA GİTME (OffersViewModel.cs)
[RelayCommand]
private async Task CompletePaymentAsync(Transaction transaction)
{
    if (transaction.Type == ProductType.Satis &&
        transaction.Status == TransactionStatus.Accepted &&
        transaction.PaymentStatus == PaymentStatus.Pending)
    {
        var navigationParameter = new Dictionary<string, object>
        {
            { "Transaction", transaction }
        };
        
        await Shell.Current.GoToAsync(nameof(PaymentPage), navigationParameter);
    }
}

// 4. ÖDEME YÖNTEMİ SEÇME (PaymentViewModel.cs)
[RelayCommand]
private async Task StartPaymentAsync(string method) // "cardsim" or "banktransfersim"
{
    // Rate limiting kontrolü
    if (!NetworkHelper.HasInternetConnection())
        return;
    
    // Ödeme simülasyonu başlat
    var result = await _transactionService.CreatePaymentSimulationAsync(
        Transaction.TransactionId, 
        method
    );
    
    if (result.Success)
    {
        PaymentDetails = result.Data;
        IsCardSelected = method == "cardsim";
        IsEftSelected = method == "banktransfersim";
        
        if (IsCardSelected)
        {
            // OTP'yi al ve göster (simülasyon için)
            var otpResult = await _transactionService.GetSimulationOtpAsync(
                PaymentDetails.PaymentId
            );
            SimulatedOtp = otpResult.Data; // "123456"
        }
    }
}

// 5. ÖDEME ONAYLAMA (PaymentViewModel.cs)
[RelayCommand]
private async Task ConfirmCardPaymentAsync()
{
    // Validasyon
    if (IsCardSelected && string.IsNullOrWhiteSpace(OtpCode))
        return;
    
    // Sanitization
    var sanitizedOtp = InputSanitizer.SanitizeText(OtpCode);
    
    // Onay
    var result = await _transactionService.ConfirmPaymentSimulationAsync(
        Transaction.TransactionId,
        PaymentDetails.PaymentId,
        sanitizedOtp
    );
    
    if (result.Success)
    {
        // Başarı mesajı
        await Shell.Current.DisplayAlert("Başarılı", 
            "Kredi kartı ödemesi onaylandı!", 
            "Tamam");
        
        // Offers sayfasına dön
        await Shell.Current.GoToAsync($"//{nameof(OffersPage)}");
    }
}

// 6. ÖDEME ONAYLAMA SERVİS KATMANI (FirebaseTransactionService.cs)
public async Task<ServiceResult<bool>> ConfirmPaymentSimulationAsync(
    string transactionId, 
    string paymentId, 
    string? otp = null)
{
    var transaction = await GetTransactionAsync(transactionId);
    
    // KART ÖDEMESİ: OTP Kontrolü
    if (transaction.PaymentMethod == PaymentMethodType.CardSim)
    {
        var saved = await GetOtpFromFirebase(paymentId);
        
        if (DateTime.UtcNow > saved.ExpiresAt)
            return FailureResult("OTP süresi doldu");
        
        if (saved.Otp != otp)
            return FailureResult("OTP geçersiz");
        
        await DeleteOtp(paymentId); // Tek kullanımlık
    }
    
    // Ödeme durumunu güncelle
    transaction.PaymentStatus = PaymentStatus.Paid;
    transaction.PaymentCompletedAt = DateTime.UtcNow;
    
    // SATIŞ: İşlemi tamamla
    if (transaction.Type == ProductType.Satis)
    {
        await CompleteTransactionInternalAsync(transaction);
    }
    
    return SuccessResult(true);
}

// 7. İŞLEMİ TAMAMLAMA (FirebaseTransactionService.cs)
private async Task<ServiceResult<Transaction>> CompleteTransactionInternalAsync(
    Transaction transaction)
{
    // 1. Transaction'ı tamamla
    transaction.Status = TransactionStatus.Completed;
    await UpdateTransaction(transaction);
    
    // 2. Ürünü kapat
    await _productService.MarkAsSoldAsync(transaction.ProductId);
    
    // 3. Puanları ver
    await _userProfileService.AddPointsAsync(transaction.SellerId, 10);
    await _userProfileService.AddPointsAsync(transaction.BuyerId, 5);
    
    // 4. Bildirimleri gönder
    await SendCompletionNotifications(transaction);
    
    return SuccessResult(transaction);
}
```

### 🛠️ Hizmet Satışı - Kod Akışı

```csharp
// 1. HİZMET TALEBİ OLUŞTURMA (ServiceSharingViewModel.cs)
[RelayCommand]
private async Task RequestServiceAsync(ServiceOffer offer)
{
    var request = new ServiceRequest
    {
        RequestId = Guid.NewGuid().ToString(),
        ServiceId = offer.ServiceId,
        ProviderId = offer.ProviderId,
        RequesterId = currentUser.UserId,
        Status = ServiceRequestStatus.Pending,
        QuotedPrice = offer.Price, // Anlaşılan fiyat
        Price = offer.Price,
        TimeCreditValue = offer.TimeCredits,
        PaymentStatus = ServicePaymentStatus.None, // ← Başlangıçta None!
        PaymentMethod = PaymentMethodType.None
    };
    
    await _serviceSharingService.RequestServiceAsync(offer, currentUser, message);
    
    // Firebase: servicerequests/{requestId}
}

// 2. TALEBİ KABUL ETME (ServiceRequestsViewModel.cs)
[RelayCommand]
private async Task AcceptRequestAsync(ServiceRequest request)
{
    await _serviceSharingService.RespondToRequestAsync(
        request.RequestId, 
        accept: true
    );
    
    // ServiceRequest güncellenir:
    // Status: Accepted
}

// 3. HİZMETİ TAMAMLAMA BUTONU (ServiceRequestsViewModel.cs)
[RelayCommand]
private async Task CompleteServiceAsync(ServiceRequest request)
{
    // ✅ YENİ YÖNTEM: PaymentPage'e yönlendir
    
    if (request.QuotedPrice > 0) // Ücretli hizmet
    {
        // Transaction modeline dönüştür
        var transaction = ConvertToTransaction(request);
        
        // PaymentPage'e git
        var navigationParameter = new Dictionary<string, object>
        {
            { "Transaction", transaction }
        };
        
        await Shell.Current.GoToAsync(nameof(PaymentPage), navigationParameter);
    }
    else // Ücretsiz veya Zaman Kredisi
    {
        // Kredi transferi yap
        await _userProfileService.TransferTimeCreditsAsync(
            request.RequesterId,
            request.ProviderId,
            request.TimeCreditValue,
            $"Hizmet tamamlandı: {request.ServiceTitle}"
        );
        
        // Tamamla
        request.Status = ServiceRequestStatus.Completed;
        await UpdateServiceRequest(request);
        
        // Puan ve bildirim
        await AddPoints(request);
        await SendNotifications(request);
    }
}

// Helper metod
private Transaction ConvertToTransaction(ServiceRequest request)
{
    return new Transaction
    {
        TransactionId = $"service_{request.RequestId}",
        ProductId = request.ServiceId,
        ProductTitle = request.ServiceTitle,
        Type = ProductType.Satis, // Hizmet de satış gibi işleniyor
        SellerId = request.ProviderId,
        BuyerId = request.RequesterId,
        Price = request.QuotedPrice,
        QuotedPrice = request.QuotedPrice,
        Status = TransactionStatus.Accepted,
        PaymentStatus = PaymentStatus.Pending,
        CreatedAt = request.RequestedAt
    };
}
```

---

## SORUN VE ÇÖZÜM

### ❌ Mevcut Sorun

```csharp
// FirebaseServiceSharingService.cs içinde
public async Task<ServiceResult<bool>> SimulatePaymentAndCompleteAsync(...)
{
    // 1. CreatePaymentSimulationAsync() - Kullanıcı görmüyor
    // 2. Task.Delay(1500) - Kullanıcı görmüyor
    // 3. OTP otomatik alınıyor - Kullanıcı görmüyor
    // 4. ConfirmPaymentSimulationAsync() - Kullanıcı görmüyor
    // 5. Completed - Sadece sonuç görünüyor
    
    // ❌ SORUN: Tüm süreç arka planda oluyor!
}
```

### ✅ Çözüm

```csharp
// ServiceRequestsViewModel.cs içinde
[RelayCommand]
private async Task CompleteServiceAsync(ServiceRequest request)
{
    if (request.QuotedPrice > 0)
    {
        // ÜRÜN SATIŞI AKIŞINI KULLAN
        // 1. Transaction'a dönüştür
        // 2. PaymentPage'e yönlendir
        // 3. Kullanıcı kart/EFT seçer
        // 4. Kullanıcı OTP girer
        // 5. Ödeme onaylanır
        // 6. Hizmet tamamlanır
        
        var transaction = ConvertToTransaction(request);
        await Shell.Current.GoToAsync(nameof(PaymentPage), 
            new Dictionary<string, object> { { "Transaction", transaction } });
    }
    else
    {
        // Zaman kredisi akışı (değişiklik yok)
        await TransferCreditsAndComplete(request);
    }
}
```

---

## ÖZET

### 📦 Ürün Satışı
✅ **Tamam** - Ödeme akışı tam çalışıyor:
- Teklif → Kabul → PaymentPage → Kart/EFT Seçimi → OTP Girişi → Onay → Tamamlandı

### 🛠️ Hizmet Satışı  
❌ **Sorunlu** - Ödeme akışı eksik:
- Talep → Kabul → "Tamamla" → ❌ Direkt tamamlanıyor (ara adımlar yok)

✅ **Çözüm**:
- Talep → Kabul → "Tamamla" → ✅ PaymentPage → (Ürün ile aynı akış) → Tamamlandı

### 🔑 Anahtar Noktalar

1. **PaymentPage her ikisi için ortak kullanılır**
2. **CreatePaymentSimulationAsync() ve ConfirmPaymentSimulationAsync() her ikisi için ortak**
3. **Tek fark**: Hizmet için Transaction modeline dönüştürme yapılması gerekiyor
4. **SimulatePaymentAndCompleteAsync() kullanılmamalı** - Bu metod ara adımları atlıyor

---

Bu dokümantasyon, ödeme akışının nasıl çalıştığını ve ürün ile hizmet arasındaki farkları detaylı olarak açıklıyor. 

**Sonraki Adımlar**:
1. `ServiceRequestsViewModel.cs` içinde `CompleteServiceAsync()` metodunu güncelle
2. `ConvertToTransaction()` helper metodunu ekle
3. PaymentPage'de hem Transaction hem de ServiceRequest'i destekle (veya sadece Transaction kullan)
4. Test et ve ara adımların çalıştığını doğrula
