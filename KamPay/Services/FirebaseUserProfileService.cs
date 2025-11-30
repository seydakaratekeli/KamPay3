using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Helpers;
using KamPay.Models;

namespace KamPay.Services
{
    public class FirebaseUserProfileService : IUserProfileService
    {
        private readonly FirebaseClient _firebaseClient;
        //kkkkkkkkkkkk
        public FirebaseUserProfileService()
        {
            _firebaseClient = new FirebaseClient(Constants.FirebaseRealtimeDbUrl);
        }

  

        /// <summary>
        /// Yeni kullanıcı için veritabanında profil ve başlangıç istatistiklerini oluşturur.
        /// </summary>
        public async Task<ServiceResult<bool>> CreateUserProfileAsync(string userId, string username, string email)
        {
            try
            {
                // 1. user_profiles koleksiyonuna yaz
                var userProfile = new UserProfile
                {
                    UserId = userId,
                    Username = username,
                    Email = email,
                    ProfileImageUrl = "", // Varsayılan veya boş profil resmi
                    MemberSince = DateTime.UtcNow
                };
                await _firebaseClient.Child("user_profiles").Child(userId).PutAsync(userProfile);

                // 2. user_stats koleksiyonuna yaz
                var userStats = new UserStats
                {
                    UserId = userId,
                    Points = 0, // Başlangıç puanı
                    CompletedTrades = 0,
                    DonationsMade = 0,
                    TimeCredits = 0, // Her yeni kullanıcıya 0 zaman kredisiyle başlat
                    ItemsShared = 0
                    // Diğer istatistik alanları varsayılan olarak 0 olacak
                };
                await _firebaseClient.Child("user_stats").Child(userId).PutAsync(userStats);

                return ServiceResult<bool>.SuccessResult(true, "Kullanıcı profili başarıyla oluşturuldu.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Profil oluşturulamadı.", ex.Message);
            }
        }

        



        /// <summary>
        /// Belirtilen kullanıcının genel profil bilgilerini getirir.
        /// </summary>
        public async Task<ServiceResult<UserProfile>> GetUserProfileAsync(string userId)
        {
            try
            {
                var profile = await _firebaseClient
                    .Child("user_profiles")
                    .Child(userId)
                    .OnceSingleAsync<UserProfile>();

                if (profile == null)
                {
                    return ServiceResult<UserProfile>.FailureResult("Kullanıcı profili bulunamadı.");
                }
                return ServiceResult<UserProfile>.SuccessResult(profile);
            }
            catch (Exception ex)
            {
                return ServiceResult<UserProfile>.FailureResult("Profil yüklenemedi.", ex.Message);
            }
        }

        public async Task<ServiceResult<bool>> UpdateUserProfileAsync(
    string userId,
    string firstName = null,
    string lastName = null,
    string username = null,
    string profileImageUrl = null)
        {
            try
            {
                var profile = await _firebaseClient
                    .Child("user_profiles")
                    .Child(userId)
                    .OnceSingleAsync<UserProfile>();

                if (profile == null)
                {
                    return ServiceResult<bool>.FailureResult("Kullanıcı profili bulunamadı.");
                }

                if (!string.IsNullOrWhiteSpace(firstName))
                    profile.FirstName = firstName;

                if (!string.IsNullOrWhiteSpace(lastName))
                    profile.LastName = lastName;

                if (!string.IsNullOrWhiteSpace(username))
                    profile.Username = username;

                if (!string.IsNullOrWhiteSpace(profileImageUrl))
                    profile.ProfileImageUrl = profileImageUrl;

                await _firebaseClient
                    .Child("user_profiles")
                    .Child(userId)
                    .PutAsync(profile);

                return ServiceResult<bool>.SuccessResult(true, "Profil başarıyla güncellendi.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Profil güncellenemedi", ex.Message);
            }
        }

        // --- MEVCUT OYUNLAŞTIRMA METOTLARINIZ (GÜNCELLENMİŞ HALİYLE) ---

        public async Task<ServiceResult<UserStats>> GetUserStatsAsync(string userId)
        {
            try
            {
                var stats = await _firebaseClient
                    .Child("user_stats") // Constants.UserStatsCollection yerine doğrudan string kullandım, kendi projenize göre değiştirebilirsiniz.
                    .Child(userId)
                    .OnceSingleAsync<UserStats>();

                if (stats == null)
                {
                    // Eğer istatistik yoksa, yeni bir tane oluşturup döndürelim.
                    stats = new UserStats { UserId = userId };
                    await _firebaseClient.Child("user_stats").Child(userId).PutAsync(stats);
                }
                return ServiceResult<UserStats>.SuccessResult(stats);
            }
            catch (Exception ex)
            {
                return ServiceResult<UserStats>.FailureResult("İstatistikler yüklenemedi", ex.Message);
            }
        }

