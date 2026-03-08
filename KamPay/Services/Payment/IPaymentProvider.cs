using KamPay.Models;

namespace KamPay.Services.Payment;

/// <summary>
/// Ödeme saðlayýcý arayüzü - Open/Closed Principle uyumlu
/// Yeni ödeme yöntemleri eklerken mevcut kodu deðiþtirmeye gerek kalmaz
/// </summary>
public interface IPaymentProvider
{
    /// <summary>
    /// Bu provider'ýn desteklediði ödeme yöntemi türü
    /// </summary>
    PaymentMethodType MethodType { get; }
    
    /// <summary>
    /// Provider adý (örn: "Stripe", "PayPal", "Card Simulation")
    /// </summary>
    string ProviderName { get; }
    
    /// <summary>
    /// Ödeme iþlemini baþlatýr
    /// </summary>
    Task<ServiceResult<PaymentDto>> InitiatePaymentAsync(
        string transactionId, 
        decimal amount, 
        string currency = "TRY");
    
    /// <summary>
    /// Ödemeyi doðrular ve onaylar
    /// </summary>
    /// <param name="paymentId">Ödeme ID'si</param>
    /// <param name="verificationData">Doðrulama verisi (OTP, token vb.)</param>
    Task<ServiceResult<bool>> ConfirmPaymentAsync(
        string paymentId, 
        string? verificationData = null);
    
    /// <summary>
    /// Ödeme durumunu sorgular
    /// </summary>
    Task<ServiceResult<PaymentDto>> GetPaymentStatusAsync(string paymentId);
}
