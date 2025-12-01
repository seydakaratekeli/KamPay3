using KamPay.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KamPay.Services
{
    /// <summary>
    /// Service interface for handling service ratings and reviews
    /// </summary>
    public interface IRatingService
    {
        /// <summary>
        /// Creates a rating for a completed service
        /// </summary>
        Task<ServiceResult<ServiceRating>> CreateRatingAsync(
            string serviceRequestId,
            string reviewerId,
            string ratedUserId,
            int stars,
            string? comment = null,
            int? communicationRating = null,
            int? punctualityRating = null,
            int? qualityRating = null);

        /// <summary>
        /// Gets all ratings for a user
        /// </summary>
        Task<ServiceResult<List<ServiceRating>>> GetUserRatingsAsync(
            string userId,
            int limit = 50);

        /// <summary>
        /// Gets rating statistics for a user
        /// </summary>
        Task<ServiceResult<UserRatingStats>> GetUserRatingStatsAsync(string userId);

        /// <summary>
        /// Reports a rating as inappropriate
        /// </summary>
        Task<ServiceResult<bool>> ReportRatingAsync(
            string ratingId,
            string reporterId,
            string reason);

        /// <summary>
        /// Hides a rating (moderator action)
        /// </summary>
        Task<ServiceResult<bool>> HideRatingAsync(
            string ratingId,
            string moderatorId);

        /// <summary>
        /// Checks if a user can rate a service request
        /// </summary>
        Task<ServiceResult<bool>> CanRateServiceAsync(
            string serviceRequestId,
            string userId);
    }
}
