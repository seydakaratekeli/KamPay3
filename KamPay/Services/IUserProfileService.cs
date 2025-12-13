using KamPay.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KamPay.Services

// bu sayfa kullanıcı profili, istatistikleri ve oyunlaştırma ile ilgili işlemleri tanımlar.
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

       
        /// Yeni bir kullanıcı için veritabanında profil ve başlangıç istatistiklerini oluşturur.
       
        Task<ServiceResult<bool>> CreateUserProfileAsync(string userId, string username, string email);

       
        /// Belirtilen kullanıcının genel profil bilgilerini (isim, resim vb.) getirir.
       
        Task<ServiceResult<UserProfile>> GetUserProfileAsync(string userId);


        // --- İstatistik ve Oyunlaştırma Metotları ---

       
        /// Belirtilen kullanıcının istatistiklerini (puan, takas sayısı vb.) getirir.
       
        Task<ServiceResult<UserStats>> GetUserStatsAsync(string userId);

       
        /// Belirli bir aksiyon için kullanıcıya standart puan ekler.
       
        Task<ServiceResult<bool>> AddPointsForAction(string userId, UserAction action);

       
        /// Belirtilen sebeple kullanıcıya belirli bir miktar puan ekler.
       
        Task<ServiceResult<bool>> AddPointsAsync(string userId, int points, string reason);

       
        /// Kullanıcının mevcut istatistiklerine göre yeni rozetler kazanıp kazanmadığını kontrol eder ve gerekirse verir.
       
        Task<ServiceResult<bool>> CheckAndAwardBadgesAsync(string userId);

       
        /// Kullanıcının sahip olduğu tüm rozetleri listeler.
       
        Task<ServiceResult<List<UserBadge>>> GetUserBadgesAsync(string userId);

       
        /// Kullanıcıya belirli bir rozeti manuel olarak verir.
       
        Task<ServiceResult<UserBadge>> AwardBadgeAsync(string userId, string badgeId);

        Task<ServiceResult<bool>> TransferTimeCreditsAsync(string fromUserId, string toUserId, int amount, string reason);

        // Not: UpdateUserStatsAsync metodu, puan/rozet ekleme işlemleri tarafından dolaylı olarak
        // kullanıldığı için genellikle doğrudan çağrılmaz. İhtiyaç halinde kullanılabilir.
        Task<ServiceResult<bool>> UpdateUserStatsAsync(UserStats stats);
       
        /// Kullanıcının profil bilgilerini (isim, kullanıcı adı, profil resmi) günceller.
       
        Task<ServiceResult<bool>> UpdateUserProfileAsync(
            string userId,
            string firstName = null,
            string lastName = null,
            string username = null,
            string profileImageUrl = null);


    }
}