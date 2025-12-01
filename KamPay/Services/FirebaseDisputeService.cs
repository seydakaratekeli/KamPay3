using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Helpers;
using KamPay.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KamPay.Services
{
    /// <summary>
    /// Firebase implementation of dispute service
    /// </summary>
    public class FirebaseDisputeService : IDisputeService
    {
        private readonly FirebaseClient _firebaseClient;
        private readonly INotificationService _notificationService;

        public FirebaseDisputeService(INotificationService notificationService)
        {
            _firebaseClient = new FirebaseClient(Constants.FirebaseRealtimeDbUrl);
            _notificationService = notificationService;
        }

        public async Task<ServiceResult<DisputeResolution>> CreateDisputeAsync(
            string complainantUserId,
            string respondentUserId,
            DisputeReason reason,
            string description,
            string? referenceId = null,
            string? referenceType = null)
        {
            try
            {
                // Get user names
                var complainantUser = await _firebaseClient
                    .Child(Constants.UsersCollection)
                    .Child(complainantUserId)
                    .OnceSingleAsync<User>();

                var respondentUser = await _firebaseClient
                    .Child(Constants.UsersCollection)
                    .Child(respondentUserId)
                    .OnceSingleAsync<User>();

                if (complainantUser == null || respondentUser == null)
                {
                    return ServiceResult<DisputeResolution>.FailureResult("Kullanıcı bulunamadı");
                }

                var dispute = new DisputeResolution
                {
                    ComplainantUserId = complainantUserId,
                    ComplainantName = complainantUser.FullName,
                    RespondentUserId = respondentUserId,
                    RespondentName = respondentUser.FullName,
                    Reason = reason,
                    Description = description,
                    ReferenceId = referenceId,
                    ReferenceType = referenceType,
                    Status = DisputeStatus.Open
                };

                // Add system note
                dispute.Notes.Add(new DisputeNote
                {
                    AuthorUserId = "system",
                    AuthorName = "Sistem",
                    Content = $"Anlaşmazlık oluşturuldu: {reason}",
                    IsSystemNote = true
                });

                await _firebaseClient
                    .Child(Constants.DisputesCollection)
                    .Child(dispute.DisputeId)
                    .PutAsync(dispute);

                // Notify respondent
                await _notificationService.CreateNotificationAsync(
                    respondentUserId,
                    "Anlaşmazlık Bildirimi",
                    $"{complainantUser.FullName} sizinle ilgili bir anlaşmazlık bildirdi",
                    NotificationType.Dispute,
                    dispute.DisputeId);

                return ServiceResult<DisputeResolution>.SuccessResult(
                    dispute,
                    "Anlaşmazlık oluşturuldu. En kısa sürede incelenecektir.");
            }
            catch (Exception ex)
            {
                return ServiceResult<DisputeResolution>.FailureResult(
                    "Anlaşmazlık oluşturulamadı",
                    ex.Message);
            }
        }

        public async Task<ServiceResult<List<DisputeResolution>>> GetUserDisputesAsync(
            string userId,
            DisputeStatus? status = null)
        {
            try
            {
                var allDisputes = await _firebaseClient
                    .Child(Constants.DisputesCollection)
                    .OnceAsync<DisputeResolution>();

                var userDisputes = allDisputes
                    .Select(d => d.Object)
                    .Where(d => d.ComplainantUserId == userId || d.RespondentUserId == userId)
                    .Where(d => !status.HasValue || d.Status == status.Value)
                    .OrderByDescending(d => d.CreatedAt)
                    .ToList();

                return ServiceResult<List<DisputeResolution>>.SuccessResult(userDisputes);
            }
            catch (Exception ex)
            {
                return ServiceResult<List<DisputeResolution>>.FailureResult(
                    "Anlaşmazlıklar alınamadı",
                    ex.Message);
            }
        }

        public async Task<ServiceResult<List<DisputeResolution>>> GetAllDisputesAsync(
            DisputeStatus? status = null)
        {
            try
            {
                var allDisputes = await _firebaseClient
                    .Child(Constants.DisputesCollection)
                    .OnceAsync<DisputeResolution>();

                var disputes = allDisputes
                    .Select(d => d.Object)
                    .Where(d => !status.HasValue || d.Status == status.Value)
                    .OrderByDescending(d => d.CreatedAt)
                    .ToList();

                return ServiceResult<List<DisputeResolution>>.SuccessResult(disputes);
            }
            catch (Exception ex)
            {
                return ServiceResult<List<DisputeResolution>>.FailureResult(
                    "Anlaşmazlıklar alınamadı",
                    ex.Message);
            }
        }

        public async Task<ServiceResult<bool>> AddDisputeNoteAsync(
            string disputeId,
            string authorUserId,
            string content,
            bool isSystemNote = false)
        {
            try
            {
                var disputeNode = _firebaseClient
                    .Child(Constants.DisputesCollection)
                    .Child(disputeId);

                var dispute = await disputeNode.OnceSingleAsync<DisputeResolution>();

                if (dispute == null)
                {
                    return ServiceResult<bool>.FailureResult("Anlaşmazlık bulunamadı");
                }

                var authorName = "Sistem";
                if (!isSystemNote)
                {
                    var user = await _firebaseClient
                        .Child(Constants.UsersCollection)
                        .Child(authorUserId)
                        .OnceSingleAsync<User>();
                    authorName = user?.FullName ?? "Bilinmeyen";
                }

                dispute.Notes.Add(new DisputeNote
                {
                    AuthorUserId = authorUserId,
                    AuthorName = authorName,
                    Content = content,
                    IsSystemNote = isSystemNote
                });

                dispute.LastUpdatedAt = DateTime.UtcNow;
                await disputeNode.PutAsync(dispute);

                return ServiceResult<bool>.SuccessResult(true, "Not eklendi");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult(
                    "Not eklenemedi",
                    ex.Message);
            }
        }

        public async Task<ServiceResult<bool>> ResolveDisputeAsync(
            string disputeId,
            string resolverUserId,
            DisputeResolutionType resolutionType,
            string? resolutionNotes = null)
        {
            try
            {
                var disputeNode = _firebaseClient
                    .Child(Constants.DisputesCollection)
                    .Child(disputeId);

                var dispute = await disputeNode.OnceSingleAsync<DisputeResolution>();

                if (dispute == null)
                {
                    return ServiceResult<bool>.FailureResult("Anlaşmazlık bulunamadı");
                }

                dispute.Status = DisputeStatus.Resolved;
                dispute.ResolutionType = resolutionType;
                dispute.ResolutionNotes = resolutionNotes;
                dispute.ResolvedByUserId = resolverUserId;
                dispute.ResolvedAt = DateTime.UtcNow;
                dispute.LastUpdatedAt = DateTime.UtcNow;

                // Add resolution note
                dispute.Notes.Add(new DisputeNote
                {
                    AuthorUserId = "system",
                    AuthorName = "Sistem",
                    Content = $"Anlaşmazlık çözümlendi: {resolutionType}. {resolutionNotes}",
                    IsSystemNote = true
                });

                await disputeNode.PutAsync(dispute);

                // Notify both parties
                await _notificationService.CreateNotificationAsync(
                    dispute.ComplainantUserId,
                    "Anlaşmazlık Çözüldü",
                    $"Anlaşmazlığınız çözümlendi: {resolutionType}",
                    NotificationType.Dispute,
                    disputeId);

                await _notificationService.CreateNotificationAsync(
                    dispute.RespondentUserId,
                    "Anlaşmazlık Çözüldü",
                    $"Anlaşmazlık çözümlendi: {resolutionType}",
                    NotificationType.Dispute,
                    disputeId);

                return ServiceResult<bool>.SuccessResult(
                    true,
                    "Anlaşmazlık çözümlendi");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult(
                    "Anlaşmazlık çözümlenemedi",
                    ex.Message);
            }
        }

        public async Task<ServiceResult<bool>> AddEvidenceAsync(
            string disputeId,
            string userId,
            string evidenceUrl)
        {
            try
            {
                var disputeNode = _firebaseClient
                    .Child(Constants.DisputesCollection)
                    .Child(disputeId);

                var dispute = await disputeNode.OnceSingleAsync<DisputeResolution>();

                if (dispute == null)
                {
                    return ServiceResult<bool>.FailureResult("Anlaşmazlık bulunamadı");
                }

                // Verify user is involved in dispute
                if (dispute.ComplainantUserId != userId && dispute.RespondentUserId != userId)
                {
                    return ServiceResult<bool>.FailureResult("Bu anlaşmazlığa kanıt ekleyemezsiniz");
                }

                dispute.EvidenceUrls.Add(evidenceUrl);
                dispute.LastUpdatedAt = DateTime.UtcNow;
                await disputeNode.PutAsync(dispute);

                return ServiceResult<bool>.SuccessResult(true, "Kanıt eklendi");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult(
                    "Kanıt eklenemedi",
                    ex.Message);
            }
        }

        public async Task<ServiceResult<bool>> UpdateDisputeStatusAsync(
            string disputeId,
            DisputeStatus newStatus)
        {
            try
            {
                var disputeNode = _firebaseClient
                    .Child(Constants.DisputesCollection)
                    .Child(disputeId);

                var dispute = await disputeNode.OnceSingleAsync<DisputeResolution>();

                if (dispute == null)
                {
                    return ServiceResult<bool>.FailureResult("Anlaşmazlık bulunamadı");
                }

                dispute.Status = newStatus;
                dispute.LastUpdatedAt = DateTime.UtcNow;
                await disputeNode.PutAsync(dispute);

                return ServiceResult<bool>.SuccessResult(true, "Durum güncellendi");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult(
                    "Durum güncellenemedi",
                    ex.Message);
            }
        }
    }
}
