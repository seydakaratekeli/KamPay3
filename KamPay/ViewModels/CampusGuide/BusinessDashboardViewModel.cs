using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KamPay.Models;
using KamPay.Services.Auth;
using KamPay.Services.CampusGuide;

namespace KamPay.ViewModels;

public partial class BusinessDashboardViewModel : ObservableObject
{
    private readonly IBusinessManagementService _businessManagementService;
    private readonly ICampaignManagementService _campaignManagementService;
    private readonly IAuthenticationService _authService;

    [ObservableProperty] private MicroBusiness? business;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private string statusMessage = string.Empty;

    public bool HasVerifiedBusiness => Business?.IsPubliclyVisible == true;
    public bool HasBusiness => Business != null;

    public BusinessDashboardViewModel(
        IBusinessManagementService businessManagementService,
        ICampaignManagementService campaignManagementService,
        IAuthenticationService authService)
    {
        _businessManagementService = businessManagementService;
        _campaignManagementService = campaignManagementService;
        _authService = authService;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            IsLoading = true;
            var result = await _businessManagementService.GetMyBusinessAsync();
            if (!result.Success)
            {
                StatusMessage = result.Message;
                return;
            }

            Business = result.Data;
            StatusMessage = Business?.VerificationStatus switch
            {
                BusinessVerificationStatus.PendingReview => "Başvurunuz admin onayı bekliyor. Onaylanınca kampanya oluşturabileceksiniz.",
                BusinessVerificationStatus.Rejected => $"Başvurunuz reddedildi. {Business.RejectionReason}",
                BusinessVerificationStatus.Suspended => "İşletmeniz askıya alınmış. Lütfen admin ile iletişime geçin.",
                BusinessVerificationStatus.Verified => "İşletmeniz yayında.",
                _ => "Başvuru durumu hazırlanıyor."
            };
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnBusinessChanged(MicroBusiness? value)
    {
        OnPropertyChanged(nameof(HasVerifiedBusiness));
        OnPropertyChanged(nameof(HasBusiness));
    }

    [RelayCommand]
    private async Task EditProfileAsync()
    {
        if (Business == null)
            return;

        await Shell.Current.GoToAsync(nameof(KamPay.Views.CampusGuide.EditBusinessProfilePage), new Dictionary<string, object>
        {
            { "Business", Business }
        });
    }

    [RelayCommand]
    private async Task ManageCampaignsAsync()
    {
        await Shell.Current.GoToAsync(nameof(KamPay.Views.CampusGuide.MyCampaignsPage));
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _authService.LogoutAsync();
        await Shell.Current.GoToAsync("//LoginPage");
    }
}
