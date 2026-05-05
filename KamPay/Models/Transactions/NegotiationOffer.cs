using System;

namespace KamPay.Models
{
    public class NegotiationOffer
    {
        public string OfferId { get; set; } = Guid.NewGuid().ToString();
        public string TransactionId { get; set; } = string.Empty;

        public string ProposerId { get; set; } = string.Empty;
        public string ProposerName { get; set; } = string.Empty;
        public ProposerRole Role { get; set; }
        public decimal Amount { get; set; }

        public OfferStatus Status { get; set; } = OfferStatus.Active;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? RespondedAt { get; set; }

        public string? ParentOfferId { get; set; }
        public int RoundNumber { get; set; }
    }

    public enum OfferStatus
    {
        Active,
        Superseded,
        Accepted,
        Rejected,
        Expired
    }

    public enum ProposerRole
    {
        Buyer,
        Seller
    }
}
