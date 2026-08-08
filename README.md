# KamPay

KamPay, Bartın Üniversitesi öğrencilerine yönelik kampüs içi ikinci el eşya alım-satım, hizmet paylaşımı ve topluluk platformudur.

## Özet

- Mobil Uygulama: .NET MAUI (net10.0) — Android + iOS  
- Backend API: ASP.NET Core Minimal API (net10.0)  
- Veritabanı: Firebase Realtime Database  
- Depolama: Firebase Storage  
- Kimlik Doğrulama: Firebase Authentication (FirebaseAuthentication.net)  
- Dil: C# — Türkçe UI, çoklu dil desteği (resx)

## Özellikler

- Ürün ilanları oluşturma, düzenleme, silme
- Görsellerle ürün paylaşımı (Firebase Storage)
- Kullanıcı kimlik doğrulama (Firebase Auth)
- Lokasyona göre arama / filtreleme
- Mesajlaşma ile alıcı-satıcı iletişimi (ileride eklenecek)

## Proje Yapısı

KamPay3 çözümünde iki ana proje bulunur:

- KamPay/ — .NET MAUI mobil uygulaması  
  - Models/, ViewModels/, Views/, Services/, Resources/, Platforms/ vb.
  - `MauiProgram.cs` içinde DI ve servis kayıtları
  - `appsettings.json`, `firebaseconfig.json` (gizli bilgileri repoya koymayın)
- KamPay.API/ — ASP.NET Core Minimal API  
  - Controllers/, Services/, Repositories/, Middlewares/, Models/
  - `firebase-admin.json` (credential, repoya eklemeyin), `appsettings.json`

Ayrıntılı yapı için repository içindeki klasörleri inceleyin.

## Gereksinimler

- .NET 10 SDK (TargetFramework: net10.0)
- .NET MAUI workload (mobil uygulama için)
- Android/iOS geliştirme araçları (emülatörler / Xcode vb.)
- Firebase projesi (Realtime DB, Storage, Auth)
- (Opsiyonel) Visual Studio 2022/2023 veya Visual Studio for Mac / VS Code + MAUI destekli eklentiler

## Hızlı Başlangıç

1. Depoyu klonlayın:
   git clone https://github.com/seydakaratekeli/KamPay3.git
2. Kök dizinde çözümü restore edin:
   dotnet restore KamPay.sln
3. Backend API'yi çalıştırma:
   - KamPay.API projesine `firebase-admin.json` (admin credential) ve gerekli `appsettings.json` dosyalarını ekleyin (gizli bilgileri commit etmeyin).
   - cd KamPay.API
   - dotnet run
4. Mobil uygulamayı çalıştırma:
   - `KamPay` projesini Visual Studio’da açın veya komut satırından hedef platformu belirleyerek build/run edin.
   - Firebase istemci yapılandırmasını `KamPay/firebaseconfig.json` içine ekleyin (API anahtarları ve proje bilgileri).
5. Gerekli gizli dosyaları (firebase-admin.json, firebaseconfig.json, gizli appsettings.*) `.gitignore` içine ekleyin ve güvenli kanallardan paylaşın.

## Yapılandırma & Gizli Bilgiler

- `KamPay/firebaseconfig.json` — Mobil uygulama için Firebase istemci yapılandırması.
- `KamPay.API/firebase-admin.json` — Backend için Firebase Admin SDK credential (commit etmeyin).
- `KamPay/appsettings.json` ve `KamPay.API/appsettings.json` içinde JwtSettings ve FirebaseDatabase bağlantı ayarları bulunur; prod/dev ayrımı için `appsettings.Development.json` kullanın.

- 
Ekran Görüntüleri

<img width="177" height="384" alt="image" src="https://github.com/user-attachments/assets/917fbe68-0e40-4136-9422-7ab295cf863b" />   <img width="177" height="384" alt="image" src="https://github.com/user-attachments/assets/df4844d5-9fe2-43e1-b10a-06698045e16a" />    <img width="177" height="384" alt="image" src="https://github.com/user-attachments/assets/a4a577f3-87d4-472c-b341-275732995f3f" />   <img width="177" height="384" alt="image" src="https://github.com/user-attachments/assets/a600f9bd-dd8e-4baf-a397-ffb03f75bb82" />   <img width="177" height="384" alt="image" src="https://github.com/user-attachments/assets/59989000-9df0-4cfb-a137-a6b0bbae0c8b" />     <img width="177" height="384" alt="image" src="https://github.com/user-attachments/assets/dca407cb-ec3a-4ad4-8159-48913cba0651" />    <img width="177" height="384" alt="image" src="https://github.com/user-attachments/assets/01a5cb32-4663-4c57-a298-3e1030c22038" />     <img width="177" height="384" alt="image" src="https://github.com/user-attachments/assets/64982d30-2bd1-4133-8480-6c3c4a1e2a92" />    <img width="177" height="384" alt="image" src="https://github.com/user-attachments/assets/d7df560c-36b0-4a1e-b4b9-4ef25763386e" />

 


## Geliştirme Notları

- MVVM: CommunityToolkit.Mvvm kullanılıyor.
- Dependency Injection: `MauiProgram.cs` içinde servis kayıtları yapılır.
- API tarafında gelen isteklerde Firebase token doğrulaması için `FirebaseTokenValidationMiddleware` bulunur.
- Testler: (Henüz eklenmedi) Birim testleri ve entegrasyon testleri için proje genişletilebilir.

## Katkıda Bulunma

- Yeni özellikler, hata düzeltmeleri veya dokümantasyon için issue açın.  
- Değişiklikler için pull request gönderin; PR açıklamasında yapılan değişiklikleri, testleri ve nasıl doğrulandığını belirtin.

## Lisans

Lisans dosyası repoda yoksa, lütfen uygun bir lisans ekleyin (örn. MIT). Lisans bilgisi buraya eklenecektir.

## İletişim

Proje sahibi: @seydakaratekeli  
Sorular/konular için GitHub Issues kullanın.
