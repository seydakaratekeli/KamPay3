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

        public string ProductId { get; set; } = "";
        public string ProductTitle { get; set; } = "";
        public string ProductThumbnail { get; set; } = "";
        public string TimeText => SentAt.ToString("HH:mm");
        public string ImageUrl { get; set; } = "";

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
        Product = 2,
        System = 3
    }

    // Mesaj gönderme için DTO (Veri Transfer Nesnesi)
    public class SendMessageRequest
    {
        public string ReceiverId { get; set; } = "";
        public string Content { get; set; } = "";
        public MessageType Type { get; set; } = MessageType.Text;
        public string ProductId { get; set; } = "";
        public string ImageUrl { get; set; } = "";
    }
}