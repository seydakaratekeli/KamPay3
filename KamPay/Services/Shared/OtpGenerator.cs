using System;
using System.Security.Cryptography;

namespace KamPay.Services.Shared
{
    /// <summary>
    /// ✅ FAZ2: Kriptografik OTP üretimi — DRY prensibine uygun paylaşılan yardımcı sınıf.
    /// FirebaseTransactionService ve FirebaseServiceSharingService içindeki
    /// duplike OTP metodları bu sınıf ile birleştirildi.
    ///
    /// ⚡ Güvenlik: new Random() yerine RandomNumberGenerator kullanır.
    /// </summary>
    public static class OtpGenerator
    {
        /// <summary>
        /// 6 haneli kriptografik olarak güvenli OTP üretir.
        /// </summary>
        public static string GenerateSecureOtp()
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[4];
            rng.GetBytes(bytes);
            var number = Math.Abs(BitConverter.ToInt32(bytes, 0)) % 900000 + 100000;
            return number.ToString("D6");
        }

        /// <summary>
        /// Belirtilen uzunlukta kriptografik OTP üretir.
        /// </summary>
        public static string GenerateSecureOtp(int digits)
        {
            if (digits < 4 || digits > 10)
                throw new ArgumentOutOfRangeException(nameof(digits), "OTP uzunluğu 4-10 arasında olmalıdır.");

            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[4];
            rng.GetBytes(bytes);
            var max = (int)Math.Pow(10, digits);
            var min = (int)Math.Pow(10, digits - 1);
            var number = Math.Abs(BitConverter.ToInt32(bytes, 0)) % (max - min) + min;
            return number.ToString($"D{digits}");
        }
    }
}
