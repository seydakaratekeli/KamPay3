using System;
using System.Collections.Generic;

namespace KamPay.Models
{
    /// <summary>
    /// Destek ticket sistemi - Kullanıcılar arası sorunları çözmek için
    /// </summary>
    public class SupportTicket
    {
        /// <summary>
        /// Benzersiz ticket kimliği (GUID)
        /// </summary>
        public string TicketId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// İnsan dostu ticket numarası (örn: ST2025000001)
        /// </summary>
        public string TicketNumber { get; set; }

        /// <summary>
        /// İlgili işlem kimliği
        /// </summary>
        public string TransactionId { get; set; }

        /// <summary>
        /// İlgili ürün başlığı (hızlı referans için)
        /// </summary>
        public string ProductTitle { get; set; }

        /// <summary>
        /// Ticket'ı oluşturan kullanıcı kimliği
        /// </summary>
        public string CreatedByUserId { get; set; }

        /// <summary>
        /// Diğer taraf kullanıcı kimliği (sadece bu iki kullanıcı görebilir)
        /// </summary>
        public string OtherPartyUserId { get; set; }

        /// <summary>
        /// Ticket kategorisi
        /// </summary>
        public TicketCategory Category { get; set; }

        /// <summary>
        /// Ticket konusu
        /// </summary>
        public string Subject { get; set; }

        /// <summary>
        /// Detaylı açıklama
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Ticket durumu
        /// </summary>
        public TicketStatus Status { get; set; } = TicketStatus.Open;

        /// <summary>
        /// Ticket mesajları
        /// </summary>
        public List<TicketMessage> Messages { get; set; } = new List<TicketMessage>();

        /// <summary>
        /// Oluşturulma tarihi
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Son güncellenme tarihi
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Çözüm tarihi
        /// </summary>
        public DateTime? ResolvedAt { get; set; }

        /// <summary>
        /// Kapatılma tarihi
        /// </summary>
        public DateTime? ClosedAt { get; set; }

        /// <summary>
        /// Admin tarafından görüldü mü?
        /// </summary>
        public bool IsAdminViewed { get; set; } = false;

        /// <summary>
        /// Admin notları (sadece admin için görünür)
        /// </summary>
        public string AdminNotes { get; set; }

        /// <summary>
        /// Öncelik seviyesi
        /// </summary>
        public TicketPriority Priority { get; set; } = TicketPriority.Normal;
    }

    /// <summary>
    /// Ticket mesaj modeli
    /// </summary>
    public class TicketMessage
    {
        /// <summary>
        /// Mesaj kimliği
        /// </summary>
        public string MessageId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Mesajı gönderen kullanıcı kimliği
        /// </summary>
        public string SenderId { get; set; }

        /// <summary>
        /// Gönderen kullanıcı adı (hızlı erişim için)
        /// </summary>
        public string SenderName { get; set; }

        /// <summary>
        /// Mesaj içeriği
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// Mesaj gönderilme tarihi
        /// </summary>
        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Admin tarafından mı gönderildi?
        /// </summary>
        public bool IsFromAdmin { get; set; } = false;

        /// <summary>
        /// Ekli dosya URL'leri (varsa)
        /// </summary>
        public List<string> AttachmentUrls { get; set; } = new List<string>();
    }

    /// <summary>
    /// Ticket kategorileri
    /// </summary>
    public enum TicketCategory
    {
        /// <summary>
        /// Teslimat sorunu
        /// </summary>
        DeliveryIssue = 0,

        /// <summary>
        /// Ürün kalitesi sorunu
        /// </summary>
        ProductQuality = 1,

        /// <summary>
        /// Ödeme sorunu
        /// </summary>
        PaymentIssue = 2,

        /// <summary>
        /// İletişim sorunu
        /// </summary>
        CommunicationIssue = 3,

        /// <summary>
        /// Ürün açıklaması uyuşmazlığı
        /// </summary>
        DescriptionMismatch = 4,

        /// <summary>
        /// İptal talebi
        /// </summary>
        CancellationRequest = 5,

        /// <summary>
        /// İade talebi
        /// </summary>
        RefundRequest = 6,

        /// <summary>
        /// Diğer
        /// </summary>
        Other = 7
    }

    /// <summary>
    /// Ticket durumları
    /// </summary>
    public enum TicketStatus
    {
        /// <summary>
        /// Açık - Yeni oluşturulmuş
        /// </summary>
        Open = 0,

        /// <summary>
        /// İşlemde - Kullanıcılar arası çözüm aşamasında
        /// </summary>
        InProgress = 1,

        /// <summary>
        /// Admin bekleniyor - Kullanıcılar çözemedi, admin müdahalesi gerekli
        /// </summary>
        WaitingForAdmin = 2,

        /// <summary>
        /// Çözüldü - Sorun çözüldü
        /// </summary>
        Resolved = 3,

        /// <summary>
        /// Kapatıldı - Ticket kapatıldı
        /// </summary>
        Closed = 4
    }

    /// <summary>
    /// Ticket öncelik seviyeleri
    /// </summary>
    public enum TicketPriority
    {
        /// <summary>
        /// Düşük öncelik
        /// </summary>
        Low = 0,

        /// <summary>
        /// Normal öncelik
        /// </summary>
        Normal = 1,

        /// <summary>
        /// Yüksek öncelik
        /// </summary>
        High = 2,

        /// <summary>
        /// Acil
        /// </summary>
        Urgent = 3
    }
}
