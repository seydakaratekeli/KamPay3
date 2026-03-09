using KamPay.Models;
using System.Threading.Tasks;

namespace KamPay.Services
{
    /// <summary>
    /// ? ISP (Interface Segregation Principle): Sadece ürün yazma/güncelleme metodlarý
    /// Write iþlemler için ayrý interface
    /// </summary>
    public interface IProductCommandService
    {
        Task<ServiceResult<Product>> AddProductAsync(ProductRequest request, User currentUser);
        Task<ServiceResult<Product>> UpdateProductAsync(string productId, ProductRequest request);
        Task<ServiceResult<bool>> DeleteProductAsync(string productId);
        Task<ServiceResult<bool>> UpdateProductOwnerAsync(string productId, string newOwnerId, bool markAsSold = true);
        Task<ServiceResult<bool>> MarkAsSoldAsync(string productId);
        Task<ServiceResult<bool>> MarkAsExchangedAsync(string productId);
        Task<ServiceResult<bool>> MarkAsReservedAsync(string productId, bool isReserved);
        Task<ServiceResult<Product>> SaveProductDirectlyAsync(Product product);
        Task<ServiceResult<bool>> UpdateUserInfoInProductsAsync(string userId, string? newName, string? newPhotoUrl);
    }
}
