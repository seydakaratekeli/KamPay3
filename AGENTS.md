# KamPay - Proje Bağlam Dosyası (AGENTS.md)

> **Son Güncelleme:** 2026-04-21
> Bu dosya, AI asistanların her oturumda codebase taraması yapmasını önlemek için hazırlanmıştır.

---

## 1. Proje Özeti

**KamPay**, üniversite öğrencileri için tasarlanmış bir **kampüs içi ikinci el ürün alışveriş, hizmet paylaşımı ve topluluk platformudur**. Bartın Üniversitesi (`@bartin.edu.tr`) öğrencilerine özeldir.

- **Mobil Uygulama:** .NET MAUI (net10.0) — Android + iOS
- **Backend API:** ASP.NET Core Minimal API (net10.0)
- **Veritabanı:** Firebase Realtime Database
- **Depolama:** Firebase Storage
- **Kimlik Doğrulama:** Firebase Authentication (FirebaseAuthentication.net)
- **Dil:** C# — Türkçe UI, Türkçe/İngilizce çoklu dil desteği (resx)

---

## 2. Solution Yapısı

```
KamPay3/                          ← Solution kök dizini
├── KamPay.sln                    ← Solution dosyası
├── global.json                   ← SDK: net10.0, rollForward: latestMajor
├── AGENTS.md                     ← BU DOSYA
│
├── KamPay/                       ← .NET MAUI Mobil Uygulama
│   ├── KamPay.csproj             ← TargetFrameworks: net10.0-android;net10.0-ios
│   ├── MauiProgram.cs            ← DI Container, tüm servis kayıtları
│   ├── App.xaml / App.xaml.cs    ← Uygulama yaşam döngüsü, global styles, auto-login
│   ├── AppShell.xaml / .cs       ← Shell navigasyon, TabBar, route kayıtları
│   ├── appsettings.json          ← Embedded resource olarak yüklenir (EmbeddedResource)
│   ├── appsettings.Development.json
│   ├── firebaseconfig.json
│   │
│   ├── Models/                   ← Domain modelleri (alt klasörlerle organize)
│   ├── ViewModels/               ← MVVM ViewModels (CommunityToolkit.Mvvm)
│   ├── Views/                    ← XAML sayfaları (ContentPage)
│   ├── Services/                 ← İş mantığı servisleri (Interface + Firebase impl)
│   ├── Converters/               ← XAML IValueConverter'lar
│   ├── Behaviors/                ← XAML Behaviors
│   ├── Extensions/               ← Markup extensions (TranslateExtension)
│   ├── Handlers/                 ← Platform-specific native handlers
│   ├── Helpers/                  ← Utility sınıfları (Constants, RateLimiter, vb.)
│   ├── Security/                 ← Güvenlik denetim servisleri
│   ├── Resources/                ← Fonts, Images, Languages, Styles
│   └── Platforms/                ← Android/iOS platform kodu
│
├── KamPay.API/                   ← ASP.NET Core Backend API
│   ├── KamPay.API.csproj         ← TargetFramework: net10.0
│   ├── Program.cs                ← API startup, Firebase Admin SDK, JWT config
│   ├── Controllers/              ← AuthController, ProductsController
│   ├── Services/                 ← Auth/, Products/
│   ├── Repositories/             ← IProductRepository, ProductRepository
│   ├── Middlewares/              ← FirebaseTokenValidationMiddleware
│   ├── Models/                   ← API-specific DTOs
│   ├── firebase-admin.json       ← Firebase Admin SDK credential
│   └── appsettings.json          ← API config (JwtSettings, FirebaseDatabase)
│
└── DOCS/                         ← Proje dokümantasyonu
```

---

## 3. Mimari

### 3.1 Genel Mimari Desen

```
┌─────────────────────────────────────────────────────────┐
│  .NET MAUI Client (MVVM)                                │
│  ┌──────┐  ┌────────────┐  ┌──────────┐                │
│  │Views │→ │ ViewModels  │→ │ Services │                │
│  │(XAML)│  │(Toolkit.Mvvm│  │(Interface│                │
│  └──────┘  └────────────┘  └────┬─────┘                │
│                                  │                      │
│            ┌─────────────────────┼──────────────┐       │
│            ▼                     ▼              ▼       │
│    Firebase Realtime DB   Firebase Storage   KamPay.API │
└────────────────────────────────────────────────┬────────┘
                                                 │
┌────────────────────────────────────────────────▼────────┐
│  ASP.NET Core API                                       │
│  Controllers → Services → Repositories → Firebase DB    │
│  + Firebase Admin SDK (token doğrulama)                 │
│  + JWT Bearer Authentication                            │
└─────────────────────────────────────────────────────────┘
```

