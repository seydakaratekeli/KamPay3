using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KamPay.Helpers;
using KamPay.Models;
using KamPay.Services.CampusGuide;

namespace KamPay.ViewModels;

public partial class MyCampaignsViewModel : ObservableObject
{
    private readonly ICampaignManagementService _campaignManagementService;

    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private bool isFormVisible;
    [ObservableProperty] private string errorMessage = string.Empty;
    [ObservableProperty] private Campaign editingCampaign = CreateDefaultCampaign();

    public ObservableRangeCollection<Campaign> Campaigns { get; } = new();

    public MyCampaignsViewModel(ICampaignManagementService campaignManagementService)
    {
        _campaignManagementService = campaignManagementService;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;
            var result = await _campaignManagementService.GetMyCampaignsAsync();
            if (!result.Success)
            {
                ErrorMessage = result.Message;
                return;
            }

            Campaigns.ReplaceRange(result.Data ?? new List<Campaign>());
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void NewCampaign()
    {
        EditingCampaign = CreateDefaultCampaign();
        IsFormVisible = true;
    }

    [RelayCommand]
    private void EditCampaign(Campaign campaign)
    {
        EditingCampaign = new Campaign
        {
            CampaignId = campaign.CampaignId,
            BusinessId = campaign.BusinessId,
            CreatedByUserId = campaign.CreatedByUserId,
            Title = campaign.Title,
            Description = campaign.Description,
            BadgeText = campaign.BadgeText,
            ImageUrl = campaign.ImageUrl,
            StartsAt = campaign.StartsAt,
            EndsAt = campaign.EndsAt,
            IsActive = campaign.IsActive,
            Status = campaign.Status,
            DisplayOrder = campaign.DisplayOrder,
            CreatedAt = campaign.CreatedAt
        };
        IsFormVisible = true;
    }

    [RelayCommand]
    private async Task SaveCampaignAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;
            var result = string.IsNullOrWhiteSpace(EditingCampaign.CampaignId)
                ? await _campaignManagementService.CreateCampaignAsync(EditingCampaign)
                : await _campaignManagementService.UpdateCampaignAsync(EditingCampaign);

            if (!result.Success)
            {
                ErrorMessage = result.Errors?.Any() == true ? string.Join("\n", result.Errors) : result.Message;
                return;
            }

            IsFormVisible = false;
            await LoadAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task DeleteCampaignAsync(Campaign campaign)
    {
        if (campaign == null)
            return;

        var confirm = await Shell.Current.DisplayAlert("Kampanyayı sil", "Bu kampanya silinsin mi?", "Sil", "Vazgeç");
        if (!confirm)
            return;

        var result = await _campaignManagementService.DeleteCampaignAsync(campaign.CampaignId);
        if (!result.Success)
        {
            ErrorMessage = result.Message;
            return;
        }

        await LoadAsync();
    }

    [RelayCommand]
    private void CloseForm() => IsFormVisible = false;

    private static Campaign CreateDefaultCampaign() => new()
    {
        StartsAt = DateTime.UtcNow,
        EndsAt = DateTime.UtcNow.AddDays(7),
        IsActive = true,
        Status = CampaignStatus.Active
    };
}
