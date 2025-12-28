# Ödeme Akış Diyagramları

## 1. Kredi Kartı Ödeme Akışı

```
┌─────────────────────────────────────────────────────────────────┐
│                    Kredi Kartı Ödeme Akışı                      │
└─────────────────────────────────────────────────────────────────┘

    ┌──────────────────┐
    │  Kullanıcı       │
    │  "Kredi Kartı"   │
    │  Seçer           │
    └────────┬─────────┘
             │
             ▼
    ┌──────────────────────────────────────────┐
    │  PaymentViewModel.StartPaymentAsync()    │
    │  - method = "cardsim"                    │
    └────────┬─────────────────────────────────┘
             │
             ▼
    ┌──────────────────────────────────────────┐
    │  TransactionService                      │
    │  .CreatePaymentSimulationAsync()         │
    │                                          │
    │  1. Transaction'ı doğrula                │
    │  2. OTP oluştur (6 hane, rastgele)       │
    │  3. Firebase'e kaydet (2 dk TTL)         │
    │  4. PaymentDto döndür                    │
    └────────┬─────────────────────────────────┘
             │
             ▼
    ┌──────────────────────────────────────────┐
    │  PaymentViewModel                        │
    │  - IsCardSelected = true                 │
    │  - GetSimulatedOtpAsync() çağır          │
    │  - SimulatedOtp değişkenine ata          │
    └────────┬─────────────────────────────────┘
             │
             ▼
    ┌──────────────────────────────────────────┐
    │  UI Güncellemesi                         │
    │                                          │
    │  ┌────────────────────────────────────┐ │
    │  │  📱 SİMÜLASYON KODU                │ │
    │  │       123456                        │ │
    │  │  Bu kodu aşağıya girin              │ │
    │  └────────────────────────────────────┘ │
    │                                          │
    │  [Doğrulama Kodu Girişi: ______]        │
    │  [✓ Ödemeyi Onayla]                     │
    └────────┬─────────────────────────────────┘
             │
             ▼ (Kullanıcı OTP girer ve onayla tıklar)
             │
    ┌──────────────────────────────────────────┐
    │  PaymentViewModel                        │
    │  .ConfirmCardPaymentAsync()              │
    │                                          │
    │  1. OTP doğrula (6 hane, sayısal)        │
    │  2. InputSanitizer ile temizle           │
    └────────┬─────────────────────────────────┘
             │
             ▼
    ┌──────────────────────────────────────────┐
    │  TransactionService                      │
    │  .ConfirmPaymentSimulationAsync()        │
    │                                          │
    │  1. Firebase'den OTP al                  │
    │  2. Süre kontrolü (< 2 dakika)           │
    │  3. OTP eşleşme kontrolü                 │
    │  4. OTP'yi sil (tek kullanımlık)         │
    │  5. PaymentStatus = Paid                 │
    │  6. CompleteTransactionInternal()        │
    └────────┬─────────────────────────────────┘
             │
             ▼
    ┌──────────────────────────────────────────┐
    │  Transaction Tamamlama                   │
    │                                          │
    │  1. Status = Completed                   │
    │  2. Ürünü "Satıldı" işaretle             │
    │  3. Kullanıcılara puan ver               │
    │  4. Bildirimleri gönder                  │
    └────────┬─────────────────────────────────┘
             │
             ▼
    ┌──────────────────┐
    │  Başarı Mesajı   │
    │  "Ödeme Onaylandı"│
    │  → Offers Sayfası│
    └──────────────────┘
```

## 2. Havale/EFT Ödeme Akışı

