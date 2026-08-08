# KamPay - Proje Bağlam Dosyası (claude.md)

> **Son Güncelleme:** 2026-05-05
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
├── KamPay.sln
├── global.json                   ← SDK: net10.0, rollForward: latestMajor
├── claude.md                     ← BU DOSYA
│
├── KamPay/                       ← .NET MAUI Mobil Uygulama
│   ├── KamPay.csproj             ← TargetFrameworks: net10.0-android;net10.0-ios
│   ├── MauiProgram.cs            ← DI Container, tüm servis kayıtları
│   ├── App.xaml / App.xaml.cs   ← Uygulama yaşam döngüsü, global styles, auto-login
│   ├── AppShell.xaml / .cs      ← Shell navigasyon, TabBar, route kayıtları
│   ├── appsettings.json          ← Embedded resource (gerçek veriler)
│   ├── appsettings.Development.json ← Dev override
│   ├── Models/
│   ├── ViewModels/
│   ├── Views/
│   ├── Services/
│   ├── Converters/
│   ├── Behaviors/
│   ├── Extensions/
│   ├── Handlers/
│   ├── Helpers/
│   ├── Security/
│   ├── Resources/
│   └── Platforms/
│
├── KamPay.API/                   ← ASP.NET Core Backend API
│   ├── KamPay.API.csproj         ← TargetFramework: net10.0
│   ├── Program.cs
│   ├── Controllers/              ← AuthController, ProductsController
│   ├── Services/                 ← Auth/, Products/
│   ├── Repositories/
│   ├── Middlewares/
│   ├── Models/
│   ├── firebase-admin.json
│   └── appsettings.json
│
└── DOCS/
```

---

## 3. Mimari

### 3.1 Genel Mimari Desen

```
.NET MAUI Client (MVVM)
  Views (XAML) → ViewModels (CommunityToolkit.Mvvm) → Services (Interface)
                                                             ↓
                              Firebase Realtime DB / Firebase Storage / KamPay.API

ASP.NET Core API
  Controllers → Services → Repositories → Firebase DB
  + Firebase Admin SDK (token doğrulama) + JWT Bearer Authentication
