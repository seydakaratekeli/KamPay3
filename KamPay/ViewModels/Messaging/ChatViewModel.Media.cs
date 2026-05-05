using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using KamPay.Services;
using KamPay.Models;
using KamPay.Views;
using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Helpers;
using System.Reactive.Linq;
using KamPay.Services.Auth;
using KamPay.Services.Messaging;
using KamPay.Services.Transactions;

namespace KamPay.ViewModels
{
    public partial class ChatViewModel
    {
        [RelayCommand]
        private async Task PickImageAsync()
        {
            if (IsUploadingImage) return;

            try
            {
                var result = await MediaPicker.PickPhotoAsync(new MediaPickerOptions
                {
                    Title = "FotoÃ„Å¸raf SeÃƒÂ§in"
                });

                if (result != null)
                {
                    SelectedImagePath = result.FullPath;
                    await SendImageMessageAsync(result.FullPath);
                }
            }
            catch (FeatureNotSupportedException)
            {
                await Application.Current!.MainPage!.DisplayAlert("Hata", "Bu cihazda fotoÃ„Å¸raf seÃƒÂ§me desteklenmiyor.", "Tamam");
            }
            catch (PermissionException)
            {
                await Application.Current!.MainPage!.DisplayAlert("Hata", "Galeri eriÃ…Å¸im izni gerekli.", "Tamam");
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"Ã¢ÂÅ’ PickImage hatasÃ„Â±: {ex.Message}");
                await Application.Current!.MainPage!.DisplayAlert("Hata", "FotoÃ„Å¸raf seÃƒÂ§ilemedi.", "Tamam");
            }
        }


        [RelayCommand]
        private async Task TakePhotoAsync()
        {
            if (IsUploadingImage) return;

            try
            {
                if (!MediaPicker.IsCaptureSupported)
                {
                    await Application.Current!.MainPage!.DisplayAlert("Hata", "Bu cihazda kamera desteklenmiyor.", "Tamam");
                    return;
                }

                var result = await MediaPicker.CapturePhotoAsync(new MediaPickerOptions
                {
                    Title = "FotoÃ„Å¸raf Ãƒâ€¡ekin"
                });

                if (result != null)
                {
                    SelectedImagePath = result.FullPath;
                    await SendImageMessageAsync(result.FullPath);
                }
            }
            catch (FeatureNotSupportedException)
            {
                await Application.Current!.MainPage!.DisplayAlert("Hata", "Bu cihazda kamera desteklenmiyor.", "Tamam");
            }
            catch (PermissionException)
            {
                await Application.Current!.MainPage!.DisplayAlert("Hata", "Kamera eriÃ…Å¸im izni gerekli.", "Tamam");
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"Ã¢ÂÅ’ TakePhoto hatasÃ„Â±: {ex.Message}");
                await Application.Current!.MainPage!.DisplayAlert("Hata", "FotoÃ„Å¸raf ÃƒÂ§ekilemedi.", "Tamam");
            }
        }


        private async Task SendImageMessageAsync(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath) || _currentUser == null || Conversation == null)
            {
                return;
            }

            // HÃ„Â±z SÃ„Â±nÃ„Â±rÃ„Â± KontrolÃƒÂ¼
            var limitCheck = KamPay.Helpers.SecureRateLimiters.ImageUpload.CheckRequest(_currentUser.UserId);
            if (!limitCheck.IsAllowed)
            {
                await Application.Current!.MainPage!.DisplayAlert(Res["Error"], limitCheck.Message, Res["Ok"]);
                return;
            }
            try
            {
                IsUploadingImage = true;

                // 1Ã¯Â¸ÂÃ¢Æ’Â£ Temp mesaj oluÃ…Å¸tur (loading state ile)
                var tempMessage = new Message
                {
                    MessageId = $"temp_{Guid.NewGuid()}",
                    ConversationId = ConversationId,
                    SenderId = _currentUser.UserId,
                    SenderName = _currentUser.FullName,
                    SenderPhotoUrl = _currentUser.ProfileImageUrl,
                    ReceiverId = Conversation.GetOtherUserId(_currentUser.UserId) ?? string.Empty,
                    ReceiverName = Conversation.GetOtherUserName(_currentUser.UserId),
                    ReceiverPhotoUrl = Conversation.GetOtherUserPhotoUrl(_currentUser.UserId),
                    Content = "ÄŸÅ¸â€œÂ· FotoÃ„Å¸raf",
                    Type = MessageType.Image,
                    ImageUrl = imagePath, // GeÃƒÂ§ici olarak local path gÃƒÂ¶ster
                    IsImageLoading = true,
                    ProductId = Conversation.ProductId,
                    ProductTitle = Conversation.ProductTitle,
                    ProductThumbnail = Conversation.ProductThumbnail,
                    IsSentByMe = true,
                    SentAt = DateTime.UtcNow,
                    IsDelivered = false,
                    IsRead = false
                };

                InsertMessageSorted(tempMessage);
                WeakReferenceMessenger.Default.Send(new ScrollToChatMessage(tempMessage));

                var mediaResult = await _chatMediaService.SendImageMessageAsync(
                    imagePath,
                    ConversationId,
                    Conversation,
                    _currentUser);

                if (!mediaResult.Success)
                {
                    Messages.Remove(tempMessage);
                    await Application.Current!.MainPage!.DisplayAlert("Hata", mediaResult.Message ?? "Gorsel gonderilemedi.", "Tamam");
                }
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"Ã¢ÂÅ’ SendImageMessage hatasÃ„Â±: {ex.Message}");
                await Application.Current!.MainPage!.DisplayAlert("Hata", "FotoÃ„Å¸raf gÃƒÂ¶nderilemedi.", "Tamam");
            }
            finally
            {
                IsUploadingImage = false;
                SelectedImagePath = null;
            }
        }


        [RelayCommand]
        private async Task ViewImageAsync(string imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl)) return;

            try
            {
                await Shell.Current.GoToAsync($"{nameof(ImageViewerPage)}?photoUrl={Uri.EscapeDataString(imageUrl)}");
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"Ã¢ÂÅ’ ViewImage hatasÃ„Â±: {ex.Message}");
            }
        }

    }
}