```
┌─────────────────────────────────────────────────────────────────┐
│                   Havale/EFT Ödeme Akışı                        │
└─────────────────────────────────────────────────────────────────┘

    ┌──────────────────┐
    │  Kullanıcı       │
    │  "EFT/Havale"    │
    │  Seçer           │
    └────────┬─────────┘
             │
             ▼
    ┌──────────────────────────────────────────┐
    │  PaymentViewModel.StartPaymentAsync()    │
    │  - method = "banktransfersim"            │
    └────────┬─────────────────────────────────┘
             │
             ▼
    ┌──────────────────────────────────────────┐
    │  TransactionService                      │
    │  .CreatePaymentSimulationAsync()         │
    │                                          │
    │  1. Transaction'ı doğrula                │
    │  2. Referans kodu oluştur                │
    │     Format: BTX-YYYYMMDDHHmmss-XXXXXX    │
    │  3. BankName = "Ziraat Bankası"          │
    │  4. PaymentDto döndür                    │
    └────────┬─────────────────────────────────┘
             │
             ▼
    ┌──────────────────────────────────────────┐
    │  PaymentViewModel                        │
    │  - IsEftSelected = true                  │
    │  - Popup: Banka bilgilerini göster       │
    └────────┬─────────────────────────────────┘
             │
             ▼
    ┌──────────────────────────────────────────┐
    │  UI Güncellemesi                         │
    │                                          │
    │  ┌────────────────────────────────────┐ │
    │  │  🏦 Havale/EFT Ödemesi             │ │
    │  │                                    │ │
    │  │  Banka: Ziraat Bankası             │ │
    │  │  Tutar: 150.00 ₺                   │ │
    │  │  Referans: BTX-20241228120000-ABC  │ │
    │  └────────────────────────────────────┘ │
    │                                          │
    │  ⚠️ Önemli: Ödeme açıklamasına          │
    │     referans kodunu mutlaka yazınız!     │
    │                                          │
    │  [✓ Ödemeyi Tamamladım]                 │
    └────────┬─────────────────────────────────┘
             │
             ▼ (Kullanıcı bankadan ödeme yapar)
             │
             ▼ (Kullanıcı "Tamamladım" tıklar)
             │
    ┌──────────────────────────────────────────┐
    │  PaymentViewModel                        │
    │  .ConfirmCardPaymentAsync()              │
    │  (İsim "Card" ama EFT için de kullanılır)│
    └────────┬─────────────────────────────────┘
             │
             ▼
    ┌──────────────────────────────────────────┐
    │  TransactionService                      │
    │  .ConfirmPaymentSimulationAsync()        │
    │                                          │
    │  1. OTP kontrolü YOK (EFT için)          │
    │  2. PaymentStatus = Paid                 │
    │  3. CompleteTransactionInternal()        │
    └────────┬─────────────────────────────────┘
             │
             ▼
    ┌──────────────────────────────────────────┐
    │  Transaction Tamamlama                   │
    │                                          │
    │  1. Status = Completed                   │
    │  2. Ürünü "Satıldı" işaretle             │
    │  3. Kullanıcılara puan ver               │
    │  4. Bildirimleri gönder                  │
    └────────┬─────────────────────────────────┘
             │
             ▼
    ┌──────────────────┐
    │  Başarı Mesajı   │
    │  "Havale/EFT     │
    │   Kaydedildi"    │
    │  → Offers Sayfası│
    └──────────────────┘
```

## 3. Hata Durumları

```
┌─────────────────────────────────────────────────────────────────┐
│                      Hata Senaryoları                           │
└─────────────────────────────────────────────────────────────────┘

1. OTP Geçersiz
   ┌────────────────┐
   │ OTP Gir        │
   │ "123456"       │
   └────┬───────────┘
        │
        ▼ (Yanlış kod)
   ┌────────────────────────┐
   │ ConfirmPayment         │
   │ - OTP Kontrolü         │
   │ - saved.Otp != input   │
   └────┬───────────────────┘
        │
        ▼
   ┌────────────────┐
   │ ❌ Hata        │
   │ "OTP geçersiz" │
   └────────────────┘

2. OTP Süresi Dolmuş
   ┌────────────────┐
   │ OTP Oluşturuldu│
   │ ExpiresAt:     │
   │ 12:00          │
   └────┬───────────┘
        │
        ▼ (2+ dakika bekle)
   ┌────────────────────────┐
   │ ConfirmPayment         │
   │ - Süre Kontrolü        │
   │ - DateTime.Now > Exp   │
   └────┬───────────────────┘
        │
        ▼
   ┌────────────────────┐
   │ ❌ Hata            │
   │ "OTP süresi doldu" │
   └────────────────────┘

3. İnternet Bağlantısı Yok
   ┌────────────────┐
   │ StartPayment   │
   └────┬───────────┘
        │
        ▼
   ┌────────────────────────┐
   │ NetworkHelper.Check    │
   │ - HasConnection?       │
   └────┬───────────────────┘
        │
        ▼ (Bağlantı yok)
   ┌────────────────────────────┐
   │ ❌ Hata                    │
   │ "İnternet bağlantısı yok"  │
   └────────────────────────────┘

4. Ödeme Zaten Başlatılmış
   ┌────────────────┐
   │ StartPayment   │
   └────┬───────────┘
        │
        ▼
   ┌────────────────────────┐
   │ Transaction Kontrolü   │
   │ - PaymentStatus?       │
   └────┬───────────────────┘
        │
        ▼ (Status != Pending)
   ┌────────────────────────────────┐
   │ ❌ Hata                        │
   │ "Ödeme zaten başlatılmış"      │
   └────────────────────────────────┘
```

## 4. Veri Akışı (Data Flow)

```
┌─────────────────────────────────────────────────────────────────┐
│                    Component İlişkileri                         │
└─────────────────────────────────────────────────────────────────┘

    ┌─────────────────────────┐
    │   PaymentPage.xaml      │
    │   (UI Layer)            │
    │                         │
    │  - Buttons              │
    │  - Entry (OTP)          │
    │  - Labels               │
    │  - Frames               │
    └──────────┬──────────────┘
               │ Binding
               ▼
    ┌─────────────────────────┐
    │   PaymentViewModel      │
    │   (Business Logic)      │
    │                         │
    │  Properties:            │
    │  - Transaction          │
    │  - OtpCode              │
    │  - SimulatedOtp         │
    │  - IsCardSelected       │
    │  - IsEftSelected        │
    │  - PaymentDetails       │
    │                         │
    │  Commands:              │
    │  - StartPaymentCommand  │
    │  - ConfirmPayment       │
    └──────────┬──────────────┘
               │ Service Call
               ▼
    ┌─────────────────────────┐
    │ ITransactionService     │
    │ (Interface)             │
    └──────────┬──────────────┘
               │ Implementation
               ▼
    ┌─────────────────────────┐
    │ FirebaseTransaction     │
    │ Service                 │
    │                         │
    │  Methods:               │
    │  - CreatePaymentSim     │
    │  - ConfirmPaymentSim    │
    │  - CompleteInternal     │
    └──────────┬──────────────┘
               │
               ├─────────────────────┐
               │                     │
               ▼                     ▼
    ┌──────────────────┐   ┌──────────────────┐
    │  Firebase        │   │  Other Services  │
    │  Realtime DB     │   │                  │
    │                  │   │  - Notification  │
    │  - Transactions  │   │  - Product       │
    │  - TempOtps      │   │  - UserProfile   │
    └──────────────────┘   └──────────────────┘
```

