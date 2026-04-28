using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Helpers;
using KamPay.Models;
using KamPay.Services.Messaging;
using System.Diagnostics;

namespace KamPay.Services;

/// <summary>
/// ?? ARMUT MODELİ: Profesyonel teklif yönetimi implementasyonu
/// ? Single Responsibility: Sadece profesyonellerin tekliflerinin CRUD işlemleri
/// ? Dependency Inversion: FirebaseClient ve diğer servislere bağımlı
/// </summary>
public class ProviderProposalManager : IProviderProposalManager
{
    private readonly FirebaseClient _firebaseClient;
    private readonly INotificationService _notificationService;
    private readonly ICustomerRequestManager _customerRequestManager;
    private readonly IMessagingService _messagingService;

    public ProviderProposalManager(
        FirebaseClient firebaseClient,
        INotificationService notificationService,
        ICustomerRequestManager customerRequestManager,
        IMessagingService messagingService)
    {
        _firebaseClient = firebaseClient ?? throw new ArgumentNullException(nameof(firebaseClient));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _customerRequestManager = customerRequestManager ?? throw new ArgumentNullException(nameof(customerRequestManager));
        _messagingService = messagingService ?? throw new ArgumentNullException(nameof(messagingService));
    }

    public async Task<ServiceResult<ProviderProposal>> SendProposalAsync(ProviderProposal proposal)
    {
        try
        {
            proposal.ProposalId = Guid.NewGuid().ToString();
            proposal.CreatedAt = DateTime.UtcNow;
            proposal.UpdatedAt = DateTime.UtcNow;
            proposal.Status = ProposalStatus.Pending;

            if (!proposal.ExpiresAt.HasValue)
            {
                proposal.ExpiresAt = DateTime.UtcNow.AddDays(7);
            }

            // Teklifi kaydet
            await _firebaseClient
                .Child(Constants.ProviderProposalsCollection)
                .Child(proposal.ProposalId)
                .PutAsync(proposal);

            // Talepteki teklif sayısını artır
            await _customerRequestManager.IncrementProposalCountAsync(proposal.CustomerRequestId);

            // Müşteriye bildirim gönder
            var customerRequest = await _customerRequestManager.GetCustomerRequestByIdAsync(proposal.CustomerRequestId);
            if (customerRequest.Success && customerRequest.Data != null)
            {
                var notification = new Notification
                {
                    UserId = customerRequest.Data.CustomerId,
                    Type = NotificationType.NewOffer,
                    Title = "Yeni Teklif",
                    Message = $"{proposal.ProviderName} talebinize {proposal.Price:C} teklif gönderdi",
                    ActionUrl = $"CustomerRequestDetailsPage?requestId={proposal.CustomerRequestId}",
                    CreatedAt = DateTime.UtcNow
                };

                await _notificationService.CreateNotificationAsync(notification);
            }

            Debug.WriteLine($"? Teklif gönderildi: {proposal.ProposalId}");
            return ServiceResult<ProviderProposal>.SuccessResult(proposal, "Teklif başarıyla gönderildi!");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? SendProposalAsync hatası: {ex.Message}");
            return ServiceResult<ProviderProposal>.FailureResult("Teklif gönderilemedi", ex.Message);
        }
    }

    public async Task<ServiceResult<List<ProviderProposal>>> GetProposalsForRequestAsync(string customerRequestId)
    {
        try
        {
            var allProposals = await _firebaseClient
                .Child(Constants.ProviderProposalsCollection)
                .OrderBy("CustomerRequestId")
                .EqualTo(customerRequestId)
                .OnceAsync<ProviderProposal>();

            var proposals = allProposals
                .Select(p => {
                    var proposal = p.Object;
                    proposal.ProposalId = p.Key;
                    return proposal;
                })
                .OrderByDescending(p => p.CreatedAt)
                .ToList();

            Debug.WriteLine($"?? {proposals.Count} teklif getirildi (Talep: {customerRequestId})");
            return ServiceResult<List<ProviderProposal>>.SuccessResult(proposals);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? GetProposalsForRequestAsync hatası: {ex.Message}");
            return ServiceResult<List<ProviderProposal>>.FailureResult("Teklifler getirilemedi", ex.Message);
        }
    }

