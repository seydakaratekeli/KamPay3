using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Helpers;
using KamPay.Models;
using System.Diagnostics;

namespace KamPay.Services;

/// <summary>
/// ?? ARMUT MODELÝ: Müþteri talep yönetimi implementasyonu
/// ? Single Responsibility: Sadece müþteri hizmet taleplerinin CRUD iþlemleri
/// ? Dependency Inversion: FirebaseClient ve INotificationService arayüzlerine baðýmlý
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
            // Talep numarasý oluþtur
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

            Debug.WriteLine($"? Müþteri talebi oluþturuldu: {request.RequestId}");
            return ServiceResult<CustomerServiceRequest>.SuccessResult(request, "Talep baþarýyla oluþturuldu!");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? CreateCustomerRequestAsync hatasý: {ex.Message}");
            return ServiceResult<CustomerServiceRequest>.FailureResult("Talep oluþturulamadý", ex.Message);
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
            Debug.WriteLine($"? GetCustomerRequestsAsync hatasý: {ex.Message}");
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
                // Kategori filtresi varsa: EqualTo kullan (sunucu tarafý)
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

                // Ýstemci tarafý sayfalama
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
                // Kategori filtresi yok: Sunucu tarafý sayfalama
                if (!string.IsNullOrEmpty(lastKey))
                {
                    items = await _firebaseClient
                        .Child(Constants.CustomerServiceRequestsCollection)
                        .OrderBy("CreatedAt")
                        .StartAt(lastKey)
                        .LimitToFirst(pageSize + 1)
                        .OnceAsync<CustomerServiceRequest>();
                }
                else
                {
                    items = await _firebaseClient
                        .Child(Constants.CustomerServiceRequestsCollection)
                        .OrderBy("CreatedAt")
                        .LimitToFirst(pageSize)
                        .OnceAsync<CustomerServiceRequest>();
                }

                var requests = items.Select(r => {
                    var req = r.Object;
                    req.RequestId = r.Key;
                    return req;
                }).ToList();

                // lastKey'i atla
                if (!string.IsNullOrEmpty(lastKey) && requests.Any() && requests.First().RequestId == lastKey)
                {
                    requests.RemoveAt(0);
                }

                // Ýstemci tarafý hafif filtreleme
                requests = requests
                    .Where(r => r.IsActive && r.Status == CustomerRequestStatus.Open)
                    .OrderByDescending(r => r.CreatedAt)
                    .ToList();

                return ServiceResult<List<CustomerServiceRequest>>.SuccessResult(requests);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? GetCustomerRequestsPagedAsync hatasý: {ex.Message}");
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
                return ServiceResult<CustomerServiceRequest>.FailureResult("Talep bulunamadý");
            }

            request.RequestId = requestId;
            return ServiceResult<CustomerServiceRequest>.SuccessResult(request);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? GetCustomerRequestByIdAsync hatasý: {ex.Message}");
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

            Debug.WriteLine($"?? {requests.Count} talep getirildi (Müþteri: {customerId})");
            return ServiceResult<List<CustomerServiceRequest>>.SuccessResult(requests);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? GetMyCustomerRequestsAsync hatasý: {ex.Message}");
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
            Debug.WriteLine($"? UpdateCustomerRequestAsync hatasý: {ex.Message}");
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
                return ServiceResult<bool>.FailureResult("Talep bulunamadý");
            }

            if (request.CustomerId != customerId)
            {
                return ServiceResult<bool>.FailureResult("Bu iþlemi yapmaya yetkiniz yok");
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
            Debug.WriteLine($"? CancelCustomerRequestAsync hatasý: {ex.Message}");
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
                return ServiceResult<bool>.FailureResult("Talep bulunamadý");
            }

            request.ProposalCount++;
            request.UpdatedAt = DateTime.UtcNow;
            await requestNode.PutAsync(request);

            return ServiceResult<bool>.SuccessResult(true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? IncrementProposalCount hatasý: {ex.Message}");
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
                return ServiceResult<bool>.FailureResult("Talep bulunamadý");
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
            Debug.WriteLine($"? UpdateRequestStatus hatasý: {ex.Message}");
            return ServiceResult<bool>.FailureResult("Durum güncellenemedi", ex.Message);
        }
    }
}
