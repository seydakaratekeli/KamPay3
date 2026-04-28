using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Helpers;
using KamPay.Models;
using System.Diagnostics;

namespace KamPay.Services;

/// <summary>
/// ?? ARMUT MODELİ: Müşteri talep yönetimi implementasyonu
/// ? Single Responsibility: Sadece müşteri hizmet taleplerinin CRUD işlemleri
/// ? Dependency Inversion: FirebaseClient ve INotificationService arayüzlerine bağımlı
/// </summary>
public class CustomerRequestManager : ICustomerRequestManager
{
    private readonly FirebaseClient _firebaseClient;
    private readonly INotificationService _notificationService;

    public CustomerRequestManager(
        FirebaseClient firebaseClient,
        INotificationService notificationService)
    {
        _firebaseClient = firebaseClient ?? throw new ArgumentNullException(nameof(firebaseClient));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
    }

    public async Task<ServiceResult<CustomerServiceRequest>> CreateCustomerRequestAsync(CustomerServiceRequest request)
    {
        try
        {
            // Talep numarası oluştur
            if (string.IsNullOrWhiteSpace(request.RequestNumber))
            {
                request.RequestNumber = $"CSR{DateTime.UtcNow:yyyyMMddHHmmss}";
            }

            request.CreatedAt = DateTime.UtcNow;
            request.UpdatedAt = DateTime.UtcNow;
            request.Status = CustomerRequestStatus.Open;
            request.IsActive = true;

            await _firebaseClient
                .Child(Constants.CustomerServiceRequestsCollection)
                .Child(request.RequestId)
                .PutAsync(request);

            Debug.WriteLine($"? Müşteri talebi oluşturuldu: {request.RequestId}");
            return ServiceResult<CustomerServiceRequest>.SuccessResult(request, "Talep başarıyla oluşturuldu!");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? CreateCustomerRequestAsync hatası: {ex.Message}");
            return ServiceResult<CustomerServiceRequest>.FailureResult("Talep oluşturulamadı", ex.Message);
        }
    }

