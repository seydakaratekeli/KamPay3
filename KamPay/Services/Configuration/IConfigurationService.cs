using KamPay.Models.Configuration;

namespace KamPay.Services.Configuration
{
    public interface IConfigurationService
    {
        EmailSettings GetEmailSettings();
        FirebaseConfigSettings GetFirebaseConfig();
    }
}