### 3.2 MVVM Katmanları

- **View → ViewModel:** Data Binding + `x:Reference` pattern (Syncfusion SfListView için)
- **ViewModel → Service:** Constructor Injection (DI)
- **Service → Firebase:** `FirebaseClient` (firebase-database-dotnet)
- **Bazı servisler → API:** `HttpClient` → `KamPay.API` (ProductApiService)

### 3.3 Servis Kayıt Sırası (MauiProgram.cs)

DI bağımlılık sırası kritiktir. Genel sıra:
1. Configuration (EmailSettings, FirebaseConfig, ApiSettings)
2. Firebase temel servisler (FirebaseClient, FirebaseAuthProvider)
3. Temel servisler (Localization, RealtimeSnapshot)
4. Profile & Notification servisleri
5. Auth servisi
6. Product, Storage, Messaging servisleri
7. ServiceSharing servisleri (Facade pattern)
8. Transaction servisleri (Facade pattern)
9. Payment sistemi (OCP — Strategy + Factory pattern)
10. Koordinatörler (Cache, Validation, Notification, Transaction)
11. UserStateService (en son — tüm bağımlılıklara ihtiyaç duyar)
12. ViewModels (Transient)
13. Pages (Transient)

---

## 4. Klasör Yapısı Detayları

### 4.1 Models/ (Domain Modelleri)

```
Models/
├── Auth/               → ApiLoginResponseDto
├── Configuration/      → AppConfig (EmailSettings, FirebaseConfig, ApiSettings)
├── EventMessages/      → MapLocationUpdateMessage, QRCodeScannedMessage (WeakReferenceMessenger)
├── Messaging/          → Conversation, Message, ScrollToChatMessage
├── Notifications/      → Notification
├── Products/           → Product, Favorite, ProductPagedResponse
├── ServiceSharing/     → ServiceOffer, CustomerServiceRequest, ProviderProposal
├── Social/             → GoodDeedPost, Comment
├── Transactions/       → Transaction, DeliveryQRCode, PaymentModels, TransactionHistory
├── Users/              → User, UserProfile, UserStats, Badge
├── Category.cs         → Kategori modeli
├── SupportTicket.cs
├── SurpriseBox.cs
└── ValidationResult.cs → Genel validasyon sonuç modeli
```

### 4.2 Services/ (İş Mantığı)

Her servis `IXxxService` interface + `FirebaseXxxService` implementation şeklinde organize edilir.

```
Services/
├── Auth/               → IAuthenticationService, FirebaseAuthService
├── Caching/            → ICacheCoordinator, CacheCoordinator, CacheManager
├── Categories/         → ICategoryService, FirebaseCategoryService
├── Configuration/      → IConfigurationService, ConfigurationService
├── Favorites/          → IFavoriteService, FirebaseFavoriteService
├── Features/           → IGoodDeedService, ISurpriseBoxService + Firebase impls
├── Localization/       → ILocalizationService, LocalizationResourceManager
├── Location/           → IReverseGeocodeService, ReverseGeocodeService
├── Messaging/          → IMessagingService, FirebaseMessagingService, MessageMediaCoordinator
├── Notifications/      → INotificationService, INotificationCoordinator + Firebase impls
├── Payment/            → IPaymentProvider, IPaymentProviderFactory (OCP pattern)
│                         CardSimulationProvider, BankTransferSimulationProvider
├── Products/           → IProductService, ProductApiService (API üzerinden)
│   ├── Coordinators/   → IProductImageCoordinator, IProductCreationCoordinator
│   └── Validation/     → IValidationCoordinator, ValidationCoordinator
├── Profile/            → IUserProfileService, IUserStateService + Firebase impls
├── QRCode/             → IQRCodeService, FirebaseQRCodeService
├── Realtime/           → IRealtimeSnapshotService<T>, FirebaseObserverService
├── ServiceSharing/     → IServiceSharingService (Facade) + 6 alt servis
│                         ServiceSharingFacade, ServiceOfferService,
│                         ServiceRequestCrudService, ServiceRequestNegotiationService,
│                         ServiceRequestCompletionService,
│                         ICustomerRequestManager, IProviderProposalManager (Armut modeli)
├── Shared/             → TransactionCompletionHelper, OtpGenerator
├── Storage/            → IStorageService, FirebaseStorageService
└── Transactions/       → ITransactionService (Facade) + 5 alt servis
                          TransactionFacade, TransactionCrudService,
                          TransactionNegotiationService, TransactionPaymentService,
                          TransactionCompletionService, TransactionOrchestrator
```

