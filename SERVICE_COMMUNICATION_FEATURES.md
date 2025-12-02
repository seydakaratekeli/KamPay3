# Hizmet Modülü: İletişim ve Pazarlık Özellikleri

## 📋 Genel Bakış

KamPay hizmet modülüne, kullanıcılar arasında doğrudan iletişim ve fiyat pazarlığı özellikleri eklenmiştir. Bu özellikler sayesinde hizmet alıcıları ve satıcıları, işlem öncesi ve sırasında sorunsuz bir şekilde iletişim kurabilir ve fiyat üzerinde anlaşabilir.

## ✨ Yeni Özellikler

### 1. 💬 Doğrudan Mesajlaşma

#### Hizmet İlanından Mesajlaşma
- **Kullanıcılar artık bir hizmet ilanını görmeden önce satıcıyla iletişime geçebilir**
- Her hizmet kartında 💬 mesaj butonu bulunur
- Butona tıklandığında, satıcıyla otomatik olarak bir konuşma başlatılır
- Kullanıcı doğrudan mesajlaşma sayfasına yönlendirilir

#### Talep Sonrası İletişim
- **Hizmet talebi oluşturduktan sonra alıcı ve satıcı mesajlaşabilir**
- "Gelen Talepler" ve "Giden Talepler" sayfalarında her talep kartında 💬 butonu
- Talep üzerinden konuşma başlatıldığında:
  - Mevcut konuşma varsa ona yönlendirilir
  - Yoksa yeni konuşma oluşturulur ve sistem mesajı eklenir
  - Sistem mesajı: "'{Hizmet Adı}' hizmeti için konuşma başlatıldı. Fiyat: {Fiyat} ₺"

### 2. 💰 Fiyat Pazarlığı

#### Talep Eden Tarafından Fiyat Teklifi
**Özellik:** Hizmet talep eden kişi, satıcının belirlediği fiyat yerine kendi teklifini sunabilir.

**Nasıl Çalışır:**
1. Kullanıcı "Giden Talepler" sayfasındaki "💰 Teklif" butonuna tıklar
2. Açılan dialog'da:
   - Mevcut fiyat gösterilir
   - Kullanıcı kendi teklif ettiği fiyatı girer
3. Teklif gönderildiğinde:
   - ServiceRequest'in `ProposedPriceByRequester` alanı güncellenir
   - `IsNegotiating` durumu `true` olur
   - Satıcıya bildirim gönderilir: "Yeni Fiyat Teklifi"
   - Eğer konuşma varsa, mesaj olarak da gönderilir: "💰 Fiyat Teklifi: {Tutar} ₺"

**Durum:**
- Sadece "Pending" (Beklemede) durumundaki taleplerde kullanılabilir
- Sadece talep eden kişi kullanabilir

#### Satıcı Tarafından Karşı Teklif
**Özellik:** Hizmet sağlayıcı, talep edenin teklifine karşılık farklı bir fiyat teklif edebilir.

**Nasıl Çalışır:**
1. Satıcı "Gelen Talepler" sayfasındaki "💰 Teklif" butonuna tıklar
2. Açılan dialog'da:
   - Talep edenin teklifi gösterilir (varsa)
   - Orijinal fiyat gösterilir
   - Satıcı karşı teklif girer
3. Karşı teklif gönderildiğinde:
   - ServiceRequest'in `CounterOfferByProvider` alanı güncellenir
   - `IsNegotiating` durumu `true` olur
   - Talep edene bildirim gönderilir: "Karşı Teklif Alındı"
   - Eğer konuşma varsa, mesaj olarak da gönderilir: "💰 Karşı Teklif: {Tutar} ₺"

**Durum:**
- Sadece "Pending" (Beklemede) durumundaki taleplerde kullanılabilir
- Sadece hizmet sağlayıcı kullanabilir

#### Anlaşma ve Fiyat Kabulü
**Özellik:** Her iki taraf da pazarlık sonucu ortaya çıkan fiyatı kabul edebilir.

**Nasıl Çalışır:**
1. Pazarlık devam ederken (IsNegotiating = true) her iki tarafta da "✓ Kabul" butonu görünür
2. Kullanıcı butona tıkladığında:
   - En son teklif edilen fiyat belirlenir:
     - Öncelik karşı teklifte (`CounterOfferByProvider`)
     - Yoksa talep edenin teklifinde (`ProposedPriceByRequester`)
   - Onay dialogu gösterilir: "Fiyat: {Anlaşılan Tutar} ₺"
