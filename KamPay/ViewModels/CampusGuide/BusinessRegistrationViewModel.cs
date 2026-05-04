using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KamPay.Models;
using KamPay.Services.CampusGuide;

namespace KamPay.ViewModels;

public partial class BusinessRegistrationViewModel : ObservableObject
{
    private readonly IBusinessRegistrationService _registrationService;

    [ObservableProperty] private string ownerFirstName = string.Empty;
    [ObservableProperty] private string ownerLastName = string.Empty;
    [ObservableProperty] private string ownerEmail = string.Empty;
    [ObservableProperty] private string ownerPhoneNumber = string.Empty;
    [ObservableProperty] private string password = string.Empty;
    [ObservableProperty] private string passwordConfirm = string.Empty;
    [ObservableProperty] private string businessName = string.Empty;
    [ObservableProperty] private string description = string.Empty;
    [ObservableProperty] private MicroBusinessCategory selectedCategory = MicroBusinessCategory.Other;
    [ObservableProperty] private string location = string.Empty;
    [ObservableProperty] private string instagramUrl = string.Empty;
    [ObservableProperty] private string websiteUrl = string.Empty;
    [ObservableProperty] private string errorMessage = string.Empty;
    [ObservableProperty] private bool isLoading;

    public List<MicroBusinessCategory> Categories { get; } =
        Enum.GetValues(typeof(MicroBusinessCategory)).Cast<MicroBusinessCategory>().ToList();

    public BusinessRegistrationViewModel(IBusinessRegistrationService registrationService)
    {
        _registrationService = registrationService;
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;

            var request = new BusinessRegistrationRequest
            {
                OwnerFirstName = OwnerFirstName,
                OwnerLastName = OwnerLastName,
                OwnerEmail = OwnerEmail,
                OwnerPhoneNumber = OwnerPhoneNumber,
                Password = Password,
                PasswordConfirm = PasswordConfirm,
                BusinessName = BusinessName,
                Description = Description,
                Category = SelectedCategory,
                Location = Location,
                InstagramUrl = InstagramUrl,
                WebsiteUrl = WebsiteUrl
            };

            var result = await _registrationService.RegisterBusinessAsync(request);
            if (!result.Success)
            {
                ErrorMessage = result.Errors?.Any() == true
                    ? string.Join("\n", result.Errors)
                    : result.Message;
                return;
            }

            await Shell.Current.DisplayAlert("Başvuru alındı", result.Message, "Tamam");
            await Shell.Current.GoToAsync("//LoginPage");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task GoToLoginAsync()
    {
        await Shell.Current.GoToAsync("//LoginPage");
    }
}
