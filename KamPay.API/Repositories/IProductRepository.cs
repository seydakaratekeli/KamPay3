using KamPay.API.Models;

namespace KamPay.API.Repositories
{
    public interface IProductRepository
    {
        Task<List<Product>> GetAllAsync(int limit = 50);
        Task<Product?> GetByIdAsync(string id);
        Task<string> AddAsync(Product product);
        Task UpdateAsync(string id, Product product);
        Task DeleteAsync(string id);
    }
}