3. Onaylandığında:
   - `QuotedPrice` ve `Price` alanları güncellenir
   - `IsNegotiating` durumu `false` olur
   - `NegotiationNotes` kaydedilir
   - Diğer tarafa bildirim gönderilir: "Fiyat Anlaşması"
   - Konuşmaya sistem mesajı eklenir: "✅ Fiyat anlaşıldı: {Tutar} ₺"

### 3. 📊 Pazarlık Durumu Göstergesi

**UI Özellikleri:**
- Pazarlık devam ederken talep kartlarında özel bir bölüm görünür:
  - "💰 Pazarlık Devam Ediyor" başlığı
  - Talep edenin teklifi (varsa)
  - Satıcının karşı teklifi (varsa)
- Her iki taraf da mevcut teklifleri görebilir
- Anlaşılan fiyat, "QuotedPrice" alanında vurgulanır

## 🔧 Teknik Detaylar

### Model Değişiklikleri (ServiceRequest)

```csharp
public class ServiceRequest
{
    // ... Mevcut özellikler ...

    // 🔥 YENİ: Mesajlaşma ve Pazarlık Özellikleri
    public string ConversationId { get; set; }
    public bool HasActiveConversation { get; set; } = false;
    
    public decimal? ProposedPriceByRequester { get; set; }
    public decimal? CounterOfferByProvider { get; set; }
    public bool IsNegotiating { get; set; } = false;
    public DateTime? LastNegotiationDate { get; set; }
    public string NegotiationNotes { get; set; }
}
```

### Yeni Servis Metodları

#### IServiceSharingService
```csharp
Task<ServiceResult<string>> StartConversationForRequestAsync(string requestId, string currentUserId);
Task<ServiceResult<bool>> ProposePrice(string requestId, decimal proposedPrice, string currentUserId);
Task<ServiceResult<bool>> SendCounterOfferAsync(string requestId, decimal counterOffer, string currentUserId);
Task<ServiceResult<bool>> AcceptNegotiatedPriceAsync(string requestId, string currentUserId);
```

### ViewModel Komutları

#### ServiceSharingViewModel
- `MessageProviderCommand`: Hizmet satıcısına mesaj gönderme

#### ServiceRequestsViewModel
- `StartConversationCommand`: Talep için konuşma başlatma
- `ProposePriceCommand`: Fiyat teklifi gönderme (alıcı)
- `SendCounterOfferCommand`: Karşı teklif gönderme (satıcı)
- `AcceptNegotiatedPriceCommand`: Anlaşılan fiyatı kabul etme

## 📱 Kullanıcı Arayüzü

### Hizmet Listesi Sayfası (ServiceSharingPage)
- Her hizmet kartında:
  - 💬 Mesaj butonu (sağ tarafta, "Talep Et" butonunun yanında)
  - "Talep Et" butonu (mevcut)

### Talep Yönetimi Sayfası (ServiceRequestsPage)

#### Gelen Talepler
- 💬 Mesaj butonu
- 💰 Teklif butonu (Pending durumunda, karşı teklif için)
- ✓ Kabul butonu (Pazarlık devam ederken)
- Reddet ve Kabul Et butonları (Pending durumunda)

#### Giden Talepler
- 💬 Mesaj butonu
- 💰 Teklif butonu (Pending durumunda, fiyat teklifi için)
- ✓ Kabul butonu (Pazarlık devam ederken)
- Tamamla butonu (Accepted durumunda)

### Pazarlık Bilgi Paneli
Pazarlık devam ederken gösterilen özel bölüm:
```
💰 Pazarlık Devam Ediyor
Sizin Teklifiniz: 150 ₺
Karşı Teklif: 175 ₺
```

## 🔔 Bildirimler

Sistem aşağıdaki durumlarda bildirim gönderir:

1. **Yeni Fiyat Teklifi**: Talep eden kişi fiyat teklif ettiğinde
   - Kime: Hizmet sağlayıcı
   - Mesaj: "{Kullanıcı Adı}, '{Hizmet Adı}' hizmeti için {Tutar} ₺ teklif etti."

2. **Karşı Teklif Alındı**: Satıcı karşı teklif gönderdiğinde
   - Kime: Talep eden
   - Mesaj: "'{Hizmet Adı}' hizmeti için karşı teklif: {Tutar} ₺"

