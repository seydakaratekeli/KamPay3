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

        // Adım 3'te Program.cs'de eklediğimiz FirebaseClient buraya otomatik gelir
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
                // Sunucumuz (API) Firebase'e gidip ürünleri alıyor
                var urunler = await _firebaseClient
                    .Child("products") // Senin Constants.ProductsCollection karşılığın
                    .OrderByKey()
                    .LimitToLast(50)
                    .OnceAsync<object>(); // Şimdilik obje olarak çekiyoruz, daha sonra Product modelini API'ye de ekleyeceğiz.

                var result = urunler.Select(x => new {
                    ProductId = x.Key,
                    Data = x.Object
                }).ToList();

                // MAUI'ye JSON olarak gönder
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Sunucu hatası: {ex.Message}");
            }
        }
    

    // POST: api/v1/products
[HttpPost]
        [Authorize] // DİKKAT: Bu etiket sayesinde giriş yapmayanlar bu metoda asla ulaşamaz!
        public async Task<IActionResult> AddProduct([FromBody] Product yeniUrun)
        {
            try
            {
                // 1. GÜVENLİK: İstek atan kişinin Token'ından Firebase UserId'sini alıyoruz
                var userId = User.Claims.FirstOrDefault(c => c.Type == "user_id")?.Value;

                if (string.IsNullOrEmpty(userId))
                    return Unauthorized("Geçersiz kullanıcı token'ı.");

                // 2. İŞ MANTIĞI: İlanın sahibini (UserId) mobil uygulamanın göndermesine 
                // güvenmiyoruz. Sunucudaki doğrulanmış kimliği (userId) zorla atıyoruz!
                yeniUrun.UserId = userId;
                yeniUrun.CreatedAt = DateTime.UtcNow;

                // Fiyat kontrolü vs. burada yapılabilir
                if (yeniUrun.Price < 0)
                    return BadRequest("Fiyat sıfırdan küçük olamaz.");

                // 3. VERİTABANI: Ürünü Firebase'e kaydet
                var response = await _firebaseClient.Child("products").PostAsync(yeniUrun);

                // Geriye kaydedilen ürünün yeni ID'sini dönüyoruz
                return Ok(new { Message = "Ürün başarıyla eklendi", ProductId = response.Key });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Sunucu hatası: {ex.Message}");
            }
        }
    }
}
