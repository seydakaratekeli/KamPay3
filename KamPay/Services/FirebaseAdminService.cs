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
    /// <summary>
    /// Firebase implementation of admin service
    /// </summary>
    public class FirebaseAdminService : IAdminService
    {
        private readonly FirebaseClient _firebaseClient;
        private readonly INotificationService _notificationService;
        private const string UserRolesCollection = "user_roles";

        public FirebaseAdminService(INotificationService notificationService)
        {
            _firebaseClient = new FirebaseClient(Constants.FirebaseRealtimeDbUrl);
            _notificationService = notificationService;
        }

        public async Task<ServiceResult<bool>> IsAdminOrModeratorAsync(string userId)
        {
            try
            {
                var role = await GetUserRoleAsync(userId);
                return ServiceResult<bool>.SuccessResult(
                    role.Data == UserRole.Admin || role.Data == UserRole.Moderator);
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Yetki kontrolü başarısız", ex.Message);
            }
        }

        public async Task<ServiceResult<UserRole>> GetUserRoleAsync(string userId)
        {
            try
            {
                var roleData = await _firebaseClient
                    .Child(UserRolesCollection)
                    .Child(userId)
                    .OnceSingleAsync<Dictionary<string, object>>();

                if (roleData != null && roleData.ContainsKey("role"))
                {
                    var roleValue = Convert.ToInt32(roleData["role"]);
                    return ServiceResult<UserRole>.SuccessResult((UserRole)roleValue);
                }

                return ServiceResult<UserRole>.SuccessResult(UserRole.User);
            }
            catch (Exception ex)
            {
                return ServiceResult<UserRole>.FailureResult("Rol alınamadı", ex.Message);
            }
        }

        public async Task<ServiceResult<bool>> BanUserAsync(
            string adminUserId,
            string targetUserId,
            string reason)
        {
            try
            {
                // Verify admin privileges
                var isAdmin = await IsAdminOrModeratorAsync(adminUserId);
                if (!isAdmin.Success || !isAdmin.Data)
                {
                    return ServiceResult<bool>.FailureResult("Yetkiniz yok");
                }

                // Get users
                var admin = await _firebaseClient
                    .Child(Constants.UsersCollection)
                    .Child(adminUserId)
                    .OnceSingleAsync<User>();

                var targetUser = await _firebaseClient
                    .Child(Constants.UsersCollection)
                    .Child(targetUserId)
                    .OnceSingleAsync<User>();

                if (admin == null || targetUser == null)
                {
                    return ServiceResult<bool>.FailureResult("Kullanıcı bulunamadı");
                }

                // Ban user
                targetUser.IsBanned = true;
                targetUser.BanReason = reason;
                await _firebaseClient
                    .Child(Constants.UsersCollection)
                    .Child(targetUserId)
                    .PutAsync(targetUser);

                // Log action
                await LogAdminActionAsync(
                    adminUserId,
                    admin.FullName,
                    AdminActionType.BanUser,
                    targetUserId,
                    targetUser.FullName,
                    reason);

                // Notify user
                await _notificationService.CreateNotificationAsync(
                    targetUserId,
                    "Hesap Engellendi",
                    $"Hesabınız engellenmiştir. Sebep: {reason}",
                    NotificationType.System,
                    null);

                return ServiceResult<bool>.SuccessResult(true, "Kullanıcı engellendi");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Engelleme başarısız", ex.Message);
            }
        }

        public async Task<ServiceResult<bool>> UnbanUserAsync(
            string adminUserId,
            string targetUserId)
        {
            try
            {
                // Verify admin privileges
                var isAdmin = await IsAdminOrModeratorAsync(adminUserId);
                if (!isAdmin.Success || !isAdmin.Data)
                {
                    return ServiceResult<bool>.FailureResult("Yetkiniz yok");
                }

                // Get users
                var admin = await _firebaseClient
                    .Child(Constants.UsersCollection)
                    .Child(adminUserId)
                    .OnceSingleAsync<User>();

                var targetUser = await _firebaseClient
                    .Child(Constants.UsersCollection)
                    .Child(targetUserId)
                    .OnceSingleAsync<User>();

                if (admin == null || targetUser == null)
                {
                    return ServiceResult<bool>.FailureResult("Kullanıcı bulunamadı");
                }

                // Unban user
                targetUser.IsBanned = false;
                targetUser.BanReason = null;
                await _firebaseClient
                    .Child(Constants.UsersCollection)
                    .Child(targetUserId)
                    .PutAsync(targetUser);

                // Log action
                await LogAdminActionAsync(
                    adminUserId,
                    admin.FullName,
                    AdminActionType.UnbanUser,
                    targetUserId,
                    targetUser.FullName,
                    "Ban kaldırıldı");

                // Notify user
                await _notificationService.CreateNotificationAsync(
                    targetUserId,
                    "Engel Kaldırıldı",
                    "Hesabınızın engeli kaldırılmıştır",
                    NotificationType.System,
                    null);

                return ServiceResult<bool>.SuccessResult(true, "Engel kaldırıldı");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("İşlem başarısız", ex.Message);
            }
        }

        public async Task<ServiceResult<bool>> VerifyUserAsync(
            string adminUserId,
            string targetUserId)
        {
            try
            {
                // Verify admin privileges
                var isAdmin = await IsAdminOrModeratorAsync(adminUserId);
                if (!isAdmin.Success || !isAdmin.Data)
                {
                    return ServiceResult<bool>.FailureResult("Yetkiniz yok");
                }

                // Get users
                var admin = await _firebaseClient
                    .Child(Constants.UsersCollection)
                    .Child(adminUserId)
                    .OnceSingleAsync<User>();

                var targetUser = await _firebaseClient
                    .Child(Constants.UsersCollection)
                    .Child(targetUserId)
                    .OnceSingleAsync<User>();

                if (admin == null || targetUser == null)
                {
                    return ServiceResult<bool>.FailureResult("Kullanıcı bulunamadı");
                }

                // Verify user
                targetUser.IsEmailVerified = true;
                await _firebaseClient
                    .Child(Constants.UsersCollection)
                    .Child(targetUserId)
                    .PutAsync(targetUser);

                // Log action
                await LogAdminActionAsync(
                    adminUserId,
                    admin.FullName,
                    AdminActionType.VerifyUser,
                    targetUserId,
                    targetUser.FullName,
                    "Kullanıcı manuel olarak doğrulandı");

                // Notify user
                await _notificationService.CreateNotificationAsync(
                    targetUserId,
                    "Hesap Doğrulandı",
                    "Hesabınız yönetici tarafından doğrulandı",
                    NotificationType.System,
                    null);

                return ServiceResult<bool>.SuccessResult(true, "Kullanıcı doğrulandı");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Doğrulama başarısız", ex.Message);
            }
        }

        public async Task<ServiceResult<bool>> PromoteToModeratorAsync(
            string adminUserId,
            string targetUserId)
        {
            try
            {
                // Verify admin privileges (only admins can promote)
                var adminRole = await GetUserRoleAsync(adminUserId);
                if (!adminRole.Success || adminRole.Data != UserRole.Admin)
                {
                    return ServiceResult<bool>.FailureResult("Sadece adminler moderatör atayabilir");
                }

                // Get users
                var admin = await _firebaseClient
                    .Child(Constants.UsersCollection)
                    .Child(adminUserId)
                    .OnceSingleAsync<User>();

                var targetUser = await _firebaseClient
                    .Child(Constants.UsersCollection)
                    .Child(targetUserId)
                    .OnceSingleAsync<User>();

                if (admin == null || targetUser == null)
                {
                    return ServiceResult<bool>.FailureResult("Kullanıcı bulunamadı");
                }

                // Set role
                await _firebaseClient
                    .Child(UserRolesCollection)
                    .Child(targetUserId)
                    .PutAsync(new { role = (int)UserRole.Moderator });

                // Log action
                await LogAdminActionAsync(
                    adminUserId,
                    admin.FullName,
                    AdminActionType.PromoteToModerator,
                    targetUserId,
                    targetUser.FullName,
                    "Moderatörlüğe terfi edildi");

                // Notify user
                await _notificationService.CreateNotificationAsync(
                    targetUserId,
                    "Moderatör Oldunuz",
                    "Tebrikler! Moderatör yetkisi kazandınız",
                    NotificationType.System,
                    null);

                return ServiceResult<bool>.SuccessResult(true, "Kullanıcı moderatör yapıldı");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("İşlem başarısız", ex.Message);
            }
        }

        public async Task<ServiceResult<bool>> DemoteFromModeratorAsync(
            string adminUserId,
            string targetUserId)
        {
            try
            {
                // Verify admin privileges
                var adminRole = await GetUserRoleAsync(adminUserId);
                if (!adminRole.Success || adminRole.Data != UserRole.Admin)
                {
                    return ServiceResult<bool>.FailureResult("Sadece adminler bu işlemi yapabilir");
                }

                // Get users
                var admin = await _firebaseClient
                    .Child(Constants.UsersCollection)
                    .Child(adminUserId)
                    .OnceSingleAsync<User>();

                var targetUser = await _firebaseClient
                    .Child(Constants.UsersCollection)
                    .Child(targetUserId)
                    .OnceSingleAsync<User>();

                if (admin == null || targetUser == null)
                {
                    return ServiceResult<bool>.FailureResult("Kullanıcı bulunamadı");
                }

                // Set role back to user
                await _firebaseClient
                    .Child(UserRolesCollection)
                    .Child(targetUserId)
                    .PutAsync(new { role = (int)UserRole.User });

                // Log action
                await LogAdminActionAsync(
                    adminUserId,
                    admin.FullName,
                    AdminActionType.DemoteFromModerator,
                    targetUserId,
                    targetUser.FullName,
                    "Moderatörlükten alındı");

                return ServiceResult<bool>.SuccessResult(true, "Moderatörlük kaldırıldı");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("İşlem başarısız", ex.Message);
            }
        }

        public async Task<ServiceResult<List<AdminAction>>> GetAdminActionsAsync(
            string? adminUserId = null,
            int limit = 100)
        {
            try
            {
                var allActions = await _firebaseClient
                    .Child(Constants.AdminActionsCollection)
                    .OnceAsync<AdminAction>();

                var actions = allActions
                    .Select(a => a.Object)
                    .Where(a => string.IsNullOrEmpty(adminUserId) || a.AdminUserId == adminUserId)
                    .OrderByDescending(a => a.CreatedAt)
                    .Take(limit)
                    .ToList();

                return ServiceResult<List<AdminAction>>.SuccessResult(actions);
            }
            catch (Exception ex)
            {
                return ServiceResult<List<AdminAction>>.FailureResult(
                    "İşlem geçmişi alınamadı",
                    ex.Message);
            }
        }

        public async Task<ServiceResult<PlatformStats>> GetPlatformStatsAsync()
        {
            try
            {
                // TODO: For production, implement caching or incremental statistics updates
                // Loading all collections is expensive and will not scale well with large datasets
                // Consider: 
                // 1. Caching stats with periodic refresh (e.g., every hour)
                // 2. Maintaining counters incrementally when items are added/removed
                // 3. Using Firebase aggregation queries when available
                
                // Get all collections
                var users = await _firebaseClient.Child(Constants.UsersCollection).OnceAsync<User>();
                var products = await _firebaseClient.Child(Constants.ProductsCollection).OnceAsync<Product>();
                var transactions = await _firebaseClient.Child(Constants.TransactionsCollection).OnceAsync<Transaction>();
                var serviceOffers = await _firebaseClient.Child(Constants.ServiceOffersCollection).OnceAsync<ServiceOffer>();
                var serviceRequests = await _firebaseClient.Child(Constants.ServiceRequestsCollection).OnceAsync<ServiceRequest>();
                var disputes = await _firebaseClient.Child(Constants.DisputesCollection).OnceAsync<DisputeResolution>();

                var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

                var stats = new PlatformStats
                {
                    TotalUsers = users.Count,
                    ActiveUsers = users.Count(u => u.Object.LastLoginAt.HasValue && u.Object.LastLoginAt.Value > thirtyDaysAgo),
                    TotalProducts = products.Count,
                    TotalTransactions = transactions.Count,
                    TotalServiceOffers = serviceOffers.Count,
                    TotalServiceRequests = serviceRequests.Count,
                    TotalDisputes = disputes.Count,
                    OpenDisputes = disputes.Count(d => d.Object.Status == DisputeStatus.Open || d.Object.Status == DisputeStatus.UnderReview),
                    LastUpdated = DateTime.UtcNow
                };

                return ServiceResult<PlatformStats>.SuccessResult(stats);
            }
            catch (Exception ex)
            {
                return ServiceResult<PlatformStats>.FailureResult(
                    "İstatistikler alınamadı",
                    ex.Message);
            }
        }

        private async Task LogAdminActionAsync(
            string adminUserId,
            string adminUserName,
            AdminActionType actionType,
            string targetUserId,
            string targetUserName,
            string reason,
            string? notes = null)
        {
            try
            {
                var action = new AdminAction
                {
                    AdminUserId = adminUserId,
                    AdminUserName = adminUserName,
                    ActionType = actionType,
                    TargetUserId = targetUserId,
                    TargetUserName = targetUserName,
                    Reason = reason,
                    Notes = notes
                };

                await _firebaseClient
                    .Child(Constants.AdminActionsCollection)
                    .Child(action.ActionId)
                    .PutAsync(action);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to log admin action: {ex.Message}");
            }
        }
    }
}