```

### 3.2 MVVM Katmanları

- **View → ViewModel:** Data Binding + `x:Reference` pattern (Syncfusion SfListView için)
- **ViewModel → Service:** Constructor Injection (DI)
- **Service → Firebase:** `FirebaseClient` (firebase-database-dotnet)
- **Bazı servisler → API:** `HttpClient` → `KamPay.API` (ProductApiService)

### 3.3 Servis Kayıt Sırası (MauiProgram.cs)

1. Configuration (EmailSettings, FirebaseConfig, ApiSettings)
2. Firebase temel servisler (FirebaseClient, FirebaseAuthProvider)
3. Temel servisler (Localization, RealtimeSnapshot)
4. Profile & Notification servisleri
5. Auth servisi
6. Product, Storage, Messaging servisleri
7. CampusGuide servisleri
8. ServiceSharing servisleri (Facade pattern)
9. Transaction servisleri (Facade pattern)
10. Payment sistemi (OCP — Strategy + Factory pattern)
11. Koordinatörler (Cache, Validation, Notification, Transaction)
12. UserStateService (en son)
13. ViewModels (Transient)
14. Pages (Transient)

---

## 4. Klasör Yapısı Detayları

### 4.1 Models/

```
Models/
├── Auth/               → ApiLoginResponseDto
├── CampusGuide/        → MicroBusiness, Campaign, BusinessRegistrationRequest, CampusGuideCacheSnapshot
├── Configuration/      → AppConfig (EmailSettings, FirebaseConfig, ApiSettings)
├── EventMessages/      → MapLocationUpdateMessage, QRCodeScannedMessage, ScrollToChatMessage
├── Messaging/          → Conversation, Message
├── Notifications/      → Notification
├── Products/           → Product, Favorite, ProductPagedResponse
├── ServiceSharing/     → ServiceOffer, CustomerServiceRequest, ProviderProposal
├── Social/             → GoodDeedPost, Comment
├── Transactions/       → Transaction, DeliveryQRCode, PaymentModels, TransactionHistory
├── Users/              → User, UserProfile, UserStats, Badge
├── Category.cs
├── SupportTicket.cs
├── SurpriseBox.cs
└── ValidationResult.cs
```

### 4.2 Services/

Her servis `IXxxService` interface + implementation şeklinde organize edilir.

```
Services/
├── Auth/               → IAuthenticationService, FirebaseAuthService
├── Caching/            → ICacheCoordinator, CacheCoordinator, CacheManager, LocalDatabaseService
├── CampusGuide/        → IMicroBusinessService, ICampaignService,
│                         IBusinessManagementService, ICampaignManagementService,
│                         IBusinessRegistrationService, FirebaseCampusGuideCache
│                         + Firebase impls (5 servis)
├── Categories/         → ICategoryService, FirebaseCategoryService
├── Configuration/      → IConfigurationService, ConfigurationService
├── Email/              ← Klasör mevcut ama BOŞ (IEmailService kaldırıldı)
├── Favorites/          → IFavoriteService, FirebaseFavoriteService
├── Features/           → IGoodDeedService, ISurpriseBoxService + Firebase impls
├── Localization/       → ILocalizationService, LocalizationResourceManager
├── Location/           → IReverseGeocodeService, ReverseGeocodeService
├── Messaging/          → IMessagingService, IMessageCommandService,
│                         IMessageQueryService, IMessageMediaCoordinator,
│                         FirebaseMessagingService, MessageMediaCoordinator
├── Notifications/      → INotificationService, INotificationCoordinator + Firebase impls
├── Payment/            → IPaymentProvider, IPaymentProviderFactory (OCP)
│                         CardSimulationProvider, BankTransferSimulationProvider
├── Products/           → IProductService, ProductApiService
│   ├── Coordinators/   → IProductImageCoordinator, IProductCreationCoordinator
│   └── Validation/     → IValidationCoordinator, ValidationCoordinator
├── Profile/            → IUserProfileService, IUserStateService + Firebase impls
├── QRCode/             → IQRCodeService, FirebaseQRCodeService
├── Realtime/           → IRealtimeSnapshotService<T>, FirebaseObserverService
├── ServiceSharing/     → IServiceSharingService (Facade) + alt servisler:
│                         ServiceSharingFacade, ServiceOfferService,
│                         ServiceRequestCrudService, ServiceRequestNegotiationService,
│                         ServiceRequestCompletionService, ServiceReviewService,
│                         ICustomerRequestManager (CustomerRequestManager),
│                         IProviderProposalManager (ProviderProposalManager),
│                         IServiceReviewService, IServicePaymentService,
│                         IServiceNegotiationService, IServiceRequestManagementService,
│                         ICustomerRequestService, IProviderProposalService
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
├── CampusGuide/        → CampusGuideViewModel, BusinessDetailViewModel,
│                         BusinessRegistrationViewModel, BusinessDashboardViewModel,
│                         EditBusinessProfileViewModel, MyCampaignsViewModel,
│                         AdminBusinessApplicationsViewModel
├── Core/               → AppShellViewModel, MainViewModel
├── Features/           → QRCodeViewModel, SurpriseBoxViewModel
├── Messaging/          → ChatViewModel (partial: .cs + .Cache.cs + .Media.cs
│                           + .Messaging.cs + .Negotiation.cs), MessagesViewModel
├── Notifications/      → NotificationsViewModel
├── Products/           → AddProductViewModel, EditProductViewModel,
│                         ProductDetailViewModel, ProductListViewModel, FavoritesViewModel
├── ServiceSharing/     → ServiceSharingViewModel, ServiceRequestsViewModel,
│                         ServiceOfferDetailViewModel, EditServiceOfferViewModel,
│                         CreateCustomerRequestViewModel, CustomerRequestsListViewModel,
│                         CustomerRequestDetailsViewModel
├── Shared/             → ImageViewerViewModel
├── Social/             → GoodDeedBoardViewModel, GoodDeedPostDetailViewModel,
│                         EditGoodDeedPostViewModel
├── Transactions/       → OffersViewModel, PaymentViewModel, TradeOfferViewModel
└── Users/              → ProfileViewModel, EditProfileViewModel
```

### 4.4 Views/

Views/ ViewModels/ ile birebir eşleşir — her VM için `.xaml` + `.xaml.cs` çifti.

```
Views/
├── Auth/               → LoginPage, RegisterPage
├── CampusGuide/        → CampusGuidePage, BusinessDetailPage, BusinessRegistrationPage,
│                         BusinessDashboardPage, EditBusinessProfilePage,
│                         MyCampaignsPage, AdminBusinessApplicationsPage
├── Core/               → MainPage
├── Features/           → QRCodeDisplayPage, QRScannerPage, SurpriseBoxPage
├── Messaging/          → ChatPage, MessagesPage, NegotiationMessagesPage
├── Notifications/      → NotificationsPage
├── Products/           → ProductListPage, AddProductPage, EditProductPage,
│                         ProductDetailPage, FavoritesPage
├── ServiceSharing/     → ServiceSharingPage, ServiceRequestsPage,
│                         ServiceOfferDetailPage, EditServiceOfferPage,
│                         CreateCustomerRequestPage, CustomerRequestsListPage,
│                         CustomerRequestDetailsPage
├── Shared/             → ImageViewerPage
├── Social/             → GoodDeedBoardPage, GoodDeedPostDetailPage, EditGoodDeedPostPage
├── Transactions/       → OffersPage, TradeOfferView, PaymentPage
└── Users/              → ProfilePage, EditProfilePage
```

### 4.5 Converters/

```
Converters/
├── Chat/               → ChatConverters, IsCurrentUserConverter, UnreadToIconConverter
├── Generic/            → InvertedBoolConverter, BoolToColorConverter, BoolToOpacityConverter,
│                         DateTimeToTimeAgoConverter, IsNotNullOrEmptyConverter, AllTrueConverter, vb.
├── Negotiation/        → CanNegotiateConverter, NegotiationStatusTextConverter, vb.
├── Product/            → ProductConverters, ProductPriceConverters, ProductTypeToEmojiConverter, vb.
└── Transaction/        → CanPayConverter, IsPendingConverter, vb.
```

---

## 5. Navigasyon Yapısı

### Shell Yapısı (AppShell.xaml) — 6 Tab

```
Shell
├── LoginPage (ShellContent, varsayılan)
└── TabBar "MainApp"
    ├── HomeTab          → ProductListPage
    ├── ServicesTab      → [ServiceSharingPage, CustomerRequestsListPage]
    ├── GoodDeedTab      → GoodDeedBoardPage
    ├── CampusGuideTab   → CampusGuidePage          ← YENİ MODÜL
    ├── MessagesTab      → [MessagesPage, NegotiationMessagesPage]
    └── ProfileTab       → ProfilePage
