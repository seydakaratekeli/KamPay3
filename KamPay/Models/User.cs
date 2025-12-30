using CommunityToolkit.Mvvm.ComponentModel;

namespace KamPay.Models
{
    // ✅ ObservableObject ekleyerek UI bildirim yeteneği kazandırdık
    public partial class User : ObservableObject
    {
        [ObservableProperty]
        private string userId = Guid.NewGuid().ToString();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FullName))] // FirstName değişince FullName'i de güncelle
        private string firstName = "";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FullName))] // LastName değişince FullName'i de güncelle
        private string lastName = "";

        [ObservableProperty]
        private string email = "";

        [ObservableProperty]
        private string passwordHash = "";

        [ObservableProperty]
        private bool isEmailVerified = false;

        [ObservableProperty]
        private string verificationCode = "";

        [ObservableProperty]
        private DateTime verificationCodeExpiry;

        [ObservableProperty]
        private DateTime createdAt = DateTime.UtcNow;

        [ObservableProperty]
        private DateTime? lastLoginAt;

        [ObservableProperty]
        private bool isActive = true;

        [ObservableProperty]
        private int trustScore = 100;

        [ObservableProperty]
        private string phoneNumber = "";

        [ObservableProperty]
        private string profileImageUrl = "";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FullName))] // Username değişince FullName fallback'i etkilenebilir
        private string username = "";

        [ObservableProperty]
        private int donationPoints = 0;

        public User() { }

        // ✅ UI tarafında anlık tetiklenen FullName mantığı
        public string FullName
        {
            get
            {
                var first = string.IsNullOrWhiteSpace(FirstName) ? "" : FirstName.Trim();
                var last = string.IsNullOrWhiteSpace(LastName) ? "" : LastName.Trim();

                var fullName = $"{first} {last}".Trim();

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

    // --- DTO'lar (Bunlar genellikle sabit veri taşıdığı için Observable olmasına gerek yoktur) ---

    public class RegisterRequest
    {
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Username { get; set; } = "";
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public string PasswordConfirm { get; set; } = "";
    }

    public class LoginRequest
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public bool RememberMe { get; set; }
    }

    public class VerificationRequest
    {
        public string Email { get; set; } = "";
        public string VerificationCode { get; set; } = "";
    }
}