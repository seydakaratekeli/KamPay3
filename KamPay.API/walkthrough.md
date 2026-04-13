# 📱 USB ile Gerçek Telefonda Test Rehberi

Bilgisayar IP: `192.168.1.5` | API Port: `5011` (HTTP)

## Adım 1: Windows Firewall'u Aç (Yönetici olarak)

**PowerShell'i Yönetici (Admin) olarak** açıp şu komutu çalıştırın:

```powershell
netsh advfirewall firewall add rule name="KamPay API (5011)" dir=in action=allow protocol=tcp localport=5011
```

> [!IMPORTANT]
> Bu adım olmadan telefon, bilgisayarınızdaki API'ye erişemez!

---

## Adım 2: API'yi Başlat

Bir **terminal/PowerShell** penceresinde:

```powershell
cd c:\Users\seyda\source\repos\seydakaratekeli\KamPay3
dotnet run --project KamPay.API --launch-profile mobile-test
```

Başarılı olduğunda şunu göreceksiniz:
```
Now listening on: http://0.0.0.0:5011
```

> [!TIP]
> Test etmek için bilgisayar tarayıcınızda şu adrese gidin:
> `http://localhost:5011/api/v1/products`
> Ürün listesi JSON olarak gelmeli.

---

## Adım 3: Telefonun Bağlantısını Doğrula

Telefonunuzun tarayıcısından (Chrome) şu adrese gidin:

```
http://192.168.1.5:5011/api/v1/products
```

- ✅ JSON veri görüyorsanız → Bağlantı çalışıyor!
- ❌ Açılmıyorsa → Firewall kuralını kontrol edin veya telefonun aynı WiFi ağında olduğundan emin olun

> [!WARNING]
> Telefonunuz bilgisayarla **aynı WiFi ağında** olmalıdır!
> USB sadece uygulama yüklemek için kullanılıyor, internet bağlantısı WiFi üzerinden gidiyor.

---

## Adım 4: MAUI Uygulamasını Telefona Yükle

**Yeni bir terminal** penceresinde (API'yi kapatmayın!):

### Yöntem A: Visual Studio ile (Önerilen)
1. Visual Studio'da **KamPay** projesini açın
2. Üst araç çubuğundan hedef cihaz olarak **telefonunuzu** seçin
3. **▶ Çalıştır (F5)** butonuna basın

### Yöntem B: Komut satırı ile
```powershell
cd c:\Users\seyda\source\repos\seydakaratekeli\KamPay3
dotnet build KamPay/KamPay.csproj -f net10.0-android -t:Run
```

---

## Akış Özeti

```
📱 Telefon                    💻 Bilgisayar
┌─────────────┐              ┌──────────────────┐
│  KamPay App │──── WiFi ───▶│  KamPay.API      │
│             │   HTTP GET   │  (port 5011)     │
│  Product    │   POST/PUT   │                  │
│  ApiService │   DELETE     │  ProductsContr.  │
│             │◀─── JSON ───│        │         │
│  Bearer     │              │        ▼         │
│  Token  ────┼──────────────┼▶ Firebase Auth   │
│  (JWT)      │              │        │         │
└─────────────┘              │        ▼         │
                             │  🔥 Firebase DB  │
                             └──────────────────┘
```

## Sorun Giderme

| Sorun | Çözüm |
|-------|-------|
| Telefonda API'ye erişilmiyor | Firewall kuralını kontrol edin |
| "Connection refused" | API'nin çalıştığından emin olun (`dotnet run`) |
| Telefon cihaz olarak görünmüyor | USB Hata Ayıklama modu açık mı? (`Ayarlar > Geliştirici Seçenekleri > USB Hata Ayıklama`) |
| IP adresi değişti | `ipconfig` ile yeni IP'yi öğrenip `ProductApiService.cs`'de güncelleyin |
| "XAJVC7000" build hatası | `KamPay/obj` ve `KamPay/bin` klasörlerini silip tekrar deneyin |
