namespace KamPay.Services
{
    public class EmailSettings
    {
        public string SmtpHost { get; set; }          // örn: "smtp.bartin.edu.tr"
        public int SmtpPort { get; set; } = 587;     // 587 (STARTTLS) veya 465 (SSL) olabilir
        public bool UseSsl { get; set; } = true;     // genelde true
        public string FromEmail { get; set; }        // örn: "kampay@bartin.edu.tr"
        public string FromName { get; set; } = "KamPay";
        public string Username { get; set; }         // smtp auth kullanýcý adý (genelde full email)
        public string Password { get; set; }         // smtp þifresi (güvenli saklanmalý)
    }
}
