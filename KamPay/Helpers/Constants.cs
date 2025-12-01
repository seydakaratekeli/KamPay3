namespace KamPay.Helpers;

public static class Constants
{
    // Firebase Realtime Database URL
    
    public const string FirebaseRealtimeDbUrl = "https://kampay-b006d-default-rtdb.europe-west1.firebasedatabase.app/";

    // �niversite e-posta domain kontrol� i�in
    public const string UniversityEmailDomain = "@bartin.edu.tr";

    // �ifre gereksinimleri
    public const int MinPasswordLength = 8;
    public const int MaxPasswordLength = 50;

    // Firebase koleksiyon yollar�
    public const string UsersCollection = "users";
    public const string PendingVerificationsCollection = "pending_verifications";
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
    public const string QRCodesCollection = "delivery_qrcodes";
    public const string SurpriseBoxesCollection = "surprise_boxes";
    public const string GoodDeedPostsCollection = "good_deed_posts";
    public const string ServiceOffersCollection = "service_offers";
    public const string ServiceRequestsCollection = "service_requests";
    public const string TransactionsCollection = "transactions";
    public const string TempOtpsCollection = "TempOtps";
    public const string DisputesCollection = "disputes";
    public const string ServiceRatingsCollection = "service_ratings";
    public const string AdminActionsCollection = "admin_actions";
    public const string PaymentTransactionsCollection = "payment_transactions";

    // Firebase Storage yollar�
    public const string ProductImagesFolder = "product_images";
    public const string ProfileImagesFolder = "profile_images";
    public const string MessageImagesFolder = "message_images";
    public const string DeliveryPhotosFolder = "deliveries"; // FAZ 2

    // �r�n kurallar�
    public const int MaxProductImages = 5;
    public const int MaxProductTitleLength = 100;
    public const int MaxProductDescriptionLength = 1000;
    public const long MaxImageSizeBytes = 5 * 1024 * 1024; // 5 MB

    // Mesajla�ma kurallar�
    public const int MaxMessageLength = 500;
    public const int MessagesPageSize = 50;

    // Puan sistemi
    public const int PointsForProductAdd = 5;
    public const int PointsForProductSold = 10;
    public const int PointsForDonation = 15;
    public const int PointsForPurchase = 5;
    public const int PointsForSurpriseBox = 20;
    public const int PointsForServiceOffer = 10;
}