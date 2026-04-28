using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Text.Json.Serialization;

namespace KamPay.Models
{
    /// <summary>
    /// İki kullanıcı arasındaki mesajlaşma oturumu.
    /// ✅ FAZ 1: ProductId ile ürün bazlı izolasyon sağlanıyor.
    /// </summary>
    public partial class Conversation : ObservableObject
    {
        public string ConversationId { get; set; } = Guid.NewGuid().ToString();

        public string User1Id { get; set; } = "";
        public string User1Name { get; set; } = "";
        public string User1PhotoUrl { get; set; } = "";

        public string User2Id { get; set; } = "";
        public string User2Name { get; set; } = "";
        public string User2PhotoUrl { get; set; } = "";

        // ✅ Ürün verileri (Gerçekte artık UI'da tek bir ürüne bağlı olmayacak, ama geçmiş veriler için kalabilir)
        public string ProductId { get; set; } = "";
        public string ProductTitle { get; set; } = "";
        public string ProductThumbnail { get; set; } = "";

        // Sohbet türü: General veya Negotiation
        public string ConversationType { get; set; } = "General";

        // ✅ UI: Mesajlar sekmesinde pazarlık/kişisel sohbetleri filtreleme
        [JsonIgnore]
        public bool IsNegotiationConversation => ConversationType == "Negotiation";

        // 🔔 [ObservableProperty] — mesaj listesi güncelleme anında UI yansır
        [ObservableProperty]
        private string lastMessage = "";

        public DateTime LastMessageTime { get; set; }
        public string LastMessageSenderId { get; set; } = "";

        public int UnreadCountUser1 { get; set; } // Hedefte User1UnreadCount olsa da mevcut veritabanınızı bozmamak adına bu isim kalabilir. İstiyorsanız değiştirebilirsiniz.
        public int UnreadCountUser2 { get; set; }

        public bool IsActive { get; set; } = true;
        
        // Kullanıcı bazlı silme durumu
        public bool DeletedByUser1 { get; set; } = false;
        public bool DeletedByUser2 { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // 🔔 [ObservableProperty] — arka planda profil yüklenince UI otomatik güncellenir
        // Firebase/DB'ye serialize edilmemesi için [JsonIgnore] ekleniyor
        [JsonIgnore]
        [ObservableProperty]
        private string otherUserName = "";

        [JsonIgnore]
        [ObservableProperty]
        private string otherUserPhotoUrl = "";

        [JsonIgnore]
        [ObservableProperty]
        private bool isOtherUserOnline;

        [JsonIgnore]
        [ObservableProperty]
        private int unreadCount;

        // ─── Yardımcı Metodlar ───

        public string GetOtherUserId(string currentUserId) =>
            User1Id == currentUserId ? User2Id : User1Id;

        public string GetOtherUserName(string currentUserId) =>
            User1Id == currentUserId ? User2Name : User1Name;

        public string GetOtherUserPhotoUrl(string currentUserId) =>
            User1Id == currentUserId ? User2PhotoUrl : User1PhotoUrl;

        public int GetUnreadCountDb(string currentUserId) =>
            currentUserId == User1Id ? UnreadCountUser1 : UnreadCountUser2;

        [JsonIgnore]
        public string LastMessageTimeText
        {
            get
            {
                var diff = DateTime.UtcNow - LastMessageTime;
                if (diff.TotalMinutes < 1) return "Şimdi";
                if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}dk";
                if (diff.TotalHours < 24) return LastMessageTime.ToString("HH:mm");
                if (diff.TotalDays < 7) return LastMessageTime.ToString("ddd");
                return LastMessageTime.ToString("dd MMM");
            }
        }
    }
}