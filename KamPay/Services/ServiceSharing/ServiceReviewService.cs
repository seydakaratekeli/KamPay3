using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Helpers;
using KamPay.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace KamPay.Services
{
    public class ServiceReviewService : IServiceReviewService
    {
        private readonly FirebaseClient _firebaseClient;

        public ServiceReviewService(FirebaseClient firebaseClient)
        {
            _firebaseClient = firebaseClient ?? throw new ArgumentNullException(nameof(firebaseClient));
        }

        public async Task<ServiceResult<bool>> CreateReviewAsync(ServiceReview review)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(review.ReviewId))
                {
                    review.ReviewId = Guid.NewGuid().ToString();
                }
                
                review.CreatedAt = DateTime.UtcNow;

                await _firebaseClient
                    .Child(Constants.ServiceReviewsCollection)
                    .Child(review.ReviewId)
                    .PutAsync(review);

                return ServiceResult<bool>.SuccessResult(true, "Değerlendirme başarıyla kaydedildi.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ CreateReviewAsync hatası: {ex.Message}");
                return ServiceResult<bool>.FailureResult("Değerlendirme kaydedilemedi.", ex.Message);
            }
        }

        public async Task<ServiceResult<List<ServiceReview>>> GetProviderReviewsAsync(string providerId)
        {
            try
            {
                var reviews = await _firebaseClient
                    .Child(Constants.ServiceReviewsCollection)
                    .OrderBy("ProviderId")
                    .EqualTo(providerId)
                    .OnceAsync<ServiceReview>();

                var reviewList = reviews.Select(r => r.Object).OrderByDescending(r => r.CreatedAt).ToList();

                return ServiceResult<List<ServiceReview>>.SuccessResult(reviewList);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ GetProviderReviewsAsync hatası: {ex.Message}");
                return ServiceResult<List<ServiceReview>>.FailureResult("Değerlendirmeler getirilemedi.", ex.Message);
            }
        }

        public async Task<ServiceResult<double>> GetProviderAverageRatingAsync(string providerId)
        {
            try
            {
                var reviewsResult = await GetProviderReviewsAsync(providerId);
                
                if (!reviewsResult.Success || reviewsResult.Data == null || !reviewsResult.Data.Any())
                {
                    return ServiceResult<double>.SuccessResult(0);
                }

                var average = reviewsResult.Data.Average(r => r.Rating);
                return ServiceResult<double>.SuccessResult(Math.Round(average, 1));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ GetProviderAverageRatingAsync hatası: {ex.Message}");
                return ServiceResult<double>.FailureResult("Ortalama puan hesaplanamadı.", ex.Message);
            }
        }

        public async Task<ServiceResult<int>> GetProviderTotalCompletedJobsAsync(string providerId)
        {
            try
            {
                // İşlem sayısı hesaplamak için hem review sayısına bakabiliriz hem de ServiceRequests'e
                // En garantilisi ServiceRequests tablosundaki "Completed" statüsündeki işlerdir.
                var requests = await _firebaseClient
                    .Child(Constants.ServiceRequestsCollection)
                    .OrderBy("ProviderId")
                    .EqualTo(providerId)
                    .OnceAsync<ServiceRequest>();

                var completedCount = requests.Select(r => r.Object).Count(r => r.Status == ServiceRequestStatus.Completed);

                return ServiceResult<int>.SuccessResult(completedCount);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ GetProviderTotalCompletedJobsAsync hatası: {ex.Message}");
                return ServiceResult<int>.FailureResult("Tamamlanan iş sayısı alınamadı.", ex.Message);
            }
        }
    }
}
