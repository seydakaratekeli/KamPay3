using KamPay.Models;

namespace KamPay.Services.Messaging
{
    public interface IChatNegotiationMigrationService
    {
        Task<ServiceResult<NegotiationChatMigrationResult>> BackfillLegacyNegotiationMessagesAsync(
            bool dryRun = true,
            CancellationToken cancellationToken = default);
    }

    public sealed class NegotiationChatMigrationResult
    {
        public int ConversationsScanned { get; set; }
        public int LegacyMessagesFound { get; set; }
        public int MessagesCopied { get; set; }
        public int MessagesAlreadyPresent { get; set; }
        public int TransactionsRepointed { get; set; }
    }
}
