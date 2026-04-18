# ?? Mailtrap ile E-posta Test Rehberi

## ?? Amaç
Gerçek SMTP sunucusuna baðlanmadan önce e-posta gönderimini **Mailtrap** ile test edin.

---

## ?? Adým 1: Mailtrap Hesabý Oluþturma

1. [https://mailtrap.io](https://mailtrap.io) adresine gidin
2. Ücretsiz hesap oluþturun (Email Testing için)
3. Gelen kutunuzu doðrulayýn

---

## ?? Adým 2: SMTP Bilgilerini Alma

### Mailtrap Dashboard'da:

1. **Email Testing** ? **Inboxes** ? **My Inbox** (veya yeni inbox oluþturun)
2. **SMTP Settings** sekmesine týklayýn
3. **Integrations** dropdown'ýndan **"Node.js / Nodemailer"** veya **"Generic SMTP"** seçin

### Örnek SMTP Bilgileri:
```
Host:     sandbox.smtp.mailtrap.io
Port:     587 (veya 2525)
Username: a1b2c3d4e5f678  ? Sizinki farklý olacak
Password: 9876543210abcd  ? Sizinki farklý olacak
Auth:     PLAIN
TLS:      Optional (but recommended)
```

---

## ?? Adým 3: appsettings.json Güncelleme

`KamPay/appsettings.json` dosyasýný açýn ve Mailtrap bilgilerini girin:

```json
{
  "EmailSettings": {
    "SmtpHost": "sandbox.smtp.mailtrap.io",
    "SmtpPort": 587,
    "UseSsl": true,
    "FromEmail": "kampay@bartin.edu.tr",
    "FromName": "KamPay Doðrulama",
    "Username": "MAILTRAP_DASHBOARD_USERNAME",
    "Password": "MAILTRAP_DASHBOARD_PASSWORD"
  }
}
```

### ?? Önemli Notlar:
- `Username` ve `Password` ? **Mailtrap Dashboard'dan kopyalayýn**
- `SmtpPort` ? `587` veya `2525` kullanabilirsiniz
- `UseSsl` ? `true` olmalý (TLS kullanýmý için)

---

## ?? Adým 4: Test Etme

### Visual Studio'da:

1. **Uygulamayý yeniden baþlatýn** (Hot Reload yeterli deðil, appsettings.json Embedded Resource)
2. Kayýt ekranýna gidin
3. Yeni bir kullanýcý kaydedin (örnek: `test@bartin.edu.tr`)

### Konsol Çýktýsý:
```
?? E-posta gönderiliyor: test@bartin.edu.tr
? Doðrulama kodu e-postaya gönderildi!
```

### Mailtrap'te Kontrol:

1. Mailtrap Dashboard ? **My Inbox** ? **Messages**
2. Yeni gelen e-postayý göreceksiniz:
   ```
   From: KamPay Doðrulama <kampay@bartin.edu.tr>
   To: test@bartin.edu.tr
   Subject: KamPay - E-posta Doðrulama
   Body: Doðrulama kodunuz: 123456
   ```

---

## ?? Adým 5: Debug ve Hata Giderme

### Debug Output'u Ýzleme (Visual Studio):

**View** ? **Output** ? **Show output from: Debug**

```plaintext
?? MauiApp baþlatýlýyor...
? Email ayarlarý yüklendi: sandbox.smtp.mailtrap.io
?? E-posta gönderiliyor: test@bartin.edu.tr
========== KamPay Doðrulama Kodu ==========
To: test@bartin.edu.tr
Kod: 123456
==========================================
? E-posta baþarýyla gönderildi: test@bartin.edu.tr
```

### Yaygýn Hatalar:

#### ? Hata 1: Authentication Failed
```
? SMTP Hatasý: 5.7.8 Error: authentication failed
```
**Çözüm:** Username ve Password'ü Mailtrap'ten tekrar kopyalayýn

#### ? Hata 2: Connection Timeout
```
? E-posta gönderilemedi: A connection attempt failed
```
**Çözüm:** Port numarasýný `2525` ile deneyin veya güvenlik duvarý ayarlarýný kontrol edin

#### ? Hata 3: TLS Negotiation Failed
```
? SMTP Hatasý: Unable to read data from the transport connection
```
**Çözüm:** `UseSsl = false` yapmayý deneyin (Mailtrap TLS'i optional olarak kabul eder)

---

## ?? Mailtrap Avantajlarý

? **Gerçek e-posta göndermeye gerek yok**  
? **Spam riský yok** (test e-postalarý gerçek kullanýcýlara gitmez)  
? **HTML/Text preview** (e-posta görünümünü kontrol edin)  
? **Email validation** (SPF, DKIM kontrolü)  
? **API access** (otomatik testler için)

---

## ?? Üretim Ortamýna Geçiþ

Test baþarýlý olduktan sonra gerçek SMTP'ye geçin:

```json
{
  "EmailSettings": {
    "SmtpHost": "smtp.bartin.edu.tr",
    "SmtpPort": 587,
    "UseSsl": true,
    "FromEmail": "kampay@bartin.edu.tr",
    "FromName": "KamPay Doðrulama",
    "Username": "kampay@bartin.edu.tr",
    "Password": "IT_DEPARTMAN_SMTP_SIFRESI"
  }
}
```

---

## ?? Test Senaryolarý

### Senaryo 1: Kayýt Doðrulama
1. Yeni kullanýcý kaydý oluþtur
2. Mailtrap'te e-postanýn geldiðini kontrol et
3. Doðrulama kodunu kopyala ve uygulamaya gir

### Senaryo 2: Kod Yeniden Gönderme
1. "Kod tekrar gönder" butonuna bas
2. Mailtrap'te yeni bir e-posta daha geldiðini doðrula
3. Yeni kodu test et

### Senaryo 3: Kod Süresi Dolmasý
1. 15 dakika bekle (veya sistem saatini ileri al)
2. Eski kodu girmeyi dene
3. "Kodun süresi dolmuþ" hatasý almalýsýn

---

## ??? Geliþmiþ Test (Opsiyonel)

### Mailtrap API ile Otomatik Test:

```csharp
// Test projesinde kullanýlabilir
var client = new HttpClient();
client.DefaultRequestHeaders.Add("Api-Token", "YOUR_MAILTRAP_API_TOKEN");

var response = await client.GetAsync("https://mailtrap.io/api/v1/inboxes/INBOX_ID/messages");
var emails = await response.Content.ReadAsStringAsync();

// En son gelen e-postadan doðrulama kodunu parse et
```

---

## ?? Yardým

**Mailtrap Desteði:**  
https://help.mailtrap.io/

**KamPay E-posta Kurulum Dokümantasyonu:**  
`DOCS/EMAIL_SETUP.md`

---

## ? Test Checklist

- [ ] Mailtrap hesabý oluþturuldu
- [ ] SMTP credentials alýndý
- [ ] `appsettings.json` güncellendi
- [ ] Uygulama yeniden baþlatýldý
- [ ] Kayýt iþlemi test edildi
- [ ] Mailtrap'te e-posta geldi
- [ ] Doðrulama kodu çalýþtý
- [ ] Debug loglarý kontrol edildi
- [ ] Üretim SMTP'ye geçiþ hazýr

?? **Test baþarýlý!** Artýk gerçek SMTP sunucusuna geçebilirsiniz.
