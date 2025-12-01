using KamPay.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KamPay.Services
{
    /// <summary>
    /// Service interface for handling dispute resolution
    /// </summary>
    public interface IDisputeService
    {
        /// <summary>
        /// Creates a new dispute
        /// </summary>
        Task<ServiceResult<DisputeResolution>> CreateDisputeAsync(
            string complainantUserId,
            string respondentUserId,
            DisputeReason reason,
            string description,
            string? referenceId = null,
            string? referenceType = null);

        /// <summary>
        /// Gets all disputes for a user
        /// </summary>
        Task<ServiceResult<List<DisputeResolution>>> GetUserDisputesAsync(
            string userId,
            DisputeStatus? status = null);

        /// <summary>
        /// Gets all open disputes (admin view)
        /// </summary>
        Task<ServiceResult<List<DisputeResolution>>> GetAllDisputesAsync(
            DisputeStatus? status = null);

        /// <summary>
        /// Adds a note to a dispute
        /// </summary>
        Task<ServiceResult<bool>> AddDisputeNoteAsync(
            string disputeId,
            string authorUserId,
            string content,
            bool isSystemNote = false);

        /// <summary>
        /// Resolves a dispute
        /// </summary>
        Task<ServiceResult<bool>> ResolveDisputeAsync(
            string disputeId,
            string resolverUserId,
            DisputeResolutionType resolutionType,
            string? resolutionNotes = null);

        /// <summary>
        /// Adds evidence to a dispute
        /// </summary>
        Task<ServiceResult<bool>> AddEvidenceAsync(
            string disputeId,
            string userId,
            string evidenceUrl);

        /// <summary>
        /// Updates dispute status
        /// </summary>
        Task<ServiceResult<bool>> UpdateDisputeStatusAsync(
            string disputeId,
            DisputeStatus newStatus);
    }
}
