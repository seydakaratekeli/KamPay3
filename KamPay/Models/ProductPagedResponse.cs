namespace KamPay.Models
{
    /// <summary>
    /// API'nin GET /api/v1/products endpoint'inden dönen cursor-based sayfalama yanıtı.
    /// </summary>
    public class ProductPagedResponse
    {
        public List<Product> Items { get; set; } = new();
        public string? NextCursor { get; set; }
        public bool HasMore { get; set; }
    }
}
