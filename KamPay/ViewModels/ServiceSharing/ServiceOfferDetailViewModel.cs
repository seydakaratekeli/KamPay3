using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KamPay.Models;
using KamPay.Services;
using KamPay.Services.Auth;
using KamPay.Services.Messaging;
using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace KamPay.ViewModels
{
    [QueryProperty(nameof(OfferId), "offerId")]
    public partial class ServiceOfferDetailViewModel : ObservableObject
    {
        private readonly IServiceSharingService _serviceService;
        private readonly IAuthenticationService _authService;
        private readonly IUserProfileService _userProfileService;
        private readonly IMessagingService _messagingService;
        private readonly IServiceReviewService _reviewService;

        [ObservableProperty]
        private string offerId = "";

        [ObservableProperty]
        private ServiceOffer? offer;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private bool isCurrentUserOwner;

        [ObservableProperty]
        private bool isNotCurrentUserOwner;

        [ObservableProperty]
        private UserStats? providerStats;

        [ObservableProperty]
        private double providerAverageRating;

        [ObservableProperty]
        private int providerCompletedJobs;

        public ServiceOfferDetailViewModel(
            IServiceSharingService serviceService,
            IAuthenticationService authService,
            IUserProfileService userProfileService,
            IMessagingService messagingService,
            IServiceReviewService reviewService)
        {
            _serviceService = serviceService;
            _authService = authService;
            _userProfileService = userProfileService;
            _messagingService = messagingService;
            _reviewService = reviewService;
        }

        partial void OnOfferIdChanged(string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                _ = LoadOfferDetailsAsync();
            }
        }

        [RelayCommand]
        private async Task LoadOfferDetailsAsync()
        {
            try
            {
                IsLoading = true;

                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    await Shell.Current.DisplayAlert("Hata", "Oturum açılmamış", "Tamam");
                    return;
                }

                var offerResult = await _serviceService.GetServiceOfferByIdAsync(OfferId);

                if (!offerResult.Success || offerResult.Data == null)
                {
                    await Shell.Current.DisplayAlert("Hata", "İlan bulunamadı", "Tamam");
                    await Shell.Current.GoToAsync("..");
                    return;
                }

                Offer = offerResult.Data;
                IsCurrentUserOwner = Offer.ProviderId == currentUser.UserId;
                IsNotCurrentUserOwner = !IsCurrentUserOwner;

                var statsResult = await _userProfileService.GetUserStatsAsync(Offer.ProviderId);
                if (statsResult.Success)
                {
                    ProviderStats = statsResult.Data;
                }

                var ratingResult = await _reviewService.GetProviderAverageRatingAsync(Offer.ProviderId);
                if (ratingResult.Success)
                {
                    ProviderAverageRating = ratingResult.Data;
                }

                var jobsResult = await _reviewService.GetProviderTotalCompletedJobsAsync(Offer.ProviderId);
                if (jobsResult.Success)
                {
                    ProviderCompletedJobs = jobsResult.Data;
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", ex.Message, "Tamam");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task RequestServiceAsync()
        {
            if (Offer == null) return;

            var user = await _authService.GetCurrentUserAsync();
            if (user == null)
            {
                await Shell.Current.DisplayAlert("Hata", "Giriş yapılmalı.", "Tamam");
                return;
            }

            try
            {
                var msg = await Application.Current!.MainPage!.DisplayPromptAsync(
                    "Hizmet Talebi",
                    $"'{Offer.Title}' için mesajınız:",
                    "Gönder",
                    "İptal",
                    "Merhaba, hizmetinizle ilgileniyorum."
                );

                if (string.IsNullOrWhiteSpace(msg)) return;

                IsLoading = true;

                var res = await _serviceService.RequestServiceAsync(Offer, user, msg);

                if (res.Success)
                    await Shell.Current.DisplayAlert("Başarılı", res.Message, "Tamam");
                else
                    await Shell.Current.DisplayAlert("Hata", res.Message ?? "Talep gönderilemedi.", "Tamam");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", ex.Message, "Tamam");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task MessageProviderAsync()
        {
            if (Offer == null || IsLoading) return;

            try
            {
                IsLoading = true;

                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    await Shell.Current.DisplayAlert("Hata", "Giriş yapılmalı.", "Tamam");
                    return;
                }

                var conversationResult = await _messagingService.GetOrCreateConversationAsync(
                    currentUser.UserId,
                    Offer.ProviderId,
                    Offer.ServiceId,
                    "Negotiation");

                if (conversationResult.Success && conversationResult.Data != null)
                {
                    await Shell.Current.GoToAsync($"ChatPage?conversationId={conversationResult.Data.ConversationId}");
                }
                else
                {
                    await Shell.Current.DisplayAlert("Hata", conversationResult.Message ?? "Mesaj gönderilemedi.", "Tamam");
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", ex.Message, "Tamam");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task GoBackAsync()
        {
            await Shell.Current.GoToAsync("..");
        }

        [RelayCommand]
        private async Task EditOfferAsync()
        {
            if (Offer == null) return;
            await Shell.Current.GoToAsync($"{nameof(Views.EditServiceOfferPage)}?offerId={Offer.ServiceId}");
        }

        [RelayCommand]
        private async Task DeleteOfferAsync()
        {
            if (Offer == null) return;

            bool confirm = await Shell.Current.DisplayAlert("Sil", "Bu ilanı silmek istediğinize emin misiniz?", "Evet", "Hayır");
            if (!confirm) return;

            try
            {
                IsLoading = true;
                var res = await _serviceService.DeleteServiceOfferAsync(Offer.ServiceId);
                if (res.Success)
                {
                    await Shell.Current.DisplayAlert("Başarılı", "İlan silindi.", "Tamam");
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    await Shell.Current.DisplayAlert("Hata", res.Message ?? "Silinemedi.", "Tamam");
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", ex.Message, "Tamam");
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