### 4.3 ViewModels/

```
ViewModels/
├── Auth/               → LoginViewModel, RegisterViewModel
├── Core/               → AppShellViewModel, MainViewModel
├── Features/           → QRCodeViewModel, SurpriseBoxViewModel
├── Messaging/          → ChatViewModel, MessagesViewModel
├── Notifications/      → NotificationsViewModel
├── Products/           → AddProductViewModel, EditProductViewModel, ProductDetailViewModel,
│                         ProductListViewModel, FavoritesViewModel
├── ServiceSharing/     → ServiceSharingViewModel, ServiceRequestsViewModel,
│                         CreateCustomerRequestViewModel, CustomerRequestsListViewModel,
│                         CustomerRequestDetailsViewModel
├── Shared/             → ImageViewerViewModel
├── Social/             → GoodDeedBoardViewModel
├── Transactions/       → OffersViewModel, PaymentViewModel, TradeOfferViewModel
└── Users/              → ProfileViewModel, EditProfileViewModel
```

### 4.4 Views/

Views/ yapısı ViewModels/ ile birebir aynıdır — her ViewModel için `.xaml` + `.xaml.cs` dosya çifti bulunur.

### 4.5 Converters/

```
Converters/
├── Chat/               → ChatConverters, IsCurrentUserConverter, UnreadToIconConverter
├── Generic/            → InvertedBoolConverter, BoolToColorConverter, DateTimeToTimeAgoConverter,
│                         IsNotNullOrEmptyConverter, AllTrueConverter, MissingConverters, vb.
├── Negotiation/        → CanNegotiateConverter, NegotiationStatusTextConverter, vb.
├── Product/            → ProductConverters, ProductPriceConverters, ProductTypeToEmojiConverter, vb.
└── Transaction/        → CanPayConverter, IsPendingConverter, vb.
```

---

## 5. Navigasyon Yapısı

### Shell Yapısı (AppShell.xaml)

```
Shell
├── LoginPage (ShellContent, varsayılan)
└── TabBar "MainApp"
    ├── HomeTab          → ProductListPage (Ana Sayfa / Ürün Listesi)
    ├── ServicesTab       → ServiceSharingPage (Hizmet Paylaşımı)
    ├── GoodDeedTab       → GoodDeedBoardPage (İyilik Panosu)
    ├── MessagesTab       → MessagesPage (Mesajlar)
    └── ProfileTab        → ProfilePage (Profil)
```

### Kayıtlı Rotalar (AppShell.xaml.cs → RegisterRoutes)

Tüm detay sayfaları `Routing.RegisterRoute()` ile kaydedilir:
- Auth: `RegisterPage`
- Products: `AddProductPage`, `EditProductPage`, `ProductDetailPage`
- Messaging: `ChatPage`
- Transactions: `OffersPage`, `TradeOfferView`, `PaymentPage`
- Features: `QRCodeDisplayPage`, `qrscanner`, `SurpriseBoxPage`, `ImageViewerPage`
- Profile: `EditProfilePage`, `NotificationsPage`, `FavoritesPage`
- ServiceSharing: `ServiceSharingPage`, `ServiceRequestsPage`
- Armut Modeli: `CreateCustomerRequestPage`, `CustomerRequestsListPage`, `CustomerRequestDetailsPage`
- Diğer: `myproducts` → ProductListPage

### Navigasyon Paterni

```csharp
// İleri navigasyon
await Shell.Current.GoToAsync(nameof(ProductDetailPage), new Dictionary<string, object> { {"Product", product} });

// Geri navigasyon
await Shell.Current.GoToAsync("..");

// Tab değiştirme
await Shell.Current.GoToAsync("//MainApp");
```

---

## 6. Temel Teknoloji & Kütüphaneler

### MAUI Client (KamPay.csproj)