## 5. State Machine (Durum Makinesi)

```
┌─────────────────────────────────────────────────────────────────┐
│              PaymentStatus State Machine                        │
└─────────────────────────────────────────────────────────────────┘

                   ┌─────────────┐
                   │  PENDING    │ (Başlangıç)
                   └──────┬──────┘
                          │
                          │ StartPayment
                          │ (CreateSimulation)
                          ▼
                   ┌─────────────┐
              ┌────│  PENDING    │────┐
              │    │ (Ödeme      │    │
              │    │  Başlatıldı)│    │
              │    └─────────────┘    │
              │                       │
              │ Confirm               │ Cancel/Timeout
              │ (OTP Valid)           │
              │                       │
              ▼                       ▼
       ┌─────────────┐         ┌─────────────┐
       │    PAID     │         │   FAILED    │
       │ (Ödeme OK)  │         │  (Hata)     │
       └──────┬──────┘         └─────────────┘
              │
              │ Complete
              │ Transaction
              ▼
       ┌─────────────┐
       │ COMPLETED   │
       │ (İşlem      │
       │  Bitti)     │
       └─────────────┘
```

## 6. Security Layers (Güvenlik Katmanları)

```
┌─────────────────────────────────────────────────────────────────┐
│                    Güvenlik Katmanları                          │
└─────────────────────────────────────────────────────────────────┘

    Kullanıcı Girişi
         │
         ▼
    ┌────────────────────────┐
    │ 1. Client-Side         │
    │    Validation          │
    │    - Format Check      │
    │    - Length Check      │
    │    - Type Check        │
    └────────┬───────────────┘
             │
             ▼
    ┌────────────────────────┐
    │ 2. Input Sanitization  │
    │    - XSS Protection    │
    │    - SQL Injection     │
    │    - Special Chars     │
    └────────┬───────────────┘
             │
             ▼
    ┌────────────────────────┐
    │ 3. Rate Limiting       │
    │    - Request Throttle  │
    │    - Spam Prevention   │
    └────────┬───────────────┘
             │
             ▼
    ┌────────────────────────┐
    │ 4. Network Check       │
    │    - Connection Valid  │
    │    - Timeout Handle    │
    └────────┬───────────────┘
             │
             ▼
    ┌────────────────────────┐
    │ 5. Business Logic      │
    │    - OTP Validation    │
    │    - Expiry Check      │
    │    - Single Use        │
    └────────┬───────────────┘
             │
             ▼
    ┌────────────────────────┐
    │ 6. Firebase Security   │
    │    - Rules             │
    │    - Authentication    │
    └────────────────────────┘
```

## 7. Timeline (Zaman Çizelgesi)

```
┌─────────────────────────────────────────────────────────────────┐
│              Ödeme İşlemi Zaman Çizelgesi                       │
└─────────────────────────────────────────────────────────────────┘

T = 0:00        Kullanıcı "Kredi Kartı" seçer
                |
T = 0:01        OTP oluşturuldu (Firebase'e kayıt)
                |
T = 0:02        OTP ekranda gösterildi
                |
T = 0:05        Kullanıcı OTP'yi giriyor...
                |
T = 0:10        "Onayla" tıklandı
                |
T = 0:11        OTP doğrulanıyor...
                |── OTP var mı?
                |── Süre dolmuş mu? (< 2 dakika)
                |── Kod doğru mu?
                |
T = 0:12        ✓ OTP başarılı
                |── OTP silindi
                |── PaymentStatus = Paid
                |
T = 0:13        Transaction tamamlanıyor...
                |── Ürün kapatılıyor
                |── Puanlar veriliyor
                |── Bildirimler gönderiliyor
                |
T = 0:15        ✓ İşlem tamamlandı
                |── Kullanıcı Offers sayfasına yönlendirildi
                |
                └──────────────────────────────────

        ⚠️ OTP Süresi: T = 2:00'de otomatik expire
```

---

## Notlar

- Bu diyagramlar ASCII art kullanılarak oluşturulmuştur
- Gerçek bir diyagram aracı (draw.io, Lucidchart) ile daha detaylı görseller oluşturulabilir
- Her akış için error handling durumları dikkate alınmıştır
- Zaman çizelgesi yaklaşık değerlerdir, gerçek süreler farklılık gösterebilir
