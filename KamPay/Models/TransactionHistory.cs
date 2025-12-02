using System;

namespace KamPay.Models
{
    /// <summary>
    /// Transaction history model to track credit transfers between users
    /// </summary>
    public class TransactionHistory
    {
        public string TransactionHistoryId { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>
        /// User ID who initiated the transfer (sender)
        /// </summary>
        public string FromUserId { get; set; }
        
        /// <summary>
        /// User ID who received the transfer (receiver)
        /// </summary>
        public string ToUserId { get; set; }
        
        /// <summary>
        /// Amount of credits transferred
        /// </summary>
        public int Amount { get; set; }
        
        /// <summary>
        /// Type of transaction (e.g., "CreditTransfer", "Purchase", "Reward")
        /// </summary>
        public TransactionHistoryType Type { get; set; }
        
        /// <summary>
        /// Optional description or reason for the transfer
        /// </summary>
        public string Description { get; set; }
        
        /// <summary>
        /// Reference to related entity (e.g., ProductId, TransactionId)
        /// </summary>
        public string ReferenceId { get; set; }
        
        /// <summary>
        /// Reference type (e.g., "Product", "Transaction", "Service")
        /// </summary>
        public string ReferenceType { get; set; }
        
        /// <summary>
        /// Timestamp when the transaction was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Status of the transaction
        /// </summary>
        public TransactionHistoryStatus Status { get; set; } = TransactionHistoryStatus.Completed;
        
        /// <summary>
        /// Sender's credit balance after the transaction
        /// </summary>
        public int FromUserBalanceAfter { get; set; }
        
        /// <summary>
        /// Receiver's credit balance after the transaction
        /// </summary>
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
