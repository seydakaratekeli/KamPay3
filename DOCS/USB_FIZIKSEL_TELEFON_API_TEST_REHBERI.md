# 📱 USB ile Gerçek Telefonda API Test Rehberi

> **KamPay — .NET MAUI + ASP.NET Core API**
> Bu rehber, hiç deneyimi olmayan bir kullanıcının USB kablosuyla bağlı **gerçek bir Android telefonda** KamPay uygulamasını çalıştırıp API'yi test edebilmesini sağlar.

---

## 📑 İçindekiler

5. [Bilgisayarın IP Adresini Öğrenme](#5--bilgisayarın-ip-adresini-öğrenme)
6. [MAUI Projesinde IP Adresini Güncelleme](#6--maui-projesinde-ip-adresini-güncelleme)
7. [API'yi Başlatma (Backend)](#7--apiyi-başlatma-backend)
8. [MAUI Uygulamasını Telefona Yükleme](#8--maui-uygulamasını-telefona-yükleme)
9. [Test Etme ve Doğrulama](#9--test-etme-ve-doğrulama)
10. [Sık Karşılaşılan Sorunlar ve Çözümler](#10--sık-karşılaşılan-sorunlar-ve-çözümler)

---


### Telefon & Bilgisayar Aynı Ağda Olmalı ⚠️

> [!IMPORTANT]
> **Telefon ve bilgisayar aynı Wi-Fi ağına bağlı olmalıdır!**
> Telefon mobil veri (4G/5G) kullanıyorsa, bilgisayardaki API'ye erişemez.
>
> Örnek:
> - ✅ Telefon: "EvWiFi" → Bilgisayar: "EvWiFi" (kablolu veya kablosuz, aynı modem)
> - ❌ Telefon: "4G Mobil Veri" → Bilgisayar: "EvWiFi"


## 5. 🌐 Bilgisayarın IP Adresini Öğrenme

Gerçek telefon, bilgisayarındaki API'ye **localhost** ile bağlanamaz. Bilgisayarın **yerel ağ IP adresini** (LAN IP) öğrenmen gerekiyor.

### Adımlar:

1. Bilgisayarda **Komut İstemi (CMD)** veya **PowerShell** aç:
   - `Windows tuşu + R` → `cmd` yaz → `Enter`
   - Veya arama çubuğuna "cmd" yaz

2. Şu komutu yaz ve Enter'a bas:

```powershell
ipconfig
```

3. Çıktıda **"Wireless LAN adapter Wi-Fi"** veya **"Ethernet adapter"** bölümünü bul:

```
Wireless LAN adapter Wi-Fi:

   Bağlantıya Özgü DNS Son Eki  . :
   Bağlantı-yerel IPv6 Adresi   . : fe80::xxxx:xxxx:xxxx
   IPv4 Adresi. . . . . . . . . . : 192.168.1.5     ← BU SENIN IP ADRESİN!
   Alt Ağ Maskesi . . . . . . . . : 255.255.255.0
   Varsayılan Ağ Geçidi . . . . . : 192.168.1.1
```

4. **IPv4 Adresi** satırındaki numarayı not al (örn: `192.168.1.5`)

> [!IMPORTANT]
> **Bu IP adresi her Wi-Fi ağında farklı olabilir!** Ağ değiştirdiğinde tekrar kontrol et.
> Genellikle şu formatlardan birinde olur: `192.168.1.X`, `192.168.0.X`, `10.0.0.X`

---

## 6. 🔧 MAUI Projesinde IP Adresini Güncelleme

Telefonun, bilgisayarındaki API'ye bağlanabilmesi için MAUI projesinde IP adresini güncellememiz gerekiyor.

### Dosya: `KamPay/Services/ProductApiService.cs`

Bu dosyayı Visual Studio'da aç ve **33-39. satırları** bul:

```csharp
#if ANDROID
        // 📱 GERÇEK CİHAZ TESTİ: Bilgisayarınızın IP adresini buraya yazın
        // CMD'de "ipconfig" komutu ile öğrenebilirsiniz
        var baseHost = "http://192.168.1.5:5011";   // ← BU SATIRI GÜNCELLE!
        
        // 🖥️ EMÜLATÖR TESTİ İÇİN: Yukarıdaki satırı yorum yapıp bunu açın
        // var baseHost = "http://10.0.2.2:5011";
```

### Yapman Gereken:

`192.168.1.5` kısmını **kendi bilgisayarının IP adresi** ile değiştir.

**Örnek:** Eğer IP adresin `192.168.1.42` ise:

```csharp
var baseHost = "http://192.168.1.42:5011";
```

> [!CAUTION]
> - `http://` kullan, `https://` **DEĞİL**! (Development ortamında SSL sertifika sorunu olmasın diye)
> - Port numarası `5011` olmalı (API'nin çalıştığı port)
> - IP adresinden sonra `/` koymayın: ✅ `http://192.168.1.5:5011` — ❌ `http://192.168.1.5:5011/`

---

## 7. 🚀 API'yi Başlatma (Backend)

MAUI uygulamasını çalıştırmadan önce, API sunucusunun çalışıyor olması gerekiyor.

### Neden "mobile-test" Profili Kullanıyoruz?

| Profil | Adres | Telefon Erişimi |
|---|---|---|
| `http` | `http://localhost:5011` | ❌ Sadece bilgisayar erişir |
| `https` | `https://localhost:7134` | ❌ Sadece bilgisayar erişir |
| **`mobile-test`** | **`http://0.0.0.0:5011`** | **✅ Tüm ağdan erişilebilir!** |

`0.0.0.0` demek "tüm ağ arayüzlerinden dinle" demektir. Bu sayede telefonun, bilgisayarın IP'si üzerinden API'ye erişebilir.

### Yöntem A: Terminal / Komut Satırı ile Başlatma (Önerilen ✅)

1. Visual Studio'da veya ayrı bir **Terminal / PowerShell** penceresi aç
2. API proje klasörüne git:

```powershell
cd c:\Users\seyda\source\repos\seydakaratekeli\KamPay3\KamPay.API
```

3. API'yi mobile-test profili ile başlat:

```powershell
dotnet run --launch-profile "mobile-test"
```

4. Şu çıktıyı görmelisin:

```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://0.0.0.0:5011        ← ✅ BAŞARILI!
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

> [!IMPORTANT]
> **Bu terminali KAPAMA!** API sunucusu bu pencerede çalışmaya devam etmeli.
> Yeni bir terminal penceresi açarak diğer işlemlerini yapabilirsin.

### Yöntem B: Visual Studio ile Başlatma

1. Solution Explorer'da **KamPay.API** projesine sağ tıkla
2. **"Set as Startup Project"** seç
3. Üst araç çubuğunda profil dropdown'ından **"mobile-test"** seç
4. **▶ Start** (veya `F5`) butonuna bas

```
┌─────────────────────────────────────────────────────┐
│  ▶  Debug ▼  |  mobile-test ▼  |  KamPay.API ▼     │
│                   ↑                                  │
│              BU PROFİLİ SEÇ!                         │
└─────────────────────────────────────────────────────┘
```

### API'nin Çalıştığını Doğrulama

Bilgisayarında tarayıcıyı aç ve şu adresi ziyaret et:

```
http://localhost:5011/swagger
```

Swagger UI açılıyorsa API çalışıyor ✅

**Telefondan da test etmek istersen:** Telefonun tarayıcısında (Chrome) şu adresi gir:

```
http://192.168.1.5:5011/swagger
```

(IP adresini kendi bilgisayarının IP'si ile değiştir)

> [!WARNING]
> **Swagger telefonun tarayıcısında açılmıyorsa:**
> 1. Windows Güvenlik Duvarı API portunu engelliyor olabilir → [Firewall Ayarları](#windows-güvenlik-duvarı-firewall-sorunu) bölümüne bak
> 2. Telefon ve bilgisayar farklı ağda olabilir
> 3. API gerçekten `0.0.0.0:5011` üzerinde dinliyor mu kontrol et

---

## 8. 📲 MAUI Uygulamasını Telefona Yükleme

API çalışıyor, telefon bağlı. Şimdi MAUI uygulamasını telefona yükleyip çalıştıralım.

### Adımlar:

1. Visual Studio'da `KamPay.sln` çözümünü aç

2. Solution Explorer'da **KamPay** projesine (MAUI projesi) sağ tıkla → **"Set as Startup Project"**

3. Üst araç çubuğunda:
   - **Framework:** `net9.0-android` seç
   - **Device:** Telefonunun adı (örn: `Samsung SM-A525F`) seç

```
┌──────────────────────────────────────────────────────────────────┐
│  ▶  Debug ▼  |  net9.0-android ▼  |  Samsung SM-A525F ▼         │
│                                        ↑                         │
│                                   TELEFONUNU SEÇ!                │
└──────────────────────────────────────────────────────────────────┘
```

4. **▶ Start** butonuna bas (veya `F5`)

5. Visual Studio şunları yapacak (ilk seferde 3-5 dakika sürebilir):
   ```
   ⏳ Build ediliyor...            (~1-2 dk)
   ⏳ APK oluşturuluyor...         (~30 sn)
   ⏳ Telefona yükleniyor...       (~30 sn)
   ⏳ Uygulama başlatılıyor...     (~10 sn)
   ✅ Uygulama telefonda açıldı!
   ```

6. Telefon ekranında **KamPay uygulaması** otomatik olarak açılacak!

> [!NOTE]
> **İlk yüklemede** telefon "Bilinmeyen kaynaklardan yüklemeye izin ver" diye sorabilir.
> **İzin ver** butonuna bas.

---

## 9. ✅ Test Etme ve Doğrulama

### Adım 1: API Bağlantısını Test Et

Uygulama telefonda açıldıktan sonra:

1. **Ana sayfaya** git (ürün listesi)
2. Ürünlerin Firebase'den yüklenip yüklenmediğini kontrol et
3. Visual Studio'nun **Output** penceresine bak:

```
✅ ProductApiService oluşturuldu (API Garsonu devrede) → http://192.168.1.5:5011/api/v1/products
```

Bu mesajı görüyorsan, MAUI uygulaması API'ye bağlanmaya çalışıyor ✅

### Adım 2: Visual Studio Output Penceresini Takip Et

Visual Studio'da **View → Output** menüsünden Output penceresini aç.
Dropdown'dan **"Debug"** seçili olmalı.

Burada uygulamanın tüm log mesajlarını görebilirsin:

```
✅ Firebase config yüklendi: kampay-b006d
✅ ProductApiService oluşturuldu → http://192.168.1.5:5011/api/v1/products
⚠️ HttpClient: SSL sertifika doğrulaması KAPALI (Development)
```

### Adım 3: Swagger UI ile Paralel Test

Telefonda uygulama çalışırken, bilgisayarında Swagger UI'ı da kullanarak API'yi test edebilirsin:

```
http://localhost:5011/swagger
```

| Test | Swagger'da | Telefonda |
|---|---|---|
| Ürünleri Listeleme | `GET /api/v1/Products` → Execute | Ana sayfa → Ürün listesi |
| Ürün Ekleme | `POST /api/v1/Products` → Body doldur → Execute | Ürün Ekle sayfası |
| Ürün Güncelleme | `PUT /api/v1/Products/{id}` → Execute | Ürün Düzenle sayfası |
| Ürün Silme | `DELETE /api/v1/Products/{id}` → Execute | Ürün detay → Sil butonu |

---

## 10. 🔧 Sık Karşılaşılan Sorunlar ve Çözümler

---

### Telefon Visual Studio'da Görünmüyor

**Belirtiler:** Debug hedef listesinde telefon yok.

**Çözümler:**

1. **USB kablosunu kontrol et:** Veri kablosu mu yoksa sadece şarj kablosu mu?
   ```
   Test: Telefonu taktığında Windows'ta "Bu Bilgisayar" altında 
   telefonun klasörleri görünüyor mu? 
   Görünüyorsa → Veri kablosu ✅
   Görünmüyorsa → Kablo değiştir!
   ```

2. **USB Driver yükle:** Bazı telefonlar ek driver gerektirir
   - Samsung: [Samsung USB Drivers](https://developer.samsung.com/android-usb-driver)
   - Xiaomi: Otomatik yüklenir
   - Diğerleri: [Google USB Driver](https://developer.android.com/studio/run/win-usb) (Visual Studio → Tools → Android → Android SDK Manager → SDK Tools → Google USB Driver ✅)

3. **ADB'yi yeniden başlat:**
   ```powershell
   # PowerShell veya CMD'de çalıştır:
   adb kill-server
   adb start-server
   adb devices
   ```
   Çıktıda telefonun seri numarası görünmeli:
   ```
   List of devices attached
   XXXXXXXXXXXXXXX    device     ← ✅ Telefon bağlı!
   ```

4. **Visual Studio'yu yönetici olarak çalıştır** (sağ tık → "Yönetici olarak çalıştır")

---

### Windows Güvenlik Duvarı (Firewall) Sorunu

**Belirtiler:** API bilgisayarda çalışıyor ama telefondan erişilemiyor.

**Çözüm: Port 5011'i Firewall'da aç**

1. Windows Arama → **"Windows Defender Güvenlik Duvarı"** yaz ve aç
2. Sol menüden **"Gelişmiş Ayarlar"** tıkla
3. **"Gelen Kurallar"** (Inbound Rules) → sağ tarafta **"Yeni Kural..."** tıkla
4. Kural türü: **Port** → İleri
5. **TCP** → Belirli yerel bağlantı noktaları: **5011** → İleri
6. **Bağlantıya izin ver** → İleri
7. Tüm profilleri işaretle (Etki Alanı, Özel, Genel) → İleri
8. İsim: **"KamPay API (5011)"** → Son

**Alternatif: PowerShell ile (Yönetici olarak çalıştır):**

```powershell
# Gelen bağlantı kuralı ekle
netsh advfirewall firewall add rule name="KamPay API Port 5011" dir=in action=allow protocol=TCP localport=5011

# Giden bağlantı kuralı ekle (opsiyonel)
netsh advfirewall firewall add rule name="KamPay API Port 5011 Out" dir=out action=allow protocol=TCP localport=5011
```

**Doğrulama:** Firewall kuralını ekledikten sonra telefonun tarayıcısında tekrar dene:
```
http://192.168.1.5:5011/swagger
```

---

### "Connection Refused" veya "Bağlantı Reddedildi" Hatası

**Belirtiler:** Telefondaki uygulama API'ye bağlanamıyor, Output'ta `Connection refused` hatası var.

**Kontrol Listesi:**

| # | Kontrol | Nasıl? |
|---|---|---|
| 1 | API çalışıyor mu? | Terminal'de `dotnet run` çalışıyor olmalı |
| 2 | Doğru profil mi? | `mobile-test` profili kullanılmalı (`0.0.0.0:5011`) |
| 3 | IP adresi doğru mu? | `ipconfig` ile kontrol et |
| 4 | Aynı ağda mı? | Telefon Wi-Fi'da mı? (mobil veri değil!) |
| 5 | Firewall açık mı? | Port 5011 izinli mi? |
| 6 | Port meşgul mü? | Başka bir uygulama 5011 portunu kullanıyor olabilir |

**Port kontrolü:**
```powershell
netstat -ano | findstr "5011"
```

---

### "ERR_CLEARTEXT_NOT_PERMITTED" Hatası

**Belirtiler:** Android, HTTP bağlantısını engellediğine dair hata veriyor.

**Çözüm:** Bu proje zaten buna karşı yapılandırılmış durumda. Ama yine de kontrol et:

**`Platforms/Android/AndroidManifest.xml`** dosyasında şu satır olmalı:
```xml
android:usesCleartextTraffic="true"
```

---

### Uygulama Yüklenirken "INSTALL_FAILED" Hatası

**Olası Çözümler:**

1. **Telefondan eski sürümü sil:** Ayarlar → Uygulamalar → KamPay → Kaldır
2. **Yeterli depolama alanı var mı?** En az 500 MB boş alan olmalı
3. **Visual Studio temizle ve tekrar derle:**
   ```
   Visual Studio → Build → Clean Solution
   Visual Studio → Build → Rebuild Solution
   ```

---

### API Yanıt Veriyor Ama Veriler Boş Geliyor

**Belirtiler:** 200 OK dönüyor ama JSON boş array `[]` geliyor.

**Kontrol Et:**
1. Firebase'de veri var mı? → Firebase Console → Realtime Database → `products` düğümü
2. Swagger UI'da `GET /api/v1/Products` dene → Orada da boş mu?
3. Firebase Admin SDK bağlantısı doğru mu? → `firebase-admin.json` dosyası yerinde mi?

---

## 📋 Hızlı Başlangıç Kontrol Listesi (Cheat Sheet)

Her test oturumu başında bu listeyi takip et:

```
□ 1. Telefon USB ile bilgisayara bağlı mı?
□ 2. USB Debugging açık mı?
□ 3. "Dosya aktarımı" modu seçili mi?
□ 4. Telefon ve bilgisayar aynı Wi-Fi'da mı?
□ 5. ipconfig ile IP adresi kontrol ettim mi?
□ 6. ProductApiService.cs'deki IP adresi güncel mi?
□ 7. API "mobile-test" profili ile çalışıyor mu?
□ 8. Tarayıcıdan http://<IP>:5011/swagger açılıyor mu?
□ 9. Visual Studio'da hedef olarak telefonum seçili mi?
□ 10. ▶ Start ile uygulamayı başlattım mı?
```

---

## 🏗️ Mimari Özet: Telefon → API → Firebase Akışı

```
┌──────────────────┐         ┌───────────────────────┐         ┌──────────────┐
│                  │  HTTP   │                       │  REST   │              │
│  📱 Telefon      │────────▶│  🖥️ Bilgisayar (API)  │────────▶│  🔥 Firebase │
│  (KamPay MAUI)   │  Wi-Fi  │  (KamPay.API)         │         │  Realtime DB │
│                  │◀────────│  http://0.0.0.0:5011  │◀────────│              │
│  USB ile bağlı   │  JSON   │                       │  JSON   │              │
│                  │         │  Swagger UI aktif      │         │              │
└──────────────────┘         └───────────────────────┘         └──────────────┘
        │                              │
        │  USB                         │
        └──────────────────────────────┘
        Debug + Log Aktarımı
```

**Veri Akışı:**
1. 📱 Telefondaki KamPay uygulaması → `ProductApiService` üzerinden HTTP isteği atar
2. 🖥️ Bilgisayardaki `KamPay.API` → İsteği alır ve `ProductsController` işler
3. 🔥 Firebase Realtime Database'den veri çeker/yazar
4. Yanıt aynı yoldan geri döner → Telefon ekranında gösterilir

---

## 📌 Önemli Dosya ve Yollar

| Dosya | Konum | Açıklama |
|---|---|---|
| API Launch Settings | `KamPay.API/Properties/launchSettings.json` | Port ve profil ayarları |
| API Program | `KamPay.API/Program.cs` | API yapılandırması |
| Products Controller | `KamPay.API/Controllers/ProductsController.cs` | CRUD endpoint'leri |
| MAUI API Servisi | `KamPay/Services/ProductApiService.cs` | **IP adresi burada değişir!** |
| Android Manifest | `KamPay/Platforms/Android/AndroidManifest.xml` | Network izinleri |
| Network Security | `KamPay/Platforms/Android/Resources/xml/network_security_config.xml` | HTTP izin ayarları |
| Firebase Admin | `KamPay.API/firebase-admin.json` | API'nin Firebase bağlantısı |

---

> [!TIP]
> **En sık yapılan hata:** IP adresini güncellemeden test etmek! Her yeni ağa bağlandığında `ipconfig` ile kontrol et ve `ProductApiService.cs` dosyasını güncelle.

---

_Bu rehber KamPay projesi için hazırlanmıştır. Son güncelleme: Nisan 2026_
