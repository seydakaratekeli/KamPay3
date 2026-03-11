using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace KamPay.Models
{
    // ✅ ObservableObject — EditProfileViewModel'de TargetProfile.ProfileImageUrl atanınca UI yansır
    public partial class UserProfile : ObservableObject
    {
        public string UserId { get; set; } = "";

        // ✅ [ObservableProperty] — ad/soyad/kullanıcı adı değişince FullName otomatik güncellenir
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FullName))]
        private string firstName = "";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FullName))]
        private string lastName = "";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FullName))]
        private string username = "";

        public string Email { get; set; } = "";

        // ✅ [ObservableProperty] — fotoğraf yüklenince Image bağlantısı anında güncellenir
        [ObservableProperty] private string profileImageUrl = "";

        public DateTime MemberSince { get; set; }

        public string FullName
        {
            get
            {
                var first = string.IsNullOrWhiteSpace(FirstName) ? "" : FirstName.Trim();
                var last = string.IsNullOrWhiteSpace(LastName) ? "" : LastName.Trim();
                var fullName = $"{first} {last}".Trim();

                if (string.IsNullOrWhiteSpace(fullName))
                {
                    if (!string.IsNullOrWhiteSpace(Username)) return Username.Trim();
                    if (!string.IsNullOrWhiteSpace(Email)) return Email.Split('@')[0].Trim();
                    return "Kullanıcı";
                }

                return fullName;
            }
        }
    }
}
