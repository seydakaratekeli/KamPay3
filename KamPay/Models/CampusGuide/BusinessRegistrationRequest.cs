namespace KamPay.Models;

public sealed class BusinessRegistrationRequest
{
    public string OwnerFirstName { get; set; } = string.Empty;
    public string OwnerLastName { get; set; } = string.Empty;
    public string OwnerEmail { get; set; } = string.Empty;
    public string OwnerPhoneNumber { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string PasswordConfirm { get; set; } = string.Empty;
    public string BusinessName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public MicroBusinessCategory Category { get; set; } = MicroBusinessCategory.Other;
    public string Location { get; set; } = string.Empty;
    public string InstagramUrl { get; set; } = string.Empty;
    public string WebsiteUrl { get; set; } = string.Empty;
}
