using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using KamPay.API.Models;
using KamPay.API.Services;

namespace KamPay.API.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class ProductsController : ControllerBase
    {
        private readonly IProductService _productService;

        public ProductsController(IProductService productService)
        {
            _productService = productService;
        }

        // GET: api/v1/products
        [HttpGet]
        public async Task<IActionResult> GetProducts()
        {
            try
            {
                var result = await _productService.GetAllProductsAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Sunucu hatası: {ex.Message}");
            }
        }

        // GET: api/v1/products/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetProductById(string id)
        {
            try
            {
                var product = await _productService.GetProductByIdAsync(id);
                if (product == null)
                    return NotFound("Ürün bulunamadı.");

                return Ok(product);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Sunucu hatası: {ex.Message}");
            }
        }

        // GET: api/v1/products/user/{userId}
        [HttpGet("user/{userId}")]
        [Authorize]
        public async Task<IActionResult> GetUserProducts(string userId)
        {
            try
            {
                var myUserId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(myUserId)) return Unauthorized();

                var result = await _productService.GetUserProductsAsync(userId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Sunucu hatası: {ex.Message}");
            }
        }

        // POST: api/v1/products
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> AddProduct([FromBody] Product yeniUrun)
        {
            try
            {
                var userId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(userId))
                    return Unauthorized("Geçersiz kullanıcı token'ı.");

                if (yeniUrun.Price < 0)
                    return BadRequest("Fiyat sıfırdan küçük olamaz.");

                var productId = await _productService.CreateProductAsync(yeniUrun, userId);

                return Ok(new { Message = "Ürün başarıyla eklendi", ProductId = productId });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Sunucu hatası: {ex.Message}");
            }
        }

        // PUT: api/v1/products/{id} (İlan Güncelleme)
        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> UpdateProduct(string id, [FromBody] Product guncelUrun)
        {
            try
            {
                var userId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId)) return Unauthorized();

                var mevcutUrunSnapshot = await _productService.GetProductByIdAsync(id);
                if (mevcutUrunSnapshot == null) return NotFound("Ürün bulunamadı.");

                if (mevcutUrunSnapshot.UserId != userId)
                {
                    return Forbid("Bu ilanı güncelleme yetkiniz yok!");
                }

                await _productService.UpdateProductAsync(id, guncelUrun, userId);

                return Ok(new { Message = "İlan başarıyla güncellendi." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        // DELETE: api/v1/products/{id} (İlan Silme)
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteProduct(string id)
        {
            try
            {
                var userId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId)) return Unauthorized();

                var mevcutUrun = await _productService.GetProductByIdAsync(id);
                if (mevcutUrun == null) return NotFound();

                if (mevcutUrun.UserId != userId) return Forbid("Bu ilanı silme yetkiniz yok.");

                await _productService.DeleteProductAsync(id, userId);

                return Ok(new { Message = "İlan başarıyla silindi." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }
}
