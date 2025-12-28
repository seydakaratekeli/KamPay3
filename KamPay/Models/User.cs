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

        //  CRITICAL FIX: Boş/null değerleri güvenli şekilde ele al
        public string FullName
        {
            get
            {
                var first = string.IsNullOrWhiteSpace(FirstName) ? "" : FirstName.Trim();
                var last = string.IsNullOrWhiteSpace(LastName) ? "" : LastName.Trim();
                
                var fullName = $"{first} {last}".Trim();
                
                // Eğer hem ad hem soyad boşsa, username veya email'i kullan
                if (string.IsNullOrWhiteSpace(fullName))
                {
                    if (!string.IsNullOrWhiteSpace(Username))
                        return Username.Trim();
                    
                    if (!string.IsNullOrWhiteSpace(Email))
                        return Email.Split('@')[0].Trim();
                    
                    return "Kullanıcı";
                }
                
                return fullName;
            }
        }
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