    public async Task<ServiceResult<List<ProviderProposal>>> GetMyProposalsAsync(string providerId)
    {
        try
        {
            var allProposals = await _firebaseClient
                .Child(Constants.ProviderProposalsCollection)
                .OrderBy("ProviderId")
                .EqualTo(providerId)
                .OnceAsync<ProviderProposal>();

            var proposals = allProposals
                .Select(p => {
                    var proposal = p.Object;
                    proposal.ProposalId = p.Key;
                    return proposal;
                })
                .OrderByDescending(p => p.CreatedAt)
                .ToList();

            Debug.WriteLine($"?? {proposals.Count} teklif getirildi (Profesyonel: {providerId})");
            return ServiceResult<List<ProviderProposal>>.SuccessResult(proposals);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? GetMyProposalsAsync hatası: {ex.Message}");
            return ServiceResult<List<ProviderProposal>>.FailureResult("Teklifler getirilemedi", ex.Message);
        }
    }

    public async Task<ServiceResult<ProviderProposal>> GetProposalByIdAsync(string proposalId)
    {
        try
        {
            var proposal = await _firebaseClient
                .Child(Constants.ProviderProposalsCollection)
                .Child(proposalId)
                .OnceSingleAsync<ProviderProposal>();

            if (proposal == null)
            {
                return ServiceResult<ProviderProposal>.FailureResult("Teklif bulunamadı");
            }

            proposal.ProposalId = proposalId;
            return ServiceResult<ProviderProposal>.SuccessResult(proposal);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? GetProposalByIdAsync hatası: {ex.Message}");
            return ServiceResult<ProviderProposal>.FailureResult("Teklif getirilemedi", ex.Message);
        }
    }

    public async Task<ServiceResult<bool>> AcceptProposalAsync(string proposalId, string customerId)
    {
        try
        {
            var proposalNode = _firebaseClient
                .Child(Constants.ProviderProposalsCollection)
                .Child(proposalId);

            var proposal = await proposalNode.OnceSingleAsync<ProviderProposal>();
            if (proposal == null)
            {
                return ServiceResult<bool>.FailureResult("Teklif bulunamadı");
            }

            if (proposal.Status != ProposalStatus.Pending)
            {
                return ServiceResult<bool>.FailureResult("Sadece bekleyen teklifleri kabul edebilirsiniz.");
            }

            var requestResult = await _customerRequestManager.GetCustomerRequestByIdAsync(proposal.CustomerRequestId);
            if (!requestResult.Success || requestResult.Data == null)
            {
                return ServiceResult<bool>.FailureResult("Bağlı talep bulunamadı.");
            }

            if (requestResult.Data.CustomerId != customerId)
            {
                return ServiceResult<bool>.FailureResult("Bu işlemi yapmaya yetkiniz yok");
            }

            if (requestResult.Data.Status == CustomerRequestStatus.ProviderSelected ||
                requestResult.Data.Status == CustomerRequestStatus.InProgress ||
                requestResult.Data.Status == CustomerRequestStatus.Completed ||
                requestResult.Data.Status == CustomerRequestStatus.Cancelled)
            {
                return ServiceResult<bool>.FailureResult("Bu talep için zaten bir teklif kabul edilmiş veya işlem sonlanmış.");
            }

            proposal.Status = ProposalStatus.Accepted;
            proposal.RespondedAt = DateTime.UtcNow;
            proposal.UpdatedAt = DateTime.UtcNow;
            await proposalNode.PutAsync(proposal);

            await _customerRequestManager.UpdateRequestStatusAsync(
                proposal.CustomerRequestId,
                CustomerRequestStatus.ProviderSelected,
                proposalId);

            await RejectOtherProposalsAsync(proposal.CustomerRequestId, proposalId);

            var notification = new Notification
            {
                UserId = proposal.ProviderId,
                Type = NotificationType.OfferAccepted,
                Title = "Teklif Kabul Edildi",
                Message = $"Teklifiniz kabul edildi! Müşteri ile iletişime geçebilirsiniz.",
                ActionUrl = $"ProposalDetailsPage?proposalId={proposalId}",
                CreatedAt = DateTime.UtcNow
            };
            await _notificationService.CreateNotificationAsync(notification);

            Debug.WriteLine($"? Teklif kabul edildi: {proposalId}");
            return ServiceResult<bool>.SuccessResult(true, "Teklif kabul edildi");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? AcceptProposalAsync hatası: {ex.Message}");
            return ServiceResult<bool>.FailureResult("Teklif kabul edilemedi", ex.Message);
        }
    }

