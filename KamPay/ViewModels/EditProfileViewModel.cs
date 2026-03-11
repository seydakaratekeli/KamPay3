using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KamPay.Models;
using KamPay.Services;

namespace KamPay.ViewModels
{
    [QueryProperty("TargetProfile", "UserProfile")]
    public partial class EditProfileViewModel : ObservableObject
    {
        private readonly IUserProfileService _userProfileService;
        private readonly IStorageService _storageService;
        private readonly IUserStateService _userStateService; // Servis eklendi

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private UserProfile _targetProfile;

        // Constructor'a IUserStateService eklendi
        public EditProfileViewModel(
            IUserProfileService userProfileService,
            IStorageService storageService,
            IUserStateService userStateService)
        {
            _userProfileService = userProfileService;
            _storageService = storageService;
            _userStateService = userStateService;
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
                    // ✅ UserProfile artık ObservableObject — ayrıca OnPropertyChanged gerekmiyor
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