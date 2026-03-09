using KamPay.Models;
using System.Threading.Tasks;

namespace KamPay.Services
{
    /// <summary>
    /// ? ISP (Interface Segregation Principle): Sadece validasyon ve yardýmcý metodlar
    /// </summary>
    public interface IProductValidationService
    {
        ValidationResult ValidateProduct(ProductRequest request);
        Task<ServiceResult<bool>> IncrementViewCountAsync(string productId);
    }
}
