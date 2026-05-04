namespace KamPay.Helpers;

public static class Constants
{
   

   

    // üniversite e-posta domain kontrolü için
    public const string UniversityEmailDomain = "@bartin.edu.tr";

    // şifre gereksinimleri
    public const int MinPasswordLength = 8;
    public const int MaxPasswordLength = 50;

    // Firebase koleksiyon yollar
    public const string UsersCollection = "users";
    public const string ProductsCollection = "products";
    public const string CategoriesCollection = "categories";
    public const string ConversationsCollection = "conversations";
    public const string MessagesCollection = "messages";
    public const string FavoritesCollection = "favorites";
    public const string NotificationsCollection = "notifications";
    public const string BadgesCollection = "badges";
    public const string UserBadgesCollection = "user_badges";
    public const string UserStatsCollection = "user_stats";
    public const string DeliveryQRCodesCollection = "delivery_qrcodes";
    public const string SurpriseBoxesCollection = "surprise_boxes";
    public const string GoodDeedPostsCollection = "good_deed_posts";
    public const string ServiceOffersCollection = "service_offers";
    public const string ServiceRequestsCollection = "service_requests";
    public const string TransactionsCollection = "transactions";
    public const string MicroBusinessesCollection = "micro_businesses";
    public const string CampaignsCollection = "campaigns";
    
    // ✅ EKLEME: Ödeme simülasyonu için geçici OTP koleksiyonu
    // Not: Gerçek üretimde bu kısa ömürlü veriler Redis gibi bir cache'de tutulmalıdır
    public const string TempOtpsCollection = "temp_otps";

    // 🎯 ARMUT MODELİ: Yeni Koleksiyonlar
    public const string CustomerServiceRequestsCollection = "customer_service_requests"; // Müşteri talepleri
    public const string ProviderProposalsCollection = "provider_proposals"; // Profesyonel teklifleri
    public const string ServiceReviewsCollection = "service_reviews"; // Hizmet değerlendirmeleri

    // Firebase Storage yollar
    public const string ProductImagesFolder = "product_images";
    public const string ProfileImagesFolder = "profile_images";
    public const string MessageImagesFolder = "message_images";
    public const string DeliveryPhotosFolder = "deliveries"; 
    public const string MicroBusinessImagesFolder = "micro_business_images";

    // ürün kurallar
    public const int MaxProductImages = 5;
    public const int MaxProductTitleLength = 100;
    public const int MaxProductDescriptionLength = 1000;
    public const long MaxImageSizeBytes = 5 * 1024 * 1024; // 5 MB

    // Mesajlaşma kurallar
    public const int MaxMessageLength = 500;
    public const int MessagesPageSize = 50;

    // Puan sistemi
    public const int PointsForProductAdd = 5;
    public const int PointsForProductSold = 10;
    public const int PointsForDonation = 15;
    public const int PointsForPurchase = 5;
    public const int PointsForSurpriseBox = 20;
    public const int PointsForServiceOffer = 10;

    /// <summary>
    /// ⚠️ KRİTİK: Bu indeksler Firebase Console'da tanımlanmalıdır!
    /// 
    /// Firebase Console → Realtime Database → Rules sekmesi → Aşağıdaki kuralları ekleyin:
    /// 
    /// {
    ///   "rules": {
    ///     ".read": "auth != null",
    ///     ".write": "auth != null",
    ///     "products": {
    ///       ".indexOn": ["CategoryId", "CreatedAt", "Type", "Price", "UserId"]
    ///     },
    ///     "service_offers": {
    ///       ".indexOn": ["Category", "CreatedAt", "ProviderId", "IsAvailable"]
    ///     },
    ///     "service_requests": {
    ///       ".indexOn": ["ProviderId", "RequesterId", "Status", "RequestedAt"]
    ///     },
    ///     "customer_service_requests": {
    ///       ".indexOn": ["Category", "CreatedAt", "CustomerId", "Status"]
    ///     },
    ///     "provider_proposals": {
    ///       ".indexOn": ["CustomerRequestId", "ProviderId", "Status", "CreatedAt"]
    ///     },
    ///     "good_deed_posts": {
    ///       ".indexOn": ["Type", "CreatedAt", "UserId"]
    ///     },
    ///     "micro_businesses": {
    ///       ".indexOn": ["IsVerified", "IsActive", "Category", "DisplayOrder", "VerificationStatus", "OwnerUserId"]
    ///     },
    ///     "campaigns": {
    ///       ".indexOn": ["BusinessId", "CreatedByUserId", "IsActive", "Status", "StartsAt", "EndsAt", "DisplayOrder"]
    ///     },
    ///     "transactions": {
    ///       ".indexOn": ["SellerId", "BuyerId", "Status", "CreatedAt"]
    ///     },
    ///     "conversations": {
    ///       ".indexOn": ["User1Id", "User2Id", "UpdatedAt"]
    ///     }
    ///   }
    /// }
    /// 
    /// ⚠️ service_requests koleksiyonuna ProviderId ve RequesterId indexleri ZORUNLUDUR!
    /// ServiceRequestsViewModel.LoadInitialSnapshotAsync bu alanlar üzerinden OrderBy sorgusu yapar.
    /// Index yoksa FirebaseException fırlatılır ve uygulama çökebilir.
    /// Bu indeksler olmadan sayfalama ve filtreleme ÇALIŞMAZ!
    /// </summary>
    public const string FirebaseIndexingNote = "See documentation above for required Firebase indexes";
}
