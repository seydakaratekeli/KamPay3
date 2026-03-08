using KamPay.Models;
using KamPay.Helpers;
using System.Diagnostics;

namespace KamPay.Services;

/// <summary>
/// ?? Validation koordinatörü implementasyonu
/// ? Single Responsibility: Sadece validation strateji orkestrasyon
/// ? Delegates to: IProductService, InputSanitizer, ImageValidator, NegotiationRules
/// </summary>
public class ValidationCoordinator : IValidationCoordinator
{
    private readonly IProductService _productService;

    public ValidationCoordinator(IProductService productService)
    {
        _productService = productService ?? throw new ArgumentNullException(nameof(productService));
    }

    #region Product Validation

    public ValidationResult ValidateProduct(ProductRequest request)
    {
        try
        {
            Debug.WriteLine($"?? Ürün doðrulamasý baþlatýlýyor: {request?.Title}");
            
            // Delegate to ProductService for business logic validation
            var result = _productService.ValidateProduct(request);
            
            Debug.WriteLine($"? Ürün doðrulama sonucu: {(result.IsValid ? "Geçerli" : "Geçersiz")}");
            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? ValidateProduct hatasý: {ex.Message}");
            return ValidationResult.Failure($"Doðrulama hatasý: {ex.Message}");
        }
    }

    #endregion

    #region Transaction Validation

    public ValidationResult ValidateTransaction(Transaction transaction)
    {
        try
        {
            Debug.WriteLine($"?? Ýþlem doðrulamasý baþlatýlýyor: {transaction?.TransactionId}");
            
            var result = new ValidationResult();
            
            if (transaction == null)
            {
                result.AddError("Ýþlem bilgisi boþ olamaz");
                return result;
            }

            // Ürün ID kontrolü
            if (string.IsNullOrWhiteSpace(transaction.ProductId))
            {
                result.AddError("Ürün ID'si gerekli");
            }

            // Alýcý ve satýcý kontrolü
            if (string.IsNullOrWhiteSpace(transaction.BuyerId))
            {
                result.AddError("Alýcý bilgisi gerekli");
            }

            if (string.IsNullOrWhiteSpace(transaction.SellerId))
            {
                result.AddError("Satýcý bilgisi gerekli");
            }

            // Ayný kiþi kontrolü
            if (!string.IsNullOrWhiteSpace(transaction.BuyerId) && 
                !string.IsNullOrWhiteSpace(transaction.SellerId) && 
                transaction.BuyerId == transaction.SellerId)
            {
                result.AddError("Alýcý ve satýcý ayný kiþi olamaz");
            }

            // Satýþ fiyat kontrolü
            if (transaction.Type == ProductType.Satis)
            {
                if (transaction.Price <= 0)
                {
                    result.AddError("Satýþ fiyatý 0'dan büyük olmalý");
                }
                else if (transaction.Price > 999999)
                {
                    result.AddError("Fiyat çok yüksek (Maksimum: 999,999 TL)");
                }
            }

            // Takas kontrolü
            if (transaction.Type == ProductType.Takas)
            {
                if (string.IsNullOrWhiteSpace(transaction.OfferedProductId))
                {
                    result.AddError("Takas için teklif edilen ürün gerekli");
                }

                // Ek nakit kontrolü - Transaction modelinde AdditionalCashByRequester veya CounterCashByOwner kullanýlýyor
                if (transaction.AdditionalCashByRequester.HasValue && transaction.AdditionalCashByRequester < 0)
                {
                    result.AddError("Ek nakit negatif olamaz");
                }
            }

            Debug.WriteLine($"? Ýþlem doðrulama sonucu: {(result.IsValid ? "Geçerli" : "Geçersiz")}");
            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? ValidateTransaction hatasý: {ex.Message}");
            return ValidationResult.Failure($"Doðrulama hatasý: {ex.Message}");
        }
    }

    #endregion

    #region User Validation