| Paket | Versiyon | Kullanım |
|-------|---------|----------|
| CommunityToolkit.Maui | 9.0.0 | UI bileşenleri, toast, popup |
| CommunityToolkit.Mvvm | 8.2.2 | MVVM altyapısı ([ObservableProperty], [RelayCommand]) |
| FFImageLoading.Maui | 1.2.4 | Optimize edilmiş görsel yükleme |
| FirebaseDatabase.net | 4.2.0 | Firebase Realtime DB |
| FirebaseStorage.net | 1.0.3 | Firebase Storage |
| FirebaseAuthentication.net | 3.7.2 | Firebase Auth |
| LiteDB | 5.0.21 | Yerel önbellek DB |
| Mapsui.Maui | 5.0.0 | Harita bileşeni |
| SkiaSharp | 3.119.1 | 2D grafik / QR kod oluşturma |
| Syncfusion.Maui.ListView | 33.1.49 | Performanslı liste (SfListView) |
| Syncfusion.Maui.Core | 33.1.49 | Syncfusion core |
| ZXing.Net.Maui | 0.4.0 | QR kod tarama |

### Backend API (KamPay.API.csproj)

| Paket | Versiyon | Kullanım |
|-------|---------|----------|
| FirebaseAdmin | 3.5.0 | Firebase Admin SDK (token doğrulama) |
| FirebaseDatabase.net | 5.0.0 | Firebase Realtime DB |
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.5 | JWT auth |
| Swashbuckle.AspNetCore | 10.1.7 | Swagger UI |

---

## 7. Yapılandırma & Gizli Bilgiler

### MAUI Client Yapılandırması

`appsettings.json` **embedded resource** olarak derlenir ve `Assembly.GetManifestResourceStream()` ile okunur:

```json
{
  "ApiSettings": {
    "RealDeviceApiUrl": "http://192.168.1.5:5011",
    "EmulatorApiUrl": "http://10.0.2.2:5011",
    "LocalhostApiUrl": "http://localhost:5011"
  },
  "EmailSettings": { "SmtpHost": "...", "SmtpPort": 587, ... },
  "FirebaseConfig": { "ApiKey": "...", "DatabaseURL": "...", ... },
  "SyncfusionSettings": { "LicenseKey": "..." }
}
```

- Development config önceliklidir (`appsettings.Development.json`)
- `UserSecretsId` ile .NET User Secrets desteği mevcuttur
- Syncfusion lisansı: ortam değişkeni `KAMPAY_SYNCFUSION_LICENSE_KEY` > appsettings.Development > appsettings

### API Yapılandırması

```
appsettings.json → FirebaseDatabase:Url, FirebaseDatabase:Secret, JwtSettings:Secret
firebase-admin.json → Firebase Admin SDK credential (veya FIREBASE_ADMIN_JSON env var)
```

- `JwtSettings:Secret` zorunludur — yoksa uygulama başlamaz
- API portu: `http://0.0.0.0:5011`

### Güvenli Depolama (SecureStorage)

Kullanıcı oturumu SecureStorage'da saklanır:
- `secure_user_id`, `secure_user_email`, `secure_firebase_token`
- `secure_remember_me`, `secure_token_expiry`

---

## 8. Önemli Tasarım Kalıpları

### 8.1 Facade Pattern (Karmaşık Servisler)

`IServiceSharingService` → `ServiceSharingFacade` (6 alt servisi orkestre eder)
`ITransactionService` → `TransactionFacade` (5 alt servisi orkestre eder)

### 8.2 OCP / Strategy Pattern (Ödeme)

```csharp
IPaymentProvider (interface)
├── CardSimulationProvider
└── BankTransferSimulationProvider

IPaymentProviderFactory → PaymentProviderFactory (IEnumerable<IPaymentProvider> alır)
```

Yeni ödeme yöntemi eklemek için: yeni `IPaymentProvider` implement et + DI'ye kaydet.

### 8.3 Coordinator Pattern

Orkestrasyon mantığı Coordinator'lara ayrılmıştır:
- `ICacheCoordinator` → Ürün cache yönetimi
- `IValidationCoordinator` → Ürün validasyon
- `INotificationCoordinator` → Bildirim yönetimi
- `ITransactionOrchestrator` → İşlem orkestrasyon
- `IProductImageCoordinator` → Görsel yükleme koordinasyonu
- `IProductCreationCoordinator` → Ürün oluşturma koordinasyonu

### 8.4 Armut Modeli (Hizmet Paylaşımı)

Müşteri talep → Profesyonel teklif akışı:
- `ICustomerRequestManager` → Müşteri taleplerini yönetir
- `IProviderProposalManager` → Profesyonel tekliflerini yönetir

### 8.5 WeakReferenceMessenger (Event Bus)

