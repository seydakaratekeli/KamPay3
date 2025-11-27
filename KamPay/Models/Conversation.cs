// KamPay/Models/Conversation.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KamPay.Models
{
    public class Conversation
    {
        public string ConversationId { get; set; } = Guid.NewGuid().ToString();
        public string User1Id { get; set; }
        public string User1Name { get; set; }
        public string User1PhotoUrl { get; set; }
        public string User2Id { get; set; }
        public string User2Name { get; set; }
        public string User2PhotoUrl { get; set; }
        public string ProductId { get; set; }
        public string ProductTitle { get; set; }
        public string ProductThumbnail { get; set; }
        public string LastMessage { get; set; }
        public DateTime LastMessageTime { get; set; }
        public string LastMessageSenderId { get; set; }
        public int UnreadCountUser1 { get; set; }
        public int UnreadCountUser2 { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // --- ViewModel'ler için yardýmcý özellikler ---
        public string OtherUserName { get; set; }
        public string OtherUserPhotoUrl { get; set; }
        public int UnreadCount { get; set; }

       
        public string GetOtherUserId(string currentUserId)
        {
            return User1Id == currentUserId ? User2Id : User1Id;
        }

        public string GetOtherUserName(string currentUserId)
        {
            return User1Id == currentUserId ? User2Name : User1Name;
        }

        public string GetOtherUserPhotoUrl(string currentUserId)
        {
            return User1Id == currentUserId ? User2PhotoUrl : User1PhotoUrl;
        }
        public int GetUnreadCount(string currentUserId)
        {
            return currentUserId == User1Id ? UnreadCountUser1 : UnreadCountUser2;
        }
       
        public string LastMessageTimeText
        {
            get
            {
                var diff = DateTime.UtcNow - LastMessageTime;
                if (diff.TotalMinutes < 1) return "Þimdi";
                if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}dk";
                if (diff.TotalHours < 24) return LastMessageTime.ToString("HH:mm");
                if (diff.TotalDays < 7) return LastMessageTime.ToString("ddd");
                return LastMessageTime.ToString("dd MMM");
            }
        }
    }
}