# Fiyat Teklifi Mekanizması - Dolap Tarzı Pazarlık Sistemi

## 🎯 Genel Bakış

KamPay uygulamasına "Dolap" tarzı bir fiyat teklifi ve pazarlık mekanizması eklenmiştir. Bu özellik sayesinde alıcılar, satılık ürünler ve hizmetler için satıcılara fiyat teklifi gönderebilir, satıcılar da bu teklifleri kabul edebilir, reddedebilir veya karşı teklif yapabilir.

## 📋 Özellikler

### 1. Fiyat Teklifi Verme
- **Ürün Detay Sayfasından**: Kullanıcılar, satılık bir ürün detayında "Fiyat Teklif Et" butonuna tıklayarak teklif verebilir
- **Özelleştirilebilir Teklif**: Kullanıcı istediği fiyatı girebilir ve isteğe bağlı bir mesaj ekleyebilir
- **Akıllı Doğrulama**: Sistem, teklifin mantıklı olup olmadığını kontrol eder (örn: orijinal fiyattan yüksekse uyarı verir)

### 2. Teklif Yönetimi
- **Alınan Teklifler**: Satıcılar, ürünleri için gelen tüm teklifleri görüntüleyebilir
- **Gönderilen Teklifler**: Alıcılar, gönderdikleri tüm teklifleri takip edebilir
- **Durum Takibi**: Her teklifin durumu görsel olarak gösterilir:
  - 🟠 **Beklemede**: Satıcının cevabı bekleniyor
  - 🔵 **Karşı Teklif**: Satıcı karşı teklif yaptı
  - 🟢 **Kabul Edildi**: Teklif kabul edildi
  - 🔴 **Reddedildi**: Teklif reddedildi
  - ⚫ **İptal Edildi**: Teklif iptal edildi
  - ⚪ **Süresi Doldu**: Teklif süresi doldu (7 gün)

### 3. Pazarlık Süreci
- **Karşı Teklif**: Satıcılar, gelen tekliflere karşı teklif yapabilir (maksimum 3 kere)
- **Karşı Teklif Kabul/Red**: Alıcılar, gelen karşı teklifleri kabul veya reddedebilir
- **Mesajlaşma**: Her teklif ve karşı teklif ile birlikte mesaj gönderilebilir
- **Son Teklif İşareti**: Hem alıcı hem satıcı "son teklif" yapabilir

### 4. Otomatik İşlemler
- **Bildirimler**: Teklif geldiğinde, kabul/red durumunda ve karşı teklif yapıldığında kullanıcılar bildirim alır
- **Ürün Rezervasyonu**: Teklif kabul edildiğinde ürün otomatik olarak rezerve edilir
- **Geçerlilik Süresi**: Teklifler 7 gün geçerlidir, süre sonunda otomatik olarak expire olur

## 🏗️ Teknik Mimari

### Model Katmanı

#### PriceQuote Model
```csharp
public class PriceQuote
{
    public string QuoteId { get; set; }
    public PriceQuoteType QuoteType { get; set; } // Product / Service
    public string ReferenceId { get; set; } // ProductId veya ServiceId
    public string SellerId { get; set; }
    public string BuyerId { get; set; }
    public decimal OriginalPrice { get; set; }
    public decimal QuotedPrice { get; set; }
    public decimal? CounterOfferPrice { get; set; }
    public PriceQuoteStatus Status { get; set; }
    public string Message { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    // ... diğer özellikler
}
```

#### Enum'lar
```csharp
public enum PriceQuoteStatus
{
    Pending,        // Beklemede
    CounterOffered, // Karşı teklif yapıldı
    Accepted,       // Kabul edildi
    Rejected,       // Reddedildi
    Expired,        // Süresi doldu
    Cancelled       // İptal edildi
}

public enum PriceQuoteType
{
    Product,  // Ürün için teklif
    Service   // Hizmet için teklif
}
```

### Servis Katmanı

