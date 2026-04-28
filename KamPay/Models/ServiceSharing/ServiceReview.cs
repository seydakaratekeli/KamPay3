using System;

namespace KamPay.Models
{
    public class ServiceReview
    {
        public string ReviewId { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty; // Hangi işlem/talep için yapıldı
        public string ProviderId { get; set; } = string.Empty; // Değerlendirilen profesyonel
        public string ReviewerId { get; set; } = string.Empty; // Değerlendiren müşteri
        public string ReviewerName { get; set; } = string.Empty;
        public double Rating { get; set; } // 1 ile 5 arası yıldız
        public string Comment { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