    public async Task<ServiceResult<bool>> RejectProposalAsync(string proposalId, string customerId, string? reason = null)
    {
        try
        {
            var proposalNode = _firebaseClient
                .Child(Constants.ProviderProposalsCollection)
                .Child(proposalId);

            var proposal = await proposalNode.OnceSingleAsync<ProviderProposal>();
            if (proposal == null)
            {
                return ServiceResult<bool>.FailureResult("Teklif bulunamadı");
            }

            var requestResult = await _customerRequestManager.GetCustomerRequestByIdAsync(proposal.CustomerRequestId);
            if (!requestResult.Success || requestResult.Data?.CustomerId != customerId)
            {
                return ServiceResult<bool>.FailureResult("Bu işlemi yapmaya yetkiniz yok");
            }

            proposal.Status = ProposalStatus.Rejected;
            proposal.RejectionReason = reason ?? "Müşteri tarafından reddedildi";
            proposal.RespondedAt = DateTime.UtcNow;
            proposal.UpdatedAt = DateTime.UtcNow;
            await proposalNode.PutAsync(proposal);

            var notification = new Notification
            {
                UserId = proposal.ProviderId,
                Type = NotificationType.OfferRejected,
                Title = "Teklif Reddedildi",
                Message = string.IsNullOrEmpty(reason) 
                    ? "Teklifiniz reddedildi" 
                    : $"Teklifiniz reddedildi: {reason}",
                ActionUrl = $"ProposalDetailsPage?proposalId={proposalId}",
                CreatedAt = DateTime.UtcNow
            };
            await _notificationService.CreateNotificationAsync(notification);

            Debug.WriteLine($"? Teklif reddedildi: {proposalId}");
            return ServiceResult<bool>.SuccessResult(true, "Teklif reddedildi");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? RejectProposalAsync hatası: {ex.Message}");
            return ServiceResult<bool>.FailureResult("Teklif reddedilemedi", ex.Message);
        }
    }

    public async Task<ServiceResult<bool>> WithdrawProposalAsync(string proposalId, string providerId)
    {
        try
        {
            var proposalNode = _firebaseClient
                .Child(Constants.ProviderProposalsCollection)
                .Child(proposalId);

            var proposal = await proposalNode.OnceSingleAsync<ProviderProposal>();
            if (proposal == null)
            {
                return ServiceResult<bool>.FailureResult("Teklif bulunamadı");
            }

            if (proposal.ProviderId != providerId)
            {
                return ServiceResult<bool>.FailureResult("Bu işlemi yapmaya yetkiniz yok");
            }

            if (proposal.Status != ProposalStatus.Pending)
            {
                return ServiceResult<bool>.FailureResult("Sadece bekleyen teklifler geri çekilebilir");
            }

            proposal.Status = ProposalStatus.Withdrawn;
            proposal.UpdatedAt = DateTime.UtcNow;
            await proposalNode.PutAsync(proposal);

            Debug.WriteLine($"? Teklif geri çekildi: {proposalId}");
            return ServiceResult<bool>.SuccessResult(true, "Teklif geri çekildi");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? WithdrawProposalAsync hatası: {ex.Message}");
            return ServiceResult<bool>.FailureResult("Teklif geri çekilemedi", ex.Message);
        }
    }

