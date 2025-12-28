# Ödeme Simülasyonu Akış Dokümantasyonu

## Genel Bakış

KamPay uygulaması, ürün satışları için iki farklı ödeme yöntemi simülasyonu sunar:
1. **Kredi Kartı Ödemesi** (OTP doğrulama ile)
2. **Havale/EFT Ödemesi** (Referans kodu ile)

Bu dokümantasyon, ödeme akışının nasıl çalıştığını detaylı olarak açıklar.

---

## 1. Ödeme Yöntemleri

### 1.1 Kredi Kartı Ödemesi (CardSim)

**Akış:**
1. Kullanıcı "Kredi Kartı" butonuna tıklar
2. Sistem 6 haneli rastgele OTP kodu oluşturur (100000-999999 arası)
3. OTP, Firebase'de `TempOtps` koleksiyonuna 2 dakika geçerlilik süresiyle kaydedilir
4. Kullanıcıya hem popup'ta hem de ekranda OTP gösterilir (simülasyon amaçlı)
5. Kullanıcı OTP'yi giriş alanına yazar
6. Sistem OTP'yi doğrular:
   - OTP mevcut mu?
   - OTP süresi dolmuş mu?
   - Girilen OTP doğru mu?
7. Doğrulama başarılıysa:
   - OTP Firebase'den silinir (tek kullanımlık)
   - Transaction durumu `Paid` olarak işaretlenir
   - Ürün `Sold` olarak işaretlenir
   - Kullanıcılara puan verilir
   - Bildirimler gönderilir

**Güvenlik Önlemleri:**
- OTP sadece 2 dakika geçerlidir
- OTP tek kullanımlıktır (kullanıldıktan sonra silinir)
- OTP input sanitization ile temizlenir (XSS/Injection koruması)
- OTP sadece 6 haneli sayı kabul edilir

### 1.2 Havale/EFT Ödemesi (BankTransferSim)

**Akış:**
1. Kullanıcı "EFT / Havale" butonuna tıklar
2. Sistem benzersiz referans kodu oluşturur (Format: `BTX-YYYYMMDDHHmmss-XXXXXX`)
3. Kullanıcıya banka bilgileri gösterilir:
   - Banka Adı: Ziraat Bankası
   - Tutar: [İşlem Tutarı]
   - Referans Kodu: [Oluşturulan Kod]
4. Kullanıcı, bankasından ödemeyi yapar ve açıklama kısmına referans kodunu yazar
5. Kullanıcı "Ödemeyi Tamamladım" butonuna tıklar
6. Sistem:
   - Transaction durumu `Paid` olarak işaretlenir
   - Ürün `Sold` olarak işaretlenir
   - Kullanıcılara puan verilir
   - Bildirimler gönderilir

**Not:** Havale/EFT simülasyonunda gerçek banka doğrulaması yapılmaz. Kullanıcının beyanına göre işlem tamamlanır.

---

## 2. Teknik Mimari

### 2.1 Dosya Yapısı

```
KamPay/
├── Views/
│   └── PaymentPage.xaml              # UI katmanı
│       └── PaymentPage.xaml.cs
├── ViewModels/
│   └── PaymentViewModel.cs           # İş mantığı ve UI binding
├── Services/
│   └── FirebaseTransactionService.cs # Ödeme ve transaction yönetimi
├── Models/
│   ├── PaymentModels.cs              # Payment DTO ve enum'lar
│   └── Transaction.cs                # Transaction model
└── Helpers/
    ├── InputSanitizer.cs             # Güvenlik (input temizleme)
    ├── NetworkHelper.cs              # Ağ yönetimi
    └── RateLimiter.cs                # Spam koruması
```

### 2.2 Veri Modelleri

#### PaymentDto
```csharp
public class PaymentDto
{
    public string PaymentId { get; set; }           // Benzersiz ödeme ID
    public decimal Amount { get; set; }             // Ödenecek tutar
    public string Currency { get; set; } = "TRY";   // Para birimi
    public ServicePaymentStatus Status { get; set; } // Ödeme durumu
    public PaymentMethodType Method { get; set; }    // Ödeme yöntemi
    public string? BankName { get; set; }           // Havale için banka adı
    public string? BankReference { get; set; }      // Havale için referans
    public DateTime CreatedAt { get; set; }         // Oluşturma zamanı
}
```

