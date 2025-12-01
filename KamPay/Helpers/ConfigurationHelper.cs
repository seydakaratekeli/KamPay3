using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace KamPay.Helpers
{
    /// <summary>
    /// Helper class to load configuration from embedded appsettings.json files
    /// </summary>
    public static class ConfigurationHelper
    {
        private static IConfiguration? _configuration;

        /// <summary>
        /// Gets the application configuration
        /// </summary>
        public static IConfiguration Configuration
        {
            get
            {
                if (_configuration == null)
                {
                    _configuration = BuildConfiguration();
                }
                return _configuration;
            }
        }

        private static IConfiguration BuildConfiguration()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var builder = new ConfigurationBuilder();

            // Load appsettings.json (embedded resource)
            var appsettingsStream = assembly.GetManifestResourceStream("KamPay.appsettings.json");
            if (appsettingsStream != null)
            {
                builder.AddJsonStream(appsettingsStream);
            }

            // Load environment-specific settings if available
            // In production, this should load from Azure Key Vault or secure storage
#if DEBUG
            var developmentStream = assembly.GetManifestResourceStream("KamPay.appsettings.Development.json");
            if (developmentStream != null)
            {
                builder.AddJsonStream(developmentStream);
            }
#endif

            // Environment variables can override settings (highest priority)
            builder.AddEnvironmentVariables(prefix: "KAMPAY_");

            return builder.Build();
        }

        /// <summary>
        /// Gets email settings from configuration
        /// </summary>
        public static EmailSettings GetEmailSettings()
        {
            var settings = new EmailSettings();
            Configuration.GetSection("EmailSettings").Bind(settings);
            
            // Validate that password is not the placeholder
            if (string.IsNullOrEmpty(settings.Password) || 
                settings.Password == "YOUR_SMTP_PASSWORD_HERE" ||
                settings.Password == "SMTP_PAROLASI_BURAYA")
            {
                throw new InvalidOperationException(
                    "SMTP password not configured. Please set it in appsettings.Development.json " +
                    "or use environment variable KAMPAY_EmailSettings__Password");
            }

            return settings;
        }
    }
}
