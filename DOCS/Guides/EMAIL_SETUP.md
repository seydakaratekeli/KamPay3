# KamPay E-posta Konfigürasyonu

## ?? SMTP Ayarlarýný Yapýlandýrma

### 1. `appsettings.json` Dosyasýný Oluþturma

Proje dizininde `KamPay/appsettings.json` dosyasý **varsayýlan olarak `.gitignore`'da** bulunur (güvenlik için).

#### Ýlk Kurulum:

1. `appsettings.Example.json` dosyasýný kopyalayýn
2. Yeni dosyayý `appsettings.json` olarak yeniden adlandýrýn
3. IT departmanýndan alýnan **gerçek SMTP þifresini** ekleyin

```bash
# Terminal'de çalýþtýrýn:
cd KamPay
cp appsettings.Example.json appsettings.json
```

### 2. `appsettings.json` Ýçeriðini Düzenleme

```json
{
  "EmailSettings": {
    "SmtpHost": "smtp.bartin.edu.tr",
    "SmtpPort": 587,
    "UseSsl": true,
    "FromEmail": "kampay@bartin.edu.tr",
    "FromName": "KamPay Doðrulama",
    "Username": "kampay@bartin.edu.tr",
    "Password": "GERÇEK_SMTP_ÞÝFRESÝNÝ_BURAYA_YAPIN"
  }
}
```

### 3. IT Departmanýndan Alýnmasý Gerekenler

? **SMTP Sunucu Adresi:** `smtp.bartin.edu.tr`  
? **Port:** `587` (veya `465` TLS için)  
? **SSL/TLS Kullanýmý:** `true`  
? **Gönderen E-posta:** `kampay@bartin.edu.tr`  
? **SMTP Kullanýcý Adý:** Genellikle e-posta adresi ile ayný  
? **SMTP Þifresi:** ?? **Güvenli saklanmalý!**

### 4. Güvenlik Notlarý

- ?? **`appsettings.json` dosyasýný asla Git'e eklemeyin**
- `.gitignore` dosyasý otomatik olarak bu dosyayý görmezden gelir
- Üretim ortamýnda **Azure Key Vault** veya **AWS Secrets Manager** kullanýn
- Geliþtirme ortamýnda bile þifreyi takým arkadaþlarýnýzla paylaþmayýn

### 5. Çalýþma Mantýðý

1. Uygulama baþlarken `MauiProgram.cs` ? `LoadEmailSettings()` metodu çalýþýr
2. `appsettings.json` dosyasý **Embedded Resource** olarak okunur
3. Ayarlar `EmailSettings` sýnýfýna deserialize edilir
4. `EmailService` bu ayarlarý DI (Dependency Injection) ile alýr
5. Kullanýcý kayýt olunca doðrulama kodu **gerçek e-posta** ile gönderilir

### 6. Hata Durumlarý

#### Dosya bulunamazsa:
```
?? appsettings.json bulunamadý, varsayýlan ayarlar kullanýlýyor
```
? Çözüm: `appsettings.json` dosyasýnýn `KamPay/` dizininde olduðundan emin olun

#### SMTP hatasý:
```
? SMTP Hatasý: The SMTP server requires a secure connection
```
? Çözüm: `SmtpPort` ve `UseSsl` ayarlarýný kontrol edin

#### Kimlik doðrulama hatasý:
```
? SMTP Hatasý: Authentication failed
```
? Çözüm: `Username` ve `Password` bilgilerini IT'den doðrulayýn

### 7. Test Etme

1. Uygulamayý çalýþtýrýn
2. Kayýt ekranýndan yeni bir kullanýcý oluþturun
3. Konsol çýktýsýný kontrol edin:
   ```
   ?? E-posta gönderiliyor: user@bartin.edu.tr
   ? Doðrulama kodu e-postaya gönderildi!
   ```

### 8. Geliþtirme Modu

E-posta gönderilemediðinde bile **debug için konsola kod yazdýrýlýr**:
```
?? GELÝÞTÝRME MODU - Kod: 123456
```

---

## ?? Hýzlý Baþlangýç Komutlarý

```bash
# 1. appsettings.json oluþtur
cp KamPay/appsettings.Example.json KamPay/appsettings.json

# 2. Dosyayý düzenle ve þifreyi ekle
# (Visual Studio, VS Code veya herhangi bir editör ile)

# 3. Projeyi derle
dotnet build

# 4. Uygulamayý çalýþtýr
dotnet run
```

---

## ?? Destek

Sorun yaþarsanýz:
- IT departmanýndan SMTP bilgilerini doðrulayýn
- `Debug Output` penceresini kontrol edin
- SMTP loglara bakýn: `Debug.WriteLine` çýktýlarý