#### IPriceQuoteService Interface
```csharp
public interface IPriceQuoteService
{
    Task<ValidationResult> CreateQuoteAsync(string userId, CreateQuoteRequest request);
    Task<ValidationResult> AcceptQuoteAsync(string userId, string quoteId);
    Task<ValidationResult> RejectQuoteAsync(string userId, string quoteId, string reason);
    Task<ValidationResult> MakeCounterOfferAsync(string userId, CounterOfferRequest request);
    Task<ValidationResult> AcceptCounterOfferAsync(string userId, string quoteId);
    Task<ValidationResult> RejectCounterOfferAsync(string userId, string quoteId);
    Task<ValidationResult> CancelQuoteAsync(string userId, string quoteId);
    Task<PriceQuote> GetQuoteByIdAsync(string quoteId);
    Task<List<PriceQuote>> GetReceivedQuotesAsync(string sellerId, PriceQuoteFilter filter = null);
    Task<List<PriceQuote>> GetSentQuotesAsync(string buyerId, PriceQuoteFilter filter = null);
    Task<int> GetUnreadQuoteCountAsync(string userId);
    // ... diğer metodlar
}
```

#### FirebasePriceQuoteService Implementation
Firebase Realtime Database kullanılarak implementasyonu yapılmıştır:
- **Firebase Path**: `price_quotes/{quoteId}`
- **Indexing**: SellerId, BuyerId, ReferenceId üzerinde sorgulamalar
- **Bildirim Entegrasyonu**: INotificationService ile entegre
- **Ürün/Hizmet Servisleri**: IProductService ve IServiceSharingService ile entegre

### ViewModel Katmanı

#### PriceQuotesViewModel
```csharp
public partial class PriceQuotesViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<PriceQuote> receivedQuotes;
    
    [ObservableProperty]
    private ObservableCollection<PriceQuote> sentQuotes;
    
    [RelayCommand]
    private async Task AcceptQuoteAsync(PriceQuote quote);
    
    [RelayCommand]
    private async Task MakeCounterOfferAsync(PriceQuote quote);
    
    [RelayCommand]
    private async Task RejectQuoteAsync(PriceQuote quote);
    
    // ... diğer komutlar
}
```

#### ProductDetailViewModel Güncellemesi
```csharp
[RelayCommand]
private async Task MakeOfferAsync()
{
    // Fiyat teklifi UI'ı
    var priceStr = await DisplayPromptAsync(...);
    var message = await DisplayPromptAsync(...);
    
    var request = new CreateQuoteRequest {
        QuoteType = PriceQuoteType.Product,
        ReferenceId = Product.ProductId,
        QuotedPrice = offerPrice,
        Message = message
    };
    
    await _priceQuoteService.CreateQuoteAsync(userId, request);
}
```

### UI Katmanı

#### PriceQuotesPage.xaml
- **Sekmeli Görünüm**: Alınan ve Gönderilen teklifler ayrı sekmelerde
- **Liste Görünümü**: Her teklif için kart tasarımı
  - Ürün görseli ve bilgisi
  - Fiyat karşılaştırması (orijinal, teklif, karşı teklif)
  - Durum badge'i
  - Aksiyon butonları (Kabul, Red, Karşı Teklif)
- **Refresh Support**: Pull-to-refresh özelliği
- **Empty State**: Teklif yoksa kullanıcı dostu mesaj

#### Ürün Detay Sayfasına Ekleme
ProductDetailPage'e "💰 Fiyat Teklif Et" butonu eklenmelidir (UI güncellemesi gerekli).

## 📊 Firebase Database Yapısı

```json
{
  "price_quotes": {
    "quote_id_1": {
      "QuoteId": "quote_id_1",
      "QuoteType": 0,
      "ReferenceId": "product_id_123",
      "ReferenceTitle": "iPhone 13",
      "ReferenceThumbnailUrl": "https://...",
      "SellerId": "seller_user_id",
      "SellerName": "Ahmet Yılmaz",
      "BuyerId": "buyer_user_id",
      "BuyerName": "Ayşe Demir",
      "OriginalPrice": 15000,
      "QuotedPrice": 13000,
      "CounterOfferPrice": 14000,
      "Currency": "TRY",
      "Status": 1,
      "Message": "Merhaba, bu fiyata alabilir miyim?",
      "CounterOfferMessage": "14000 TL yapabilirim",
      "CreatedAt": "2025-12-02T16:00:00Z",
      "ExpiresAt": "2025-12-09T16:00:00Z",
      "IsRead": true,
      "IsFinal": false,
      "CounterOfferCount": 1
    }
  }
}
```

