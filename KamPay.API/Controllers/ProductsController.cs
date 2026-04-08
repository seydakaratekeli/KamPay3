using Microsoft.AspNetCore.Mvc;
using Firebase.Database;
using Firebase.Database.Query;
using Microsoft.AspNetCore.Authorization;
using KamPay.API.Models;

namespace KamPay.API.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class ProductsController : ControllerBase
    {
        private readonly FirebaseClient _firebaseClient;

        public ProductsController(FirebaseClient firebaseClient)
        {
            _firebaseClient = firebaseClient;
        }

        // GET: api/v1/products
        [HttpGet]
        public async Task<IActionResult> GetProducts()
        {
            try
            {
                var urunler = await _firebaseClient
                    .Child("products")
                    .OrderByKey()
                    .LimitToLast(50)
                    .OnceAsync<Product>();

                var result = urunler.Select(x =>
                {
                    var urun = x.Object;
                    urun.ProductId = x.Key;
                    return urun;
                }).ToList();

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
                var userId = User.Claims.FirstOrDefault(c => c.Type == "user_id")?.Value;

                if (string.IsNullOrEmpty(userId))
                    return Unauthorized("Geçersiz kullanıcı token'ı.");

                yeniUrun.UserId = userId;
                yeniUrun.CreatedAt = DateTime.UtcNow;

                if (yeniUrun.Price < 0)
                    return BadRequest("Fiyat sıfırdan küçük olamaz.");

                var response = await _firebaseClient.Child("products").PostAsync(yeniUrun);

                return Ok(new { Message = "Ürün başarıyla eklendi", ProductId = response.Key });
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
                var userId = User.Claims.FirstOrDefault(c => c.Type == "user_id")?.Value;
                if (string.IsNullOrEmpty(userId)) return Unauthorized();

                var mevcutUrunSnapshot = await _firebaseClient.Child("products").Child(id).OnceSingleAsync<Product>();
                if (mevcutUrunSnapshot == null) return NotFound("Ürün bulunamadı.");

                if (mevcutUrunSnapshot.UserId != userId)
                {
                    return Forbid("Bu ilanı güncelleme yetkiniz yok!");
                }

                mevcutUrunSnapshot.Title = guncelUrun.Title;
                mevcutUrunSnapshot.Price = guncelUrun.Price;
                mevcutUrunSnapshot.Description = guncelUrun.Description;
                mevcutUrunSnapshot.UpdatedAt = DateTime.UtcNow;

                await _firebaseClient.Child("products").Child(id).PutAsync(mevcutUrunSnapshot);

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
                var userId = User.Claims.FirstOrDefault(c => c.Type == "user_id")?.Value;
                if (string.IsNullOrEmpty(userId)) return Unauthorized();

                var mevcutUrun = await _firebaseClient.Child("products").Child(id).OnceSingleAsync<Product>();
                if (mevcutUrun == null) return NotFound();

                if (mevcutUrun.UserId != userId) return Forbid("Bu ilanı silme yetkiniz yok.");

                await _firebaseClient.Child("products").Child(id).DeleteAsync();

                return Ok(new { Message = "İlan başarıyla silindi." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }
}
