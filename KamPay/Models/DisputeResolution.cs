namespace KamPay.Models
{
    /// <summary>
    /// Enum for dispute status
    /// </summary>
    public enum DisputeStatus
    {
        Open = 0,
        UnderReview = 1,
        Resolved = 2,
        Closed = 3,
        Escalated = 4
    }

    /// <summary>
    /// Enum for dispute resolution type
    /// </summary>
    public enum DisputeResolutionType
    {
        None = 0,
        Refund = 1,
        PartialRefund = 2,
        Redelivery = 3,
        Warning = 4,
        BanUser = 5,
        NoAction = 6
    }

    /// <summary>
    /// Enum for dispute reason
    /// </summary>
    public enum DisputeReason
    {
        ProductNotAsDescribed = 0,
        ProductNotReceived = 1,
        DamagedProduct = 2,
        LateDel = 3,
        ServiceNotProvided = 4,
        ServiceNotSatisfactory = 5,
        PaymentIssue = 6,
        Harassment = 7,
        Fraud = 8,
        Other = 9
    }

    /// <summary>
    /// Dispute resolution model for handling conflicts between users
    /// </summary>
    public class DisputeResolution
    {
        public string DisputeId { get; set; } = Guid.NewGuid().ToString();
        
        // Parties involved
        public string ComplainantUserId { get; set; } = string.Empty;
        public string ComplainantName { get; set; } = string.Empty;
        public string RespondentUserId { get; set; } = string.Empty;
        public string RespondentName { get; set; } = string.Empty;
        
        // Dispute details
        public DisputeReason Reason { get; set; }
        public string Description { get; set; } = string.Empty;
        public DisputeStatus Status { get; set; } = DisputeStatus.Open;
        
        // Related entity
        public string? ReferenceId { get; set; }
        public string? ReferenceType { get; set; } // "Transaction", "ServiceRequest", etc.
        
        // Evidence
        public List<string> EvidenceUrls { get; set; } = new();
        
        // Resolution
        public DisputeResolutionType ResolutionType { get; set; } = DisputeResolutionType.None;
        public string? ResolutionNotes { get; set; }
        public string? ResolvedByUserId { get; set; }
        public DateTime? ResolvedAt { get; set; }
        
        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastUpdatedAt { get; set; }
        
        // Admin notes
        public List<DisputeNote> Notes { get; set; } = new();
    }

    /// <summary>
    /// Note added to a dispute by admin or system
    /// </summary>
    public class DisputeNote
    {
        public string NoteId { get; set; } = Guid.NewGuid().ToString();
        public string AuthorUserId { get; set; } = string.Empty;
        public string AuthorName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsSystemNote { get; set; }
    }
}
