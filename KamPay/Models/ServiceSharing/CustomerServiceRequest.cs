using System;
using System.Collections.Generic;

namespace KamPay.Models
{
    /// <summary>
    /// ?? ARMUT MODELÝ: Müþterinin oluþturduðu hizmet talebi
    /// Profesyoneller bu taleplere teklif gönderir
    /// </summary>
    public class CustomerServiceRequest
    {
        /// <summary>
        /// Benzersiz talep kimliði
        /// </summary>
        public string RequestId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Ýnsan dostu talep numarasý (örn: CSR2025000001)
        /// </summary>
        public string RequestNumber { get; set; } = "";

        /// <summary>
        /// Talebi oluþturan müþteri kimliði
        /// </summary>
        public string CustomerId { get; set; } = "";

        /// <summary>
        /// Müþteri adý (hýzlý eriþim için)
        /// </summary>
        public string CustomerName { get; set; } = "";

        /// <summary>
        /// Müþteri profil fotoðrafý
        /// </summary>
        public string CustomerPhotoUrl { get; set; } = "default_avatar.png";

        /// <summary>
        /// Hizmet kategorisi
        /// </summary>
        public ServiceCategory Category { get; set; }

        /// <summary>
        /// Talep baþlýðý (örn: "50m² Salon Boyasý Ýþi")
        /// </summary>
        public string Title { get; set; } = "";

        /// <summary>
        /// Detaylý açýklama
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// Ýþin yapýlmasý istenen tarih
        /// </summary>
        public DateTime? PreferredDate { get; set; }

        /// <summary>
        /// Ýþin yapýlacaðý konum
        /// </summary>
        public string Location { get; set; } = "";

        /// <summary>
        /// Konum enlem
        /// </summary>
        public double? Latitude { get; set; }

        /// <summary>
        /// Konum boylam
        /// </summary>
        public double? Longitude { get; set; }

        /// <summary>
        /// Müþterinin beklediði bütçe aralýðý (min)
        /// </summary>
        public decimal? BudgetMin { get; set; }

        /// <summary>
        /// Müþterinin beklediði bütçe aralýðý (max)
        /// </summary>
        public decimal? BudgetMax { get; set; }

        /// <summary>
        /// Para birimi
        /// </summary>
        public string Currency { get; set; } = "TRY";

        /// <summary>
        /// Talep görselleri (iþ yerinin fotoðraflarý)
        /// </summary>
        public List<string> ImageUrls { get; set; } = new();

        /// <summary>
        /// Oluþturulma tarihi
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Güncellenme tarihi
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Talep durumu
        /// </summary>
        public CustomerRequestStatus Status { get; set; } = CustomerRequestStatus.Open;

        /// <summary>
        /// Son teklif alma tarihi (opsiyonel)
        /// </summary>
        public DateTime? Deadline { get; set; }

        /// <summary>
        /// Gelen teklif sayýsý
        /// </summary>
        public int ProposalCount { get; set; } = 0;

        /// <summary>
        /// Seçilen teklif ID'si (müþteri bir teklifi kabul edince)
        /// </summary>
        public string? SelectedProposalId { get; set; }

        /// <summary>
        /// Talebin aktif olup olmadýðý (arþivlendi mi?)
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Müþteri notlarý (iç notlar)
        /// </summary>
        public string CustomerNotes { get; set; } = "";

        /// <summary>
        /// UI için bütçe metni
        /// </summary>
        public string BudgetText
        {
            get
            {
                if (BudgetMin.HasValue && BudgetMax.HasValue)
                    return $"{BudgetMin:N0} - {BudgetMax:N0} {Currency}";
                else if (BudgetMin.HasValue)
                    return $"Min: {BudgetMin:N0} {Currency}";
                else if (BudgetMax.HasValue)
                    return $"Max: {BudgetMax:N0} {Currency}";
                return "Bütçe belirtilmedi";
            }
        }

        /// <summary>
        /// UI için tarih metni
        /// </summary>
        public string PreferredDateText => PreferredDate?.ToString("dd MMMM yyyy") ?? "Belirtilmedi";

        /// <summary>
        /// UI için durum metni
        /// </summary>
        public string StatusText => Status switch
        {
            CustomerRequestStatus.Open => "?? Teklif Bekleniyor",
            CustomerRequestStatus.Evaluating => "?? Teklifler Deðerlendiriliyor",
            CustomerRequestStatus.ProviderSelected => "? Profesyonel Seçildi",
            CustomerRequestStatus.InProgress => "?? Ýþ Devam Ediyor",
            CustomerRequestStatus.Completed => "? Tamamlandý",
            CustomerRequestStatus.Cancelled => "? Ýptal Edildi",
            CustomerRequestStatus.Expired => "? Süresi Doldu",
            _ => "Bilinmiyor"
        };
    }

    /// <summary>
    /// Müþteri talep durumlarý
    /// </summary>
    public enum CustomerRequestStatus
    {
        /// <summary>
        /// Açýk - Teklif bekleniyor
        /// </summary>
        Open = 0,

        /// <summary>
        /// Teklifler deðerlendiriliyor
        /// </summary>
        Evaluating = 1,

        /// <summary>
        /// Profesyonel seçildi
        /// </summary>
        ProviderSelected = 2,

        /// <summary>
        /// Ýþ devam ediyor
        /// </summary>
        InProgress = 3,

        /// <summary>
        /// Tamamlandý
        /// </summary>
        Completed = 4,

        /// <summary>
        /// Ýptal edildi
        /// </summary>
        Cancelled = 5,

        /// <summary>
        /// Süresi doldu
        /// </summary>
        Expired = 6
    }
}
