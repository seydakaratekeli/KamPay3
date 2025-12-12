// KamPay/Models/Message.cs

using System;
using Newtonsoft.Json;

namespace KamPay.Models
{
    
    public class Message
    {
        public string MessageId { get; set; } = Guid.NewGuid().ToString();
        public string ConversationId { get; set; }
        public string SenderId { get; set; }
        public string SenderName { get; set; }
        public string SenderPhotoUrl { get; set; }
        public string ReceiverId { get; set; }
        public string ReceiverName { get; set; }
        public string ReceiverPhotoUrl { get; set; }
        public string Content { get; set; }
        
        // Alternatif property adı için backward compatibility
        [JsonIgnore]
        public string Text 
        { 
            get => Content; 
            set => Content = value; 
        }
        
        public MessageType Type { get; set; } = MessageType.Text;
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
        
        // Alternatif property adı için backward compatibility
        [JsonIgnore]
        public DateTime Timestamp 
        { 
            get => SentAt; 
            set => SentAt = value; 
        }
        
        public bool IsRead { get; set; } = false;
        public DateTime? ReadAt { get; set; }
        public bool IsDeleted { get; set; } = false;
        
        // Mesaj durumu özellikleri
        public bool IsDelivered { get; set; } = true;
        
        // Sistem mesajı kontrolü için
        public bool IsSystemMessage { get; set; } = false;

        // Ürün referansı 
        public string ProductId { get; set; }
        public string ProductTitle { get; set; }
        public string ProductThumbnail { get; set; }
        public string TimeText => SentAt.ToString("HH:mm");

        // Fotoğraf mesajları için
        public string ImageUrl { get; set; }

        // (Veritabanına kaydedilmeyecek, sadece UI için)
        [JsonIgnore] 
        public bool IsSentByMe { get; set; }

        // UI loading göstergesi için
        [JsonIgnore]
        public bool IsImageLoading { get; set; }

    }

    public enum MessageType
    {
        Text = 0,
        Image = 1,
        Product = 2,
        System = 3
    }

    // Mesaj gönderme için DTO (Veri Transfer Nesnesi)
    public class SendMessageRequest
    {
        public string ReceiverId { get; set; }
        public string Content { get; set; }
        public MessageType Type { get; set; } = MessageType.Text;
        public string ProductId { get; set; }
        public string ImageUrl { get; set; }
    }
}