## 🔄 İş Akışı Diyagramları

### Alıcı İş Akışı
```
1. Alıcı → Ürün Detay → "Fiyat Teklif Et"
2. Alıcı → Fiyat Girer → Mesaj Ekler (opsiyonel)
3. Sistem → Teklifi Oluşturur
4. Sistem → Satıcıya Bildirim Gönderir
5. Alıcı → "Gönderilen Teklifler" sekmesinden takip eder
```

### Satıcı İş Akışı
```
1. Satıcı → Bildirim Alır / "Alınan Teklifler" sekmesini açar
2. Satıcı → Teklifi İnceler
3. Satıcı → Seçenek:
   a) Kabul Et → Ürün rezerve olur → Alıcıya bildirim
   b) Reddet → Red nedeni girebilir → Alıcıya bildirim
   c) Karşı Teklif → Yeni fiyat ve mesaj gönderir → Alıcıya bildirim
```

### Karşı Teklif İş Akışı
```
1. Satıcı → Karşı Teklif Yapar (max 3 kere)
2. Alıcı → Bildirim Alır
3. Alıcı → "Gönderilen Teklifler" → Karşı Teklifi Görür
4. Alıcı → Seçenek:
   a) Kabul Et → İşlem tamamlanır
   b) Reddet → Teklif reddedilir
```

## 🔐 Güvenlik Kontrolleri

1. **Kullanıcı Doğrulama**: Her işlemde userId kontrolü
2. **Sahiplik Kontrolü**: Kullanıcı kendi ürününe teklif veremez
3. **Durum Kontrolü**: Sadece uygun durumlarda işlem yapılabilir
4. **Fiyat Validasyonu**: Negatif veya sıfır fiyat kabul edilmez
5. **Ürün Durumu**: Satılan veya rezerve ürünlere teklif verilemez
6. **Limit Kontrolü**: Maksimum 3 karşı teklif yapılabilir

## 🎨 Kullanıcı Deneyimi

### Başarılı Durum Mesajları
- ✅ "Teklifiniz ({price} ₺) satıcıya gönderildi!"
- ✅ "Teklif kabul edildi! 🎉"
- ✅ "Karşı teklif gönderildi! 💬"

### Hata Mesajları
- ❌ "Kendi ürününüze teklif veremezsiniz"
- ❌ "Bu ürün artık müsait değil"
- ❌ "Maksimum karşı teklif sayısına ulaşıldı"

### Bildirimler
- 💰 "Yeni Fiyat Teklifi! {user} {product} için {price} ₺ teklif etti"
- 🎉 "Teklifiniz Kabul Edildi! {product} için {price} ₺ teklifiniz kabul edildi"
- 💬 "Karşı Teklif Aldınız! {user}, {product} için {price} ₺ karşı teklif yaptı"

## 🚀 Kullanım Senaryoları

### Senaryo 1: Basit Teklif ve Kabul
```
1. Ayşe, Ahmet'in 5000 ₺'lik bisikletini görür
2. Ayşe 4500 ₺ teklif eder, "Öğrenciyim, biraz indirim yapabilir misiniz?" mesajı ekler
3. Ahmet bildirimi görür, teklifi kabul eder
4. Bisiklet Ayşe için rezerve edilir
5. Ayşe ödeme yapabilir
```

### Senaryo 2: Karşı Teklif ve Anlaşma
```
1. Mehmet, Zeynep'in 3000 ₺'lik telefonunu görür
2. Mehmet 2500 ₺ teklif eder
3. Zeynep 2800 ₺ karşı teklif yapar
4. Mehmet 2800 ₺'yi kabul eder
5. Telefon Mehmet için rezerve edilir
```

### Senaryo 3: Pazarlık ve Red
```
1. Can, 10000 ₺'lik laptopa 7000 ₺ teklif eder
2. Satıcı 9500 ₺ karşı teklif yapar
3. Can 8000 ₺ tekrar teklif yapmak ister (yeni teklif olarak)
4. Satıcı ilk teklifi reddeder
5. Can yeni bir teklif oluşturabilir
```

