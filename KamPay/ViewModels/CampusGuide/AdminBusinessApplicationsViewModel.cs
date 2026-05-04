using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KamPay.Helpers;
using KamPay.Models;
using KamPay.Services.CampusGuide;

namespace KamPay.ViewModels;

public partial class AdminBusinessApplicationsViewModel : ObservableObject
{
    private readonly IBusinessManagementService _businessManagementService;

    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private string errorMessage = string.Empty;

    public ObservableRangeCollection<MicroBusiness> PendingBusinesses { get; } = new();

    public AdminBusinessApplicationsViewModel(IBusinessManagementService businessManagementService)
    {
        _businessManagementService = businessManagementService;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;
            var result = await _businessManagementService.GetPendingBusinessesAsync();
            if (!result.Success)
            {
                ErrorMessage = result.Message;
                return;
            }

            PendingBusinesses.ReplaceRange(result.Data ?? new List<MicroBusiness>());
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task VerifyAsync(MicroBusiness business)
    {
        if (business == null)
            return;

        var result = await _businessManagementService.VerifyBusinessAsync(business.BusinessId);
        if (!result.Success)
        {
            ErrorMessage = result.Message;
            return;
        }

        await LoadAsync();
    }

    [RelayCommand]
    private async Task RejectAsync(MicroBusiness business)
    {
        if (business == null)
            return;

        var reason = await Shell.Current.DisplayPromptAsync("Başvuruyu reddet", "Reddetme nedeni:", "Reddet", "Vazgeç");
        if (reason == null)
            return;

        var result = await _businessManagementService.RejectBusinessAsync(business.BusinessId, reason);
        if (!result.Success)
        {
            ErrorMessage = result.Message;
            return;
        }

        await LoadAsync();
    }
}