```

### Kayıtlı Rotalar (AppShell.xaml.cs → RegisterRoutes)

```
Auth:           RegisterPage
Products:       AddProductPage, EditProductPage, ProductDetailPage
Profile:        EditProfilePage, NotificationsPage, FavoritesPage
Messaging:      ChatPage
Transactions:   OffersPage, TradeOfferView, PaymentPage
Features:       QRCodeDisplayPage, qrscanner (QRScannerPage), SurpriseBoxPage, ImageViewerPage
Social:         GoodDeedBoardPage, GoodDeedPostDetailPage, EditGoodDeedPostPage
ServiceSharing: ServiceSharingPage, ServiceRequestsPage, ServiceOfferDetailPage,
                EditServiceOfferPage, CreateCustomerRequestPage,
                CustomerRequestsListPage, CustomerRequestDetailsPage
CampusGuide:    BusinessDetailPage, BusinessRegistrationPage, BusinessDashboardPage,
                EditBusinessProfilePage, MyCampaignsPage, AdminBusinessApplicationsPage
Diğer:          myproducts → ProductListPage
```

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

`appsettings.json` **embedded resource** olarak derlenir ve `Assembly.GetManifestResourceStream()` ile okunur.
`appsettings.Development.json` önceliklidir (override eder).

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

> **NOT:** `appsettings.Example.json` ve `firebaseconfig.json` (placeholder) kaldırıldı.

- `UserSecretsId` ile .NET User Secrets desteği mevcuttur
- Syncfusion lisansı: env `KAMPAY_SYNCFUSION_LICENSE_KEY` > appsettings.Development > appsettings

### API Yapılandırması

```
appsettings.json → FirebaseDatabase:Url, FirebaseDatabase:Secret, JwtSettings:Secret
firebase-admin.json → Firebase Admin SDK credential (veya FIREBASE_ADMIN_JSON env var)
```

- `JwtSettings:Secret` zorunludur — yoksa uygulama başlamaz
- API portu: `http://0.0.0.0:5011`