## 📱 Navigasyon

### Teklif Sayfasına Erişim
```csharp
// AppShell veya herhangi bir sayfadan:
await Shell.Current.GoToAsync(nameof(PriceQuotesPage));
```

### Ürün Detayından Teklif Verme
```csharp
// ProductDetailPage'de "Fiyat Teklif Et" butonu ile:
await MakeOfferCommand.ExecuteAsync(null);
```

## 🔄 Gelecek Geliştirmeler

### Öncelik 1: UI İyileştirmeleri
- [ ] ProductDetailPage'e "Fiyat Teklif Et" butonu ekleme
- [ ] Profil sayfasına "Tekliflerim" bölümü ekleme
- [ ] Bildirim badge'lerinde teklif sayısı gösterme

### Öncelik 2: Hizmetler İçin Teklif
- [ ] ServiceSharingPage'e teklif verme özelliği ekleme
- [ ] ServiceRequestViewModel'e teklif entegrasyonu

### Öncelik 3: Gelişmiş Özellikler
- [ ] Teklif geçmişi ve istatistikler
- [ ] Otomatik teklif kabul/red kuralları
- [ ] Toplu teklif yönetimi
- [ ] Teklif süre uzatma
- [ ] Favorilere eklenen ürünler için otomatik teklif önerileri

### Öncelik 4: Analitik
- [ ] Kullanıcı pazarlık başarı oranı
- [ ] Ortalama teklif-kabul süresi
- [ ] Popüler teklif fiyat aralıkları
- [ ] Karşı teklif etkinlik analizi

## 🧪 Test Senaryoları

### Manuel Test Checklist
- [ ] Ürün detayından teklif verme
- [ ] Kendi ürününe teklif vermeyi engelleme
- [ ] Satılan ürüne teklif vermeyi engelleme
- [ ] Alınan teklifleri görüntüleme
- [ ] Gönderilen teklifleri görüntüleme
- [ ] Teklif kabul etme
- [ ] Teklif reddetme
- [ ] Karşı teklif yapma
- [ ] Karşı teklifi kabul etme
- [ ] Karşı teklifi reddetme
- [ ] Teklif iptal etme
- [ ] Bildirim alma
- [ ] Teklif süresi dolunca durum değişimi
- [ ] Ürün rezervasyonu

## 💡 İpuçları ve En İyi Pratikler

### Satıcılar İçin
- Karşı teklif yaparken açıklayıcı mesaj ekleyin
- Makul teklifleri değerlendirin, hemen reddetmeyin
- İlk teklifi her zaman kabul etmek zorunda değilsiniz

### Alıcılar İçin
- Gerçekçi teklifler yapın (örn: %20-30 indirim)
- Teklifinizle birlikte kibar bir mesaj ekleyin
- Karşı teklifleri değerlendirin, anlaşma sağlamaya çalışın

### Geliştiriciler İçin
- Firebase query'lerinde index kullanımına dikkat edin
- Bildirim gönderiminde hata yönetimi yapın
- UI'da loading state'leri göstermeyi unutmayın
- Offline durumları ele alın

## 📝 Değişiklik Geçmişi

### v1.0.0 (2025-12-02)
- ✨ İlk versiyon: Temel fiyat teklifi mekanizması
- ✨ Karşı teklif özelliği
- ✨ Bildirim entegrasyonu
- ✨ Teklif yönetim sayfası
- ✨ ProductDetailViewModel entegrasyonu

## 🤝 Katkıda Bulunma

Bu özellik, KamPay projesinin genişletilebilir mimarisi sayesinde eklendi. Gelecek geliştirmeler için:
1. Yeni özellik önerileri GitHub Issues'a eklenebilir
2. UI/UX iyileştirmeleri yapılabilir
3. Test coverage artırılabilir
4. Dokümantasyon güncellenebilir

---

**Not**: Bu özellik Firebase Realtime Database kullanmaktadır. Production'a geçmeden önce Firebase Security Rules'un uygun şekilde yapılandırılması gerekmektedir.
