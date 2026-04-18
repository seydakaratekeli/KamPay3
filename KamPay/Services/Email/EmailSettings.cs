namespace KamPay.Services
{
    // bu sayfa, e-posta gönderimi için gerekli ayarlarý tutar
    public class EmailSettings
    {
        public string SmtpHost { get; set; } = string.Empty;          // örn: "smtp.bartin.edu.tr"
        public int SmtpPort { get; set; }
        public bool UseSsl { get; set; }
        public string FromEmail { get; set; } = string.Empty;        // örn: "kampay@bartin.edu.tr"
        public string FromName { get; set; } = "KamPay";
        public string Username { get; set; } = string.Empty;         // smtp auth kullanýcý adý (genelde full email)
        public string Password { get; set; } = string.Empty;         // smtp þifresi (güvenli saklanmalý)
    }
}
