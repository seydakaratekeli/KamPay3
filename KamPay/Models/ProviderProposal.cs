using System;
using System.Collections.Generic;

namespace KamPay.Models
{
    /// <summary>
    /// ?? ARMUT MODELÝ: Profesyonelin müþteri talebine gönderdiði teklif
    /// </summary>
    public class ProviderProposal
    {
        /// <summary>
        /// Benzersiz teklif kimliði
        /// </summary>
        public string ProposalId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Ýnsan dostu teklif numarasý (örn: PP2025000001)
        /// </summary>
        public string ProposalNumber { get; set; } = "";

        /// <summary>
        /// Ýlgili müþteri talebi ID'si
        /// </summary>
        public string CustomerRequestId { get; set; } = "";

        /// <summary>
        /// Talep baþlýðý (hýzlý referans)
        /// </summary>
        public string RequestTitle { get; set; } = "";

        /// <summary>
        /// Teklifi gönderen profesyonel kimliði
        /// </summary>
        public string ProviderId { get; set; } = "";

        /// <summary>
        /// Profesyonel adý
        /// </summary>
        public string ProviderName { get; set; } = "";

        /// <summary>
        /// Profesyonel profil fotoðrafý
        /// </summary>
        public string ProviderPhotoUrl { get; set; } = "default_avatar.png";

        /// <summary>
        /// Müþteri kimliði (bildirim için)
        /// </summary>
        public string CustomerId { get; set; } = "";

        /// <summary>
        /// Müþteri adý (hýzlý eriþim)
        /// </summary>
        public string CustomerName { get; set; } = "";

        /// <summary>
        /// Teklif edilen fiyat
        /// </summary>
        public decimal Price { get; set; }

        /// <summary>
        /// Para birimi
        /// </summary>
        public string Currency { get; set; } = "TRY";

        /// <summary>
        /// Profesyonelin teklif mesajý
        /// </summary>
        public string Message { get; set; } = "";

        /// <summary>
        /// Tahmini tamamlanma süresi (gün cinsinden)
        /// </summary>
        public int EstimatedDays { get; set; } = 1;

        /// <summary>
        /// Profesyonelin referans çalýþmalarý (fotoðraflar)
        /// </summary>
        public List<string> PortfolioImageUrls { get; set; } = new();

        /// <summary>
        /// Profesyonelin deneyim yýlý
        /// </summary>
        public int ExperienceYears { get; set; } = 0;

        /// <summary>
        /// Profesyonelin puaný (0-5 arasý)
        /// </summary>
        public double Rating { get; set; } = 0;

        /// <summary>
        /// Tamamlanan iþ sayýsý
        /// </summary>
        public int CompletedJobsCount { get; set; } = 0;

        /// <summary>
        /// Teklif oluþturma tarihi
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Güncellenme tarihi
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Teklif durumu
        /// </summary>
        public ProposalStatus Status { get; set; } = ProposalStatus.Pending;

        /// <summary>
        /// Müþteri tarafýndan görüldü mü?
        /// </summary>
        public bool IsViewedByCustomer { get; set; } = false;

        /// <summary>
        /// Görülme tarihi
        /// </summary>
        public DateTime? ViewedAt { get; set; }

        /// <summary>
        /// Kabul/Red tarihi
        /// </summary>
        public DateTime? RespondedAt { get; set; }

        /// <summary>
        /// Müþterinin red etme nedeni
        /// </summary>
        public string RejectionReason { get; set; } = "";

        /// <summary>
        /// Teklif geçerlilik süresi
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Teklif detaylarý (opsiyonel - JSON formatýnda ek bilgiler)
        /// </summary>
        public string Details { get; set; } = "";

        /// <summary>
        /// Ýþ sözleþmesi oluþturuldu mu? (Teklif kabul edilince)
        /// </summary>
        public bool IsContractCreated { get; set; } = false;

        /// <summary>
        /// Sözleþme ID'si
        /// </summary>
        public string? ServiceContractId { get; set; }

        /// <summary>
        /// UI için fiyat metni
        /// </summary>
        public string PriceText => $"{Price:N2} {Currency}";

        /// <summary>
        /// UI için süre metni
        /// </summary>
        public string EstimatedTimeText => EstimatedDays == 1 
            ? "1 gün" 
            : $"{EstimatedDays} gün";

        /// <summary>
        /// UI için durum metni
        /// </summary>
        public string StatusText => Status switch
        {
            ProposalStatus.Pending => "? Müþteri Cevabý Bekleniyor",
            ProposalStatus.Accepted => "? Kabul Edildi",
            ProposalStatus.Rejected => "? Reddedildi",
            ProposalStatus.Withdrawn => "?? Geri Çekildi",
            ProposalStatus.Expired => "? Süresi Doldu",
            _ => "Bilinmiyor"
        };

        /// <summary>
        /// Profesyonel deneyim metni
        /// </summary>
        public string ExperienceText => ExperienceYears > 0 
            ? $"{ExperienceYears} yýl deneyim" 
            : "Yeni profesyonel";

        /// <summary>
        /// Puanlama metni
        /// </summary>
        public string RatingText => Rating > 0 
            ? $"? {Rating:0.0} ({CompletedJobsCount} iþ)" 
            : "Henüz deðerlendirme yok";
    }

    /// <summary>
    /// Teklif durumlarý
    /// </summary>
    public enum ProposalStatus
    {
        /// <summary>
        /// Beklemede - Müþteri henüz cevap vermedi
        /// </summary>
        Pending = 0,

        /// <summary>
        /// Kabul edildi
        /// </summary>
        Accepted = 1,

        /// <summary>
        /// Reddedildi
        /// </summary>
        Rejected = 2,

        /// <summary>
        /// Profesyonel tarafýndan geri çekildi
        /// </summary>
        Withdrawn = 3,

        /// <summary>
        /// Süresi doldu
        /// </summary>
        Expired = 4
    }
}
