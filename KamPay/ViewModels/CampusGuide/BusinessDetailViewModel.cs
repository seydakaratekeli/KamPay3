using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KamPay.Helpers;
using KamPay.Models;
using KamPay.Services.CampusGuide;

namespace KamPay.ViewModels;

[QueryProperty(nameof(Business), "Business")]
public partial class BusinessDetailViewModel : ObservableObject
{
    private readonly ICampaignService _campaignService;

    [ObservableProperty] private MicroBusiness? business;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private bool isRefreshing;
    [ObservableProperty] private string statusMessage = string.Empty;

    public ObservableRangeCollection<Campaign> Campaigns { get; } = new();

    public bool HasBusiness => Business != null;
    public bool HasCampaigns => Campaigns.Count > 0;
    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public BusinessDetailViewModel(ICampaignService campaignService)
    {
        _campaignService = campaignService;
        Campaigns.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasCampaigns));
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (Business == null)
            return;

        IsRefreshing = true;
        try
        {
            await LoadCampaignsAsync(forceRefresh: true);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task GoBackAsync()
    {
        await Shell.Current.GoToAsync("..");
    }

    partial void OnBusinessChanged(MicroBusiness? value)
    {
        OnPropertyChanged(nameof(HasBusiness));
        if (value != null)
            _ = LoadCampaignsAsync(forceRefresh: false);
    }

    partial void OnStatusMessageChanged(string value) => OnPropertyChanged(nameof(HasStatusMessage));

    private async Task LoadCampaignsAsync(bool forceRefresh)
    {
        if (Business == null)
            return;

        IsLoading = !IsRefreshing;
        StatusMessage = string.Empty;

        try
        {
            var result = await _campaignService.GetActiveCampaignsByBusinessIdAsync(Business.BusinessId, forceRefresh);
            if (!result.Success)
            {
                Campaigns.ReplaceRange(Array.Empty<Campaign>());
                StatusMessage = result.Message;
                return;
            }

            var campaigns = result.Data ?? new List<Campaign>();
            Campaigns.ReplaceRange(campaigns);
            StatusMessage = campaigns.Count == 0
                ? "Bu işletmenin aktif kampanyası bulunmuyor."
                : result.Message == "İşlem başarılı" ? string.Empty : result.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
