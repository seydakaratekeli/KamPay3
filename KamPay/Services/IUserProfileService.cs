using KamPay.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KamPay.Services
{
    // Bu enum'un Models klasöründe veya uygun bir yerde tanımlı olduğundan emin ol.
    // Eğer değilse, bu enum'u projenize ekleyin.
    public enum UserAction
    {
        AddProduct,
        CompleteTransaction,
        MakeDonation,
        ReceiveDonation,
        ShareService,
        WriteReview
    }

    public interface IUserProfileService
    {
        // --- Profil Metotları ---

        /// <summary>
        /// Yeni bir kullanıcı için veritabanında profil ve başlangıç istatistiklerini oluşturur.
        /// </summary>
        Task<ServiceResult<bool>> CreateUserProfileAsync(string userId, string username, string email);

        /// <summary>
        /// Belirtilen kullanıcının genel profil bilgilerini (isim, resim vb.) getirir.
        /// </summary>
        Task<ServiceResult<UserProfile>> GetUserProfileAsync(string userId);


        // --- İstatistik ve Oyunlaştırma Metotları ---

        /// <summary>
        /// Belirtilen kullanıcının istatistiklerini (puan, takas sayısı vb.) getirir.
        /// </summary>
        Task<ServiceResult<UserStats>> GetUserStatsAsync(string userId);

        /// <summary>
        /// Belirli bir aksiyon için kullanıcıya standart puan ekler.
        /// </summary>
        Task<ServiceResult<bool>> AddPointsForAction(string userId, UserAction action);

        /// <summary>
        /// Belirtilen sebeple kullanıcıya belirli bir miktar puan ekler.
        /// </summary>
        Task<ServiceResult<bool>> AddPointsAsync(string userId, int points, string reason);

        /// <summary>
        /// Kullanıcının mevcut istatistiklerine göre yeni rozetler kazanıp kazanmadığını kontrol eder ve gerekirse verir.
        /// </summary>
        Task<ServiceResult<bool>> CheckAndAwardBadgesAsync(string userId);

        /// <summary>
        /// Kullanıcının sahip olduğu tüm rozetleri listeler.
        /// </summary>
        Task<ServiceResult<List<UserBadge>>> GetUserBadgesAsync(string userId);

        /// <summary>
        /// Kullanıcıya belirli bir rozeti manuel olarak verir.
        /// </summary>
        Task<ServiceResult<UserBadge>> AwardBadgeAsync(string userId, string badgeId);

        Task<ServiceResult<bool>> TransferTimeCreditsAsync(string fromUserId, string toUserId, int amount, string reason);

        // Not: UpdateUserStatsAsync metodu, puan/rozet ekleme işlemleri tarafından dolaylı olarak
        // kullanıldığı için genellikle doğrudan çağrılmaz. İhtiyaç halinde kullanılabilir.
        Task<ServiceResult<bool>> UpdateUserStatsAsync(UserStats stats);
        /// <summary>
        /// Kullanıcının profil bilgilerini (isim, kullanıcı adı, profil resmi) günceller.
        /// </summary>
        Task<ServiceResult<bool>> UpdateUserProfileAsync(
            string userId,
            string firstName = null,
            string lastName = null,
            string username = null,
            string profileImageUrl = null);


    }
}