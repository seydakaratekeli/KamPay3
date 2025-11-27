using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KamPay.Models;
using KamPay.Services;
using System;
using System.Threading.Tasks;

namespace KamPay.ViewModels
{
    public partial class SurpriseBoxViewModel : ObservableObject
    {
        private readonly ISurpriseBoxService _surpriseBoxService;
        private readonly IAuthenticationService _authenticationService;
        private readonly IUserProfileService _userProfileService;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanRedeem))] // IsLoading değişince CanRedeem'i de güncelle
        private bool isLoading;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasError))]
        private string errorMessage;

        [ObservableProperty]
        private Product redemptionResult;

        // Kullanıcının puanını tutar
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanRedeem))] // Puan değişince CanRedeem'i güncelle
        private int userPoints;

        [ObservableProperty]
        private string successMessage;

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        // Butonun aktif olup olmayacağını belirleyen özellik
        // Hem yükleme yapmıyor olmalı hem de puanı en az 100 olmalı
        public bool CanRedeem => !IsLoading && UserPoints >= 100;

        public event EventHandler<bool> RedemptionCompleted;

        public SurpriseBoxViewModel(
            ISurpriseBoxService surpriseBoxService,
            IAuthenticationService authenticationService,
            IUserProfileService userProfileService)
        {
            _surpriseBoxService = surpriseBoxService;
            _authenticationService = authenticationService;
            _userProfileService = userProfileService;

            _ = LoadUserPointsAsync();
        }

        // Puanları yükleme metodu
        private async Task LoadUserPointsAsync()
        {
            try
            {
                var user = await _authenticationService.GetCurrentUserAsync();
                if (user != null)
                {
                    var statsResult = await _userProfileService.GetUserStatsAsync(user.UserId);
                    if (statsResult.Success)
                    {
                        // MainThread'de çalıştırarak UI güncellemesini garantiye alalım
                        await MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            UserPoints = statsResult.Data.Points;
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Puan yükleme hatası: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task RedeemBoxAsync()
        {
            if (!CanRedeem) return; // Ekstra güvenlik kontrolü

            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;
                SuccessMessage = string.Empty;

                var user = await _authenticationService.GetCurrentUserAsync();
                if (user == null)
                {
                    ErrorMessage = "Lütfen giriş yapın.";
                    RedemptionCompleted?.Invoke(this, false);
                    return;
                }

                var result = await _surpriseBoxService.RedeemSurpriseBoxAsync(user.UserId);

                if (result.Success && result.Data != null)
                {
                    RedemptionResult = result.Data;
                    SuccessMessage = result.Message;

                    // Puanları güncelle (Kutu açıldığı için puan düştü)
                    await LoadUserPointsAsync();

                    RedemptionCompleted?.Invoke(this, true);
                }
                else
                {
                    ErrorMessage = result.Message;
                    RedemptionCompleted?.Invoke(this, false);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Beklenmedik bir hata oluştu.";
                Console.WriteLine($"❌ RedeemBox hatası: {ex.Message}");
                RedemptionCompleted?.Invoke(this, false);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void Reset()
        {
            ErrorMessage = string.Empty;
            SuccessMessage = string.Empty;
            RedemptionResult = null;
            // Puanları tekrar kontrol etmeye gerek yok, CanRedeem otomatik hesaplanacak
        }

        public async Task RefreshAsync()
        {
            await LoadUserPointsAsync();
        }
    }
}