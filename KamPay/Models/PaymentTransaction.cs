namespace KamPay.Models
{
    /// <summary>
    /// Payment transaction model for tracking all payment operations
    /// </summary>
    public class PaymentTransaction
    {
        public string PaymentId { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "TRY";
        public PaymentMethodType Method { get; set; }
        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
        public string Description { get; set; } = string.Empty;
        
        // Reference to related entity (ServiceRequest, Transaction, etc.)
        public string ReferenceId { get; set; } = string.Empty;
        public string ReferenceType { get; set; } = string.Empty; // "ServiceRequest", "Product", etc.
        
        // Transaction details
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        public DateTime? RefundedAt { get; set; }
        
        // Payment provider info (for future PayTr integration)
        public string? ProviderTransactionId { get; set; }
        public string? ProviderResponse { get; set; }
        
        // Verification
        public string? VerificationCode { get; set; }
        public int VerificationAttempts { get; set; }
        public DateTime? VerificationCodeSentAt { get; set; }
        
        // Refund details
        public decimal RefundedAmount { get; set; }
        public string? RefundReason { get; set; }
        
        // Metadata
        public string? ErrorMessage { get; set; }
        public string? MaskedCardNumber { get; set; } // For display purposes only
    }

    public enum PaymentStatus
    {
        Pending = 0,
        AwaitingConfirmation = 1,
        Completed = 2,
        Failed = 3,
        Refunded = 4,
        PartiallyRefunded = 5,
        Cancelled = 6
    }

    public enum PaymentMethodType
    {
        None = 0,
        CardSim = 1,              // Simulated card payment
        BankTransferSim = 2,      // Simulated bank transfer
        WalletSim = 3,            // Simulated wallet payment
        PayTrCard = 10,           // Real PayTr card payment (Phase 2)
        PayTrBankTransfer = 11    // Real PayTr bank transfer (Phase 2)
    }

    // Enum for service payment status
    public enum ServicePaymentStatus
    {
        None = 0,
        Pending = 1,
        Completed = 2,
        Failed = 3,
        Refunded = 4
    }
}