        public async Task<ServiceResult<bool>> AddPointsForAction(string userId, UserAction action)
        {
            int points = GetPointsForAction(action);
            string reason = GetReasonForAction(action);

            if (points > 0)
            {
                return await AddPointsAsync(userId, points, reason);
            }
            return ServiceResult<bool>.SuccessResult(true, "Bu eylem için puan tanımlanmamış.");
        }

        public async Task<ServiceResult<bool>> AddPointsAsync(string userId, int points, string reason)
        {
            try
            {
                var statsResult = await GetUserStatsAsync(userId);
                if (!statsResult.Success)
                {
                    return ServiceResult<bool>.FailureResult("Puan eklenemedi: İstatistikler alınamadı.");
                }
                var stats = statsResult.Data;
                stats.Points += points; // DÜZELTİLDİ

                await UpdateUserStatsAsync(stats);

                // Puan kazandıktan sonra rozet kontrolü yap
                await CheckAndAwardBadgesAsync(userId);

                return ServiceResult<bool>.SuccessResult(true, $"+{points} puan kazanıldı.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Puan eklenemedi", ex.Message);
            }
        }

        public async Task<ServiceResult<bool>> UpdateUserStatsAsync(UserStats stats)
        {
            try
            {
                await _firebaseClient
                    .Child("user_stats")
                    .Child(stats.UserId)
                    .PutAsync(stats);
                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("İstatistikler güncellenemedi", ex.Message);
            }
        }
        // MEVCUT KODUNUZ (DEĞİŞİKLİK YOK)
        public async Task<ServiceResult<List<UserBadge>>> GetUserBadgesAsync(string userId)
        {
            try
            {
                var allBadges = await _firebaseClient
                    .Child(Constants.UserBadgesCollection)
                    .OnceAsync<UserBadge>();

                var userBadges = allBadges
                    .Select(b => b.Object)
                    .Where(b => b.UserId == userId)
                    .OrderByDescending(b => b.EarnedAt)
                    .ToList();

                return ServiceResult<List<UserBadge>>.SuccessResult(userBadges);
            }
            catch (Exception ex)
            {
                return ServiceResult<List<UserBadge>>.FailureResult("Rozetler yüklenemedi", ex.Message);
            }
        }

        // MEVCUT KODUNUZ (DEĞİŞİKLİK YOK)
        public async Task<ServiceResult<UserBadge>> AwardBadgeAsync(string userId, string badgeId)
        {
            try
            {
                var existingBadges = await GetUserBadgesAsync(userId);
                if (existingBadges.Success && existingBadges.Data.Any(b => b.BadgeId == badgeId))
                {
                    return ServiceResult<UserBadge>.FailureResult("Rozet zaten kazanılmış");
                }

                var badge = await _firebaseClient
                    .Child(Constants.BadgesCollection)
                    .Child(badgeId)
                    .OnceSingleAsync<Badge>();

                if (badge == null)
                {
                    return ServiceResult<UserBadge>.FailureResult("Rozet bulunamadı");
                }

                var userBadge = new UserBadge
                {
                    UserId = userId,
                    BadgeId = badgeId,
                    BadgeName = badge.Name,
                    BadgeIcon = badge.IconName,
                    BadgeColor = badge.Color
                };

                await _firebaseClient
                    .Child(Constants.UserBadgesCollection)
                    .Child(userBadge.UserBadgeId)
                    .PutAsync(userBadge);

                var notification = new Notification
                {
                    UserId = userId,
                    Type = NotificationType.BadgeEarned,
                    Title = "🎉 Yeni Rozet Kazandın!",
                    Message = $"\"{badge.Name}\" rozetini kazandın: {badge.Description}",
                    RelatedEntityId = badgeId,
                    RelatedEntityType = "Badge"
                };

                await _firebaseClient
                    .Child(Constants.NotificationsCollection)
                    .Child(notification.NotificationId)
                    .PutAsync(notification);

                return ServiceResult<UserBadge>.SuccessResult(userBadge, $"Tebrikler! {badge.Name} kazandınız!");
            }
            catch (Exception ex)
            {
                return ServiceResult<UserBadge>.FailureResult("Rozet verilemedi", ex.Message);
            }
        }

