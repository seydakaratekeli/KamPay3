namespace KamPay.Models.Configuration
{
    public class EmailSettings
    {
        public string SmtpHost { get; set; } = string.Empty;
        public int SmtpPort { get; set; }
        public bool UseSsl { get; set; }
        public string FromEmail { get; set; } = string.Empty;
        public string FromName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class FirebaseConfigSettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string AuthDomain { get; set; } = string.Empty;
        public string DatabaseURL { get; set; } = string.Empty;
        public string ProjectId { get; set; } = string.Empty;
        public string StorageBucket { get; set; } = string.Empty;
    }

    public class ApiSettings
    {
        public string RealDeviceApiUrl { get; set; } = string.Empty;
        public string EmulatorApiUrl { get; set; } = string.Empty;
        public string LocalhostApiUrl { get; set; } = string.Empty;
    }

    public class BusinessRulesSettings
    {
        public string UniversityEmailDomain { get; set; } = string.Empty;
        public int MinPasswordLength { get; set; } = 8;
        public int MaxPasswordLength { get; set; } = 50;
        public int MaxProductImages { get; set; } = 5;
        public long MaxImageSizeBytes { get; set; } = 5242880;
    }

    public class AppConfig
    {
        public EmailSettings? EmailSettings { get; set; }
        public FirebaseConfigSettings? FirebaseConfig { get; set; }
        public ApiSettings? ApiSettings { get; set; }
        public BusinessRulesSettings? BusinessRules { get; set; }
    }
}
