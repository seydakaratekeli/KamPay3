using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KamPay.Models;
using KamPay.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace KamPay.ViewModels
{
    /// <summary>
    /// ?? ARMUT MODELÝ: Müþteri talebi detay sayfasý
    /// Profesyoneller buradan teklif gönderebilir
    /// Müþteriler buradan gelen teklifleri görebilir
    /// </summary>
    [QueryProperty(nameof(RequestId), "requestId")]
    public partial class CustomerRequestDetailsViewModel : ObservableObject
    {
        private readonly IServiceSharingService _serviceService;
        private readonly IAuthenticationService _authService;
        private readonly IUserProfileService _userProfileService;

        [ObservableProperty]
        private string requestId = "";

        [ObservableProperty]
        private CustomerServiceRequest? customerRequest;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private bool isCurrentUserCustomer; // Talep sahibi mi?

        [ObservableProperty]
        private bool canSendProposal; // Profesyonel teklif gönderebilir mi?

        [ObservableProperty]
        private bool hasAlreadySentProposal; // Daha önce teklif gönderdi mi?

        // Teklif gönderme formu
        [ObservableProperty]
        private decimal proposalPrice;

        [ObservableProperty]
        private string proposalMessage = "";

        [ObservableProperty]
        private int estimatedDays = 1;

        [ObservableProperty]
        private bool isProposalFormVisible;

        // Gelen teklifler (Müþteri için)
        public ObservableCollection<ProviderProposal> Proposals { get; } = new();

        public CustomerRequestDetailsViewModel(
            IServiceSharingService serviceService,
            IAuthenticationService authService,
            IUserProfileService userProfileService)
        {
            _serviceService = serviceService;
            _authService = authService;
            _userProfileService = userProfileService;
        }

        partial void OnRequestIdChanged(string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                _ = LoadRequestDetailsAsync();
            }
        }

        /// <summary>
        /// Talep detaylarýný yükle
        /// </summary>
        [RelayCommand]
        private async Task LoadRequestDetailsAsync()
        {
            try
            {
                IsLoading = true;

                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    await Shell.Current.DisplayAlert("Hata", "Oturum açýlmamýþ", "Tamam");
                    return;
                }

                // Talebi getir
                var requestResult = await _serviceService.GetCustomerRequestByIdAsync(RequestId);

                if (!requestResult.Success || requestResult.Data == null)
                {
                    await Shell.Current.DisplayAlert("Hata", "Talep bulunamadý", "Tamam");
                    await Shell.Current.GoToAsync("..");
                    return;
                }

                CustomerRequest = requestResult.Data;

                // Kullanýcý rolünü belirle
                IsCurrentUserCustomer = CustomerRequest.CustomerId == currentUser.UserId;

                if (IsCurrentUserCustomer)
                {
                    // Müþteri ise teklifleri yükle
                    await LoadProposalsAsync();
                    CanSendProposal = false;
                }
                else
                {
                    // Profesyonel ise daha önce teklif göndermiþ mi kontrol et
                    var myProposals = await _serviceService.GetProposalsForRequestAsync(RequestId);
                    
                    if (myProposals.Success && myProposals.Data != null)
                    {
                        var existingProposal = myProposals.Data.FirstOrDefault(p => p.ProviderId == currentUser.UserId);
                        HasAlreadySentProposal = existingProposal != null;
                        CanSendProposal = !HasAlreadySentProposal && CustomerRequest.Status == CustomerRequestStatus.Open;
                    }
                    else
                    {
                        CanSendProposal = CustomerRequest.Status == CustomerRequestStatus.Open;
                    }
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

        /// <summary>
        /// Teklifleri yükle (Müþteri için)
        /// </summary>
        private async Task LoadProposalsAsync()
        {
            try
            {
                var result = await _serviceService.GetProposalsForRequestAsync(RequestId);

                if (result.Success && result.Data != null)
                {
                    Proposals.Clear();
                    foreach (var proposal in result.Data.OrderByDescending(p => p.CreatedAt))
                    {
                        Proposals.Add(proposal);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? LoadProposalsAsync hatasý: {ex.Message}");
            }
        }

        /// <summary>
        /// Teklif formunu aç
        /// </summary>
        [RelayCommand]
        private void OpenProposalForm()
        {
            IsProposalFormVisible = true;
        }

        /// <summary>
        /// Teklif formunu kapat
        /// </summary>
        [RelayCommand]
        private void CloseProposalForm()
        {
            IsProposalFormVisible = false;
        }

        /// <summary>
        /// Teklif gönder
        /// </summary>
        [RelayCommand]
        private async Task SendProposalAsync()
        {
            try
            {
                // Validasyon
                if (ProposalPrice <= 0)
                {
                    await Shell.Current.DisplayAlert("Uyarý", "Geçerli bir fiyat girin", "Tamam");
                    return;
                }

                if (string.IsNullOrWhiteSpace(ProposalMessage))
                {
                    await Shell.Current.DisplayAlert("Uyarý", "Teklif mesajý gerekli", "Tamam");
                    return;
                }

                if (EstimatedDays <= 0)
                {
                    await Shell.Current.DisplayAlert("Uyarý", "Geçerli bir tamamlanma süresi girin", "Tamam");
                    return;
                }

                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null || CustomerRequest == null) return;

                IsLoading = true;

                // Profesyonel istatistiklerini al
                var statsResult = await _userProfileService.GetUserStatsAsync(currentUser.UserId);
                var stats = statsResult.Success ? statsResult.Data : new UserStats();

                // Teklif oluþtur
                var proposal = new ProviderProposal
                {
                    CustomerRequestId = RequestId,
                    RequestTitle = CustomerRequest.Title,
                    ProviderId = currentUser.UserId,
                    ProviderName = currentUser.FullName,
                    ProviderPhotoUrl = currentUser.ProfileImageUrl ?? "default_avatar.png",
                    CustomerId = CustomerRequest.CustomerId,
                    CustomerName = CustomerRequest.CustomerName,
                    Price = ProposalPrice,
                    Message = ProposalMessage.Trim(),
                    EstimatedDays = EstimatedDays,
                    Rating = 0, // Þimdilik varsayýlan deðer
                    CompletedJobsCount = stats.CompletedTrades // TotalProducts yerine CompletedTrades
                };

                var result = await _serviceService.SendProposalAsync(proposal);

                if (result.Success)
                {
                    await Shell.Current.DisplayAlert(
                        "Baþarýlý",
                        "Teklifiniz müþteriye gönderildi!",
                        "Harika!"
                    );

                    // Formu temizle ve kapat
                    ClearProposalForm();
                    IsProposalFormVisible = false;
                    HasAlreadySentProposal = true;
                    CanSendProposal = false;
                }
                else
                {
                    await Shell.Current.DisplayAlert("Hata", result.Message, "Tamam");
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

        /// <summary>
        /// Teklifi kabul et (Müþteri)
        /// </summary>
        [RelayCommand]
        private async Task AcceptProposalAsync(ProviderProposal proposal)
        {
            if (proposal == null) return;

            var confirm = await Shell.Current.DisplayAlert(
                "Teklifi Kabul Et",
                $"{proposal.ProviderName} tarafýndan gönderilen {proposal.Price:N2}? teklifini kabul ediyor musunuz?\n\n" +
                $"Bu iþlem geri alýnamaz ve diðer tüm teklifler otomatik olarak reddedilecektir.",
                "Evet, Kabul Et",
                "Hayýr"
            );

            if (!confirm) return;

            try
            {
                IsLoading = true;

                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null) return;

                var result = await _serviceService.AcceptProposalAsync(proposal.ProposalId, currentUser.UserId);

                if (result.Success)
                {
                    await Shell.Current.DisplayAlert(
                        "Baþarýlý",
                        "Teklif kabul edildi! Profesyonel bilgilendirildi.",
                        "Tamam"
                    );

                    // Sayfayý yenile
                    await LoadRequestDetailsAsync();
                }
                else
                {
                    await Shell.Current.DisplayAlert("Hata", result.Message, "Tamam");
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

        /// <summary>
        /// Teklifi reddet (Müþteri)
        /// </summary>
        [RelayCommand]
        private async Task RejectProposalAsync(ProviderProposal proposal)
        {
            if (proposal == null) return;

            var reason = await Shell.Current.DisplayPromptAsync(
                "Teklifi Reddet",
                "Teklifi reddetme nedeninizi belirtebilirsiniz (opsiyonel):",
                "Gönder",
                "Ýptal",
                placeholder: "Örn: Fiyat yüksek"
            );

            if (reason == null) return; // Ýptal

            try
            {
                IsLoading = true;

                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null) return;

                var result = await _serviceService.RejectProposalAsync(
                    proposal.ProposalId,
                    currentUser.UserId,
                    reason
                );

                if (result.Success)
                {
                    await Shell.Current.DisplayAlert("Baþarýlý", "Teklif reddedildi", "Tamam");
                    await LoadProposalsAsync();
                }
                else
                {
                    await Shell.Current.DisplayAlert("Hata", result.Message, "Tamam");
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

        /// <summary>
        /// Konumu haritada göster
        /// </summary>
        [RelayCommand]
        private async Task OpenLocationAsync()
        {
            if (CustomerRequest == null || 
                CustomerRequest.Latitude == null || 
                CustomerRequest.Longitude == null) 
                return;

            try
            {
                var location = new Location(
                    CustomerRequest.Latitude.Value,
                    CustomerRequest.Longitude.Value
                );

                var options = new MapLaunchOptions { Name = CustomerRequest.Location };
                await Map.OpenAsync(location, options);
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", $"Harita açýlamadý: {ex.Message}", "Tamam");
            }
        }

        /// <summary>
        /// Formu temizle
        /// </summary>
        private void ClearProposalForm()
        {
            ProposalPrice = 0;
            ProposalMessage = "";
            EstimatedDays = 1;
        }

        [RelayCommand]
        private async Task GoBackAsync()
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}
