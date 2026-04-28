using KamPay.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KamPay.Services
{
    public interface IServiceReviewService
    {
        Task<ServiceResult<bool>> CreateReviewAsync(ServiceReview review);
        Task<ServiceResult<List<ServiceReview>>> GetProviderReviewsAsync(string providerId);
        Task<ServiceResult<double>> GetProviderAverageRatingAsync(string providerId);
        Task<ServiceResult<int>> GetProviderTotalCompletedJobsAsync(string providerId);
    }
}
