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




        /// Yeni kullanıcı için veritabanında profil ve başlangıç istatistiklerini oluşturur.

        // Services/FirebaseUserProfileService.cs
        public async Task<ServiceResult<bool>> CreateUserProfileAsync(User user)
        {
            try
            {
                // 1. user_profiles koleksiyonunu oluştur
                var userProfile = new UserProfile
                {
                    UserId = user.UserId,
                    FirstName = user.FirstName, // Artık parçalamaya gerek yok, nesneden geliyor
                    LastName = user.LastName,
                    Username = user.Username,
                    Email = user.Email,
                    ProfileImageUrl = string.IsNullOrEmpty(user.ProfileImageUrl)
                        ? $"https://ui-avatars.com/api/?name={Uri.EscapeDataString(user.FirstName)}+{Uri.EscapeDataString(user.LastName)}&background=random"
                        : user.ProfileImageUrl,
                    MemberSince = DateTime.UtcNow
                };

                await _firebaseClient.Child("user_profiles").Child(user.UserId).PutAsync(userProfile);

                // 2. user_stats koleksiyonunu oluştur
                var userStats = new UserStats
                {
                    UserId = user.UserId,
                    Points = 0,
                    TimeCredits = 0,
                    // Diğer alanlar default 0
                };
                await _firebaseClient.Child("user_stats").Child(user.UserId).PutAsync(userStats);

                return ServiceResult<bool>.SuccessResult(true, "Profil ve istatistikler başarıyla oluşturuldu.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ CreateUserProfileAsync hatası: {ex.Message}");
                return ServiceResult<bool>.FailureResult("Profil oluşturulamadı.", ex.Message);
            }
        }




        /// Belirtilen kullanıcının genel profil bilgilerini getirir.

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
                
                // ✅ Debug log - hangi alanların boş olduğunu görelim
                Console.WriteLine($"📋 GetUserProfileAsync - UserId: {userId}");
                Console.WriteLine($"   FirstName: '{profile.FirstName ?? "NULL"}'");
                Console.WriteLine($"   LastName: '{profile.LastName ?? "NULL"}'");
                Console.WriteLine($"   Username: '{profile.Username ?? "NULL"}'");
                Console.WriteLine($"   Email: '{profile.Email ?? "NULL"}'");
                Console.WriteLine($"   ProfileImageUrl: '{profile.ProfileImageUrl ?? "NULL"}'");
                
                return ServiceResult<UserProfile>.SuccessResult(profile);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ GetUserProfileAsync hatası: {ex.Message}");
                return ServiceResult<UserProfile>.FailureResult("Profil yüklenemedi.", ex.Message);
            }
        }

        public async Task<ServiceResult<bool>> UpdateUserProfileAsync(
     string userId,
     string? firstName = null,
     string? lastName = null,
     string? username = null,
     string? profileImageUrl = null)
        {
            try
            {
                // 1. Güncellenecek verileri bir sözlükte topla
                var updates = new Dictionary<string, object>();

                if (!string.IsNullOrWhiteSpace(firstName)) updates.Add("FirstName", firstName);
                if (!string.IsNullOrWhiteSpace(lastName)) updates.Add("LastName", lastName);
                if (!string.IsNullOrWhiteSpace(username)) updates.Add("Username", username);
                if (!string.IsNullOrWhiteSpace(profileImageUrl)) updates.Add("ProfileImageUrl", profileImageUrl);
                if (!string.IsNullOrWhiteSpace(firstName) || !string.IsNullOrWhiteSpace(lastName))
                {
                    string fName = firstName ?? "";
                    string lName = lastName ?? "";
                    updates.Add("FullName", $"{fName} {lName}".Trim());
                }
                if (updates.Count == 0) return ServiceResult<bool>.SuccessResult(true);

                // 2. user_profiles koleksiyonunu kısmi güncelle (Patch)
                await _firebaseClient
                    .Child("user_profiles")
                    .Child(userId)
                    .PatchAsync(updates);

                // 3. users koleksiyonunu güncelle (Senkronizasyon ŞART)
                await _firebaseClient
                    .Child("users")
                    .Child(userId)
                    .PatchAsync(updates);

                return ServiceResult<bool>.SuccessResult(true, "Profil her iki tabloda da güncellendi.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Güncelleme hatası", ex.Message);
            }
        }

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
                stats.Points += points; // 

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

                //  IMPLEMENTED: Transaction history logging for audit trail and reliability
                var transactionHistory = new TransactionHistory
                {
                    FromUserId = fromUserId,
                    ToUserId = toUserId,
                    Amount = amount,
                    Type = TransactionHistoryType.CreditTransfer,
                    Description = reason ?? "Zaman kredisi transferi",
                    Status = TransactionHistoryStatus.Completed,
                    FromUserBalanceAfter = fromUserStats.TimeCredits,
                    ToUserBalanceAfter = toUserStats.TimeCredits,
                    CreatedAt = DateTime.UtcNow
                };

                await _firebaseClient
                    .Child("transaction_history")
                    .Child(transactionHistory.TransactionHistoryId)
                    .PutAsync(transactionHistory);

                return ServiceResult<bool>.SuccessResult(true, "Kredi transferi başarılı.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Kredi transferi sırasında hata oluştu.", ex.Message);
            }
        }
        
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
                            shouldAward = stats.Points >= badge.RequiredPoints; // 
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