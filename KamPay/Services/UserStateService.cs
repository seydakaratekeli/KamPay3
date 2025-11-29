using KamPay.Models;
using System;
using System.Threading.Tasks;

namespace KamPay.Services
{
    public class UserStateService : IUserStateService
    {
        private readonly IAuthenticationService _authService;
        private readonly IUserProfileService _profileService;
        private User _currentUser;

        public User CurrentUser 
        { 
            get => _currentUser;
            private set
            {
                _currentUser = value;
                UserProfileChanged?.Invoke(this, _currentUser);
            }
        }

        public event EventHandler<User> UserProfileChanged;

        public UserStateService(
            IAuthenticationService authService, 
            IUserProfileService profileService)
        {
            _authService = authService;
            _profileService = profileService;
        }

        public async Task<ServiceResult<User>> RefreshCurrentUserAsync()
        {
            try
            {
                var user = await _authService.GetCurrentUserAsync();
                if (user == null)
                {
                    return ServiceResult<User>.FailureResult("Kullanıcı oturumu bulunamadı");
                }

                // Profil bilgilerini Firebase'den al
                var profileResult = await _profileService.GetUserProfileAsync(user.UserId);
                if (profileResult.Success && profileResult.Data != null)
                {
                    var profile = profileResult.Data;
                    user.FirstName = profile.FirstName;
                    user.LastName = profile.LastName;
                    user.ProfileImageUrl = profile.ProfileImageUrl;
                    user.Email = profile.Email;
                }

                CurrentUser = user;
                return ServiceResult<User>.SuccessResult(user);
            }
            catch (Exception ex)
            {
                return ServiceResult<User>.FailureResult("Kullanıcı bilgileri yüklenemedi", ex.Message);
            }
        }

        public async Task<ServiceResult<bool>> UpdateUserProfileAsync(
            string firstName = null,
            string lastName = null,
            string username = null,
            string profileImageUrl = null)
        {
            if (CurrentUser == null)
            {
                return ServiceResult<bool>.FailureResult("Kullanıcı oturumu bulunamadı");
            }

            try
            {
                // Firebase'de güncelle
                var result = await _profileService.UpdateUserProfileAsync(
                    CurrentUser.UserId,
                    firstName,
                    lastName,
                    username,
                    profileImageUrl);

                if (!result.Success)
                {
                    return result;
                }

                // Local state'i güncelle
                if (!string.IsNullOrWhiteSpace(firstName))
                    CurrentUser.FirstName = firstName;

                if (!string.IsNullOrWhiteSpace(lastName))
                    CurrentUser.LastName = lastName;

                if (!string.IsNullOrWhiteSpace(username))
                    CurrentUser.Username = username;

                if (!string.IsNullOrWhiteSpace(profileImageUrl))
                    CurrentUser.ProfileImageUrl = profileImageUrl;

                // Event tetikle - tüm sayfalar güncellenecek
                UserProfileChanged?.Invoke(this, CurrentUser);

                return ServiceResult<bool>.SuccessResult(true, "Profil güncellendi");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Profil güncellenemedi", ex.Message);
            }
        }

        public void ClearUser()
        {
            CurrentUser = null;
        }
    }
}
