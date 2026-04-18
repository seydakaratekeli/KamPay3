using KamPay.API.Models;
using KamPay.API.Repositories;

namespace KamPay.API.Services.Products
{
    public class ProductService : IProductService
    {
        private readonly IProductRepository _productRepository;

        public ProductService(IProductRepository productRepository)
        {
            _productRepository = productRepository;
        }

        public async Task<List<Product>> GetAllProductsAsync()
        {
            return await _productRepository.GetAllAsync();
        }

        public async Task<PagedResult<Product>> GetProductsPagedAsync(int pageSize, string? cursor, ProductQueryOptions? options = null)
        {
            return await _productRepository.GetPagedAsync(pageSize, cursor, options);
        }

        public async Task<List<Product>> GetUserProductsAsync(string userId)
        {
            return await _productRepository.GetByUserIdAsync(userId);
        }

        public async Task<Product?> GetProductByIdAsync(string id)
        {
            return await _productRepository.GetByIdAsync(id);
        }

        public async Task<string> CreateProductAsync(Product product, string userId)
        {
            product.UserId = userId;
            product.CreatedAt = DateTime.UtcNow;
            return await _productRepository.AddAsync(product);
        }

        public async Task<bool> UpdateProductAsync(string id, Product updatedProduct, string userId)
        {
            var existingProduct = await _productRepository.GetByIdAsync(id);
            if (existingProduct == null || existingProduct.UserId != userId)
                return false;

            existingProduct.Title = updatedProduct.Title;
            existingProduct.Price = updatedProduct.Price;
            existingProduct.Description = updatedProduct.Description;
            existingProduct.UpdatedAt = DateTime.UtcNow;

            await _productRepository.UpdateAsync(id, existingProduct);
            return true;
        }

        public async Task<bool> DeleteProductAsync(string id, string userId)
        {
            var existingProduct = await _productRepository.GetByIdAsync(id);
            if (existingProduct == null || existingProduct.UserId != userId)
                return false;

            await _productRepository.DeleteAsync(id);
            return true;
        }
    }
}
