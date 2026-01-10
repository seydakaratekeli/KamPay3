using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KamPay.Models;
using KamPay.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KamPay.ViewModels
{
    [QueryProperty("TargetProfile", "UserProfile")]
    public partial class EditProfileViewModel : ObservableObject
    {
        private readonly IUserProfileService _userProfileService;
        private readonly IStorageService _storageService;
        private readonly IUserStateService _userStateService;
        private readonly IProductService _productService;
        private readonly IMessagingService _messagingService;
        private readonly IServiceSharingService _serviceSharingService;
        private readonly IGoodDeedService _goodDeedService;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private UserProfile _targetProfile;

        public EditProfileViewModel(
            IUserProfileService userProfileService,
            IStorageService storageService,
            IUserStateService userStateService,
            IProductService productService,
            IMessagingService messagingService,
            IServiceSharingService serviceSharingService,
            IGoodDeedService goodDeedService)
        {
            _userProfileService = userProfileService;
            _storageService = storageService;
            _userStateService = userStateService;
            _productService = productService;
            _messagingService = messagingService;
            _serviceSharingService = serviceSharingService;
            _goodDeedService = goodDeedService;
        }

        [RelayCommand]
        private async Task ChangePhotoAsync()
        {
            if (IsBusy) return;

            try
            {
                var photo = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions
                {
                    Title = "Profil Fotoğrafı Seç"
                });

                if (photo == null) return;

                IsBusy = true;

                var uploadResult = await _storageService.UploadProfileImageAsync(photo.FullPath, TargetProfile.UserId);

                if (uploadResult.Success)
                {
                    TargetProfile.ProfileImageUrl = uploadResult.Data;

                    // ✅ Daha temiz UI güncelleme: Nesnenin değiştiğini bildir
                    OnPropertyChanged(nameof(TargetProfile));

                    await Shell.Current.DisplayAlert("Başarılı", "Fotoğraf yüklendi. Kaydetmeyi unutmayın.", "Tamam");
                }
                else
                {
                    await Shell.Current.DisplayAlert("Hata", uploadResult.Message, "Tamam");
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", "Fotoğraf seçilirken hata: " + ex.Message, "Tamam");
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private async Task SaveProfileAsync()
        {
            if (IsBusy || TargetProfile == null) return;

            try
            {
                IsBusy = true;

                var result = await _userProfileService.UpdateUserProfileAsync(
                    TargetProfile.UserId,
                    TargetProfile.FirstName,
                    TargetProfile.LastName,
                    TargetProfile.Username,
                    TargetProfile.ProfileImageUrl
                );

                if (result.Success)
                {
                    // ✅ YEREL DURUM GÜNCELLEME (UI'ın anında değişmesini sağlar)
                    var currentUser = await _userStateService.GetCurrentUserAsync();
                    if (currentUser != null)
                    {
                        currentUser.FirstName = TargetProfile.FirstName;
                        currentUser.LastName = TargetProfile.LastName;
                        currentUser.Username = TargetProfile.Username;
                        currentUser.ProfileImageUrl = TargetProfile.ProfileImageUrl;

                        // Bu çağrı ProfileViewModel'deki event'i tetikler ve tüm UI'ı yeniler
                        _userStateService.SetUser(currentUser);
                    }
                    
                    // ✅ YENİ: Cascade update - Tüm koleksiyonlardaki kullanıcı bilgilerini güncelle
                    var fullName = TargetProfile.GetFullName();
                    var updateTasks = new List<Task<ServiceResult<bool>>>
                    {
                        _productService.UpdateUserInfoInProductsAsync(TargetProfile.UserId, fullName, TargetProfile.ProfileImageUrl),
                        _messagingService.UpdateUserInfoInMessagesAsync(TargetProfile.UserId, fullName, TargetProfile.ProfileImageUrl),
                        _messagingService.UpdateUserInfoInConversationsAsync(TargetProfile.UserId, fullName, TargetProfile.ProfileImageUrl),
                        _serviceSharingService.UpdateUserInfoInServicesAsync(TargetProfile.UserId, fullName, TargetProfile.ProfileImageUrl),
                        _goodDeedService.UpdateUserInfoInPostsAsync(TargetProfile.UserId, fullName, TargetProfile.ProfileImageUrl)
                    };

                    var updateResults = await Task.WhenAll(updateTasks);
                    var failedUpdates = updateResults.Where(r => !r.Success).ToList();

                    if (failedUpdates.Any())
                    {
                        Console.WriteLine($"⚠️ Bazı koleksiyonlar güncellenemedi: {failedUpdates.Count}");
                        // Yine de devam et, kritik değil
                    }
                    else
                    {
                        Console.WriteLine("✅ Tüm koleksiyonlarda kullanıcı bilgileri güncellendi");
                    }

                    await Shell.Current.DisplayAlert("Başarılı", "Profiliniz güncellendi.", "Tamam");
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    await Shell.Current.DisplayAlert("Hata", result.Message, "Tamam");
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", "Sistem hatası: " + ex.Message, "Tamam");
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private async Task CancelAsync() => await Shell.Current.GoToAsync("..");
    }
}