### Güvenli Depolama (SecureStorage)

- `secure_user_id`, `secure_user_email`, `secure_firebase_token`
- `secure_remember_me`, `secure_token_expiry`

---

## 8. Önemli Tasarım Kalıpları

### 8.1 Facade Pattern

- `IServiceSharingService` → `ServiceSharingFacade` (çok sayıda alt servis orkestre eder)
- `ITransactionService` → `TransactionFacade` (5 alt servis)

### 8.2 OCP / Strategy Pattern (Ödeme)

```csharp
IPaymentProvider → CardSimulationProvider | BankTransferSimulationProvider
IPaymentProviderFactory → PaymentProviderFactory
```

### 8.3 Coordinator Pattern

- `ICacheCoordinator`, `IValidationCoordinator`, `INotificationCoordinator`
- `ITransactionOrchestrator`, `IProductImageCoordinator`, `IProductCreationCoordinator`

### 8.4 Armut Modeli (Hizmet Paylaşımı)

- `ICustomerRequestManager` → Müşteri talepleri
- `IProviderProposalManager` → Profesyonel teklifleri

### 8.5 Partial Class Pattern (ChatViewModel)

`ChatViewModel` karmaşıklığı nedeniyle partial class olarak bölünmüştür:
- `ChatViewModel.cs` — ana sınıf, init, lifecycle
- `ChatViewModel.Cache.cs` — önbellek yönetimi
- `ChatViewModel.Media.cs` — medya işlemleri
- `ChatViewModel.Messaging.cs` — mesaj gönderme/alma
- `ChatViewModel.Negotiation.cs` — pazarlık akışı

### 8.6 WeakReferenceMessenger (Event Bus)

- `MapLocationUpdateMessage`, `QRCodeScannedMessage`, `ScrollToChatMessage`

---

## 9. Firebase Yapısı

### Koleksiyon Yolları

| Koleksiyon | Açıklama |
|-----------|----------|
| `users` | Kullanıcı profilleri |
| `products` | Ürünler |
| `categories` | Kategoriler |
| `conversations` | Mesajlaşma konuşmaları |
| `messages` | Mesajlar |
| `favorites` | Favoriler |
| `notifications` | Bildirimler |
| `transactions` | İşlemler |
| `delivery_qrcodes` | Teslimat QR kodları |
| `surprise_boxes` | Sürpriz kutular |
| `good_deed_posts` | İyilik panosu gönderileri |
| `service_offers` | Hizmet ilanları |
| `service_requests` | Hizmet talepleri |
| `customer_service_requests` | Müşteri talepleri (Armut) |
| `provider_proposals` | Profesyonel teklifleri (Armut) |
| `micro_businesses` | Kampüs mikro işletmeleri |
| `campaigns` | İşletme kampanyaları |
| `business_registrations` | İşletme başvuruları |

### Storage Yolları

- `product_images/`, `profile_images/`, `message_images/`, `deliveries/`

### Gerekli Firebase Indexler

