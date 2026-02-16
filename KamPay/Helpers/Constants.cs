namespace KamPay.Helpers;

public static class Constants
{
    // Firebase Realtime Database URL
    
    public const string FirebaseRealtimeDbUrl = "https://kampay-b006d-default-rtdb.europe-west1.firebasedatabase.app/";

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
    
    // ✅ EKLEME: Ödeme simülasyonu için geçici OTP koleksiyonu
    // Not: Gerçek üretimde bu kısa ömürlü veriler Redis gibi bir cache'de tutulmalıdır
    public const string TempOtpsCollection = "temp_otps";

    // 🎯 ARMUT MODELİ: Yeni Koleksiyonlar
    public const string CustomerServiceRequestsCollection = "customer_service_requests"; // Müşteri talepleri
    public const string ProviderProposalsCollection = "provider_proposals"; // Profesyonel teklifleri

    // Firebase Storage yollar
    public const string ProductImagesFolder = "product_images";
    public const string ProfileImagesFolder = "profile_images";
    public const string MessageImagesFolder = "message_images";
    public const string DeliveryPhotosFolder = "deliveries"; 

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
    ///     "products": {
    ///       ".indexOn": ["CategoryId", "CreatedAt", "Type", "Price", "UserId"]
    ///     },
    ///     "service_offers": {
    ///       ".indexOn": ["Category", "CreatedAt", "ProviderId"]
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
    ///     "transactions": {
    ///       ".indexOn": ["SellerId", "BuyerId", "Status", "CreatedAt"]
    ///     }
    ///   }
    /// }
    /// 
    /// Bu indeksler olmadan sayfalama ve filtreleme ÇALIŞMAZ!
    /// </summary>
    public const string FirebaseIndexingNote = "See documentation above for required Firebase indexes";
}