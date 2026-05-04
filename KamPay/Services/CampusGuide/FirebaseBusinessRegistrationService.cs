using Firebase.Auth;
using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Helpers;
using KamPay.Models;
using AppUser = KamPay.Models.User;

namespace KamPay.Services.CampusGuide;

public class FirebaseBusinessRegistrationService : IBusinessRegistrationService
{
    private readonly FirebaseAuthProvider _authProvider;
    private readonly FirebaseClient _firebaseClient;

    public FirebaseBusinessRegistrationService(FirebaseAuthProvider authProvider, FirebaseClient firebaseClient)
    {
        _authProvider = authProvider ?? throw new ArgumentNullException(nameof(authProvider));
        _firebaseClient = firebaseClient ?? throw new ArgumentNullException(nameof(firebaseClient));
    }

    public ValidationResult ValidateBusinessRegistration(BusinessRegistrationRequest request)
    {
        var result = new ValidationResult();

        if (string.IsNullOrWhiteSpace(request.OwnerFirstName))
            result.AddError("Yetkili adı boş bırakılamaz.");
        if (string.IsNullOrWhiteSpace(request.OwnerLastName))
            result.AddError("Yetkili soyadı boş bırakılamaz.");
        if (string.IsNullOrWhiteSpace(request.OwnerEmail) || !InputSanitizer.IsValidEmail(request.OwnerEmail))
            result.AddError("Geçerli bir işletme e-postası girin.");
        if (request.OwnerEmail.EndsWith(Constants.UniversityEmailDomain, StringComparison.OrdinalIgnoreCase))
            result.AddError("İşletme başvurusu için kurumsal/işletme e-postası kullanın.");
        if (string.IsNullOrWhiteSpace(request.OwnerPhoneNumber))
            result.AddError("Yetkili telefon numarası zorunludur.");
        if (string.IsNullOrWhiteSpace(request.BusinessName))
            result.AddError("İşletme adı boş bırakılamaz.");
        if (string.IsNullOrWhiteSpace(request.Description))
            result.AddError("İşletme açıklaması boş bırakılamaz.");
        if (string.IsNullOrWhiteSpace(request.Location))
            result.AddError("Kampüs içi konum bilgisi zorunludur.");
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < Constants.MinPasswordLength)
            result.AddError($"Şifre en az {Constants.MinPasswordLength} karakter olmalıdır.");
        if (request.Password != request.PasswordConfirm)
            result.AddError("Şifreler eşleşmiyor.");

        return result;
    }

    public async Task<ServiceResult<MicroBusiness>> RegisterBusinessAsync(BusinessRegistrationRequest request)
    {
        try
        {
            var validation = ValidateBusinessRegistration(request);
            if (!validation.IsValid)
                return ServiceResult<MicroBusiness>.FailureResult("Başvuru bilgileri geçersiz.", validation.Errors.ToArray());

            var email = request.OwnerEmail.Trim().ToLowerInvariant();
            var ownerName = $"{request.OwnerFirstName.Trim()} {request.OwnerLastName.Trim()}".Trim();
            var authResult = await _authProvider.CreateUserWithEmailAndPasswordAsync(email, request.Password, ownerName);

            var businessId = Guid.NewGuid().ToString();
            var user = new AppUser
            {
                UserId = authResult.User.LocalId,
                FirstName = InputSanitizer.SanitizeName(request.OwnerFirstName.Trim()),
                LastName = InputSanitizer.SanitizeName(request.OwnerLastName.Trim()),
                Email = email,
                Username = request.BusinessName.Trim(),
                PhoneNumber = request.OwnerPhoneNumber.Trim(),
                PasswordHash = string.Empty,
                IsEmailVerified = false,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                Role = UserRole.BusinessOwner,
                BusinessId = businessId
            };

            var business = new MicroBusiness
            {
                BusinessId = businessId,
                OwnerUserId = user.UserId,
                OwnerEmail = user.Email,
                OwnerFullName = user.FullName,
                OwnerPhoneNumber = user.PhoneNumber,
                Name = request.BusinessName.Trim(),
                Description = request.Description.Trim(),
                Category = request.Category,
                Location = request.Location.Trim(),
                InstagramUrl = request.InstagramUrl.Trim(),
                WebsiteUrl = request.WebsiteUrl.Trim(),
                IsActive = true,
                IsVerified = false,
                VerificationStatus = BusinessVerificationStatus.PendingReview,
                SubmittedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            await _firebaseClient.Child(Constants.UsersCollection).Child(user.UserId).PutAsync(user);
            await _firebaseClient.Child(Constants.MicroBusinessesCollection).Child(business.BusinessId).PutAsync(business);
            await _authProvider.SendEmailVerificationAsync(authResult.FirebaseToken);

            return ServiceResult<MicroBusiness>.SuccessResult(
                business,
                "İşletme başvurunuz alındı. E-postanızı doğruladıktan sonra admin onayı bekleyebilirsiniz.");
        }
        catch (FirebaseAuthException ex)
        {
            return ServiceResult<MicroBusiness>.FailureResult("İşletme hesabı oluşturulamadı.", ex.Reason.ToString());
        }
        catch (Exception ex)
        {
            return ServiceResult<MicroBusiness>.FailureResult("İşletme başvurusu oluşturulamadı.", ex.Message);
        }
    }
}
