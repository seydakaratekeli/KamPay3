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
    /// Firebase implementation of rating service
    /// </summary>
    public class FirebaseRatingService : IRatingService
    {
        private readonly FirebaseClient _firebaseClient;
        private readonly INotificationService _notificationService;
        private const string UserRatingStatsCollection = "user_rating_stats";

        public FirebaseRatingService(INotificationService notificationService)
        {
            _firebaseClient = new FirebaseClient(Constants.FirebaseRealtimeDbUrl);
            _notificationService = notificationService;
        }

        public async Task<ServiceResult<ServiceRating>> CreateRatingAsync(
            string serviceRequestId,
            string reviewerId,
            string ratedUserId,
            int stars,
            string? comment = null,
            int? communicationRating = null,
            int? punctualityRating = null,
            int? qualityRating = null)
        {
            try
            {
                // Validate stars
                if (stars < 1 || stars > 5)
                {
                    return ServiceResult<ServiceRating>.FailureResult("Puan 1-5 arasında olmalıdır");
                }

                // Check if already rated
                var canRate = await CanRateServiceAsync(serviceRequestId, reviewerId);
                if (!canRate.Success || !canRate.Data)
                {
                    return ServiceResult<ServiceRating>.FailureResult(
                        canRate.Message ?? "Bu hizmeti zaten değerlendirdiniz");
                }

                // Get service request details
                var serviceRequest = await _firebaseClient
                    .Child(Constants.ServiceRequestsCollection)
                    .Child(serviceRequestId)
                    .OnceSingleAsync<ServiceRequest>();

                if (serviceRequest == null)
                {
                    return ServiceResult<ServiceRating>.FailureResult("Hizmet talebi bulunamadı");
                }

                if (serviceRequest.Status != ServiceRequestStatus.Completed)
                {
                    return ServiceResult<ServiceRating>.FailureResult(
                        "Sadece tamamlanmış hizmetler değerlendirilebilir");
                }

                // Get user names
                var reviewer = await _firebaseClient
                    .Child(Constants.UsersCollection)
                    .Child(reviewerId)
                    .OnceSingleAsync<User>();

                var ratedUser = await _firebaseClient
                    .Child(Constants.UsersCollection)
                    .Child(ratedUserId)
                    .OnceSingleAsync<User>();

                if (reviewer == null || ratedUser == null)
                {
                    return ServiceResult<ServiceRating>.FailureResult("Kullanıcı bulunamadı");
                }

                var rating = new ServiceRating
                {
                    ServiceRequestId = serviceRequestId,
                    ServiceId = serviceRequest.ServiceId,
                    ServiceTitle = serviceRequest.ServiceTitle,
                    ReviewerId = reviewerId,
                    ReviewerName = reviewer.FullName,
                    RatedUserId = ratedUserId,
                    RatedUserName = ratedUser.FullName,
                    Stars = stars,
                    Comment = comment,
                    CommunicationRating = communicationRating,
                    PunctualityRating = punctualityRating,
                    QualityRating = qualityRating
                };

                await _firebaseClient
                    .Child(Constants.ServiceRatingsCollection)
                    .Child(rating.RatingId)
                    .PutAsync(rating);

                // Update rating statistics
                await UpdateUserRatingStatsAsync(ratedUserId);

                // Notify rated user
                await _notificationService.CreateNotificationAsync(
                    ratedUserId,
                    "Yeni Değerlendirme",
                    $"{reviewer.FullName} sizi {stars} yıldız ile değerlendirdi",
                    NotificationType.Rating,
                    rating.RatingId);

                return ServiceResult<ServiceRating>.SuccessResult(
                    rating,
                    "Değerlendirme kaydedildi. Teşekkürler!");
            }
            catch (Exception ex)
            {
                return ServiceResult<ServiceRating>.FailureResult(
                    "Değerlendirme kaydedilemedi",
                    ex.Message);
            }
        }

        public async Task<ServiceResult<List<ServiceRating>>> GetUserRatingsAsync(
            string userId,
            int limit = 50)
        {
            try
            {
                var allRatings = await _firebaseClient
                    .Child(Constants.ServiceRatingsCollection)
                    .OnceAsync<ServiceRating>();

                var userRatings = allRatings
                    .Select(r => r.Object)
                    .Where(r => r.RatedUserId == userId && !r.IsHidden)
                    .OrderByDescending(r => r.CreatedAt)
                    .Take(limit)
                    .ToList();

                return ServiceResult<List<ServiceRating>>.SuccessResult(userRatings);
            }
            catch (Exception ex)
            {
                return ServiceResult<List<ServiceRating>>.FailureResult(
                    "Değerlendirmeler alınamadı",
                    ex.Message);
            }
        }

        public async Task<ServiceResult<UserRatingStats>> GetUserRatingStatsAsync(string userId)
        {
            try
            {
                var stats = await _firebaseClient
                    .Child(UserRatingStatsCollection)
                    .Child(userId)
                    .OnceSingleAsync<UserRatingStats>();

                if (stats == null)
                {
                    // Calculate stats if not cached
                    stats = await CalculateUserRatingStatsAsync(userId);
                }

                return ServiceResult<UserRatingStats>.SuccessResult(stats);
            }
            catch (Exception ex)
            {
                return ServiceResult<UserRatingStats>.FailureResult(
                    "İstatistikler alınamadı",
                    ex.Message);
            }
        }

        public async Task<ServiceResult<bool>> ReportRatingAsync(
            string ratingId,
            string reporterId,
            string reason)
        {
            try
            {
                var ratingNode = _firebaseClient
                    .Child(Constants.ServiceRatingsCollection)
                    .Child(ratingId);

                var rating = await ratingNode.OnceSingleAsync<ServiceRating>();

                if (rating == null)
                {
                    return ServiceResult<bool>.FailureResult("Değerlendirme bulunamadı");
                }

                rating.IsReported = true;
                rating.ReportReason = reason;
                await ratingNode.PutAsync(rating);

                return ServiceResult<bool>.SuccessResult(
                    true,
                    "Değerlendirme bildirildi. İncelenecektir.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult(
                    "Bildirme işlemi başarısız",
                    ex.Message);
            }
        }

        public async Task<ServiceResult<bool>> HideRatingAsync(
            string ratingId,
            string moderatorId)
        {
            try
            {
                var ratingNode = _firebaseClient
                    .Child(Constants.ServiceRatingsCollection)
                    .Child(ratingId);

                var rating = await ratingNode.OnceSingleAsync<ServiceRating>();

                if (rating == null)
                {
                    return ServiceResult<bool>.FailureResult("Değerlendirme bulunamadı");
                }

                rating.IsHidden = true;
                rating.ModeratedAt = DateTime.UtcNow;
                await ratingNode.PutAsync(rating);

                // Update stats after hiding
                await UpdateUserRatingStatsAsync(rating.RatedUserId);

                return ServiceResult<bool>.SuccessResult(true, "Değerlendirme gizlendi");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult(
                    "Gizleme işlemi başarısız",
                    ex.Message);
            }
        }

        public async Task<ServiceResult<bool>> CanRateServiceAsync(
            string serviceRequestId,
            string userId)
        {
            try
            {
                // Check if service request exists and is completed
                var serviceRequest = await _firebaseClient
                    .Child(Constants.ServiceRequestsCollection)
                    .Child(serviceRequestId)
                    .OnceSingleAsync<ServiceRequest>();

                if (serviceRequest == null)
                {
                    return ServiceResult<bool>.FailureResult("Hizmet talebi bulunamadı");
                }

                if (serviceRequest.Status != ServiceRequestStatus.Completed)
                {
                    return ServiceResult<bool>.FailureResult(
                        "Sadece tamamlanmış hizmetler değerlendirilebilir");
                }

                // Check if user is involved in the service
                if (serviceRequest.ProviderId != userId && serviceRequest.RequesterId != userId)
                {
                    return ServiceResult<bool>.FailureResult(
                        "Bu hizmeti değerlendirme yetkiniz yok");
                }

                // Check if already rated
                var allRatings = await _firebaseClient
                    .Child(Constants.ServiceRatingsCollection)
                    .OnceAsync<ServiceRating>();

                var existingRating = allRatings
                    .Select(r => r.Object)
                    .FirstOrDefault(r => r.ServiceRequestId == serviceRequestId && r.ReviewerId == userId);

                if (existingRating != null)
                {
                    return ServiceResult<bool>.FailureResult("Bu hizmeti zaten değerlendirdiniz");
                }

                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult(
                    "Kontrol başarısız",
                    ex.Message);
            }
        }

        private async Task<UserRatingStats> CalculateUserRatingStatsAsync(string userId)
        {
            var allRatings = await _firebaseClient
                .Child(Constants.ServiceRatingsCollection)
                .OnceAsync<ServiceRating>();

            var userRatings = allRatings
                .Select(r => r.Object)
                .Where(r => r.RatedUserId == userId && !r.IsHidden)
                .ToList();

            var stats = new UserRatingStats
            {
                UserId = userId,
                TotalRatings = userRatings.Count,
                FiveStars = userRatings.Count(r => r.Stars == 5),
                FourStars = userRatings.Count(r => r.Stars == 4),
                ThreeStars = userRatings.Count(r => r.Stars == 3),
                TwoStars = userRatings.Count(r => r.Stars == 2),
                OneStar = userRatings.Count(r => r.Stars == 1),
                AverageStars = userRatings.Any() ? userRatings.Average(r => r.Stars) : 0,
                LastUpdated = DateTime.UtcNow
            };

            return stats;
        }

        private async Task UpdateUserRatingStatsAsync(string userId)
        {
            var stats = await CalculateUserRatingStatsAsync(userId);
            await _firebaseClient
                .Child(UserRatingStatsCollection)
                .Child(userId)
                .PutAsync(stats);
        }
    }
}
