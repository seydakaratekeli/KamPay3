// KamPay/Models/Message.cs

using CommunityToolkit.Mvvm.ComponentModel;
using System;
using Newtonsoft.Json;

namespace KamPay.Models
{
    // ✅ ObservableObject — IsSentByMe / IsImageLoading değişince UI anında güncellenir
    public partial class Message : ObservableObject
    {
        public string MessageId { get; set; } = Guid.NewGuid().ToString();
        public string ConversationId { get; set; } = "";
        public string SenderId { get; set; } = "";
        public string SenderName { get; set; } = "";
        public string SenderPhotoUrl { get; set; } = "";
        public string ReceiverId { get; set; } = "";
        public string ReceiverName { get; set; } = "";
        public string ReceiverPhotoUrl { get; set; } = "";
        public string Content { get; set; } = "";

        [JsonIgnore]
        public string Text
        {
            get => Content;
            set => Content = value;
        }

        public MessageType Type { get; set; } = MessageType.Text;
        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        [JsonIgnore]
        public DateTime Timestamp
        {
            get => SentAt;
            set => SentAt = value;
        }

        public bool IsRead { get; set; } = false;
        public DateTime? ReadAt { get; set; }
        public bool IsDeleted { get; set; } = false;
        public bool IsDelivered { get; set; } = true;
        public bool IsSystemMessage { get; set; } = false;

        // Ürün bilgileri (ilan kartı ve pazarlık mesajları için)
        public string ProductId { get; set; } = "";
        public string ProductTitle { get; set; } = "";
        public string ProductThumbnail { get; set; } = "";

        // ✅ YENİ: ilan kartında fiyat göster
        public decimal? ProductPrice { get; set; }

        // ✅ YENİ: Hizmet teklifi bilgileri (service offer card için)
        public string ServiceOfferId { get; set; } = "";
        public string ServiceOfferTitle { get; set; } = "";

        public string TimeText => SentAt.ToString("HH:mm");
        public string ImageUrl { get; set; } = "";

        // Pazarlık (Negotiation) özellikleri
        public decimal? ProposedPrice { get; set; }
        public string? NegotiationAction { get; set; }
        public string? RelatedTransactionId { get; set; }

        // ✅ Pazarlık teklifinin hala geçerli olup olmadığını tutar
        public bool IsActiveOffer { get; set; } = true;

        // ─── UI-only Computed Properties ───

        /// <summary>Tıklanabilir ürün ilan kartı mı?</summary>
        [JsonIgnore]
        public bool IsProductCard =>
            Type == MessageType.ProductCard ||
            (Type == MessageType.Negotiation && !string.IsNullOrEmpty(ProductId));

        /// <summary>Tıklanabilir hizmet teklifi kartı mı?</summary>
        [JsonIgnore]
        public bool IsServiceCard => Type == MessageType.ServiceCard;

        // ✅ [ObservableProperty] — ChatViewModel'de temp mesajdan gerçek mesaja geçişte UI anında değişir
        [ObservableProperty]
        [property: JsonIgnore]
        private bool isSentByMe;

        // ✅ [ObservableProperty] — Görsel yükleme tamamlanınca yükleme göstergesi kaybolur
        [ObservableProperty]
        [property: JsonIgnore]
        private bool isImageLoading;
    }

    public enum MessageType
    {
        Text = 0,
        Image = 1,
        Product = 2,          // Legacy — geriye dönük uyumluluk için korundu
        System = 3,
        Negotiation = 4,
        ProductCard = 5,      // ✅ YENİ: Tıklanabilir ürün/ilan kartı
        ServiceCard = 6       // ✅ YENİ: Tıklanabilir hizmet teklifi kartı
    }

    // Mesaj gönderme için DTO (Veri Transfer Nesnesi)
    public class SendMessageRequest
    {
        public string ReceiverId { get; set; } = "";
        public string Content { get; set; } = "";
        public MessageType Type { get; set; } = MessageType.Text;

        // Ürün bilgileri
        public string ProductId { get; set; } = "";
        public string ProductTitle { get; set; } = "";       // ✅ YENİ
        public string ProductThumbnail { get; set; } = "";   // ✅ YENİ
        public decimal? ProductPrice { get; set; }           // ✅ YENİ

        // Hizmet teklifi bilgileri
        public string ServiceOfferId { get; set; } = "";     // ✅ YENİ
        public string ServiceOfferTitle { get; set; } = "";  // ✅ YENİ

        public string ImageUrl { get; set; } = "";

        // Pazarlık DTO alanları
        public decimal? ProposedPrice { get; set; }
        public string? NegotiationAction { get; set; }
        public string? RelatedTransactionId { get; set; }

        public string ConversationType { get; set; } = "General"; // ✅ EKLENDI
    }
}