using KamPay.Models;
using System;
using System.Collections.Generic;
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
        private User _currentUser;

        public User CurrentUser 
        { 
            get => _currentUser;
            private set
            {
                _currentUser = value;
                // Fire event for all changes including null (for logout scenarios)
                UserProfileChanged?.Invoke(this, _currentUser);
            }
        }

        public event EventHandler<User> UserProfileChanged;

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
                // Firebase'de kullanıcı profilini güncelle
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

                string newFullName = CurrentUser.FullName;
                string newPhotoUrl = CurrentUser.ProfileImageUrl;

                // 🔥 Firebase'deki tüm ilgili verileri paralel olarak güncelle
                var tasks = new List<Task<ServiceResult<bool>>>
                {
                    _productService.UpdateUserInfoInProductsAsync(CurrentUser.UserId, newFullName, newPhotoUrl),
                    _serviceService.UpdateUserInfoInServicesAsync(CurrentUser.UserId, newFullName, newPhotoUrl),
                    _goodDeedService.UpdateUserInfoInPostsAsync(CurrentUser.UserId, newFullName, newPhotoUrl),
                    _messagingService.UpdateUserInfoInMessagesAsync(CurrentUser.UserId, newFullName, newPhotoUrl),
                    _messagingService.UpdateUserInfoInConversationsAsync(CurrentUser.UserId, newFullName, newPhotoUrl)
                };

                // Paralel çalıştır ve sonuçları logla
                try
                {
                    await Task.WhenAll(tasks);

                    // Hata olan task'ları logla
                    foreach (var task in tasks)
                    {
                        if (!task.Result.Success)
                        {
                            Console.WriteLine($"⚠️ Bulk update uyarısı: {task.Result.Message}");
                        }
                    }
                }
                catch (Exception taskEx)
                {
                    // Task hatalarını logla ama işlemi başarısız olarak işaretleme
                    // Çünkü kullanıcı profili zaten güncellendi
                    Console.WriteLine($"⚠️ Bulk update hatası: {taskEx.Message}");
                }

                // Explicitly trigger event after property updates to notify all listeners.
                // Note: This is NOT redundant - modifying properties on CurrentUser (e.g., CurrentUser.FirstName = x)
                // does not trigger the CurrentUser setter, only full reassignment (CurrentUser = newUser) does.
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
