using KamPay.Models;
using System.Threading;
using System.Threading.Tasks;

namespace KamPay.Services
{
    /// <summary>
    /// ISP: Sadece ürün sorgulama metodları.
    /// </summary>
    public interface IProductQueryService
    {
        Task<ServiceResult<Product>> GetProductByIdAsync(string productId, CancellationToken cancellationToken = default);
        Task<ServiceResult<List<Product>>> GetAllProductsAsync(ProductFilter? filter = null, CancellationToken cancellationToken = default);
        Task<ServiceResult<List<Product>>> GetUserProductsAsync(string userId, CancellationToken cancellationToken = default);
        Task<ServiceResult<List<Category>>> GetCategoriesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Cursor-based sayfalama. cursor=null → ilk sayfa; cursor değeri → sonraki sayfa.
        /// </summary>
        Task<ServiceResult<ProductPagedResponse>> GetProductsPagedAsync(
            int pageSize = 20,
            string? cursor = null,
            ProductFilter? filter = null,
            CancellationToken cancellationToken = default);
    }
}
