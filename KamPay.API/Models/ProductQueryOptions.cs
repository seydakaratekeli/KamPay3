namespace KamPay.API.Models
{
    public class ProductQueryOptions
    {
        public string? CategoryId { get; set; }
        public ProductType? Type { get; set; }
        public string? Search { get; set; }
    }

    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public string? NextCursor { get; set; }
        public bool HasMore { get; set; }
    }
}
