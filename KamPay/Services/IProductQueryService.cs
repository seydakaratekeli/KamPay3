using KamPay.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KamPay.Services
{
    /// <summary>
    /// ? ISP (Interface Segregation Principle): Sadece ürün sorgulama metodlarý
    /// Read-only iþlemler için ayrý interface
    /// </summary>
    public interface IProductQueryService
    {
        Task<ServiceResult<Product>> GetProductByIdAsync(string productId);
        Task<ServiceResult<List<Product>>> GetAllProductsAsync(ProductFilter? filter = null);
        Task<ServiceResult<List<Product>>> GetUserProductsAsync(string userId);
        Task<ServiceResult<List<Product>>> GetProductsAsync(string? categoryId = null, string? searchText = null);
        Task<ServiceResult<List<Product>>> GetProductsPagedAsync(
            int pageSize = 20,
            string? lastKey = null,
            ProductFilter? filter = null);
        Task<ServiceResult<List<Category>>> GetCategoriesAsync();
    }
}
