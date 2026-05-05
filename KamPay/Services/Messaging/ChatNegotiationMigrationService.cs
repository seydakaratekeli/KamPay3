using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Helpers;
using KamPay.Models;

namespace KamPay.Services.Messaging
{
    public sealed class ChatNegotiationMigrationService : IChatNegotiationMigrationService
    {
        private readonly FirebaseClient _firebaseClient;
        private readonly IMessagingService _messagingService;

        public ChatNegotiationMigrationService(
            FirebaseClient firebaseClient,
            IMessagingService messagingService)
        {
            _firebaseClient = firebaseClient;
            _messagingService = messagingService;
        }

        public async Task<ServiceResult<NegotiationChatMigrationResult>> BackfillLegacyNegotiationMessagesAsync(
            bool dryRun = true,
            CancellationToken cancellationToken = default)
        {
            var result = new NegotiationChatMigrationResult();

            try
            {
                var conversations = await _firebaseClient
                    .Child(Constants.ConversationsCollection)
                    .OnceAsync<Conversation>();

                foreach (var item in conversations)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var sourceConversation = item.Object;
                    if (sourceConversation == null ||
                        !sourceConversation.IsActive ||
                        sourceConversation.IsNegotiationConversation)
                    {
                        continue;
                    }

                    sourceConversation.ConversationId = item.Key;
                    result.ConversationsScanned++;

                    var messages = await _firebaseClient
                        .Child(Constants.MessagesCollection)
                        .Child(sourceConversation.ConversationId)
                        .OnceAsync<Message>();

                    var legacyNegotiationMessages = messages
                        .Where(m => m.Object != null && !m.Object.IsDeleted && m.Object.Type == MessageType.Negotiation)
                        .OrderBy(m => m.Object.SentAt)
                        .ToList();

                    if (legacyNegotiationMessages.Count == 0)
                        continue;

                    result.LegacyMessagesFound += legacyNegotiationMessages.Count;

                    var targetConversationResult = await _messagingService.GetOrCreateConversationAsync(
                        sourceConversation.User1Id,
                        sourceConversation.User2Id,
                        sourceConversation.ProductId,
                        "Negotiation");

                    if (!targetConversationResult.Success || targetConversationResult.Data == null)
                    {
                        AppLogger.DebugLog($"Negotiation migration hedef conversation olusturulamadi: {sourceConversation.ConversationId}");
                        continue;
                    }

                    var targetConversation = targetConversationResult.Data;

                    foreach (var legacyMessage in legacyNegotiationMessages)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var targetMessage = await _firebaseClient
                            .Child(Constants.MessagesCollection)
                            .Child(targetConversation.ConversationId)
                            .Child(legacyMessage.Key)
                            .OnceSingleAsync<Message>();

                        if (targetMessage != null)
                        {
                            result.MessagesAlreadyPresent++;
                            continue;
                        }

                        var copy = legacyMessage.Object;
                        copy.MessageId = legacyMessage.Key;
                        copy.ConversationId = targetConversation.ConversationId;

                        if (!dryRun)
                        {
                            await _firebaseClient
                                .Child(Constants.MessagesCollection)
                                .Child(targetConversation.ConversationId)
                                .Child(copy.MessageId)
                                .PutAsync(copy);
                        }

                        result.MessagesCopied++;

                        if (!string.IsNullOrWhiteSpace(copy.RelatedTransactionId))
                        {
                            var currentConversationId = await _firebaseClient
                                .Child(Constants.TransactionsCollection)
                                .Child(copy.RelatedTransactionId)
                                .Child(nameof(Transaction.ConversationId))
                                .OnceSingleAsync<string>();

                            if (currentConversationId != targetConversation.ConversationId)
                            {
                                if (!dryRun)
                                {
                                    await _firebaseClient
                                        .Child(Constants.TransactionsCollection)
                                        .Child(copy.RelatedTransactionId)
                                        .Child(nameof(Transaction.ConversationId))
                                        .PutAsync(targetConversation.ConversationId);
                                }

                                result.TransactionsRepointed++;
                            }
                        }
                    }
                }

                return ServiceResult<NegotiationChatMigrationResult>.SuccessResult(
                    result,
                    dryRun ? "Dry-run tamamlandi." : "Legacy pazarlik mesajlari backfill edildi.");
            }
            catch (Exception ex)
            {
                AppLogger.DebugLog($"Negotiation migration hatasi: {ex.Message}");
                return ServiceResult<NegotiationChatMigrationResult>.FailureResult(
                    "Legacy pazarlik mesajlari migrate edilemedi.",
                    ex.Message);
            }
        }
    }
}
