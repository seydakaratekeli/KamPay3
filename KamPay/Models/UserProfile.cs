using System;

namespace KamPay.Models
{
    public class UserProfile
    {
        public string UserId { get; set; } = "";

        // FirstName / LastName ekliyoruz
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";

        //  Kullanıcı adı (örneğin takma ad)
        public string Username { get; set; } = "";

        public string Email { get; set; } = "";
        public string ProfileImageUrl { get; set; } = "";
        public DateTime MemberSince { get; set; }

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
}
