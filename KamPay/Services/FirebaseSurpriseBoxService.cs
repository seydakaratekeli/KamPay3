using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Helpers;
using KamPay.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KamPay.Services
{
    // Sürpriz kutusu hizmeti - Firebase Realtime Database kullanarak sürpriz kutu işlemlerini yönetir. amacı, kullanıcıların sürpriz kutu açmalarını sağlamak ve ilgili işlemleri gerçekleştirmektir.
    public class FirebaseSurpriseBoxService : ISurpriseBoxService
    {
        private readonly FirebaseClient _firebaseClient;
        private readonly IUserProfileService _userProfileService;
        private readonly IProductService _productService;
        private readonly INotificationService _notificationService;
        private const int BoxCost = 100;

        public FirebaseSurpriseBoxService(
            IUserProfileService userProfileService,
            IProductService productService,
            INotificationService notificationService)
        {
            _firebaseClient = new FirebaseClient(Constants.FirebaseRealtimeDbUrl);
            _userProfileService = userProfileService;
            _productService = productService;
            _notificationService = notificationService;
        }

        public async Task<ServiceResult<Product>> RedeemSurpriseBoxAsync(string userId)
        {
            try
            {
                KamPay.Helpers.AppLogger.DebugLog($"🔍 Sürpriz kutu açılıyor - UserId: {userId}");

                // 1. Kullanıcı bilgilerini al
                var currentUserResult = await _userProfileService.GetUserProfileAsync(userId);
                if (!currentUserResult.Success || currentUserResult.Data == null)
                {
                    KamPay.Helpers.AppLogger.DebugLog($"❌ Kullanıcı profili alınamadı: {currentUserResult.Message}");
                    return ServiceResult<Product>.FailureResult("Kullanıcı bilgisi alınamadı.");
                }
                var currentUser = currentUserResult.Data;
                KamPay.Helpers.AppLogger.DebugLog($"✅ Kullanıcı profili alındı: {currentUser.FullName}");

                // 2. Kullanıcının puanını kontrol et - DEBUG EKLENDI
                var userStatsResult = await _userProfileService.GetUserStatsAsync(userId);

                //  DEBUG: Result kontrolü
                KamPay.Helpers.AppLogger.DebugLog($"🔍 GetUserStatsAsync - Success: {userStatsResult.Success}");
                KamPay.Helpers.AppLogger.DebugLog($"🔍 GetUserStatsAsync - Message: {userStatsResult.Message}");

                if (!userStatsResult.Success)
                {
                    KamPay.Helpers.AppLogger.DebugLog($"❌ İstatistikler alınamadı: {userStatsResult.Message}");
                    return ServiceResult<Product>.FailureResult("Kullanıcı istatistikleri alınamadı.", userStatsResult.Message);
                }

                //  DEBUG: Data null kontrolü
                if (userStatsResult.Data == null)
                {
                    KamPay.Helpers.AppLogger.DebugLog("❌ UserStats Data NULL!");
                    return ServiceResult<Product>.FailureResult("Kullanıcı istatistikleri bulunamadı.");
                }

                var userStats = userStatsResult.Data;

                //  DEBUG: Puan bilgisi
                KamPay.Helpers.AppLogger.DebugLog($"💰 Kullanıcı Puanı: {userStats.Points}");
                KamPay.Helpers.AppLogger.DebugLog($"💰 Gerekli Puan: {BoxCost}");
                KamPay.Helpers.AppLogger.DebugLog($"💰 Yeterli mi?: {userStats.Points >= BoxCost}");

                if (userStats.Points < BoxCost)
                {
                    KamPay.Helpers.AppLogger.DebugLog($"❌ Yetersiz puan! Mevcut: {userStats.Points}, Gerekli: {BoxCost}");
                    return ServiceResult<Product>.FailureResult(
                        "Yetersiz Puan!",
                        $"Bu işlem için {BoxCost} puana ihtiyacınız var. Mevcut puanınız: {userStats.Points}"
                    );
                }

                KamPay.Helpers.AppLogger.DebugLog("✅ Puan kontrolü başarılı, ürünler sorgulanıyor...");

                // 3. Uygun ürünleri sorgula
                var surpriseBoxProducts = await _firebaseClient
                    .Child(Constants.ProductsCollection)
                    .OnceAsync<Product>();

                KamPay.Helpers.AppLogger.DebugLog($"🔍 Toplam ürün sayısı: {surpriseBoxProducts.Count}");

                var availableDonations = surpriseBoxProducts
                    .Where(p => p.Object != null &&
                               p.Object.Type == ProductType.Bagis &&
                                p.Object.IsForSurpriseBox &&
                                !p.Object.IsSold &&
                                p.Object.IsActive &&
                                p.Object.UserId != userId)
                    .Select(p => {
                        p.Object.ProductId = p.Key;
                        return p.Object;
                    })
                    .ToList();

                KamPay.Helpers.AppLogger.DebugLog($"🎁 Sürpriz kutusu için uygun ürün sayısı: {availableDonations.Count}");

                if (availableDonations.Count == 0)
                {
                    return ServiceResult<Product>.FailureResult("Ürün Yok", "Şu anda sürpriz kutusu için uygun bir ürün bulunmuyor.");
                }

                // 4. Rastgele bir ürün seç
                var random = new Random();
                var surpriseProduct = availableDonations[random.Next(availableDonations.Count)];
                var previousOwnerId = surpriseProduct.UserId;
                var previousOwnerName = surpriseProduct.UserName;

                KamPay.Helpers.AppLogger.DebugLog($"🎲 Seçilen ürün: {surpriseProduct.Title}");

                // 5. Puanı düş
                KamPay.Helpers.AppLogger.DebugLog($"💳 {BoxCost} puan düşülüyor...");
                var pointsDeducted = await _userProfileService.AddPointsAsync(
                    userId,
                    -BoxCost,
                    $"Sürpriz Kutu açıldı - {surpriseProduct.Title}"
                );

                if (!pointsDeducted.Success)
                {
                    KamPay.Helpers.AppLogger.DebugLog($"❌ Puan düşülemedi: {pointsDeducted.Message}");
                    return ServiceResult<Product>.FailureResult("Hata", "Puan düşülürken bir sorun oluştu.");
                }

                KamPay.Helpers.AppLogger.DebugLog("✅ Puan başarıyla düşüldü");

                // 6. Ürün sahipliğini güncelle
                var ownerUpdated = await _productService.UpdateProductOwnerAsync(
                    surpriseProduct.ProductId,
                    userId,
                    markAsSold: true
                );

                if (!ownerUpdated.Success)
                {
                    KamPay.Helpers.AppLogger.DebugLog("⚠️ Sahiplik güncellenemedi, puan iade ediliyor...");
                    await _userProfileService.AddPointsAsync(userId, BoxCost, "Sürpriz Kutu hatası (puan iadesi)");
                    return ServiceResult<Product>.FailureResult("Hata", "Ürün sahipliği güncellenemedi. Puanınız iade edildi.");
                }

                // 7. Kullanıcı istatistiklerini güncelle (alıcı için)
                userStats.ItemsShared++;
                await _firebaseClient
                    .Child(Constants.UserStatsCollection)
                    .Child(userId)
                    .PutAsync(userStats);

                // 8. Önceki sahibin istatistiklerini güncelle (bağışçı için)
                var donorStatsResult = await _userProfileService.GetUserStatsAsync(previousOwnerId);
                if (donorStatsResult.Success && donorStatsResult.Data != null)
                {
                    var donorStats = donorStatsResult.Data;
                    donorStats.DonationsMade++;
                    await _firebaseClient
                        .Child(Constants.UserStatsCollection)
                        .Child(previousOwnerId)
                        .PutAsync(donorStats);

                    await _userProfileService.AddPointsAsync(
                        previousOwnerId,
                        50,
                        $"'{surpriseProduct.Title}' sürpriz kutusundan kazanıldı - Teşekkürler!"
                    );
                }

                // 9. Alıcıya bildirim gönder
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = userId,
                    Type = NotificationType.SurpriseBoxWon,
                    Title = "🎁 Sürpriz Kutu Ödülü!",
                    Message = $"Tebrikler! '{surpriseProduct.Title}' ürününü kazandınız. {previousOwnerName} bu ürünü bağışlamıştı.",
                    RelatedEntityId = surpriseProduct.ProductId,
                    RelatedEntityType = "Product",
                    ActionUrl = $"ProductDetailPage?productId={surpriseProduct.ProductId}"
                });

                // 10. Bağışçıya bildirim gönder
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = previousOwnerId,
                    Type = NotificationType.DonationClaimed,
                    Title = "💝 Bağışınız Değerlendirildi!",
                    Message = $"{currentUser.FullName}, sürpriz kutusundan '{surpriseProduct.Title}' ürününüzü kazandı. 50 puan hediye ettik!",
                    RelatedEntityId = surpriseProduct.ProductId,
                    RelatedEntityType = "Product",
                    ActionUrl = null
                });

                // 11. İşlem geçmişi oluştur
                var surpriseBoxTransaction = new
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    Type = "SurpriseBoxRedemption",
                    ProductId = surpriseProduct.ProductId,
                    ProductTitle = surpriseProduct.Title,
                    RecipientId = userId,
                    RecipientName = currentUser.FullName,
                    DonorId = previousOwnerId,
                    DonorName = previousOwnerName,
                    PointsCost = BoxCost,
                    CreatedAt = DateTime.UtcNow
                };

                await _firebaseClient
                    .Child("surprise_box_transactions")
                    .PostAsync(surpriseBoxTransaction);

                // 12. Rozet kontrolü yap
                await CheckAndAwardBadges(userId, userStats);
                if (donorStatsResult.Success && donorStatsResult.Data != null)
                {
                    await CheckAndAwardBadges(previousOwnerId, donorStatsResult.Data);
                }

                KamPay.Helpers.AppLogger.DebugLog($"✅ Sürpriz kutu başarıyla açıldı: {surpriseProduct.Title}");

                return ServiceResult<Product>.SuccessResult(
                    surpriseProduct,
                    $"Tebrikler! {previousOwnerName} tarafından bağışlanan '{surpriseProduct.Title}' ürününü kazandınız!"
                );
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"❌ Sürpriz kutu hatası: {ex.Message}");
                KamPay.Helpers.AppLogger.DebugLog($"❌ StackTrace: {ex.StackTrace}");
                return ServiceResult<Product>.FailureResult("Beklenmedik Hata", ex.Message);
            }
        }

        // Rozet kontrol ve verme sistemi
        // NOT: BadgeId ile kontrol yapılarak FirebaseUserProfileService ile tutarlılık sağlanıyor.
        // BadgeName yerine BadgeId kullanılır çünkü aynı isimde farklı ID'lerle rozet oluşturulabilir.
        private async Task CheckAndAwardBadges(string userId, UserStats stats)
        {
            try
            {
                var badges = await _userProfileService.GetUserBadgesAsync(userId);
                // BadgeId ile kontrol et - FirebaseUserProfileService.CheckAndAwardBadgesAsync ile tutarlı
                var existingBadgeIds = badges.Success && badges.Data != null 
                    ? badges.Data.Select(b => b.BadgeId).ToList() 
                    : new List<string>();

                // Bağış rozetleri - BadgeId ile karşılaştır
                if (stats.DonationsMade >= 1 && !existingBadgeIds.Contains("first_donation"))
                {
                    await CreateAndAwardBadge(userId, "first_donation", "İlk Bağış", "İlk bağışını yaptın! 🎁", "🎁");
                }

                if (stats.DonationsMade >= 5 && !existingBadgeIds.Contains("generous_heart"))
                {
                    await CreateAndAwardBadge(userId, "generous_heart", "Cömert Kalp", "5 bağış yaptın! 💝", "💝");
                }

                if (stats.DonationsMade >= 10 && !existingBadgeIds.Contains("super_donor"))
                {
                    await CreateAndAwardBadge(userId, "super_donor", "Süper Bağışçı", "10 bağış yaptın! 🌟", "🌟");
                }

                // Sürpriz kutu rozetleri - BadgeId ile karşılaştır
                if (stats.ItemsShared >= 1 && !existingBadgeIds.Contains("lucky_one"))
                {
                    await CreateAndAwardBadge(userId, "lucky_one", "Şanslı", "İlk sürpriz kutunu açtın! 🍀", "🍀");
                }

                if (stats.ItemsShared >= 5 && !existingBadgeIds.Contains("box_hunter"))
                {
                    await CreateAndAwardBadge(userId, "box_hunter", "Kutu Avcısı", "5 sürpriz kutu açtın! 🎰", "🎰");
                }
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"⚠️ Rozet kontrolü hatası: {ex.Message}");
            }
        }

        private async Task CreateAndAwardBadge(string userId, string badgeId, string badgeName, string description, string icon)
        {
            try
            {
                var existingBadge = await _firebaseClient
                    .Child(Constants.BadgesCollection)
                    .Child(badgeId)
                    .OnceSingleAsync<Badge>();

                if (existingBadge == null)
                {
                    var newBadge = new Badge
                    {
                        BadgeId = badgeId,
                        Name = badgeName,
                        Description = description,
                        IconName = icon,
                        Color = "#4CAF50",
                        CreatedAt = DateTime.UtcNow
                    };

                    await _firebaseClient
                        .Child(Constants.BadgesCollection)
                        .Child(badgeId)
                        .PutAsync(newBadge);
                }

                await _userProfileService.AwardBadgeAsync(userId, badgeId);
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"⚠️ Badge oluşturma hatası ({badgeName}): {ex.Message}");
            }
        }
    }
}
