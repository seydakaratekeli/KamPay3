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
                Console.WriteLine($"🔍 Sürpriz kutu açılıyor - UserId: {userId}");

                // 1. Kullanıcı bilgilerini al
                var currentUserResult = await _userProfileService.GetUserProfileAsync(userId);
                if (!currentUserResult.Success)
                {
                    Console.WriteLine($"❌ Kullanıcı profili alınamadı: {currentUserResult.Message}");
                    return ServiceResult<Product>.FailureResult("Kullanıcı bilgisi alınamadı.");
                }
                var currentUser = currentUserResult.Data;
                Console.WriteLine($"✅ Kullanıcı profili alındı: {currentUser.FullName}");

                // 2. Kullanıcının puanını kontrol et - DEBUG EKLENDI
                var userStatsResult = await _userProfileService.GetUserStatsAsync(userId);

                // 🔥 DEBUG: Result kontrolü
                Console.WriteLine($"🔍 GetUserStatsAsync - Success: {userStatsResult.Success}");
                Console.WriteLine($"🔍 GetUserStatsAsync - Message: {userStatsResult.Message}");

                if (!userStatsResult.Success)
                {
                    Console.WriteLine($"❌ İstatistikler alınamadı: {userStatsResult.Message}");
                    return ServiceResult<Product>.FailureResult("Kullanıcı istatistikleri alınamadı.", userStatsResult.Message);
                }

                // 🔥 DEBUG: Data null kontrolü
                if (userStatsResult.Data == null)
                {
                    Console.WriteLine("❌ UserStats Data NULL!");
                    return ServiceResult<Product>.FailureResult("Kullanıcı istatistikleri bulunamadı.");
                }

                var userStats = userStatsResult.Data;

                // 🔥 DEBUG: Puan bilgisi
                Console.WriteLine($"💰 Kullanıcı Puanı: {userStats.Points}");
                Console.WriteLine($"💰 Gerekli Puan: {BoxCost}");
                Console.WriteLine($"💰 Yeterli mi?: {userStats.Points >= BoxCost}");

                if (userStats.Points < BoxCost)
                {
                    Console.WriteLine($"❌ Yetersiz puan! Mevcut: {userStats.Points}, Gerekli: {BoxCost}");
                    return ServiceResult<Product>.FailureResult(
                        "Yetersiz Puan!",
                        $"Bu işlem için {BoxCost} puana ihtiyacınız var. Mevcut puanınız: {userStats.Points}"
                    );
                }

                Console.WriteLine("✅ Puan kontrolü başarılı, ürünler sorgulanıyor...");

                // 3. Uygun ürünleri sorgula
                var surpriseBoxProducts = await _firebaseClient
                    .Child(Constants.ProductsCollection)
                    .OnceAsync<Product>();

                Console.WriteLine($"🔍 Toplam ürün sayısı: {surpriseBoxProducts.Count}");

                var availableDonations = surpriseBoxProducts
                    .Where(p => p.Object.Type == ProductType.Bagis &&
                                p.Object.IsForSurpriseBox &&
                                !p.Object.IsSold &&
                                p.Object.IsActive &&
                                p.Object.UserId != userId)
                    .Select(p => {
                        p.Object.ProductId = p.Key;
                        return p.Object;
                    })
                    .ToList();

                Console.WriteLine($"🎁 Sürpriz kutusu için uygun ürün sayısı: {availableDonations.Count}");

                if (availableDonations.Count == 0)
                {
                    return ServiceResult<Product>.FailureResult("Ürün Yok", "Şu anda sürpriz kutusu için uygun bir ürün bulunmuyor.");
                }

                // 4. Rastgele bir ürün seç
                var random = new Random();
                var surpriseProduct = availableDonations[random.Next(availableDonations.Count)];
                var previousOwnerId = surpriseProduct.UserId;
                var previousOwnerName = surpriseProduct.UserName;

                Console.WriteLine($"🎲 Seçilen ürün: {surpriseProduct.Title}");

                // 5. Puanı düş
                Console.WriteLine($"💳 {BoxCost} puan düşülüyor...");
                var pointsDeducted = await _userProfileService.AddPointsAsync(
                    userId,
                    -BoxCost,
                    $"Sürpriz Kutu açıldı - {surpriseProduct.Title}"
                );

                if (!pointsDeducted.Success)
                {
                    Console.WriteLine($"❌ Puan düşülemedi: {pointsDeducted.Message}");
                    return ServiceResult<Product>.FailureResult("Hata", "Puan düşülürken bir sorun oluştu.");
                }

                Console.WriteLine("✅ Puan başarıyla düşüldü");

                // 6. Ürün sahipliğini güncelle
                var ownerUpdated = await _productService.UpdateProductOwnerAsync(
                    surpriseProduct.ProductId,
                    userId,
                    markAsSold: true
                );

                if (!ownerUpdated.Success)
                {
                    Console.WriteLine("⚠️ Sahiplik güncellenemedi, puan iade ediliyor...");
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
                if (donorStatsResult.Success)
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
                await CheckAndAwardBadges(previousOwnerId, donorStatsResult.Data);

                Console.WriteLine($"✅ Sürpriz kutu başarıyla açıldı: {surpriseProduct.Title}");

                return ServiceResult<Product>.SuccessResult(
                    surpriseProduct,
                    $"Tebrikler! {previousOwnerName} tarafından bağışlanan '{surpriseProduct.Title}' ürününü kazandınız!"
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Sürpriz kutu hatası: {ex.Message}");
                Console.WriteLine($"❌ StackTrace: {ex.StackTrace}");
                return ServiceResult<Product>.FailureResult("Beklenmedik Hata", ex.Message);
            }
        }

        // Rozet kontrol ve verme sistemi
        private async Task CheckAndAwardBadges(string userId, UserStats stats)
        {
            try
            {
                var badges = await _userProfileService.GetUserBadgesAsync(userId);
                var existingBadges = badges.Success ? badges.Data.Select(b => b.BadgeName).ToList() : new List<string>();

                // Bağış rozetleri
                if (stats.DonationsMade >= 1 && !existingBadges.Contains("İlk Bağış"))
                {
                    await CreateAndAwardBadge(userId, "first_donation", "İlk Bağış", "İlk bağışını yaptın! 🎁", "🎁");
                }

                if (stats.DonationsMade >= 5 && !existingBadges.Contains("Cömert Kalp"))
                {
                    await CreateAndAwardBadge(userId, "generous_heart", "Cömert Kalp", "5 bağış yaptın! 💝", "💝");
                }

                if (stats.DonationsMade >= 10 && !existingBadges.Contains("Süper Bağışçı"))
                {
                    await CreateAndAwardBadge(userId, "super_donor", "Süper Bağışçı", "10 bağış yaptın! 🌟", "🌟");
                }

                // Sürpriz kutu rozetleri
                if (stats.ItemsShared >= 1 && !existingBadges.Contains("Şanslı"))
                {
                    await CreateAndAwardBadge(userId, "lucky_one", "Şanslı", "İlk sürpriz kutunu açtın! 🍀", "🍀");
                }

                if (stats.ItemsShared >= 5 && !existingBadges.Contains("Kutu Avcısı"))
                {
                    await CreateAndAwardBadge(userId, "box_hunter", "Kutu Avcısı", "5 sürpriz kutu açtın! 🎰", "🎰");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Rozet kontrolü hatası: {ex.Message}");
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
                Console.WriteLine($"⚠️ Badge oluşturma hatası ({badgeName}): {ex.Message}");
            }
        }
    }
}