Sayfalar arası iletişim için CommunityToolkit.Mvvm `WeakReferenceMessenger` kullanılır:
- `MapLocationUpdateMessage` → Harita konum güncellemesi
- `QRCodeScannedMessage` → QR kod tarama sonucu
- `ScrollToChatMessage` → Chat scroll komutu

---

## 9. Firebase Yapısı

### Koleksiyon Yolları (Constants.cs)

| Koleksiyon | Anahtar |
|-----------|---------|
| `users` | Kullanıcı profilleri |
| `products` | Ürünler |
| `categories` | Kategoriler |
| `conversations` | Mesajlaşma konuşmaları |
| `messages` | Mesajlar |
| `favorites` | Favoriler |
| `notifications` | Bildirimler |
| `transactions` | İşlemler (alım/satım) |
| `delivery_qrcodes` | Teslimat QR kodları |
| `surprise_boxes` | Sürpriz kutular |
| `good_deed_posts` | İyilik panosu gönderileri |
| `service_offers` | Hizmet ilanları |
| `service_requests` | Hizmet talepleri |
| `customer_service_requests` | Müşteri talepleri (Armut modeli) |
| `provider_proposals` | Profesyonel teklifleri (Armut modeli) |

### Storage Yolları

- `product_images/` → Ürün görselleri
- `profile_images/` → Profil görselleri
- `message_images/` → Mesaj görselleri
- `deliveries/` → Teslimat fotoğrafları

### Gerekli Firebase Indexler

Firebase Console'da şu indexler tanımlanmalıdır:
- `products` → `CategoryId`, `CreatedAt`, `Type`, `Price`, `UserId`
- `service_offers` → `Category`, `CreatedAt`, `ProviderId`
- `customer_service_requests` → `Category`, `CreatedAt`, `CustomerId`, `Status`
- `provider_proposals` → `CustomerRequestId`, `ProviderId`, `Status`, `CreatedAt`
- `good_deed_posts` → `Type`, `CreatedAt`, `UserId`
- `transactions` → `SellerId`, `BuyerId`, `Status`, `CreatedAt`

---

## 10. Tema & Stil Sistemi

### Renk Paleti (Colors.xaml)

- **Primary:** `#1E88E5` (Modern Mavi)
- **PrimaryDark:** `#1565C0`
- **PrimaryLight:** `#42A5F5`
- **Secondary:** `#26C6DA` (Cyan)
- **Background:** `#F5F9FC`
- **Surface:** `#FFFFFF`
- **TextPrimary:** `#212121`
- **TextSecondary:** `#757575`
- **Success:** `#66BB6A`, **Warning:** `#FFA726`, **Error:** `#EF5350`

### Global Stiller (App.xaml)

- `ModernCard` (Frame), `ModernEntry`, `ModernButton`, `SecondaryButton`
- `TitleLabel`, `SubtitleLabel`
- Tüm converter'lar global olarak `App.xaml`'de kayıtlıdır

### Fontlar

- OpenSans-Regular, OpenSans-Semibold
- MaterialIcons-Regular (Material Design ikonları)

---

## 11. Çoklu Dil Desteği

- `Resources/Languages/AppResources.resx` (Türkçe — varsayılan)
- `Resources/Languages/AppResources.en.resx` (İngilizce)
- `LocalizationResourceManager` singleton servisi
- XAML'de kullanım: `{extensions:Translate Key}` markup extension
- Dil tercihi: `Preferences.Get("AppLanguage", "tr")`
- PublicResXFileCodeGenerator ile derleme zamanı kod üretimi

---

## 12. API Yapısı (KamPay.API)

### Endpoints

- `POST /api/auth/login` → Firebase token ile JWT döner
- `GET/POST/PUT/DELETE /api/products` → Ürün CRUD
- Swagger UI: Development modunda `/swagger` adresinde aktif

### Middleware Pipeline

```
Request → FirebaseTokenValidationMiddleware → Authentication → Authorization → Controllers
```

### Güvenlik