- `products` → `CategoryId`, `CreatedAt`, `Type`, `Price`, `UserId`
- `service_offers` → `Category`, `CreatedAt`, `ProviderId`
- `customer_service_requests` → `Category`, `CreatedAt`, `CustomerId`, `Status`
- `provider_proposals` → `CustomerRequestId`, `ProviderId`, `Status`, `CreatedAt`
- `good_deed_posts` → `Type`, `CreatedAt`, `UserId`
- `transactions` → `SellerId`, `BuyerId`, `Status`, `CreatedAt`
- `micro_businesses` → `Category`, `IsVerified`, `OwnerId`
- `campaigns` → `BusinessId`, `IsActive`, `ExpiresAt`

---

## 10. Tema & Stil Sistemi

### Renk Paleti (Colors.xaml)

- **Primary:** `#1E88E5` | **PrimaryDark:** `#1565C0` | **PrimaryLight:** `#42A5F5`
- **Secondary:** `#26C6DA` (Cyan) | **Background:** `#F5F9FC` | **Surface:** `#FFFFFF`
- **TextPrimary:** `#212121` | **TextSecondary:** `#757575`
- **Success:** `#66BB6A` | **Warning:** `#FFA726` | **Error:** `#EF5350`

### Global Stiller (App.xaml)

- `ModernCard` (Frame), `ModernEntry`, `ModernButton`, `SecondaryButton`
- `TitleLabel`, `SubtitleLabel`
- Tüm converter'lar global olarak `App.xaml`'de kayıtlıdır

### Fontlar

- OpenSans-Regular, OpenSans-Semibold
- MaterialIcons-Regular

---

## 11. Çoklu Dil Desteği

- `Resources/Languages/AppResources.resx` (Türkçe — varsayılan)
- `Resources/Languages/AppResources.en.resx` (İngilizce)
- XAML kullanım: `{extensions:Translate Key}`
- Dil tercihi: `Preferences.Get("AppLanguage", "tr")`

---

## 12. API Yapısı (KamPay.API)

### Endpoints

- `POST /api/auth/login` → Firebase token ile JWT döner
- `GET/POST/PUT/DELETE /api/products` → Ürün CRUD
- Swagger UI: Development'ta `/swagger`

### Middleware Pipeline

```
Request → FirebaseTokenValidationMiddleware → Authentication → Authorization → Controllers
```

---

## 13. Kritik Kurallar & Dikkat Edilecekler

### Kodlama Kuralları

1. **Interface-first:** Her servis önce interface, sonra implementation
2. **DI sırası önemlidir** — `MauiProgram.cs` bağımlılık zincirine göre sıralanmış
3. **Singleton vs Transient:**
   - Servisler → `Singleton`
   - ViewModels → `Transient` (istisna: `AppShellViewModel` → Singleton)
   - Pages → `Transient`
4. **Namespace = Klasör yapısı:** `KamPay.Services.Auth`, `KamPay.Models.CampusGuide`, vb.

### XAML Kuralları

1. **SfListView binding:** `x:Reference` pattern — doğrudan `{Binding}` DEĞİL
2. **Yeni converter:** Eklenince `App.xaml`'e global kayıt yapılmalı
3. **Tüm converter'lar** `Converters/` altında kategorize edilir

### Güvenlik Kuralları

1. Hassas veriler → `appsettings.Development.json` veya User Secrets
2. **SecureStorage** kullanıcı oturumu için (Preferences DEĞİL)
3. Production'da SSL doğrulama aktif olmalı

### Bilinen Durumlar