    public async Task<ServiceResult<ServiceRequest>> CreateServiceContractFromProposalAsync(string proposalId)
    {
        try
        {
            var proposalResult = await GetProposalByIdAsync(proposalId);
            if (!proposalResult.Success || proposalResult.Data == null)
            {
                return ServiceResult<ServiceRequest>.FailureResult("Teklif bulunamadı");
            }

            var proposal = proposalResult.Data;

            if (proposal.IsContractCreated)
            {
                return ServiceResult<ServiceRequest>.FailureResult("Bu tekliften zaten bir sözleşme oluşturulmuş.");
            }

            if (proposal.Status != ProposalStatus.Accepted)
            {
                return ServiceResult<ServiceRequest>.FailureResult("Sadece kabul edilmiş tekliflerden sözleşme oluşturulabilir");
            }

            var requestResult = await _customerRequestManager.GetCustomerRequestByIdAsync(proposal.CustomerRequestId);
            if (!requestResult.Success || requestResult.Data == null)
            {
                return ServiceResult<ServiceRequest>.FailureResult("Talep bulunamadı");
            }

            var customerRequest = requestResult.Data;

            var serviceRequest = new ServiceRequest
            {
                RequestId = Guid.NewGuid().ToString(),
                ServiceId = proposal.CustomerRequestId,
                ServiceTitle = customerRequest.Title,
                ProviderId = proposal.ProviderId,
                ProviderName = proposal.ProviderName,
                RequesterId = customerRequest.CustomerId,
                RequesterName = customerRequest.CustomerName,
                Price = proposal.Price,
                QuotedPrice = proposal.Price,
                Currency = proposal.Currency,
                TimeCreditValue = 0,
                Message = proposal.Message,
                Status = ServiceRequestStatus.Accepted,
                PaymentStatus = ServicePaymentStatus.None,
                PaymentMethod = PaymentMethodType.None,
                RequestedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _firebaseClient
                .Child(Constants.ServiceRequestsCollection)
                .Child(serviceRequest.RequestId)
                .PutAsync(serviceRequest);

            proposal.IsContractCreated = true;
            proposal.ServiceContractId = serviceRequest.RequestId;
            proposal.UpdatedAt = DateTime.UtcNow;

            await _firebaseClient
                .Child(Constants.ProviderProposalsCollection)
                .Child(proposalId)
                .PutAsync(proposal);

            await _customerRequestManager.UpdateRequestStatusAsync(
                customerRequest.RequestId,
                CustomerRequestStatus.InProgress,
                proposalId);

            var providerNotification = new Notification
            {
                UserId = proposal.ProviderId,
                Type = NotificationType.ServiceCompleted,
                Title = "İş Başladı",
                Message = $"{customerRequest.Title} için iş sözleşmesi oluşturuldu",
                ActionUrl = $"ServiceRequestDetailsPage?requestId={serviceRequest.RequestId}",
                CreatedAt = DateTime.UtcNow
            };

            var customerNotification = new Notification
            {
                UserId = customerRequest.CustomerId,
                Type = NotificationType.ServiceCompleted,
                Title = "İş Başladı",
                Message = $"{proposal.ProviderName} ile iş sözleşmeniz oluşturuldu",
                ActionUrl = $"ServiceRequestDetailsPage?requestId={serviceRequest.RequestId}",
                CreatedAt = DateTime.UtcNow
            };

            await Task.WhenAll(
                _notificationService.CreateNotificationAsync(providerNotification),
                _notificationService.CreateNotificationAsync(customerNotification)
            );

            Debug.WriteLine($"? Hizmet sözleşmesi oluşturuldu: {serviceRequest.RequestId}");
            return ServiceResult<ServiceRequest>.SuccessResult(serviceRequest, "İş sözleşmesi oluşturuldu!");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? CreateServiceContractFromProposalAsync hatası: {ex.Message}");
            return ServiceResult<ServiceRequest>.FailureResult("Sözleşme oluşturulamadı", ex.Message);
        }
    }

    private async Task RejectOtherProposalsAsync(string customerRequestId, string acceptedProposalId)
    {
        try
        {
            var allProposals = await _firebaseClient
                .Child(Constants.ProviderProposalsCollection)
                .OrderBy("CustomerRequestId")
                .EqualTo(customerRequestId)
                .OnceAsync<ProviderProposal>();

            var tasksToReject = new List<Task>();

            foreach (var proposalEntry in allProposals)
            {
                if (proposalEntry.Key != acceptedProposalId && proposalEntry.Object.Status == ProposalStatus.Pending)
                {
                    var proposal = proposalEntry.Object;
                    var patchData = new Dictionary<string, object>
                    {
                        { "Status", (int)ProposalStatus.Rejected },
                        { "RejectionReason", "Başka bir teklif kabul edildi" },
                        { "RespondedAt", DateTime.UtcNow.ToString("O") },
                        { "UpdatedAt", DateTime.UtcNow.ToString("O") }
                    };

                    var task = _firebaseClient
                        .Child(Constants.ProviderProposalsCollection)
                        .Child(proposalEntry.Key)
                        .PatchAsync(patchData);

                    tasksToReject.Add(task);

                    var notification = new Notification
                    {
                        UserId = proposal.ProviderId,
                        Type = NotificationType.OfferRejected,
                        Title = "Teklif Reddedildi",
                        Message = "Başka bir teklif kabul edildiği için teklifiniz reddedildi",
                        ActionUrl = $"ProposalDetailsPage?proposalId={proposalEntry.Key}",
                        CreatedAt = DateTime.UtcNow
                    };
                    tasksToReject.Add(_notificationService.CreateNotificationAsync(notification));
                }
            }

            if (tasksToReject.Any())
            {
                await Task.WhenAll(tasksToReject);
                Debug.WriteLine($"? {tasksToReject.Count / 2} teklif otomatik reddedildi");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"?? RejectOtherProposals hatası: {ex.Message}");
        }
    }
}
