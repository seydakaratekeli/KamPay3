using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KamPay.Models;
using KamPay.Services.CampusGuide;

namespace KamPay.ViewModels;

[QueryProperty(nameof(Business), "Business")]
public partial class EditBusinessProfileViewModel : ObservableObject
{
    private readonly IBusinessManagementService _businessManagementService;

    [ObservableProperty] private MicroBusiness? business;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private string errorMessage = string.Empty;

    public List<MicroBusinessCategory> Categories { get; } =
        Enum.GetValues(typeof(MicroBusinessCategory)).Cast<MicroBusinessCategory>().ToList();

    public EditBusinessProfileViewModel(IBusinessManagementService businessManagementService)
    {
        _businessManagementService = businessManagementService;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (Business == null)
            return;

        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;
            var result = await _businessManagementService.UpdateMyBusinessAsync(Business);
            if (!result.Success)
            {
                ErrorMessage = result.Message;
                return;
            }

            await Shell.Current.DisplayAlert("Başarılı", result.Message, "Tamam");
            await Shell.Current.GoToAsync("..");
        }
        finally
        {
            IsLoading = false;
        }
    }
}