1. **Ödeme sistemi simülasyondur** — gerçek entegrasyon yok
2. **Android Custom Handlers** (Glide, RecyclerView) yorum satırında
3. **Syncfusion lisansı** gereklidir — lisanssız watermark çıkar
4. **`Email/` klasörü boştur** — IEmailService kaldırıldı (Firebase Auth'a geçildi)
5. Üniversite e-posta: `@bartin.edu.tr`

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
- Başlık: maks. 100 karakter | Açıklama: maks. 1000 karakter
- Mesaj: maks. 500 karakter | Sayfalama: 50/sayfa

---

## 14. Geliştirme Ortamı

- .NET 10 SDK (`global.json`: 10.0.100)
- Visual Studio 2022+ veya Rider
- Android SDK (API 21+, target API 36)

```bash
# API çalıştırma
cd KamPay.API && dotnet run   # http://0.0.0.0:5011

# MAUI build
cd KamPay && dotnet build -f net10.0-android
```

**API URL:** Gerçek cihaz → `RealDeviceApiUrl` | Emülatör → `EmulatorApiUrl` | Windows → `LocalhostApiUrl`

---

## 15. Dosya Referans Haritası

### En Sık Değiştirilen Dosyalar

| Dosya | Açıklama |
|-------|----------|
| `MauiProgram.cs` | DI kayıtları — yeni servis/VM/Page eklerken |
| `AppShell.xaml.cs` | Yeni rota → `RegisterRoutes()` güncellenir |
| `App.xaml` | Yeni converter → global kayıt |
| `Constants.cs` | Yeni Firebase koleksiyonu veya sabit |
| `Colors.xaml` | Yeni renk tanımı |

### Büyük / Karmaşık Dosyalar

| Dosya | ~Satır | Not |
|-------|--------|-----|
| `ChatViewModel.cs` (+partials) | ~3000 toplam | Partial class — dikkatli düzenlenmeli |
| `GoodDeedBoardViewModel.cs` | ~1500 | Sosyal özellikler + realtime |
| `OffersViewModel.cs` | ~1600 | Teklif/pazarlık/ödeme akışı |
| `ServiceRequestsViewModel.cs` | ~1400 | Hizmet talep yönetimi |
| `CustomerRequestsListPage.xaml` | ~1000 | Armut modeli UI |
| `ProductDetailViewModel.cs` | ~1100 | Ürün detay + satın alma |
| `QRCodeViewModel.cs` | ~1100 | QR oluşturma/tarama/doğrulama |
| `FirebaseAuthService.cs` | ~1400 | Tüm auth işlemleri |
| `ServiceSharingPage.xaml` | ~1000 | Hizmet paylaşımı UI |
| `MessagesViewModel.cs` | ~1000 | Mesajlaşma listesi |

---

## 16. Negotiation System (2026-05-05)

Bu oturumda ürün alış/satış ve takas pazarlığı için kalıcı teklif zinciri mimarisi eklendi. Yeni çalışmalarda pazarlık durumunu yalnızca `Transaction` üzerindeki legacy scalar alanlardan okumak yerine aktif teklif + geçmiş modelini birlikte kullan.

### Ana Model ve Koleksiyon

- `Models/Transactions/NegotiationOffer.cs` yeni pazarlık teklif modelidir.
- `OfferStatus`: `Active`, `Accepted`, `Rejected`, `Expired`, `Superseded`, `Cancelled`.
- `ProposerRole`: `Buyer`, `Seller`.
- Firebase koleksiyonu: `negotiation_offers`.
- `Constants.NegotiationOffersCollection = "negotiation_offers"`.
- Firebase indexleri: `Status`, `CreatedAt`, `ProposerId`.

### Transaction Entegrasyonu

- `TransactionStatus.Negotiating = 5`, `Expired = 6` olarak tanımlıdır.
- `Transaction.CurrentActiveOfferId` aktif teklifin Firebase id'sini tutar.
- UI için `Transaction.ActiveNegotiationOffer`, `NegotiationOfferHistory`, `HasNegotiationOfferHistory` alanları kullanılır.
- `Transaction.AgreedAmount`, önce aktif kabul edilmiş `NegotiationOffer` tutarını, sonra legacy `NegotiatedPrice` alanını dikkate alır.

### Servis Mimarisi

- `INegotiationOfferService` pazarlık akışı için ana interface'tir.
- `FirebaseNegotiationOfferService` teklif oluşturma, kabul, red, expire, aktif teklif ve geçmiş okuma operasyonlarını yönetir.
- `TransactionNegotiationService` artık doğrudan Firebase yazımı yapan ana servis değil; pazarlık işlemlerini `INegotiationOfferService` üzerinden delege eder.
- Yeni servis `MauiProgram.cs` içinde `AddSingleton<INegotiationOfferService, FirebaseNegotiationOfferService>()` ile kayıtlıdır ve transaction servislerinden önce/uygun sırada enjekte edilir.

### Akış Kuralları

- Her yeni aktif teklif, önceki aktif teklifi `Superseded` yapar.
- Aynı kullanıcı kendi son aktif teklifini kabul edemez.
- Mesajlaşma tarafında pasif/eskimiş teklifler kabul edilemez.
- Kabul/red/expire öncesinde transaction tekrar okunur; `LastActionBy`, `NegotiationRoundCount` ve `CurrentActiveOfferId` ile stale write guard uygulanır.
- Ürün satıldı veya silindi durumlarında bekleyen/negotiating/accepted transaction'lar iptal edilir, aktif pazarlık teklifleri expire edilir ve ilgili alıcılara bildirim gönderilir.

### UI ve Converter Notları

- `OffersViewModel`, transaction'ları aktif teklif ve teklif geçmişiyle zenginleştirir.
- `OffersPage.xaml`, gelen/giden teklif kartlarında `NegotiationOfferHistory` listesini gösterir.
- `CanAcceptNegotiationConverter`, kendi teklifini kabul etmeyi engeller ve aktif teklif id'si ile legacy alanları birlikte kontrol eder.
- `NegotiationStatusTextConverter`, aynı anda hem satış hem takas teklifini aktif gibi göstermemek için `LastActionBy` ve aktif teklif bilgisini kullanır.
- `IsPendingOrNegotiatingConverter`, pending ve negotiating durumlarında aksiyon butonlarını gösterir.

### Test ve Dokümantasyon

- Senaryo matrisi: `DOCS/negotiation_system_test_matrix.md`.
- Firebase kuralları/index notları: `DOCS/firebase_rules.md`.
- Doğrulama komutu:

```powershell
dotnet build KamPay\KamPay.csproj -f net10.0-windows10.0.19041.0 --no-restore -v:minimal /clp:ErrorsOnly
```

---

## 17. Chat Realtime System (2026-05-05)

Bu oturumda chat sistemi icin realtime, lifecycle, servis ayrimi ve negotiation chat route duzenlemeleri yapildi. Yeni calismalarda mesaj yuklemeyi yalnizca Firebase `AsObservable()` initial dump akisini beklemeye birakma; once snapshot, sonra realtime listener kullan.

### Ana Dosyalar

- `Services/Messaging/IChatRealtimeService.cs`
- `Services/Messaging/ChatRealtimeService.cs`
- `Services/Messaging/IChatCacheService.cs`
- `Services/Messaging/ChatCacheService.cs`
- `Services/Messaging/IChatMediaService.cs`
- `Services/Messaging/ChatMediaService.cs`
- `Services/Messaging/INegotiationChatService.cs`
- `Services/Messaging/NegotiationChatService.cs`
- `ViewModels/Messaging/NegotiationChatViewModel.cs`
- `Views/Messaging/NegotiationChatPage.xaml` / `.xaml.cs`
- `Models/EventMessages/ConnectivityRestoredMessage.cs`

### Realtime Akisi

- `ChatRealtimeService.LoadAndListenAsync()` once son 50 mesaji `OnceAsync + LimitToLast(50)` ile yukler.
- Snapshot UI'a yansidiktan sonra `AsObservable<Message>()` ile realtime dinleme baslar.
- Delete eventleri ve soft-delete (`Message.IsDeleted`) islenir.
- Duplicate mesajlar `_knownMessageIds` ve `_messageLookup` ile elenir.
- Eski mesajlar `LoadOlderMessagesAsync()` ile sayfali yuklenir.

### Lifecycle ve Connectivity

- `ChatPage.OnDisappearing()` artik ViewModel'i dispose etmez; `PauseRealtimeListeners()` cagrilir.
- `ChatPage.OnAppearing()` `ResumeRealtimeListeners()` ile listener'i geri baslatir.
- `App.xaml.cs`, `Connectivity.ConnectivityChanged` ile internet geri geldiginde `ConnectivityRestoredMessage` yollar.
- `ChatViewModel`, `ConnectivityRestoredMessage` aldiginda realtime listener'i yeniden baslatir.

### Chat / Negotiation Ayrimi

- `ChatPage` normal sohbetler icin kullanilir.
- `NegotiationChatPage`, negotiation conversation icin ayri route olarak kayitlidir.
- `MessagesViewModel.ConversationTappedAsync`, `Conversation.IsNegotiationConversation` true ise `NegotiationChatPage`e gider.
- `NegotiationChatViewModel`, `ChatViewModel` kalitimi kullanmaz; ortak mesajlasma islerini kompozisyonla icindeki normal chat oturumuna delege eder, pazarlik state ve komutlarini kendi yonetir.
- `INegotiationChatService`, teklif/kabul/red islemlerinde `INegotiationOfferService` odaklidir; transaction facade uyumluluk ve orkestrasyon katmani olarak kalir.

### Servis Kayitlari

`MauiProgram.cs` icinde su servisler kayitlidir:

```csharp
builder.Services.AddTransient<IChatRealtimeService, ChatRealtimeService>();
builder.Services.AddSingleton<IChatCacheService, ChatCacheService>();
builder.Services.AddTransient<IChatMediaService, ChatMediaService>();
builder.Services.AddTransient<INegotiationChatService, NegotiationChatService>();
builder.Services.AddTransient<IChatNegotiationMigrationService, ChatNegotiationMigrationService>();
builder.Services.AddTransient<NegotiationChatViewModel>();
builder.Services.AddTransient<NegotiationChatPage>();
```

`AppShell.xaml.cs` icinde `NegotiationChatPage` route'u kayitlidir.

### Firebase ve Performans Notlari

- `conversations` indexleri: `User1Id`, `User2Id`, `LastMessageTime`, `ConversationType`, `UpdatedAt`.
- `messages` indexi: `SentAt`.
- `negotiation_offers` indexleri: `Status`, `CreatedAt`, `ProposerId`, `TransactionId`.
- `FirebaseNegotiationOfferService` root-level `PatchAsync` kullanmaz; Firebase SDK uyumlulugu icin multi-path update'leri tek tek `PutAsync` olarak uygular.
- `IChatRealtimeService` transient olmalidir; her chat sayfasi kendi listener state'ini tasir.
- `IChatCacheService` singleton olabilir; cache uygulama boyunca paylasilir.
- Realtime listener eklerken mutlaka dispose/stop yolu ekle.

### Final Clean Architecture Notlari

- `ChatViewModel` artik yalnizca normal sohbet sorumlulugundadir; transaction chipleri, `FilteredMessages`, aktif pazarlik state'i ve teklif komutlari burada bulunmaz.
- `ChatPage.xaml` normal sohbet ekranidir; pazarlik kartlari ve teklif butonlari bu ekranda yer almaz.
- `NegotiationChatPage.xaml`, pazarlik sohbeti icin ayrilmis ekrandir; aktif islem chipleri, teklif kartlari, kabul/red/karsi teklif komutlari burada kalir.
- Legacy general sohbetlerde kalmis `MessageType.Negotiation` mesajlari icin `IChatNegotiationMigrationService.BackfillLegacyNegotiationMessagesAsync()` idempotent backfill saglar. Varsayilan `dryRun = true`; destructive silme yapmaz, mesajlari negotiation conversation'a kopyalar ve transaction `ConversationId` alanini node bazli gunceller.
- Normal chat media akisi `IChatMediaService` uzerinden ilerler; `ChatViewModel.Media` icinde eski unreachable Firebase Storage blogu tutulmaz.
