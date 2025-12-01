namespace KamPay.Models
{
    /// <summary>
    /// User role enumeration
    /// </summary>
    public enum UserRole
    {
        User = 0,
        Moderator = 1,
        Admin = 2
    }

    /// <summary>
    /// Admin action model for tracking administrative actions
    /// </summary>
    public class AdminAction
    {
        public string ActionId { get; set; } = Guid.NewGuid().ToString();
        public string AdminUserId { get; set; } = string.Empty;
        public string AdminUserName { get; set; } = string.Empty;
        public AdminActionType ActionType { get; set; }
        public string TargetUserId { get; set; } = string.Empty;
        public string TargetUserName { get; set; } = string.Empty;
        public string? TargetEntityId { get; set; }
        public string? TargetEntityType { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Admin action type enumeration
    /// </summary>
    public enum AdminActionType
    {
        BanUser = 0,
        UnbanUser = 1,
        VerifyUser = 2,
        UnverifyUser = 3,
        DeleteProduct = 4,
        DeleteServiceOffer = 5,
        ResolveDispute = 6,
        HideRating = 7,
        SendWarning = 8,
        PromoteToModerator = 9,
        DemoteFromModerator = 10
    }
}