3. **Fiyat Anlaşması**: Taraflardan biri fiyatı kabul ettiğinde
   - Kime: Diğer taraf
   - Mesaj: "'{Hizmet Adı}' hizmeti için {Tutar} ₺ fiyat üzerinde anlaşıldı."

## 🎯 Kullanım Senaryoları

### Senaryo 1: Basit İletişim (Pazarlıksız)
1. Kullanıcı A bir hizmet ilanı görür
2. Fiyat uygun, ancak detay sormak istiyor
3. 💬 butonuna tıklar ve satıcıyla mesajlaşır
4. Anlaştıktan sonra "Talep Et" butonuna basar
5. Satıcı talebi kabul eder
6. İşlem tamamlanır

### Senaryo 2: Fiyat Pazarlığı ile Hizmet Alma
1. Kullanıcı B bir hizmet için 200 ₺ fiyat görür
2. "Talep Et" butonuna basar
3. Talep oluşturulunca "💰 Teklif" butonuna basıp 150 ₺ teklif eder
4. Satıcı bildirimi alır ve "Gelen Talepler" sayfasında teklifi görür
5. Satıcı 💬 butonu ile mesajlaşarak durumu konuşur
6. Satıcı "💰 Teklif" ile 175 ₺ karşı teklif gönderir
7. Kullanıcı B karşı teklifi kabul etmek için "✓ Kabul" butonuna basar
8. Fiyat 175 ₺ olarak kilitlenir
9. Satıcı talebi kabul eder
10. İşlem 175 ₺ üzerinden tamamlanır

### Senaryo 3: Pazarlık Sonrası İptal
1. Kullanıcı C 100 ₺ teklif eder
2. Satıcı 150 ₺ karşı teklif verir
3. Kullanıcı C kabul etmez
4. Talep "Pending" durumunda kalır
5. Satıcı isterse talebi reddedebilir veya yeni teklif gönderebilir

## 🔒 Güvenlik ve Kontroller

### Yetkilendirme
- Fiyat teklifi sadece talep eden kişi yapabilir
- Karşı teklif sadece hizmet sağlayıcı yapabilir
- Konuşma başlatma her iki taraf için de açık
- Fiyat kabulü her iki taraf da yapabilir

### Validasyon
- Fiyat değerleri 0'dan büyük olmalıdır
- Kullanıcı kendi hizmetine mesaj gönderemez
- Kullanıcı kendi hizmetini talep edemez
- Talep durumu kontrolü (Pending, Accepted, vb.)

### Veri Tutarlılığı
- QuotedPrice ve Price senkronize tutulur
- IsNegotiating durumu doğru güncellenir
- LastNegotiationDate her işlemde kaydedilir
- NegotiationNotes opsiyonel olarak doldurulur

## 📝 Gelecek İyileştirmeler

1. **Otomatik Pazarlık Limiti**: Maksimum/minimum fiyat aralığı belirleme
2. **Pazarlık Geçmişi**: Tüm teklif ve karşı tekliflerin detaylı kaydı
3. **Zaman Aşımı**: Belirli süre sonra pazarlığın otomatik kapanması
4. **Çoklu Teklif**: Aynı hizmet için birden fazla teklif alma
5. **Şablon Mesajlar**: Hızlı yanıtlar için hazır mesajlar
6. **Fiyat İstatistikleri**: Benzer hizmetlerin ortalama fiyatını gösterme

## 🐛 Bilinen Sorunlar

- Şu anda bilinen bir sorun bulunmamaktadır.

## 📊 Test Senaryoları

### Test 1: Mesajlaşma Başlatma
- ✅ Hizmet ilanından mesaj gönderme
- ✅ Talep sonrası mesaj gönderme
- ✅ Konuşma sayfasına yönlendirme

### Test 2: Fiyat Pazarlığı
- ✅ Talep eden fiyat teklifi
- ✅ Satıcı karşı teklif
- ✅ Fiyat kabulü
- ✅ Bildirim gönderimi

### Test 3: UI Görünürlük
- ✅ Butonların doğru durumlarda görünmesi
- ✅ Pazarlık bilgilerinin gösterilmesi
- ✅ Converter'ların çalışması

## 🤝 Katkıda Bulunanlar

- **Proje Sahibi & Geliştirici**: Seyda Karatekeli
- **Destek**: GitHub Copilot AI Assistant

---

**Son Güncelleme**: 2 Aralık 2025
**Versiyon**: 1.0.0
