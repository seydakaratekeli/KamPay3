using KamPay.Models;
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

        // UI'ın ve diğer ViewModel'lerin dinlediği olay
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
            // Olayı tetikle (Bu sayede ProfileViewModel gibi dinleyiciler UI'ı yeniler)
            UserProfileChanged?.Invoke(this, _currentUser);
        }

        public async Task<ServiceResult<User>> RefreshCurrentUserAsync()
        {
            try
            {
                var user = await _authService.GetCurrentUserAsync();
                if (user == null) return ServiceResult<User>.FailureResult("Kullanıcı oturumu bulunamadı");

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
                if (_currentUser != null) return ServiceResult<User>.SuccessResult(_currentUser, "Önbellek kullanıldı");
                return ServiceResult<User>.FailureResult("Kullanıcı bilgileri yüklenemedi", ex.Message);
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
                // 1. Firebase Ana Güncelleme (users ve user_profiles koleksiyonları)
                // Not: profileService.UpdateUserProfileAsync içinde FullName hesaplanıp gönderilmelidir.
                var result = await _profileService.UpdateUserProfileAsync(
                    CurrentUser.UserId, firstName, lastName, username, profileImageUrl);

                if (!result.Success) return result;

                // 2. Yerel Nesneyi Güncelle (ObservableProperty sayesinde UI anında tepki verir)
                if (!string.IsNullOrWhiteSpace(firstName)) CurrentUser.FirstName = firstName;
                if (!string.IsNullOrWhiteSpace(lastName)) CurrentUser.LastName = lastName;
                if (!string.IsNullOrWhiteSpace(username)) CurrentUser.Username = username;
                if (!string.IsNullOrWhiteSpace(profileImageUrl)) CurrentUser.ProfileImageUrl = profileImageUrl;

                // ✅ 3. Senkronize Edilmiş FullName ile Diğer Tabloları Güncelle
                // CurrentUser.FullName artık FirstName ve LastName'den otomatik oluşur.
                string updatedFullName = CurrentUser.FullName;
                string updatedPhotoUrl = CurrentUser.ProfileImageUrl;

                // Paralel olarak diğer veritabanı düğümlerini (Ürünler, Mesajlar vb.) güncelle
                await RunBulkUpdatesAsync(updatedFullName, updatedPhotoUrl);

                // 4. Global UI Bildirimi (Diğer ViewModel'leri haberdar et)
                SetUser(CurrentUser);

                return ServiceResult<bool>.SuccessResult(true, "Profil başarıyla güncellendi");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Profil güncellenirken hata oluştu", ex.Message);
            }
        }

        private async Task RunBulkUpdatesAsync(string fullName, string photoUrl)
        {
            // Veritabanındaki tüm ilişkili kayıtlarda isim ve fotoğrafı modernize et
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
                // Bulk update hataları kritik değildir, logla ama ana işlemi bozma
                System.Diagnostics.Debug.WriteLine($"⚠️ Bulk update senkronizasyon hatası: {ex.Message}");
            }
        }

        public void ClearUser()
        {
            _currentUser = null;
            UserProfileChanged?.Invoke(this, null);
        }
    }
}