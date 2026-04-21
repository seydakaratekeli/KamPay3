using KamPay.API.Models;

namespace KamPay.API.Services.Products
{
    public interface IProductService
    {
        Task<List<Product>> GetAllProductsAsync();
        Task<PagedResult<Product>> GetProductsPagedAsync(int pageSize, string? cursor, ProductQueryOptions? options = null);
        Task<List<Product>> GetUserProductsAsync(string userId);
        Task<Product?> GetProductByIdAsync(string id);
        Task<string> CreateProductAsync(Product product, string userId);
        Task<UpdateResult> UpdateProductAsync(string id, Product updatedProduct, string userId); // bool → UpdateResult
        Task<DeleteResult> DeleteProductAsync(string id, string userId); // bool → DeleteResult
    }
}