- Firebase ID Token doğrulama (Firebase Admin SDK)
- JWT Bearer token (API kendi token'ı)
- `ServerCertificateCustomValidationCallback` → Development'ta SSL bypass

---

## 13. Kritik Kurallar & Dikkat Edilecekler

### Kodlama Kuralları

1. **Interface-first:** Her servis önce interface tanımlanır, sonra implementation
2. **DI sırası önemlidir:** `MauiProgram.cs`'deki kayıt sırası bağımlılık zincirine göre ayarlanmıştır
3. **Singleton vs Transient:**
   - Servisler → `Singleton` (uygulama boyunca tek instance)
   - ViewModels → `Transient` (her navigasyonda yeni instance)
   - Pages → `Transient`
   - İstisna: `AppShellViewModel` → Singleton
4. **Namespace = Klasör yapısı:** `KamPay.Services.Auth`, `KamPay.Models.Products`, vb.

### XAML Kuralları

1. **SfListView binding:** `x:Reference` pattern kullanılır, doğrudan `{Binding}` yerine
2. **Converter'lar:** Yenisi eklenince `App.xaml`'e global kayıt yapılmalı
3. **Tüm converter'lar** `Converters/` altında kategorize edilir

### Güvenlik Kuralları

1. **Hassas veriler** `appsettings.Development.json` veya User Secrets'ta tutulmalı
2. **SecureStorage** kullanıcı oturum bilgileri için kullanılır (Preferences DEĞİL)
3. `Constants.cs`'deki Firebase URL'i **legacy referans** — asıl config `appsettings.json`'dan gelir
4. Production'da SSL doğrulama aktif olmalı

### Bilinen Durumlar

1. **Ödeme sistemi simülasyondur** — gerçek ödeme entegrasyonu yoktur
2. **Android Custom Handlers** (Glide, RecyclerView) şu an kapalıdır (yorum satırında)
3. **Syncfusion lisansı** gereklidir — lisans olmadan watermark görünür
4. Chat'te `OnSleep`'de 30 dk'dan eski önbellek temizlenir
5. Üniversite e-posta domaini: `@bartin.edu.tr`

### Puan Sistemi

| Aksiyon | Puan |
|---------|------|
| Ürün ekleme | 5 |
| Ürün satma | 10 |
| Bağış yapma | 15 |
| Satın alma | 5 |
| Sürpriz kutu | 20 |
| Hizmet teklifi | 10 |

### Ürün Kuralları

- Maks. 5 görsel, maks. 5MB/görsel
- Başlık: maks. 100 karakter
- Açıklama: maks. 1000 karakter
- Mesaj: maks. 500 karakter, sayfalama: 50/sayfa

---

## 14. Geliştirme Ortamı

### Ön Koşullar

- .NET 10 SDK (`global.json`: 10.0.100)
- Visual Studio 2022+ veya Rider
- Android SDK (API 21+, target API 36)
- iOS 14+ (Xcode gerekli)

### API Çalıştırma

```bash
cd KamPay.API
dotnet run   # http://0.0.0.0:5011
```

### MAUI Çalıştırma

```bash
cd KamPay
dotnet build -f net10.0-android
# veya Visual Studio'dan doğrudan çalıştırılır
```

### API URL Yapılandırması

- **Gerçek cihaz:** `RealDeviceApiUrl` (bilgisayarın yerel IP'si, ör. `192.168.1.5:5011`)
- **Android Emülatör:** `EmulatorApiUrl` (`10.0.2.2:5011`)
- **Windows:** `LocalhostApiUrl` (`localhost:5011`)

---

## 15. Dosya Referans Haritası

### En Sık Değiştirilen Dosyalar

| Dosya | Açıklama |
|-------|----------|
| `MauiProgram.cs` | DI kayıtları — yeni servis/VM/Page eklerken burası güncellenir |
| `AppShell.xaml.cs` | Yeni rota eklerken `RegisterRoutes()` güncellenir |
| `App.xaml` | Yeni converter eklerken global kayıt yapılır |
| `Constants.cs` | Yeni Firebase koleksiyonu veya sabit eklerken |
| `Colors.xaml` | Yeni renk tanımı eklerken |

### Büyük / Karmaşık Dosyalar (dikkatli düzenlenmeli)

| Dosya | ~Satır | Not |
|-------|--------|-----|
| `ChatViewModel.cs` | ~1500 | Realtime mesajlaşma, medya, önbellek |
| `OffersViewModel.cs` | ~1600 | Teklif/pazarlık/ödeme akışı |
| `ProductDetailViewModel.cs` | ~1100 | Ürün detay + satın alma akışları |
| `QRCodeViewModel.cs` | ~1100 | QR oluşturma/tarama/doğrulama |
| `FirebaseAuthService.cs` | ~1400 | Tüm auth işlemleri |
| `ServiceSharingPage.xaml` | ~1000 | Karmaşık hizmet paylaşımı UI |