    public async Task<ServiceResult<List<CustomerServiceRequest>>> GetCustomerRequestsAsync(
        ServiceCategory? category = null,
        string? location = null)
    {
        try
        {
            var allRequests = await _firebaseClient
                .Child(Constants.CustomerServiceRequestsCollection)
                .OnceAsync<CustomerServiceRequest>();

            var requests = allRequests
                .Select(r => {
                    var req = r.Object;
                    req.RequestId = r.Key;
                    return req;
                })
                .Where(r => r.IsActive && r.Status == CustomerRequestStatus.Open)
                .ToList();

            // Kategori filtresi
            if (category.HasValue)
            {
                requests = requests.Where(r => r.Category == category.Value).ToList();
            }

            // Konum filtresi (basit string match)
            if (!string.IsNullOrWhiteSpace(location))
            {
                requests = requests.Where(r => 
                    r.Location.Contains(location, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            requests = requests.OrderByDescending(r => r.CreatedAt).ToList();

            Debug.WriteLine($"?? {requests.Count} aktif talep getirildi");
            return ServiceResult<List<CustomerServiceRequest>>.SuccessResult(requests);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? GetCustomerRequestsAsync hatası: {ex.Message}");
            return ServiceResult<List<CustomerServiceRequest>>.FailureResult("Talepler getirilemedi", ex.Message);
        }
    }

    public async Task<ServiceResult<List<CustomerServiceRequest>>> GetCustomerRequestsPagedAsync(
        int pageSize = 20,
        string? lastKey = null,
        ServiceCategory? category = null)
    {
        try
        {
            IEnumerable<Firebase.Database.FirebaseObject<CustomerServiceRequest>> items;

            if (category.HasValue)
            {
                // Kategori filtresi varsa: EqualTo kullan (sunucu tarafı)
                items = await _firebaseClient
                    .Child(Constants.CustomerServiceRequestsCollection)
                    .OrderBy("Category")
                    .EqualTo((int)category.Value)
                    .OnceAsync<CustomerServiceRequest>();

                var allRequests = items
                    .Select(r => {
                        var req = r.Object;
                        req.RequestId = r.Key;
                        return req;
                    })
                    .Where(r => r.IsActive && r.Status == CustomerRequestStatus.Open)
                    .OrderByDescending(r => r.CreatedAt)
                    .ToList();

                // İstemci tarafı sayfalama
                if (!string.IsNullOrEmpty(lastKey))
                {
                    var lastIndex = allRequests.FindIndex(r => r.RequestId == lastKey);
                    if (lastIndex >= 0)
                    {
                        allRequests = allRequests.Skip(lastIndex + 1).Take(pageSize).ToList();
                    }
                }
                else
                {
                    allRequests = allRequests.Take(pageSize).ToList();
                }

                return ServiceResult<List<CustomerServiceRequest>>.SuccessResult(allRequests);
            }
            else
            {
                // Kategori filtresi yok: Sunucu tarafı hatalı pagination yerine güvenli istemci pagination
                items = await _firebaseClient
                    .Child(Constants.CustomerServiceRequestsCollection)
                    .OnceAsync<CustomerServiceRequest>();

                var allRequests = items
                    .Select(r => {
                        var req = r.Object;
                        req.RequestId = r.Key;
                        return req;
                    })
                    .Where(r => r.IsActive && r.Status == CustomerRequestStatus.Open)
                    .OrderByDescending(r => r.CreatedAt)
                    .ToList();

                if (!string.IsNullOrEmpty(lastKey))
                {
                    var lastIndex = allRequests.FindIndex(r => r.RequestId == lastKey);
                    if (lastIndex >= 0)
                    {
                        allRequests = allRequests.Skip(lastIndex + 1).Take(pageSize).ToList();
                    }
                }
                else
                {
                    allRequests = allRequests.Take(pageSize).ToList();
                }

                return ServiceResult<List<CustomerServiceRequest>>.SuccessResult(allRequests);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? GetCustomerRequestsPagedAsync hatası: {ex.Message}");
            return ServiceResult<List<CustomerServiceRequest>>.FailureResult("Talepler yüklenemedi", ex.Message);
        }
    }

    public async Task<ServiceResult<CustomerServiceRequest>> GetCustomerRequestByIdAsync(string requestId)
    {
        try
        {
            var request = await _firebaseClient
                .Child(Constants.CustomerServiceRequestsCollection)
                .Child(requestId)
                .OnceSingleAsync<CustomerServiceRequest>();

            if (request == null)
            {
                return ServiceResult<CustomerServiceRequest>.FailureResult("Talep bulunamadı");
            }

            request.RequestId = requestId;
            return ServiceResult<CustomerServiceRequest>.SuccessResult(request);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? GetCustomerRequestByIdAsync hatası: {ex.Message}");
            return ServiceResult<CustomerServiceRequest>.FailureResult("Talep getirilemedi", ex.Message);
        }
    }

    public async Task<ServiceResult<List<CustomerServiceRequest>>> GetMyCustomerRequestsAsync(string customerId)
    {
        try
        {
            var allRequests = await _firebaseClient
                .Child(Constants.CustomerServiceRequestsCollection)
                .OrderBy("CustomerId")
                .EqualTo(customerId)
                .OnceAsync<CustomerServiceRequest>();

            var requests = allRequests
                .Select(r => {
                    var req = r.Object;
                    req.RequestId = r.Key;
                    return req;
                })
                .OrderByDescending(r => r.CreatedAt)
                .ToList();

            Debug.WriteLine($"?? {requests.Count} talep getirildi (Müşteri: {customerId})");
            return ServiceResult<List<CustomerServiceRequest>>.SuccessResult(requests);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? GetMyCustomerRequestsAsync hatası: {ex.Message}");
            return ServiceResult<List<CustomerServiceRequest>>.FailureResult("Talepler getirilemedi", ex.Message);
        }
    }

    public async Task<ServiceResult<bool>> UpdateCustomerRequestAsync(CustomerServiceRequest request)
    {
        try
        {
            request.UpdatedAt = DateTime.UtcNow;

            await _firebaseClient
                .Child(Constants.CustomerServiceRequestsCollection)
                .Child(request.RequestId)
                .PutAsync(request);

            Debug.WriteLine($"? Talep güncellendi: {request.RequestId}");
            return ServiceResult<bool>.SuccessResult(true, "Talep güncellendi");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? UpdateCustomerRequestAsync hatası: {ex.Message}");
            return ServiceResult<bool>.FailureResult("Talep güncellenemedi", ex.Message);
        }
    }

    public async Task<ServiceResult<bool>> CancelCustomerRequestAsync(string requestId, string customerId)
    {
        try
        {
            var requestNode = _firebaseClient
                .Child(Constants.CustomerServiceRequestsCollection)
                .Child(requestId);

            var request = await requestNode.OnceSingleAsync<CustomerServiceRequest>();

            if (request == null)
            {
                return ServiceResult<bool>.FailureResult("Talep bulunamadı");
            }

            if (request.CustomerId != customerId)
            {
                return ServiceResult<bool>.FailureResult("Bu işlemi yapmaya yetkiniz yok");
            }

            request.Status = CustomerRequestStatus.Cancelled;
            request.IsActive = false;
            request.UpdatedAt = DateTime.UtcNow;

            await requestNode.PutAsync(request);

            Debug.WriteLine($"? Talep iptal edildi: {requestId}");
            return ServiceResult<bool>.SuccessResult(true, "Talep iptal edildi");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? CancelCustomerRequestAsync hatası: {ex.Message}");
            return ServiceResult<bool>.FailureResult("Talep iptal edilemedi", ex.Message);
        }
    }

    public async Task<ServiceResult<bool>> IncrementProposalCountAsync(string requestId)
    {
        try
        {
            var requestNode = _firebaseClient
                .Child(Constants.CustomerServiceRequestsCollection)
                .Child(requestId);

            var request = await requestNode.OnceSingleAsync<CustomerServiceRequest>();
            if (request == null)
            {
                return ServiceResult<bool>.FailureResult("Talep bulunamadı");
            }

            request.ProposalCount++;
            request.UpdatedAt = DateTime.UtcNow;
            await requestNode.PutAsync(request);

            return ServiceResult<bool>.SuccessResult(true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? IncrementProposalCount hatası: {ex.Message}");
            return ServiceResult<bool>.FailureResult("Sayaç güncellenemedi", ex.Message);
        }
    }

    public async Task<ServiceResult<bool>> UpdateRequestStatusAsync(
        string requestId,
        CustomerRequestStatus newStatus,
        string? selectedProposalId = null)
    {
        try
        {
            var requestNode = _firebaseClient
                .Child(Constants.CustomerServiceRequestsCollection)
                .Child(requestId);

            var request = await requestNode.OnceSingleAsync<CustomerServiceRequest>();
            if (request == null)
            {
                return ServiceResult<bool>.FailureResult("Talep bulunamadı");
            }

            request.Status = newStatus;
            request.UpdatedAt = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(selectedProposalId))
            {
                request.SelectedProposalId = selectedProposalId;
            }

            await requestNode.PutAsync(request);

            return ServiceResult<bool>.SuccessResult(true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? UpdateRequestStatus hatası: {ex.Message}");
            return ServiceResult<bool>.FailureResult("Durum güncellenemedi", ex.Message);
        }
    }
}
