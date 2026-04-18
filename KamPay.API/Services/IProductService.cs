using KamPay.API.Models;

namespace KamPay.API.Services
{
    public interface IProductService
    {
        Task<List<Product>> GetAllProductsAsync();
        Task<PagedResult<Product>> GetProductsPagedAsync(int pageSize, string? cursor, ProductQueryOptions? options = null);
        Task<List<Product>> GetUserProductsAsync(string userId);
        Task<Product?> GetProductByIdAsync(string id);
        Task<string> CreateProductAsync(Product product, string userId);
        Task<bool> UpdateProductAsync(string id, Product updatedProduct, string userId);
        Task<bool> DeleteProductAsync(string id, string userId);
    }
}
