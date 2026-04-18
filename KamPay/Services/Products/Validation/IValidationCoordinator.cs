using KamPay.Models;

namespace KamPay.Services;

/// <summary>
/// ?? Doðrulama (Validation) stratejilerini yönetir
/// Single Responsibility: Sadece validation orkestrasyon ve strateji yönetimi
/// </summary>
public interface IValidationCoordinator
{
    /// <summary>
    /// Ürün doðrulamasý yapar
    /// </summary>
    ValidationResult ValidateProduct(ProductRequest request);
    
    /// <summary>
    /// Ýþlem (Transaction) doðrulamasý yapar
    /// </summary>
    ValidationResult ValidateTransaction(Transaction transaction);
    
    /// <summary>
    /// Kullanýcý doðrulamasý yapar
    /// </summary>
    ValidationResult ValidateUser(User user);
    
    /// <summary>
    /// Hizmet talebi doðrulamasý yapar
    /// </summary>
    ValidationResult ValidateServiceRequest(ServiceRequest request);
    
    /// <summary>
    /// Müþteri hizmet talebi doðrulamasý yapar
    /// </summary>
    ValidationResult ValidateCustomerRequest(CustomerServiceRequest request);
    
    /// <summary>
    /// Profesyonel teklif doðrulamasý yapar
    /// </summary>
    ValidationResult ValidateProviderProposal(ProviderProposal proposal);
    
    /// <summary>
    /// Mesaj doðrulamasý yapar
    /// </summary>
    ValidationResult ValidateMessage(SendMessageRequest message);
    
    /// <summary>
    /// Fiyat pazarlýðý doðrulamasý yapar
    /// </summary>
    ValidationResult ValidateNegotiation(decimal proposedPrice, decimal originalPrice, int roundCount, DateTime? startedAt);
    
    /// <summary>
    /// E-posta doðrulamasý yapar
    /// </summary>
    ValidationResult ValidateEmail(string email);
    
    /// <summary>
    /// Þifre doðrulamasý yapar
    /// </summary>
    ValidationResult ValidatePassword(string password);
    
    /// <summary>
    /// Telefon numarasý doðrulamasý yapar
    /// </summary>
    ValidationResult ValidatePhoneNumber(string phoneNumber);
}