    public ValidationResult ValidateUser(User user)
    {
        try
        {
            Debug.WriteLine($"?? Kullanýcý doðrulamasý baþlatýlýyor: {user?.Email}");
            
            var result = new ValidationResult();
            
            if (user == null)
            {
                result.AddError("Kullanýcý bilgisi boþ olamaz");
                return result;
            }

            // E-posta kontrolü
            if (string.IsNullOrWhiteSpace(user.Email))
            {
                result.AddError("E-posta gerekli");
            }
            else
            {
                var emailValidation = ValidateEmail(user.Email);
                if (!emailValidation.IsValid)
                {
                    result.AddError(emailValidation.GetErrorMessage());
                }
            }

            // Ad Soyad kontrolü
            if (string.IsNullOrWhiteSpace(user.FullName))
            {
                result.AddError("Ad Soyad gerekli");
            }
            else if (!InputSanitizer.IsValidName(user.FullName))
            {
                result.AddError("Geçersiz ad soyad formatý");
            }
            else if (user.FullName.Length < 2)
            {
                result.AddError("Ad Soyad en az 2 karakter olmalý");
            }
            else if (user.FullName.Length > 100)
            {
                result.AddError("Ad Soyad en fazla 100 karakter olabilir");
            }

            // Telefon kontrolü (opsiyonel)
            if (!string.IsNullOrWhiteSpace(user.PhoneNumber))
            {
                var phoneValidation = ValidatePhoneNumber(user.PhoneNumber);
                if (!phoneValidation.IsValid)
                {
                    result.AddError(phoneValidation.GetErrorMessage());
                }
            }

            Debug.WriteLine($"? Kullanýcý doðrulama sonucu: {(result.IsValid ? "Geçerli" : "Geçersiz")}");
            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? ValidateUser hatasý: {ex.Message}");
            return ValidationResult.Failure($"Doðrulama hatasý: {ex.Message}");
        }
    }

    #endregion

    #region Service Request Validation

    public ValidationResult ValidateServiceRequest(ServiceRequest request)
    {
        try
        {
            Debug.WriteLine($"?? Hizmet talebi doðrulamasý baþlatýlýyor: {request?.ServiceTitle}");
            
            var result = new ValidationResult();
            
            if (request == null)
            {
                result.AddError("Hizmet talebi boþ olamaz");
                return result;
            }

            // Baþlýk kontrolü
            if (string.IsNullOrWhiteSpace(request.ServiceTitle))
            {
                result.AddError("Hizmet baþlýðý gerekli");
            }
            else if (InputSanitizer.ContainsDangerousContent(request.ServiceTitle))
            {
                result.AddError("Hizmet baþlýðý geçersiz karakterler içeriyor");
            }

            // Fiyat kontrolü
            if (request.Price < 0)
            {
                result.AddError("Fiyat negatif olamaz");
            }
            else if (request.Price > 999999)
            {
                result.AddError("Fiyat çok yüksek");
            }

            // Saðlayýcý ve talep eden kontrolü
            if (string.IsNullOrWhiteSpace(request.ProviderId))
            {
                result.AddError("Hizmet saðlayýcý bilgisi gerekli");
            }

            if (string.IsNullOrWhiteSpace(request.RequesterId))
            {
                result.AddError("Talep eden bilgisi gerekli");
            }

            Debug.WriteLine($"? Hizmet talebi doðrulama sonucu: {(result.IsValid ? "Geçerli" : "Geçersiz")}");
            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? ValidateServiceRequest hatasý: {ex.Message}");
            return ValidationResult.Failure($"Doðrulama hatasý: {ex.Message}");
        }
    }

