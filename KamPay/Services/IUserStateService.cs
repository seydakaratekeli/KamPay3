using KamPay.Models;
using System;
using System.Threading.Tasks;

namespace KamPay.Services
{
    public interface IUserStateService
    {
        /// <summary>
        /// Mevcut kullanıcı bilgilerini döner
        /// </summary>
        User CurrentUser { get; }

        /// <summary>
        /// Kullanıcı profili değiştiğinde tetiklenir
        /// </summary>
        event EventHandler<User> UserProfileChanged;

        /// <summary>
        /// Kullanıcı bilgilerini yükler ve günceller
        /// </summary>
        Task<ServiceResult<User>> RefreshCurrentUserAsync();

        /// <summary>
        /// Kullanıcı profil bilgilerini günceller ve tüm sayfalara bildirir
        /// </summary>
        Task<ServiceResult<bool>> UpdateUserProfileAsync(
            string firstName = null,
            string lastName = null,
            string username = null,
            string profileImageUrl = null);

        /// <summary>
        /// Kullanıcı oturumunu temizler
        /// </summary>
        void ClearUser();
    }
}
