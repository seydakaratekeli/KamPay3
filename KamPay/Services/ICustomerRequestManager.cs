using KamPay.Models;

namespace KamPay.Services;

/// <summary>
/// ?? ARMUT MODELÝ: Müþteri talep yönetimi
/// Single Responsibility: Sadece müþteri hizmet taleplerinin CRUD iþlemleri
/// </summary>
public interface ICustomerRequestManager
{
    /// <summary>
    /// Müþteri yeni bir hizmet talebi oluþturur
    /// </summary>
    Task<ServiceResult<CustomerServiceRequest>> CreateCustomerRequestAsync(CustomerServiceRequest request);

    /// <summary>
    /// Tüm aktif müþteri taleplerini getirir (Profesyonellerin göreceði liste)
    /// </summary>
    Task<ServiceResult<List<CustomerServiceRequest>>> GetCustomerRequestsAsync(
        ServiceCategory? category = null, 
        string? location = null);

    /// <summary>
    /// Sayfalama ile müþteri taleplerini getirir (Performans optimizasyonu)
    /// </summary>
    Task<ServiceResult<List<CustomerServiceRequest>>> GetCustomerRequestsPagedAsync(
        int pageSize = 20, 
        string? lastKey = null, 
        ServiceCategory? category = null);

    /// <summary>
    /// Belirli bir müþteri talebini ID ile getirir
    /// </summary>
    Task<ServiceResult<CustomerServiceRequest>> GetCustomerRequestByIdAsync(string requestId);

    /// <summary>
    /// Müþterinin kendi oluþturduðu talepleri getirir
    /// </summary>
    Task<ServiceResult<List<CustomerServiceRequest>>> GetMyCustomerRequestsAsync(string customerId);

    /// <summary>
    /// Müþteri talebini günceller
    /// </summary>
    Task<ServiceResult<bool>> UpdateCustomerRequestAsync(CustomerServiceRequest request);

    /// <summary>
    /// Müþteri talebini iptal eder
    /// </summary>
    Task<ServiceResult<bool>> CancelCustomerRequestAsync(string requestId, string customerId);

    /// <summary>
    /// Talepteki teklif sayýsýný artýrýr (Internal use)
    /// </summary>
    Task<ServiceResult<bool>> IncrementProposalCountAsync(string requestId);

    /// <summary>
    /// Talebin durumunu günceller (Internal use)
    /// </summary>
    Task<ServiceResult<bool>> UpdateRequestStatusAsync(
        string requestId, 
        CustomerRequestStatus newStatus, 
        string? selectedProposalId = null);
}
