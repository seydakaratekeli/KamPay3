using KamPay.Helpers;
using KamPay.Models;

namespace KamPay.Services.Messaging
{
    public sealed class ChatMediaService : IChatMediaService
    {
        private readonly IStorageService _storageService;
        private readonly IMessagingService _messagingService;

        public ChatMediaService(
            IStorageService storageService,
            IMessagingService messagingService)
        {
            _storageService = storageService;
            _messagingService = messagingService;
        }

        public async Task<ServiceResult<Message>> SendImageMessageAsync(
            string imagePath,
            string conversationId,
            Conversation conversation,
            User currentUser)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
                return ServiceResult<Message>.FailureResult("Gorsel yolu bos olamaz.");

            var limitCheck = SecureRateLimiters.ImageUpload.CheckRequest(currentUser.UserId);
            if (!limitCheck.IsAllowed)
                return ServiceResult<Message>.FailureResult(limitCheck.Message);

            var receiverId = conversation.GetOtherUserId(currentUser.UserId);
            if (string.IsNullOrEmpty(receiverId))
                return ServiceResult<Message>.FailureResult("Alici bilgisi bulunamadi.");

            var uploadResult = await _storageService.UploadMessageImageAsync(imagePath, conversationId);
            if (!uploadResult.Success || string.IsNullOrWhiteSpace(uploadResult.Data))
                return ServiceResult<Message>.FailureResult(uploadResult.Message ?? "Gorsel yuklenemedi.");

            var request = new SendMessageRequest
            {
                ReceiverId = receiverId,
                Content = "Fotograf",
                Type = MessageType.Image,
                ProductId = conversation.ProductId,
                ImageUrl = uploadResult.Data,
                ConversationType = conversation.ConversationType ?? "General"
            };

            return await _messagingService.SendMessageAsync(request, currentUser);
        }
    }
}
