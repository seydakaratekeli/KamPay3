using System;

namespace KamPay.Models
{
    //KULLANILMIYOR SONRA BAK 
    //bu sayfada kullanýcýlar arasýndaki kredi transferlerini takip etmek için TransactionHistory modeli tanýmlanmýþtýr. ama tamamlanmamýþ olabilir. 
    
    /// Transaction history model to track credit transfers between users
    
    public class TransactionHistory
    {
        public string TransactionHistoryId { get; set; } = Guid.NewGuid().ToString();
        
        
        /// User ID who initiated the transfer (sender)
        
        public string FromUserId { get; set; } = "";
        
        
        /// User ID who received the transfer (receiver)
        
        public string ToUserId { get; set; } = "";
        
        
        /// Amount of credits transferred
        
        public int Amount { get; set; }
        
        
        /// Type of transaction (e.g., "CreditTransfer", "Purchase", "Reward")
        
        public TransactionHistoryType Type { get; set; }
        
        
        /// Optional description or reason for the transfer
        
        public string Description { get; set; } = "";
        
        
        /// Reference to related entity (e.g., ProductId, TransactionId)
        
        public string ReferenceId { get; set; } = "";
        
        
        /// Reference type (e.g., "Product", "Transaction", "Service")
        
        public string ReferenceType { get; set; } = "";
        
        
        /// Timestamp when the transaction was created
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        
        /// Status of the transaction
        
        public TransactionHistoryStatus Status { get; set; } = TransactionHistoryStatus.Completed;
        
        
        /// Sender's credit balance after the transaction
        
        public int FromUserBalanceAfter { get; set; }
        
        
        /// Receiver's credit balance after the transaction
        
        public int ToUserBalanceAfter { get; set; }
    }
    
    public enum TransactionHistoryType
    {
        CreditTransfer,
        Purchase,
        Sale,
        Reward,
        Refund,
        Fee,
        Adjustment
    }
    
    public enum TransactionHistoryStatus
    {
        Pending,
        Completed,
        Failed,
        Cancelled
    }
}
