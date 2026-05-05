using KamPay.Models;

namespace KamPay.Services.Messaging
{
    public interface IChatMediaService
    {
        Task<ServiceResult<Message>> SendImageMessageAsync(
            string imagePath,
            string conversationId,
            Conversation conversation,
            User currentUser);
    }
}
