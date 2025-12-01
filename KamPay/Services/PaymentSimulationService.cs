using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Helpers;
using KamPay.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KamPay.Services
{
    /// <summary>
    /// Payment simulation service for testing
    /// Will be replaced with real PayTr integration in Phase 2
    /// </summary>
    public class PaymentSimulationService : IPaymentService
    {
        private readonly FirebaseClient _firebaseClient;
        private const int OtpValiditySeconds = 300; // 5 minutes

        public PaymentSimulationService()
        {
            _firebaseClient = new FirebaseClient(Constants.FirebaseRealtimeDbUrl);
        }

        public async Task<ServiceResult<PaymentTransaction>> InitiatePaymentAsync(
            string userId,
            decimal amount,
            string currency,
            PaymentMethodType method,
            string description,
            string referenceId,
            string referenceType)
        {
            try
            {
                var payment = new PaymentTransaction
                {
                    UserId = userId,
                    Amount = amount,
                    Currency = currency,
                    Method = method,
                    Description = description,
                    ReferenceId = referenceId,
                    ReferenceType = referenceType,
                    Status = PaymentStatus.AwaitingConfirmation
                };

                // Simulate verification code generation
                payment.VerificationCode = GenerateOtp();
                payment.VerificationCodeSentAt = DateTime.UtcNow;

                // Simulate masked card for card payments
                if (method == PaymentMethodType.CardSim)
                {
                    payment.MaskedCardNumber = "**** **** **** 1234";
                }

                await _firebaseClient
                    .Child(Constants.PaymentTransactionsCollection)
                    .Child(payment.PaymentId)
                    .PutAsync(payment);

                return ServiceResult<PaymentTransaction>.SuccessResult(
                    payment,
                    "Ödeme başlatıldı. Doğrulama kodu güvenli kanal üzerinden gönderildi.");
            }
            catch (Exception ex)
            {
                return ServiceResult<PaymentTransaction>.FailureResult(
                    "Ödeme başlatılamadı",
                    ex.Message);
            }
        }

        public async Task<ServiceResult<bool>> ConfirmPaymentAsync(
            string paymentId,
            string? verificationCode = null)
        {
            try
            {
                var paymentNode = _firebaseClient
                    .Child(Constants.PaymentTransactionsCollection)
                    .Child(paymentId);

                var payment = await paymentNode.OnceSingleAsync<PaymentTransaction>();

                if (payment == null)
                {
                    return ServiceResult<bool>.FailureResult("Ödeme bulunamadı");
                }

                if (payment.Status != PaymentStatus.AwaitingConfirmation)
                {
                    return ServiceResult<bool>.FailureResult(
                        $"Ödeme durumu uygun değil: {payment.Status}");
                }

                // Check verification code expiry
                if (payment.VerificationCodeSentAt.HasValue &&
                    DateTime.UtcNow > payment.VerificationCodeSentAt.Value.AddSeconds(OtpValiditySeconds))
                {
                    payment.Status = PaymentStatus.Failed;
                    payment.ErrorMessage = "Doğrulama kodu süresi doldu";
                    await paymentNode.PutAsync(payment);
                    return ServiceResult<bool>.FailureResult("Doğrulama kodu süresi doldu");
                }

                // Verify code
                if (!string.IsNullOrEmpty(payment.VerificationCode) &&
                    payment.VerificationCode != verificationCode)
                {
                    payment.VerificationAttempts++;
                    await paymentNode.PutAsync(payment);

                    if (payment.VerificationAttempts >= 3)
                    {
                        payment.Status = PaymentStatus.Cancelled;
                        payment.ErrorMessage = "Çok fazla yanlış deneme";
                        await paymentNode.PutAsync(payment);
                        return ServiceResult<bool>.FailureResult("Çok fazla yanlış deneme. Ödeme iptal edildi.");
                    }

                    return ServiceResult<bool>.FailureResult(
                        $"Yanlış doğrulama kodu. Kalan deneme: {3 - payment.VerificationAttempts}");
                }

                // Complete payment
                payment.Status = PaymentStatus.Completed;
                payment.CompletedAt = DateTime.UtcNow;
                await paymentNode.PutAsync(payment);

                return ServiceResult<bool>.SuccessResult(
                    true,
                    "Ödeme başarıyla tamamlandı!");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult(
                    "Ödeme onaylanamadı",
                    ex.Message);
            }
        }

        public async Task<ServiceResult<bool>> RefundPaymentAsync(
            string paymentId,
            decimal amount,
            string reason)
        {
            try
            {
                var paymentNode = _firebaseClient
                    .Child(Constants.PaymentTransactionsCollection)
                    .Child(paymentId);

                var payment = await paymentNode.OnceSingleAsync<PaymentTransaction>();

                if (payment == null)
                {
                    return ServiceResult<bool>.FailureResult("Ödeme bulunamadı");
                }

                if (payment.Status != PaymentStatus.Completed)
                {
                    return ServiceResult<bool>.FailureResult(
                        "Sadece tamamlanmış ödemeler iade edilebilir");
                }

                if (amount > payment.Amount - payment.RefundedAmount)
                {
                    return ServiceResult<bool>.FailureResult(
                        "İade tutarı, ödeme tutarından fazla olamaz");
                }

                payment.RefundedAmount += amount;
                payment.RefundReason = reason;
                payment.RefundedAt = DateTime.UtcNow;

                if (payment.RefundedAmount >= payment.Amount)
                {
                    payment.Status = PaymentStatus.Refunded;
                }
                else
                {
                    payment.Status = PaymentStatus.PartiallyRefunded;
                }

                await paymentNode.PutAsync(payment);

                return ServiceResult<bool>.SuccessResult(
                    true,
                    $"{amount} {payment.Currency} iade işlemi başarılı");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult(
                    "İade işlemi başarısız",
                    ex.Message);
            }
        }

        public async Task<ServiceResult<PaymentTransaction>> GetPaymentStatusAsync(string paymentId)
        {
            try
            {
                var payment = await _firebaseClient
                    .Child(Constants.PaymentTransactionsCollection)
                    .Child(paymentId)
                    .OnceSingleAsync<PaymentTransaction>();

                if (payment == null)
                {
                    return ServiceResult<PaymentTransaction>.FailureResult("Ödeme bulunamadı");
                }

                return ServiceResult<PaymentTransaction>.SuccessResult(payment);
            }
            catch (Exception ex)
            {
                return ServiceResult<PaymentTransaction>.FailureResult(
                    "Ödeme durumu alınamadı",
                    ex.Message);
            }
        }

        public async Task<ServiceResult<List<PaymentTransaction>>> GetPaymentHistoryAsync(
            string userId,
            int limit = 50)
        {
            try
            {
                var allPayments = await _firebaseClient
                    .Child(Constants.PaymentTransactionsCollection)
                    .OnceAsync<PaymentTransaction>();

                var userPayments = allPayments
                    .Select(p => p.Object)
                    .Where(p => p.UserId == userId)
                    .OrderByDescending(p => p.CreatedAt)
                    .Take(limit)
                    .ToList();

                return ServiceResult<List<PaymentTransaction>>.SuccessResult(userPayments);
            }
            catch (Exception ex)
            {
                return ServiceResult<List<PaymentTransaction>>.FailureResult(
                    "Ödeme geçmişi alınamadı",
                    ex.Message);
            }
        }

        private string GenerateOtp()
        {
            // Use cryptographically secure random number generation
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            var bytes = new byte[4];
            rng.GetBytes(bytes);
            
            // Convert to positive integer and get 6 digits
            // This avoids modulo bias by rejecting values that would cause bias
            uint number = BitConverter.ToUInt32(bytes, 0);
            const uint maxValue = uint.MaxValue - (uint.MaxValue % 900000);
            
            // Retry if we get a value in the biased range
            while (number >= maxValue)
            {
                rng.GetBytes(bytes);
                number = BitConverter.ToUInt32(bytes, 0);
            }
            
            return (100000 + (number % 900000)).ToString();
        }
    }
}
