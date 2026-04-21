using KamPay.Models.Configuration;

namespace KamPay.Services.Configuration
{
    public class ConfigurationService : IConfigurationService
    {
        private readonly EmailSettings _emailSettings;
        private readonly FirebaseConfigSettings _firebaseConfig;

        public ConfigurationService(EmailSettings emailSettings, FirebaseConfigSettings firebaseConfig)
        {
            _emailSettings = emailSettings;
            _firebaseConfig = firebaseConfig;
        }

        public EmailSettings GetEmailSettings() => _emailSettings;

        public FirebaseConfigSettings GetFirebaseConfig() => _firebaseConfig;
    }
}