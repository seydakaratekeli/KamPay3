using System;
using System.Collections.Generic;

namespace KamPay.Models
{
    //SONRA BAKILIR ŞU AN KALSIN
    // burda sistem arabulucuğu - kullanıcılar arası sorunları çözmek için destek ticket sistemi oluşturulacak-  BUNU ADMIN PANELİNE BAĞLA-  KULLANICI ARAYÜZÜNDE GÖSTER - 
  
    /// Destek ticket sistemi - Kullanıcılar arası sorunları çözmek için
    
    public class SupportTicket
    {
        
        /// Benzersiz ticket kimliği (GUID)
        
        public string TicketId { get; set; } = Guid.NewGuid().ToString();

        
        /// İnsan dostu ticket numarası (örn: ST2025000001)
        
        public string TicketNumber { get; set; }

        
        /// İlgili işlem kimliği
        
        public string TransactionId { get; set; }

        
        /// İlgili ürün başlığı (hızlı referans için)
        
        public string ProductTitle { get; set; }

        
        /// Ticket'ı oluşturan kullanıcı kimliği
        
        public string CreatedByUserId { get; set; }

        
        /// Diğer taraf kullanıcı kimliği (sadece bu iki kullanıcı görebilir)
        
        public string OtherPartyUserId { get; set; }

        
        /// Ticket kategorisi
        
        public TicketCategory Category { get; set; }

        
        /// Ticket konusu
        
        public string Subject { get; set; }

        
        /// Detaylı açıklama
        
        public string Description { get; set; }

        
        /// Ticket durumu
        
        public TicketStatus Status { get; set; } = TicketStatus.Open;

        
        /// Ticket mesajları
        
        public List<TicketMessage> Messages { get; set; } = new List<TicketMessage>();

        
        /// Oluşturulma tarihi
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        
        /// Son güncellenme tarihi
        
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        
        /// Çözüm tarihi
        
        public DateTime? ResolvedAt { get; set; }

        
        /// Kapatılma tarihi
        
        public DateTime? ClosedAt { get; set; }

        
        /// Admin tarafından görüldü mü?
        
        public bool IsAdminViewed { get; set; } = false;

        
        /// Admin notları (sadece admin için görünür)
        
        public string AdminNotes { get; set; }

        
        /// Öncelik seviyesi
        
        public TicketPriority Priority { get; set; } = TicketPriority.Normal;
    }

    
    /// Ticket mesaj modeli
    
    public class TicketMessage
    {
        
        /// Mesaj kimliği
        
        public string MessageId { get; set; } = Guid.NewGuid().ToString();

        
        /// Mesajı gönderen kullanıcı kimliği
        
        public string SenderId { get; set; }

        
        /// Gönderen kullanıcı adı (hızlı erişim için)
        
        public string SenderName { get; set; }

        
        /// Mesaj içeriği
        
        public string Content { get; set; }

        
        /// Mesaj gönderilme tarihi
        
        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        
        /// Admin tarafından mı gönderildi?
        
        public bool IsFromAdmin { get; set; } = false;

        
        /// Ekli dosya URL'leri (varsa)
        
        public List<string> AttachmentUrls { get; set; } = new List<string>();
    }

    
    /// Ticket kategorileri
    
    public enum TicketCategory
    {
        
        /// Teslimat sorunu
        
        DeliveryIssue = 0,

        
        /// Ürün kalitesi sorunu
        
        ProductQuality = 1,

        
        /// Ödeme sorunu
        
        PaymentIssue = 2,

        
        /// İletişim sorunu
        
        CommunicationIssue = 3,

        
        /// Ürün açıklaması uyuşmazlığı
        
        DescriptionMismatch = 4,

        
        /// İptal talebi
        
        CancellationRequest = 5,

        
        /// İade talebi
        
        RefundRequest = 6,

        
        /// Diğer
        
        Other = 7
    }

    
    /// Ticket durumları
    
    public enum TicketStatus
    {
        
        /// Açık - Yeni oluşturulmuş
        
        Open = 0,

        
        /// İşlemde - Kullanıcılar arası çözüm aşamasında
        
        InProgress = 1,

        
        /// Admin bekleniyor - Kullanıcılar çözemedi, admin müdahalesi gerekli
        
        WaitingForAdmin = 2,

        
        /// Çözüldü - Sorun çözüldü
        
        Resolved = 3,

        
        /// Kapatıldı - Ticket kapatıldı
        
        Closed = 4
    }

    
    /// Ticket öncelik seviyeleri
    
    public enum TicketPriority
    {
        
        /// Düşük öncelik
        
        Low = 0,

        
        /// Normal öncelik
        
        Normal = 1,

        
        /// Yüksek öncelik
        
        High = 2,

        
        /// Acil
        
        Urgent = 3
    }
}
