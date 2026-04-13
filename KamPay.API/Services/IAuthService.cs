namespace KamPay.API.Services
{
    public interface IAuthService
    {
        /// <summary>
        /// Firebase ID Token'ı doğrular ve geriye kendi API'mizde kullanılacak Custom JWT token ile UID'yi döndürür.
        /// </summary>
        /// <param name="firebaseIdToken">Mobil uygulamadan gelen Firebase ID Token</param>
        /// <returns>Custom JWT Token string ve Firebase UID</returns>
        Task<(string Token, string Uid, DateTime Expiration)> LoginWithFirebaseAsync(string firebaseIdToken);
    }
}
