using KamPay.Models;
using System;
using System.Threading.Tasks;

namespace KamPay.Services
{
    public interface IUserStateService
    {
        User? CurrentUser { get; }

        /// <summary>
        /// Mevcut oturumdaki kullanıcı ID'si.
        /// Converter'lar Preferences yerine bu property'yi kullanmalıdır.
        /// </summary>
        string CurrentUserId { get; }

        event EventHandler<User?>? UserProfileChanged;

        // ViewModel'lerin beklediği yeni metotlar
        Task<User?> GetCurrentUserAsync();
        void SetUser(User user);

        Task<ServiceResult<User>> RefreshCurrentUserAsync();

        Task<ServiceResult<bool>> UpdateUserProfileAsync(
            string? firstName = null,
            string? lastName = null,
            string? username = null,
            string? profileImageUrl = null);

        void ClearUser();
    }
}