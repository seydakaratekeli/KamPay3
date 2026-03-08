using Firebase.Database;
using Firebase.Database.Query; // ? Extension methods için
using KamPay.Helpers;
using KamPay.Models;
using System;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace KamPay.Services.Payment
{
    /// <summary>
    /// ?? KART SÝMÜLASYONU PROVIDER
    /// 
    /// OCP Prensibi: Bu sýnýfý deðiþtirmeden yeni provider ekleyebilirsin.
    /// Örneðin: StripePaymentProvider, IyzicoPaymentProvider vs.
    /// </summary>
    public class CardSimulationProvider : IPaymentProvider
    {
        private readonly FirebaseClient _firebaseClient;
        private const int OtpValidityMinutes = 2;

        public string ProviderName => "Card Simulation";
        public PaymentMethodType MethodType => PaymentMethodType.CardSim;

        public CardSimulationProvider(FirebaseClient firebaseClient)
        {
            _firebaseClient = firebaseClient ?? throw new ArgumentNullException(nameof(firebaseClient));
        }

        public async Task<ServiceResult<PaymentDto>> InitiatePaymentAsync(string transactionId, decimal amount, string currency = "TRY")
        {
            try
            {
                var payment = new PaymentDto
                {
                    PaymentId = Guid.NewGuid().ToString(),
                    Amount = amount,
                    Currency = currency,
                    Method = MethodType,
                    Status = ServicePaymentStatus.Initiated,
                    CreatedAt = DateTime.UtcNow
                };

                // ?? GÜVENLÝK: Kriptografik OTP oluþtur
                var otp = GenerateSecureOtp();

                // ? Geçici OTP'yi Firebase'e kaydet (2 dakika geçerli)
                await _firebaseClient
                    .Child(Constants.TempOtpsCollection)
                    .Child(payment.PaymentId)
                    .PutAsync(new
                    {
                        Otp = otp,
                        ExpiresAt = DateTime.UtcNow.AddMinutes(OtpValidityMinutes)
                    });

                System.Diagnostics.Debug.WriteLine($"? Kart simülasyonu OTP: {otp} (PaymentId: {payment.PaymentId})");

                return ServiceResult<PaymentDto>.SuccessResult(payment, $"OTP oluþturuldu: {otp} (Simülasyon)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? CardSim hatasý: {ex.Message}");
                return ServiceResult<PaymentDto>.FailureResult("Kart simülasyonu baþlatýlamadý", ex.Message);
            }
        }

        public async Task<ServiceResult<bool>> ConfirmPaymentAsync(string paymentId, string? verificationData = null)
        {
            try
            {
                var otpNode = _firebaseClient.Child(Constants.TempOtpsCollection).Child(paymentId);
                var savedOtp = await otpNode.OnceSingleAsync<dynamic>();

                if (savedOtp == null)
                    return ServiceResult<bool>.FailureResult("OTP bulunamadý.");

                var expiresAt = DateTime.Parse(savedOtp.ExpiresAt.ToString());
                if (DateTime.UtcNow > expiresAt)
                    return ServiceResult<bool>.FailureResult("OTP süresi doldu.");

                var storedOtp = savedOtp.Otp.ToString();
                if (string.IsNullOrWhiteSpace(verificationData) || storedOtp != verificationData)
                    return ServiceResult<bool>.FailureResult("OTP geçersiz.");

                // ? Kullanýldýktan sonra sil (tek kullanýmlýk)
                await otpNode.DeleteAsync();

                return ServiceResult<bool>.SuccessResult(true, "OTP doðrulandý.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("OTP doðrulama hatasý", ex.Message);
            }
        }

        public async Task<ServiceResult<PaymentDto>> GetPaymentStatusAsync(string paymentId)
        {
            try
            {
                // Simülasyon için basit durum kontrolü
                return await Task.FromResult(ServiceResult<PaymentDto>.SuccessResult(
                    new PaymentDto 
                    { 
                        PaymentId = paymentId, 
                        Status = ServicePaymentStatus.Paid 
                    }
                ));
            }
            catch (Exception ex)
            {
                return ServiceResult<PaymentDto>.FailureResult("Durum sorgulanamadý", ex.Message);
            }
        }

        private static string GenerateSecureOtp()
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[4];
            rng.GetBytes(bytes);
            var randomNumber = BitConverter.ToUInt32(bytes, 0);
            var otp = (randomNumber % 900000) + 100000; // 100000-999999
            return otp.ToString("D6");
        }
    }
}
