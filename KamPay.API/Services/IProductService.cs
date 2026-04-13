using KamPay.API.Models;

namespace KamPay.API.Services
{
    public interface IProductService
    {
        Task<List<Product>> GetAllProductsAsync();
        Task<Product?> GetProductByIdAsync(string id);
        Task<string> CreateProductAsync(Product product, string userId);
        Task<bool> UpdateProductAsync(string id, Product updatedProduct, string userId);
        Task<bool> DeleteProductAsync(string id, string userId);
    }
}