#### PaymentMethodType
```csharp
public enum PaymentMethodType
{
    None = 0,              // Seçim yapılmadı
    CardSim = 1,           // Kredi kartı
    BankTransferSim = 2,   // Havale/EFT
    WalletSim = 3          // Cüzdan (gelecek)
}
```

#### ServicePaymentStatus
```csharp
public enum ServicePaymentStatus
{
    None = 0,      // Ödeme yok
    Initiated = 1, // Ödeme başlatıldı
    Paid = 2,      // Ödeme tamamlandı
    Failed = 3     // Ödeme başarısız
}
```

### 2.3 Servis Metodları

#### CreatePaymentSimulationAsync
Ödeme simülasyonunu başlatır.

**Parametreler:**
- `transactionId`: İşlem ID'si
- `method`: "cardsim" veya "banktransfersim"

**Dönen Değer:**
- `ServiceResult<PaymentDto>`: Ödeme bilgilerini içeren sonuç

**İşleyiş:**
1. Transaction'ı doğrula
2. Ödeme tutarını belirle (QuotedPrice veya Price)
3. PaymentDto oluştur
4. Kart ise OTP üret ve kaydet
5. Havale ise referans kodu oluştur
6. Transaction'ı güncelle

#### ConfirmPaymentSimulationAsync
Ödemeyi onaylar ve işlemi tamamlar.

**Parametreler:**
- `transactionId`: İşlem ID'si
- `paymentId`: Ödeme ID'si
- `otp`: OTP kodu (kart ödemeleri için)

**Dönen Değer:**
- `ServiceResult<bool>`: Başarı durumu

**İşleyiş:**
1. Transaction'ı al
2. Kart ise OTP'yi doğrula
3. Ödeme durumunu güncelle
4. Satış ise CompleteTransactionInternalAsync çağır
5. Bildirimleri gönder

---

## 3. UI/UX Akışı

### 3.1 Kredi Kartı Akışı

```
[Ürün Detayları]
       ↓
[Kredi Kartı Seç]
       ↓
[OTP Oluşturuldu - Popup Göster]
       ↓
[OTP Girişi - Ekranda OTP Gösterilir]
       ↓
[Ödemeyi Onayla]
       ↓
[OTP Doğrulama]
       ↓
[Başarılı] → [Teklif Sayfasına Dön]
```

### 3.2 Havale/EFT Akışı

```
[Ürün Detayları]
       ↓
[EFT/Havale Seç]
       ↓
[Banka Bilgileri Göster - Popup]
       ↓
[Referans Kodu Göster - Ekranda]
       ↓
[Kullanıcı Bankadan Ödeme Yapar]
       ↓
[Ödemeyi Tamamladım]
       ↓
[Başarılı] → [Teklif Sayfasına Dön]
```

---

## 4. Firebase Veri Yapısı

### 4.1 TempOtps Koleksiyonu
```json
{
  "TempOtps": {
    "payment-id-1": {
      "Otp": "123456",
      "ExpiresAt": "2024-12-28T12:00:00Z"
    }
  }
}
```

### 4.2 Transactions Koleksiyonu (Payment İlgili Alanlar)
```json
{
  "Transactions": {
    "transaction-id-1": {
      "PaymentStatus": "Paid",
      "PaymentMethod": 1,
      "PaymentSimulationId": "payment-id-1",
      "PaymentCompletedAt": "2024-12-28T12:00:00Z",
      ...
    }
  }
}
```

---

## 5. Hata Yönetimi

### 5.1 Yaygın Hatalar ve Çözümleri

| Hata | Sebep | Çözüm |
|------|-------|-------|
| "İşlem bulunamadı" | Transaction ID geçersiz | Transaction'ın var olduğunu doğrula |
| "OTP bulunamadı" | OTP süresi dolmuş veya silinmiş | Yeni ödeme başlat |
| "OTP süresi doldu" | 2 dakikadan uzun süre geçti | Yeni ödeme başlat |
| "OTP geçersiz" | Yanlış kod girildi | Doğru kodu gir veya yeni ödeme başlat |
| "Bu işlem için ödeme zaten başlatılmış" | Duplicate payment | Mevcut ödemeyi tamamla |

### 5.2 Güvenlik Kontrolleri