    public ValidationResult ValidateCustomerRequest(CustomerServiceRequest request)
    {
        try
        {
            Debug.WriteLine($"?? Müþteri hizmet talebi doðrulamasý baþlatýlýyor: {request?.Title}");
            
            var result = new ValidationResult();
            
            if (request == null)
            {
                result.AddError("Talep bilgisi boþ olamaz");
                return result;
            }

            // Baþlýk kontrolü
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                result.AddError("Talep baþlýðý gerekli");
            }
            else if (request.Title.Length < 5)
            {
                result.AddError("Baþlýk en az 5 karakter olmalý");
            }
            else if (request.Title.Length > 100)
            {
                result.AddError("Baþlýk en fazla 100 karakter olabilir");
            }

            // Açýklama kontrolü
            if (string.IsNullOrWhiteSpace(request.Description))
            {
                result.AddError("Açýklama gerekli");
            }
            else if (request.Description.Length < 20)
            {
                result.AddError("Açýklama en az 20 karakter olmalý");
            }
            else if (request.Description.Length > 2000)
            {
                result.AddError("Açýklama en fazla 2000 karakter olabilir");
            }

            // Bütçe kontrolü - BudgetMin ve BudgetMax nullable
            if (request.BudgetMin.HasValue && request.BudgetMin < 0)
            {
                result.AddError("Minimum bütçe negatif olamaz");
            }

            if (request.BudgetMax.HasValue && request.BudgetMin.HasValue && 
                request.BudgetMax < request.BudgetMin)
            {
                result.AddError("Maksimum bütçe minimumdan düþük olamaz");
            }

            // Konum kontrolü
            if (string.IsNullOrWhiteSpace(request.Location))
            {
                result.AddError("Konum bilgisi gerekli");
            }

            // Müþteri kontrolü
            if (string.IsNullOrWhiteSpace(request.CustomerId))
            {
                result.AddError("Müþteri bilgisi gerekli");
            }

            Debug.WriteLine($"? Müþteri talebi doðrulama sonucu: {(result.IsValid ? "Geçerli" : "Geçersiz")}");
            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? ValidateCustomerRequest hatasý: {ex.Message}");
            return ValidationResult.Failure($"Doðrulama hatasý: {ex.Message}");
        }
    }

    public ValidationResult ValidateProviderProposal(ProviderProposal proposal)
    {
        try
        {
            Debug.WriteLine($"?? Profesyonel teklif doðrulamasý baþlatýlýyor: {proposal?.ProposalId}");
            
            var result = new ValidationResult();
            
            if (proposal == null)
            {
                result.AddError("Teklif bilgisi boþ olamaz");
                return result;
            }

            // Fiyat kontrolü
            if (proposal.Price <= 0)
            {
                result.AddError("Teklif fiyatý 0'dan büyük olmalý");
            }
            else if (proposal.Price > 999999)
            {
                result.AddError("Teklif fiyatý çok yüksek");
            }

            // Mesaj kontrolü
            if (string.IsNullOrWhiteSpace(proposal.Message))
            {
                result.AddError("Teklif mesajý gerekli");
            }
            else if (proposal.Message.Length < 10)
            {
                result.AddError("Teklif mesajý en az 10 karakter olmalý");
            }
            else if (proposal.Message.Length > 1000)
            {
                result.AddError("Teklif mesajý en fazla 1000 karakter olabilir");
            }

            // Tahmini süre kontrolü - EstimatedDays kullanýlýyor
            if (proposal.EstimatedDays <= 0)
            {
                result.AddError("Tahmini süre 0'dan büyük olmalý");
            }
            else if (proposal.EstimatedDays > 365)
            {
                result.AddError("Tahmini süre çok uzun (maksimum 365 gün)");
            }

            // Profesyonel kontrolü
            if (string.IsNullOrWhiteSpace(proposal.ProviderId))
            {
                result.AddError("Profesyonel bilgisi gerekli");
            }

            // Talep kontrolü
            if (string.IsNullOrWhiteSpace(proposal.CustomerRequestId))
            {
                result.AddError("Müþteri talebi bilgisi gerekli");
            }

            Debug.WriteLine($"? Teklif doðrulama sonucu: {(result.IsValid ? "Geçerli" : "Geçersiz")}");
            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? ValidateProviderProposal hatasý: {ex.Message}");
            return ValidationResult.Failure($"Doðrulama hatasý: {ex.Message}");
        }
    }

    #endregion

    #region Message Validation

    public ValidationResult ValidateMessage(SendMessageRequest message)
    {
        try
        {
            Debug.WriteLine($"?? Mesaj doðrulamasý baþlatýlýyor");
            
            var result = new ValidationResult();
            
            if (message == null)
            {
                result.AddError("Mesaj bilgisi boþ olamaz");
                return result;
            }

            // Alýcý kontrolü
            if (string.IsNullOrWhiteSpace(message.ReceiverId))
            {
                result.AddError("Alýcý bilgisi gerekli");
            }

            // Ýçerik kontrolü
            if (string.IsNullOrWhiteSpace(message.Content))
            {
                result.AddError("Mesaj içeriði boþ olamaz");
            }
            else
            {
                // Tehlikeli içerik kontrolü
                if (InputSanitizer.ContainsDangerousContent(message.Content))
                {
                    result.AddError("Mesaj güvenlik açýsýndan geçersiz içerik barýndýrýyor");
                }

                // Uzunluk kontrolü
                if (message.Content.Length > 5000)
                {
                    result.AddError("Mesaj çok uzun (maksimum 5000 karakter)");
                }
            }

            // Görsel mesaj kontrolü
            if (message.Type == MessageType.Image)
            {
                if (string.IsNullOrWhiteSpace(message.ImageUrl))
                {
                    result.AddError("Görsel mesaj için görsel URL'si gerekli");
                }
            }

            Debug.WriteLine($"? Mesaj doðrulama sonucu: {(result.IsValid ? "Geçerli" : "Geçersiz")}");
            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? ValidateMessage hatasý: {ex.Message}");
            return ValidationResult.Failure($"Doðrulama hatasý: {ex.Message}");
        }
    }

    #endregion

    #region Negotiation Validation

    public ValidationResult ValidateNegotiation(decimal proposedPrice, decimal originalPrice, int roundCount, DateTime? startedAt)
    {
        try
        {
            Debug.WriteLine($"?? Pazarlýk doðrulamasý baþlatýlýyor: {proposedPrice} TL");
            
            // Delegate to NegotiationRules helper
            var priceValidation = NegotiationRules.ValidateProposedPrice(proposedPrice, originalPrice);
            if (!priceValidation.IsValid)
            {
                return priceValidation;
            }

            var continueValidation = NegotiationRules.CanContinueNegotiation(roundCount, startedAt);
            if (!continueValidation.IsValid)
            {
                return continueValidation;
            }

            Debug.WriteLine($"? Pazarlýk doðrulama sonucu: Geçerli");
            return ValidationResult.Success("Pazarlýk geçerli");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? ValidateNegotiation hatasý: {ex.Message}");
            return ValidationResult.Failure($"Doðrulama hatasý: {ex.Message}");
        }
    }

    #endregion

    #region Basic Field Validations

    public ValidationResult ValidateEmail(string email)
    {
        try
        {
            var result = new ValidationResult();

            if (string.IsNullOrWhiteSpace(email))
            {
                result.AddError("E-posta adresi boþ olamaz");
                return result;
            }

            if (!InputSanitizer.IsValidEmail(email))
            {
                result.AddError("Geçersiz e-posta formatý");
            }

            if (email.Length > 254)
            {
                result.AddError("E-posta adresi çok uzun");
            }

            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? ValidateEmail hatasý: {ex.Message}");
            return ValidationResult.Failure($"Doðrulama hatasý: {ex.Message}");
        }
    }

    public ValidationResult ValidatePassword(string password)
    {
        try
        {
            var result = new ValidationResult();

            if (string.IsNullOrWhiteSpace(password))
            {
                result.AddError("Þifre boþ olamaz");
                return result;
            }

            if (password.Length < 6)
            {
                result.AddError("Þifre en az 6 karakter olmalý");
            }

            if (password.Length > 128)
            {
                result.AddError("Þifre çok uzun (maksimum 128 karakter)");
            }

            // Þifre güvenlik kontrolü
            bool hasUpperCase = password.Any(char.IsUpper);
            bool hasLowerCase = password.Any(char.IsLower);
            bool hasDigit = password.Any(char.IsDigit);

            if (!hasUpperCase || !hasLowerCase || !hasDigit)
            {
                result.AddError("Þifre en az bir büyük harf, bir küçük harf ve bir rakam içermelidir");
            }

            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? ValidatePassword hatasý: {ex.Message}");
            return ValidationResult.Failure($"Doðrulama hatasý: {ex.Message}");
        }
    }

    public ValidationResult ValidatePhoneNumber(string phoneNumber)
    {
        try
        {
            var result = new ValidationResult();

            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                result.AddError("Telefon numarasý boþ olamaz");
                return result;
            }

            if (!InputSanitizer.IsValidPhoneNumber(phoneNumber))
            {
                result.AddError("Geçersiz telefon numarasý formatý");
            }

            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? ValidatePhoneNumber hatasý: {ex.Message}");
            return ValidationResult.Failure($"Doðrulama hatasý: {ex.Message}");
        }
    }

    #endregion
}
