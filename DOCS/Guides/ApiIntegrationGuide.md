# KamPay API - MAUI Entegrasyon Özeti ve Test Rehberi

Bu belgede MAUI uygulaması ile `KamPay.API` arasında kurulan güvenli `JWT` (JSON Web Token) iletişim entegrasyonuna ait detaylar ve test adımları yer almaktadır.

---

## Neler Yapıldı?

### 1- Özel API Login Modeli Eklendi (`ApiLoginResponseDto.cs`)
Güvenli JWT yapısının istemci (MAUI) tarafında C# modeli oluşturuldu:
- `ApiLoginResponseDto` modeli içerisinde `Token`, `Uid` ve `Expiration` özellikleri tanımlandı.
- Gelen JSON boyut farklılıklarına karşı `JsonPropertyName` tag'leri (`[JsonPropertyName("token")]` vb.) eklendi.

### 2- FirebaseAuthService Güncellendi (Token Değişimi & Saklama)
MAUI uygulaması üzerinden Firebase Authentication kullanıldığında, ortaya çıkan native `FirebaseToken` hemen KamPay.API'ye gönderilerek API’nin kendi oluşturduğu Custom JWT ile değiştirildi:
- **`LoginAsync`:** MAUI'de manuel giriş yapıldığı an API'ye (`/api/Auth/login`) Firebase ID token gönderilir. Karşılığında API'nin JWT'si alınır.
- **`TryAutoLoginAsync`:** "Beni hatırla" işaretinde, Firebase token süresi geçerli olsa bile API Token (`KAMPAY_API_JWT`) yerel depolama da eksikse, arka planda tekrardan API'den JWT alınması sağlandı.
- **`LogoutAsync`:** Çıkış senaryosuna sadece Firebase/Session temizliği değil, `SecureStorage.Remove("KAMPAY_API_JWT")` eklenerek tam güvenlik sağlandı.

> _Tüm API bağlantıları, canlı veya gerçek cihaz testi aşamasına uygun olması açısından bilgisayarın Local IP adresi (`http://192.168...:5011`) ve HttpClient formatı hedeflenerek yapılandırıldı._

### 3- ProductApiService Güncellendi (Bearer Token Adaptasyonu)
Artık "API Garsonu" olarak adlandırılan `ProductApiService`, ürünleri kendi API'mize iletirken kimlik kapısını (`[Authorize]`) JWT ile aşıyor:
- **`SetAuthHeaderAsync()`** metodu oluşturuldu. Her kimlik isteyen işleme (örneğin; `POST /api/v1/products`) başlamadan önce `SecureStorage` alanından çıkartılan `KAMPAY_API_JWT`, Header içerisine **`Bearer <token>`** olarak eklenir.

---

## 🛠 Test Rehberi / Nasıl Çalıştırılır?

Aşağıdaki adımları takip ederek uçtan uca senaryoları test edebilirsiniz.

### Adım 1: IP Kontrolü
Bilgisayarınızın mevcut IP adresini bulun (Örn: `192.168.1.5`).
Aşağıdaki iki dosya içerisinde bu IP adresinin doğru yazıldığından emin olun:
- `KamPay\Services\ProductApiService.cs` (baseHost bölümü)
- `KamPay\Services\FirebaseAuthService.cs` (`http://IP_ADRESI:5011/api/Auth/login` vb.)

### Adım 2: API'yi Arka Planda Başlatma
1. Visual Studio Terminalini / PowerShell açıp `KamPay.API` klasörüne girin.
2. Aşağıdaki komutla tüm telefon vb. cihazların erişebileceği şekilde `5011` portundan başlatın:
   ```bash
   dotnet run --urls "http://0.0.0.0:5011"
   ```

### Adım 3: MAUI Cihaz/Emülatör Başlatma
1. Visual Studio'dan **KamPay** (MAUI Projesi) seçiliyken Gerçek Cihazınıza veya Emülatörünüze yayınlayın (`F5`).
2. Uygulama açıldıktan sonra eğer direkt bir açık oturum gördüyseniz ilk olarak uygulamadan **"Çıkış Yap" (Logout)** seçeneğini kullanın. _(Bu yeni API JWT'nin sağlıklı biçimde belleğe gömülmesi için gereklidir.)_
3. E-posta ve şifrenizi girerek işlemi gerçekleştirin.

### Adım 4: Gözlem ve Ürün İşlemleri
Giriş sonrasında **Visual Studio Output (Çıkış)** penceresinde şu mesajların gelip gelmediğini kontrol edin:
> `=== SİZİN API'NIZIN JWT'Sİ ===` <br>
> `eyJh.......` <br>
> `✅ KamPay API JWT başarıyla alındı ve kaydedildi.`

Bu yazı geldikten sonra dilediğiniz bir senaryoyu test edebilirsiniz. (Örnek: **Ürün Ekle**). 
Sorunsuz çalışıyorsa, API tarafında `200 OK` aldığınızı, ve verinizin Firebase/API katmanına başarıyla gönderildiğini anlayacaksınız!
