using System;

namespace KamPay.Models
{
    public class User
    {
        public string UserId { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Email { get; set; } = "";
        public string PasswordHash { get; set; } = "";
        public bool IsEmailVerified { get; set; }
        public string VerificationCode { get; set; } = "";
        public DateTime VerificationCodeExpiry { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public bool IsActive { get; set; }

        // Güven puanı (ilerde kullanılacak)
        public int TrustScore { get; set; }

        // Profil bilgileri
        public string PhoneNumber { get; set; } = "";
        public string ProfileImageUrl { get; set; } = "";
        public string Username { get; set; } = "";

        // Bağış puanları (ilerde oyunlaştırma için)
        public int DonationPoints { get; set; }

        public User()
        {
            UserId = Guid.NewGuid().ToString();
            CreatedAt = DateTime.UtcNow;
            IsEmailVerified = false;
            IsActive = true;
            TrustScore = 100; // Başlangıç puanı
            DonationPoints = 0;
        }

        public string FullName => $"{FirstName} {LastName}";
    }

    // Kayıt için DTO
    public class RegisterRequest
    {
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public string PasswordConfirm { get; set; } = "";
    }

    // Giriş için DTO
    public class LoginRequest
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public bool RememberMe { get; set; }
    }

    // Doğrulama için DTO
    public class VerificationRequest
    {
        public string Email { get; set; } = "";
        public string VerificationCode { get; set; } = "";
    }
}