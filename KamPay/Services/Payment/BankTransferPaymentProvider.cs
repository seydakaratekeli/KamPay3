using Firebase.Database;
using KamPay.Helpers;
using KamPay.Models;

namespace KamPay.Services.Payment;

/// <summary>
/// ? Havale/EFT simülasyon provider'ý
/// Banka referans kodu oluþturur
/// </summary>
public class BankTransferPaymentProvider : IPaymentProvider
{
    private readonly FirebaseClient _firebaseClient;
    
    public PaymentMethodType MethodType => PaymentMethodType.BankTransferSim;
    public string ProviderName => "Havale/EFT Simülasyonu";
    
    public BankTransferPaymentProvider(FirebaseClient firebaseClient)
    {
        _firebaseClient = firebaseClient ?? throw new ArgumentNullException(nameof(firebaseClient));
    }
    
    public async Task<ServiceResult<PaymentDto>> InitiatePaymentAsync(
        string transactionId, 
        decimal amount, 
        string currency = "TRY")
    {
        try
        {
            var payment = new PaymentDto
            {
                Amount = amount,
                Currency = currency,
                Status = ServicePaymentStatus.Initiated,
                Method = MethodType,
                BankName = "Ziraat Bankasý",
                BankReference = GenerateBankReference()
            };
            
            System.Diagnostics.Debug.WriteLine(
                $"? {ProviderName}: Referans oluþturuldu - {payment.BankReference}");
            
            return ServiceResult<PaymentDto>.SuccessResult(payment, 
                $"Havale bilgileri oluþturuldu. Referans: {payment.BankReference}");
        }
        catch (Exception ex)
        {
            return ServiceResult<PaymentDto>.FailureResult("Ödeme baþlatýlamadý", ex.Message);
        }
    }
    
    public Task<ServiceResult<bool>> ConfirmPaymentAsync(
        string paymentId, 
        string? verificationData = null)
    {
        // Havale/EFT için manuel onay - otomatik onay simülasyonu
        System.Diagnostics.Debug.WriteLine(
            $"? {ProviderName}: Manuel onay simüle edildi (PaymentId: {paymentId})");
        
        return Task.FromResult(ServiceResult<bool>.SuccessResult(true, 
            "Havale/EFT ödemesi kaydedildi (Manuel onay bekleniyor)"));
    }
    
    public Task<ServiceResult<PaymentDto>> GetPaymentStatusAsync(string paymentId)
    {
        return Task.FromResult(ServiceResult<PaymentDto>.SuccessResult(
            new PaymentDto { PaymentId = paymentId, Status = ServicePaymentStatus.Paid }));
    }
    
    private static string GenerateBankReference()
    {
        return $"BTX-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..6].ToUpper()}";
    }
}