1. **Input Sanitization**: Tüm kullanıcı girdileri `InputSanitizer.SanitizeText()` ile temizlenir
2. **Rate Limiting**: Spam koruması için `RateLimiters.ApiCall` kullanılır
3. **Network Check**: Her işlemde internet bağlantısı kontrol edilir
4. **OTP Expiration**: OTP'ler 2 dakika sonra otomatik silinir
5. **Single Use OTP**: OTP kullanıldıktan sonra hemen silinir

---

## 6. Geliştirici Notları

### 6.1 Simülasyon Modundan Gerçek Ödemeye Geçiş

Gerçek ödeme gateway entegrasyonu için:

1. **PaymentMethodType'a gerçek yöntemler ekle:**
   ```csharp
   Card = 1,           // Gerçek kredi kartı
   BankTransfer = 2,   // Gerçek havale/EFT
   ```

2. **Ödeme gateway SDK'sını entegre et:**
   - Iyzico, PayTR, veya başka bir ödeme sağlayıcısı
   - `CreatePaymentSimulationAsync` yerine `CreatePaymentAsync` kullan

3. **Webhook endpoint ekle:**
   - Ödeme sağlayıcısından gelen bildirimleri al
   - `ConfirmPaymentSimulationAsync` metodunu güncelle

4. **3D Secure entegrasyonu:**
   - Kart ödemeleri için 3D Secure akışını ekle
   - Redirect URL'lerini yapılandır

### 6.2 Test Senaryoları

#### Kredi Kartı Testi
```
1. Ürün seç ve "Kredi Kartı" seç
2. OTP'yi kopyala (ekranda görünen)
3. OTP'yi gir ve onayla
4. Başarı mesajını kontrol et
5. Transaction durumunu kontrol et (PaymentStatus = Paid)
```

#### Havale/EFT Testi
```
1. Ürün seç ve "EFT/Havale" seç
2. Referans kodunu not al
3. "Ödemeyi Tamamladım" butonuna tıkla
4. Başarı mesajını kontrol et
5. Transaction durumunu kontrol et (PaymentStatus = Paid)
```

#### Hata Testi
```
1. Yanlış OTP gir → "OTP geçersiz" hatası beklenir
2. 2 dakika bekle ve OTP gir → "OTP süresi doldu" hatası beklenir
3. Aynı transaction için tekrar ödeme başlat → "Ödeme zaten başlatılmış" hatası beklenir
```

---

## 7. Performans ve Optimizasyon

### 7.1 Firebase Optimizasyonları
- OTP'ler TTL (Time To Live) ile otomatik silinir
- Transaction güncellemeleri batch işlemlerle yapılabilir
- Ödeme geçmişi için indexing kullanılmalı

### 7.2 UI Performansı
- Loading indicator'lar kullanıcı deneyimini iyileştirir
- Network hatalarında retry mekanizması eklenebilir
- Ödeme başarılı olduğunda navigation stack temizlenir

---

## 8. Sık Sorulan Sorular (FAQ)

**S: Neden simülasyon kullanıyoruz?**
A: Gerçek ödeme gateway entegrasyonu için banka anlaşması ve ücretler gerekir. Simülasyon, uygulamanın test ve geliştirme aşamasında kullanılır.

**S: OTP neden 2 dakika geçerli?**
A: Güvenlik için OTP'ler kısa ömürlü olmalıdır. 2 dakika, kullanıcının işlemi tamamlaması için yeterli süre sağlar.

**S: Havale/EFT'de gerçek doğrulama yapılmıyor mu?**
A: Hayır, simülasyon modunda kullanıcının beyanına güvenilir. Gerçek uygulamada banka API'si ile doğrulama yapılmalıdır.

**S: Transaction tamamlandıktan sonra iptal edilebilir mi?**
A: Şu anda hayır. Gelecekte "İade/İptal" özelliği eklenebilir.

---

## 9. Değişiklik Geçmişi

| Tarih | Versiyon | Değişiklik |
|-------|----------|------------|
| 2024-12-28 | 1.0 | İlk versiyon - Kredi Kartı ve Havale/EFT simülasyonu |

---

## 10. İletişim ve Destek

Bu dokümantasyon hakkında sorularınız için lütfen proje sahibiyle iletişime geçin.