        // YENİ METODU IMPLEMENTE EDİN
        public async Task<ServiceResult<bool>> TransferTimeCreditsAsync(string fromUserId, string toUserId, int amount, string reason)
        {
            try
            {
                // İki kullanıcının da istatistiklerini al
                var fromUserStatsResult = await GetUserStatsAsync(fromUserId);
                var toUserStatsResult = await GetUserStatsAsync(toUserId);

                if (!fromUserStatsResult.Success || !toUserStatsResult.Success)
                {
                    return ServiceResult<bool>.FailureResult("Kullanıcı istatistikleri alınamadı.");
                }

                var fromUserStats = fromUserStatsResult.Data;
                var toUserStats = toUserStatsResult.Data;

                // Hizmeti alan kişinin yeterli kredisi var mı? (Opsiyonel ama önerilir)
                if (fromUserStats.TimeCredits < amount)
                {
                    return ServiceResult<bool>.FailureResult("Yetersiz zaman kredisi.");
                }

                // Kredi transferini yap
                fromUserStats.TimeCredits -= amount;
                toUserStats.TimeCredits += amount;

                // İki kullanıcının da istatistiklerini güncelle
                await UpdateUserStatsAsync(fromUserStats);
                await UpdateUserStatsAsync(toUserStats);

                // TODO: Bu önemli işlemi bir "transaction_history" koleksiyonuna kaydetmek,
                // projenizin güvenilirliğini artırır. Raporunuzda bundan bahsedebilirsiniz.

                return ServiceResult<bool>.SuccessResult(true, "Kredi transferi başarılı.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Kredi transferi sırasında hata oluştu.", ex.Message);
            }
        }
        // MEVCUT KODUNUZ (DEĞİŞİKLİK YOK)
        public async Task<ServiceResult<bool>> CheckAndAwardBadgesAsync(string userId)
        {
            try
            {
                var statsResult = await GetUserStatsAsync(userId);
                if (!statsResult.Success)
                {
                    return ServiceResult<bool>.FailureResult("İstatistikler alınamadı");
                }

                var stats = statsResult.Data;
                var userBadgesResult = await GetUserBadgesAsync(userId);
                var earnedBadgeIds = userBadgesResult.Success
                    ? userBadgesResult.Data.Select(b => b.BadgeId).ToList()
                    : new List<string>();

                var allBadges = Badge.GetDefaultBadges();

                foreach (var badge in allBadges)
                {
                    if (earnedBadgeIds.Contains(badge.BadgeId))
                        continue;

                    bool shouldAward = false;

                    switch (badge.Category)
                    {
                        case BadgeCategory.Seller:
                            shouldAward = stats.TotalProducts >= badge.RequiredCount;
                            break;
                        case BadgeCategory.Buyer:
                            shouldAward = stats.PurchasedProducts >= badge.RequiredCount;
                            break;
                        case BadgeCategory.Donation:
                            shouldAward = stats.DonatedProducts >= badge.RequiredCount;
                            break;
                        case BadgeCategory.Points:
                            shouldAward = stats.Points >= badge.RequiredPoints; // DÜZELTİLDİ
                            break;
                    }

                    if (shouldAward)
                    {
                        await _firebaseClient
                            .Child(Constants.BadgesCollection)
                            .Child(badge.BadgeId)
                            .PutAsync(badge);

                        await AwardBadgeAsync(userId, badge.BadgeId);
                    }
                }

                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Kontrol edilemedi", ex.Message);
            }
        }


        private int GetPointsForAction(UserAction action)
        {
            return action switch
            {
                UserAction.AddProduct => 10,
                UserAction.MakeDonation => 50,
                UserAction.CompleteTransaction => 25,
                UserAction.ReceiveDonation => 10,
                _ => 0
            };
        }

        private string GetReasonForAction(UserAction action)
        {
            return action switch
            {
                UserAction.AddProduct => "Yeni ürün ekledin.",
                UserAction.MakeDonation => "Değerli bir bağış yaptın.",
                UserAction.CompleteTransaction => "Başarılı bir takas/satış tamamladın.",
                UserAction.ReceiveDonation => "Bir bağışı teslim aldın.",
                _ => "Genel aktivite."
            };
        }
    }
}