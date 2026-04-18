using KamPay.Models;

namespace KamPay.Services
{
    public interface IFavoriteService
    {
        Task<ServiceResult<Favorite>> AddToFavoritesAsync(string userId, string productId);
        Task<ServiceResult<bool>> RemoveFromFavoritesAsync(string userId, string productId);
        Task<ServiceResult<List<Favorite>>> GetUserFavoritesAsync(string userId);
        Task<ServiceResult<bool>> IsFavoriteAsync(string userId, string productId);
        Task<ServiceResult<int>> GetProductFavoriteCountAsync(string productId);
    }
}