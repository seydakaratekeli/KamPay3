using KamPay.Models;
using KamPay.Services.Auth;
using KamPay.Services.Messaging;
using KamPay.Services.Products;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KamPay.Services
{
    public class UserStateService : IUserStateService
    {
        private readonly IAuthenticationService _authService;
        private readonly IUserProfileService _profileService;
        private readonly IProductService _productService;
        private readonly IServiceSharingService _serviceService;
        private readonly IGoodDeedService _goodDeedService;
        private readonly IMessagingService _messagingService;

        private User? _currentUser;
        public User? CurrentUser => _currentUser;

        /// <inheritdoc/>
        public string CurrentUserId => _currentUser?.UserId ?? string.Empty;

        // UI'Ä±n ve diÄŸer ViewModel'lerin dinlediÄŸi olay
        public event EventHandler<User?>? UserProfileChanged;

        public UserStateService(
            IAuthenticationService authService,
            IUserProfileService profileService,
            IProductService productService,
            IServiceSharingService serviceService,
            IGoodDeedService goodDeedService,
            IMessagingService messagingService)
        {
            _authService = authService;
            _profileService = profileService;
            _productService = productService;
            _serviceService = serviceService;
            _goodDeedService = goodDeedService;
            _messagingService = messagingService;
        }

        public async Task<User?> GetCurrentUserAsync()
        {
            if (_currentUser == null)
                await RefreshCurrentUserAsync();

            return _currentUser;
        }

        public void SetUser(User user)
        {
            _currentUser = user;
            // OlayÄ± tetikle (Bu sayede ProfileViewModel gibi dinleyiciler UI'Ä± yeniler)
            UserProfileChanged?.Invoke(this, _currentUser);
        }

        public async Task<ServiceResult<User>> RefreshCurrentUserAsync()
        {
            try
            {
                var user = await _authService.GetCurrentUserAsync();
                if (user == null) return ServiceResult<User>.FailureResult("KullanÄ±cÄ± oturumu bulunamadÄ±");

                var profileResult = await _profileService.GetUserProfileAsync(user.UserId);

                if (profileResult.Success && profileResult.Data != null)
                {
                    var profile = profileResult.Data;

                    // Verileri yerel modele senkronize et
                    if (!string.IsNullOrWhiteSpace(profile.FirstName)) user.FirstName = profile.FirstName;
                    if (!string.IsNullOrWhiteSpace(profile.LastName)) user.LastName = profile.LastName;
                    if (!string.IsNullOrWhiteSpace(profile.ProfileImageUrl)) user.ProfileImageUrl = profile.ProfileImageUrl;
                    if (!string.IsNullOrWhiteSpace(profile.Username)) user.Username = profile.Username;
                }

                _currentUser = user;
                UserProfileChanged?.Invoke(this, _currentUser);
                return ServiceResult<User>.SuccessResult(user);
            }
            catch (Exception ex)
            {
                if (_currentUser != null) return ServiceResult<User>.SuccessResult(_currentUser, "Ã–nbellek kullanÄ±ldÄ±");
                return ServiceResult<User>.FailureResult("KullanÄ±cÄ± bilgileri yÃ¼klenemedi", ex.Message);
            }
        }

        public async Task<ServiceResult<bool>> UpdateUserProfileAsync(
            string? firstName = null,
            string? lastName = null,
            string? username = null,
            string? profileImageUrl = null)
        {
            if (CurrentUser == null) return ServiceResult<bool>.FailureResult("Oturum yok");

            try
            {
                // 1. Firebase Ana GÃ¼ncelleme (users ve user_profiles koleksiyonlarÄ±)
                // Not: profileService.UpdateUserProfileAsync iÃ§inde FullName hesaplanÄ±p gÃ¶nderilmelidir.
                var result = await _profileService.UpdateUserProfileAsync(
                    CurrentUser.UserId, firstName, lastName, username, profileImageUrl);

                if (!result.Success) return result;

                // 2. Yerel Nesneyi GÃ¼ncelle (ObservableProperty sayesinde UI anÄ±nda tepki verir)
                if (!string.IsNullOrWhiteSpace(firstName)) CurrentUser.FirstName = firstName;
                if (!string.IsNullOrWhiteSpace(lastName)) CurrentUser.LastName = lastName;
                if (!string.IsNullOrWhiteSpace(username)) CurrentUser.Username = username;
                if (!string.IsNullOrWhiteSpace(profileImageUrl)) CurrentUser.ProfileImageUrl = profileImageUrl;

                // âœ… 3. Senkronize EdilmiÅŸ FullName ile DiÄŸer TablolarÄ± GÃ¼ncelle
                // CurrentUser.FullName artÄ±k FirstName ve LastName'den otomatik oluÅŸur.
                string updatedFullName = CurrentUser.FullName;
                string updatedPhotoUrl = CurrentUser.ProfileImageUrl;

                // Paralel olarak diÄŸer veritabanÄ± dÃ¼ÄŸÃ¼mlerini (ÃœrÃ¼nler, Mesajlar vb.) gÃ¼ncelle
                await RunBulkUpdatesAsync(updatedFullName, updatedPhotoUrl);

                // 4. Global UI Bildirimi (DiÄŸer ViewModel'leri haberdar et)
                SetUser(CurrentUser);

                return ServiceResult<bool>.SuccessResult(true, "Profil baÅŸarÄ±yla gÃ¼ncellendi");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Profil gÃ¼ncellenirken hata oluÅŸtu", ex.Message);
            }
        }

        private async Task RunBulkUpdatesAsync(string fullName, string photoUrl)
        {
            // VeritabanÄ±ndaki tÃ¼m iliÅŸkili kayÄ±tlarda isim ve fotoÄŸrafÄ± modernize et
            var tasks = new List<Task<ServiceResult<bool>>>
            {
                _productService.UpdateUserInfoInProductsAsync(CurrentUser.UserId, fullName, photoUrl),
                _serviceService.UpdateUserInfoInServicesAsync(CurrentUser.UserId, fullName, photoUrl),
                _goodDeedService.UpdateUserInfoInPostsAsync(CurrentUser.UserId, fullName, photoUrl),
                _messagingService.UpdateUserInfoInMessagesAsync(CurrentUser.UserId, fullName, photoUrl),
                _messagingService.UpdateUserInfoInConversationsAsync(CurrentUser.UserId, fullName, photoUrl)
            };

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                // Bulk update hatalarÄ± kritik deÄŸildir, logla ama ana iÅŸlemi bozma
                KamPay.Helpers.AppLogger.DebugLog($"âš ï¸ Bulk update senkronizasyon hatasÄ±: {ex.Message}");
            }
        }

        public void ClearUser()
        {
            _currentUser = null;
            UserProfileChanged?.Invoke(this, null);
        }
    }
}
