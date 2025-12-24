using KamPay.Models;
using System;
using System.Threading.Tasks;

namespace KamPay.Services
{
    // bu sayfanın amacı kullanıcı oturum bilgilerini yönetmektir. mevcut kullanıcı bilgilerini tutar, günceller ve değişiklikleri bildirir.
    public interface IUserStateService
    {
       
        /// Mevcut kullanıcı bilgilerini döner
       
        User? CurrentUser { get; }

       
        /// Kullanıcı profili değiştiğinde tetiklenir
       
        event EventHandler<User>? UserProfileChanged;

       
        /// Kullanıcı bilgilerini yükler ve günceller
       
        Task<ServiceResult<User>> RefreshCurrentUserAsync();

       
        /// Kullanıcı profil bilgilerini günceller ve tüm sayfalara bildirir
       
        Task<ServiceResult<bool>> UpdateUserProfileAsync(
            string? firstName = null,
            string? lastName = null,
            string? username = null,
            string? profileImageUrl = null);

       
        /// Kullanıcı oturumunu temizler
       
        void ClearUser();
    }
}
