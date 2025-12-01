using System;

namespace KamPay.Models
{
    public enum ServicePaymentStatus
    {
        None = 0,
        Initiated = 1,
        Paid = 2,
        Failed = 3,
        Completed = 4,
        Refunded = 5
    }


    public enum PaymentMethodType
    {
        None = 0,
        CardSim = 1,
        BankTransferSim = 2,
        WalletSim = 3,
        PayTrCard = 10,           // Real PayTr card payment (Phase 2)
        PayTrBankTransfer = 11    // Real PayTr bank transfer (Phase 2)
    }

    // Ödeme simülasyonu için sade DTO
    public class PaymentDto
    {
        public string PaymentId { get; set; } = Guid.NewGuid().ToString();
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "TRY";
        public ServicePaymentStatus Status { get; set; } = ServicePaymentStatus.Initiated;
        public PaymentMethodType Method { get; set; } = PaymentMethodType.CardSim;
        public string? MaskedCardLast4 { get; set; }   // Kart için yalnızca son4
        public string? BankName { get; set; }          // EFT/Havale için
        public string? BankReference { get; set; }     // EFT referans simülasyonu
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
