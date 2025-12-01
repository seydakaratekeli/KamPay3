using KamPay.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KamPay.Services
{
    /// <summary>
    /// Service interface for admin operations
    /// </summary>
    public interface IAdminService
    {
        /// <summary>
        /// Checks if a user has admin or moderator privileges
        /// </summary>
        Task<ServiceResult<bool>> IsAdminOrModeratorAsync(string userId);

        /// <summary>
        /// Gets user role
        /// </summary>
        Task<ServiceResult<UserRole>> GetUserRoleAsync(string userId);

        /// <summary>
        /// Bans a user
        /// </summary>
        Task<ServiceResult<bool>> BanUserAsync(
            string adminUserId,
            string targetUserId,
            string reason);

        /// <summary>
        /// Unbans a user
        /// </summary>
        Task<ServiceResult<bool>> UnbanUserAsync(
            string adminUserId,
            string targetUserId);

        /// <summary>
        /// Verifies a user manually
        /// </summary>
        Task<ServiceResult<bool>> VerifyUserAsync(
            string adminUserId,
            string targetUserId);

        /// <summary>
        /// Promotes a user to moderator
        /// </summary>
        Task<ServiceResult<bool>> PromoteToModeratorAsync(
            string adminUserId,
            string targetUserId);

        /// <summary>
        /// Demotes a moderator to regular user
        /// </summary>
        Task<ServiceResult<bool>> DemoteFromModeratorAsync(
            string adminUserId,
            string targetUserId);

        /// <summary>
        /// Gets all admin actions (audit log)
        /// </summary>
        Task<ServiceResult<List<AdminAction>>> GetAdminActionsAsync(
            string? adminUserId = null,
            int limit = 100);

        /// <summary>
        /// Gets platform statistics
        /// </summary>
        Task<ServiceResult<PlatformStats>> GetPlatformStatsAsync();
    }

    /// <summary>
    /// Platform statistics model
    /// </summary>
    public class PlatformStats
    {
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; } // Active in last 30 days
        public int TotalProducts { get; set; }
        public int TotalTransactions { get; set; }
        public int TotalServiceOffers { get; set; }
        public int TotalServiceRequests { get; set; }
        public int OpenDisputes { get; set; }
        public int TotalDisputes { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}
