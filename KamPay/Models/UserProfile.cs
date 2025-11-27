using System;

namespace KamPay.Models
{
    public class UserProfile
    {
        public string UserId { get; set; }

        // 🔹 Artık FirstName / LastName ekliyoruz
        public string FirstName { get; set; }
        public string LastName { get; set; }

        // 🔹 Kullanıcı adı (örneğin takma ad)
        public string Username { get; set; }

        public string Email { get; set; }
        public string ProfileImageUrl { get; set; }
        public DateTime MemberSince { get; set; }

        // 🔹 Kolay erişim için birleştirilmiş ad
        public string FullName => $"{FirstName} {LastName}".Trim();
    }
}
