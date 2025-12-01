namespace KamPay.Models
{
    public class DeliveryPhotoUploadResult
    {
        public string FullPhotoUrl { get; set; } = string.Empty;
        public string ThumbnailUrl { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }
}
