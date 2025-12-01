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
    public interface IQRCodeService
    {
        Task<ServiceResult<DeliveryQRCode>> GenerateDeliveryQRCodeAsync(string transactionId, string productId, string productTitle, string sellerId, string buyerId);
        Task<ServiceResult<DeliveryQRCode>> ValidateQRCodeAsync(string qrCodeData);
        Task<ServiceResult<bool>> CompleteDeliveryAsync(string qrCodeId);
        string GenerateQRCodeData(DeliveryQRCode delivery);
        Task<ServiceResult<List<DeliveryQRCode>>> GetQRCodesForTransactionAsync(string transactionId);

        /// <summary>
        /// Süre sınırlı ve konum doğrulamalı QR kod oluşturur
        /// </summary>
        Task<ServiceResult<DeliveryQRCode>> GenerateSecureDeliveryQRCodeAsync(
            string transactionId,
            string productId,
            string productTitle,
            string giverUserId,
            string receiverUserId,
            int validityMinutes,
            double? meetingPointLatitude = null,
            double? meetingPointLongitude = null,
            string? meetingPointName = null);

        /// <summary>
        /// QR kodu konum ve PIN doğrulaması ile tarar
        /// </summary>
        Task<ServiceResult<bool>> ScanQRCodeWithLocationAsync(
            string qrCodeId,
            double currentLatitude,
            double currentLongitude,
            string? verificationPin = null);

        /// <summary>
        /// QR kod süresini uzatır (1 kez, max 30 dakika)
        /// </summary>
        Task<ServiceResult<DateTime>> ExtendQRCodeValidityAsync(
            string qrCodeId,
            int additionalMinutes);

        /// <summary>
        /// QR kodu iptal eder
        /// </summary>
        Task<ServiceResult<bool>> CancelDeliveryQRCodeAsync(
            string qrCodeId,
            string userId,
            string reason);

        /// <summary>
        /// Teslimat fotoğrafı yükler (FAZ 2)
        /// </summary>
        Task<ServiceResult<string>> UploadDeliveryPhotoAsync(
            string qrCodeId, byte[] photoData, string userId);

        /// <summary>
        /// Fotoğraf gerekli mi kontrol eder (FAZ 2)
        /// </summary>
        Task<ServiceResult<bool>> IsPhotoRequiredAsync(string qrCodeId);
    }

    public class FirebaseQRCodeService : IQRCodeService
    {
        private readonly FirebaseClient _firebaseClient;
        private readonly IUserProfileService _userProfileService;
        private readonly IStorageService _storageService;
        private const string QRCodesCollection = "delivery_qrcodes";
        
        // 📌 Güvenlik sabitleri
        private const int MaxExtensionMinutes = 30;
        private const int ExtendTimeThresholdMinutes = 15;

        public FirebaseQRCodeService(IUserProfileService userProfileService, IStorageService storageService)
        {
            _firebaseClient = new FirebaseClient(Constants.FirebaseRealtimeDbUrl);
            _userProfileService = userProfileService;
            _storageService = storageService;
        }

        public async Task<ServiceResult<bool>> CompleteDeliveryAsync(string qrCodeId)
        {
            try
            {
                var deliveryNode = _firebaseClient.Child(QRCodesCollection).Child(qrCodeId);
                var delivery = await deliveryNode.OnceSingleAsync<DeliveryQRCode>();

                if (delivery == null || delivery.IsUsed)
                {
                    return ServiceResult<bool>.FailureResult("Teslimat bulunamadı veya zaten tamamlanmış.");
                }

                delivery.IsUsed = true;
                delivery.UsedAt = DateTime.UtcNow;
                delivery.Status = DeliveryStatus.Completed;
                delivery.DeliveryStatus = DeliveryStatus.Completed;
                await deliveryNode.PutAsync(delivery);

                var transaction = await _firebaseClient.Child(Constants.TransactionsCollection).Child(delivery.TransactionId).OnceSingleAsync<Transaction>();
                if (transaction != null)
                {
                    var allCodesResult = await GetQRCodesForTransactionAsync(transaction.TransactionId);

                    if (allCodesResult.Success && allCodesResult.Data.All(c => c.IsUsed))
                    {
                        await MarkProductAsSold(transaction.ProductId);
                        if (!string.IsNullOrEmpty(transaction.OfferedProductId))
                        {
                            await MarkProductAsSold(transaction.OfferedProductId);
                        }

                        transaction.Status = TransactionStatus.Completed;
                        await _firebaseClient.Child(Constants.TransactionsCollection).Child(transaction.TransactionId).PutAsync(transaction);

                        if (transaction.Type == ProductType.Bagis)
                        {
                            await _userProfileService.AddPointsForAction(transaction.SellerId, UserAction.MakeDonation);
                            await _userProfileService.AddPointsForAction(transaction.BuyerId, UserAction.ReceiveDonation);
                        }
                        else
                        {
                            await _userProfileService.AddPointsForAction(transaction.SellerId, UserAction.CompleteTransaction);
                            await _userProfileService.AddPointsForAction(transaction.BuyerId, UserAction.CompleteTransaction);
                        }
                    }
                }

                return ServiceResult<bool>.SuccessResult(true, "Teslimat tamamlandı!");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Teslimat tamamlanamadı", ex.Message);
            }
        }

        #region Güvenli QR Kod Metodları

        /// <summary>
        /// Süre sınırlı ve konum doğrulamalı QR kod oluşturur
        /// </summary>
        public async Task<ServiceResult<DeliveryQRCode>> GenerateSecureDeliveryQRCodeAsync(
            string transactionId,
            string productId,
            string productTitle,
            string giverUserId,
            string receiverUserId,
            int validityMinutes,
            double? meetingPointLatitude = null,
            double? meetingPointLongitude = null,
            string? meetingPointName = null)
        {
            try
            {
                var delivery = new DeliveryQRCode
                {
                    TransactionId = transactionId,
                    ProductId = productId,
                    ProductTitle = productTitle,
                    SellerId = giverUserId,
                    BuyerId = receiverUserId,
                    ValidityMinutes = validityMinutes,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(validityMinutes),
                    MeetingPointLatitude = meetingPointLatitude,
                    MeetingPointLongitude = meetingPointLongitude,
                    MeetingPointName = meetingPointName,
                    VerificationPin = GeneratePin(),
                    DeliveryStatus = DeliveryStatus.Pending
                };

                delivery.QRCodeData = GenerateSecureQRData();
                await _firebaseClient.Child(QRCodesCollection).Child(delivery.QRCodeId).PutAsync(delivery);
                return ServiceResult<DeliveryQRCode>.SuccessResult(delivery, "Güvenli QR kod oluşturuldu");
            }
            catch (Exception ex)
            {
                return ServiceResult<DeliveryQRCode>.FailureResult("QR kod oluşturulamadı", ex.Message);
            }
        }

        /// <summary>
        /// QR kodu konum ve PIN doğrulaması ile tarar
        /// </summary>
        public async Task<ServiceResult<bool>> ScanQRCodeWithLocationAsync(
            string qrCodeId,
            double currentLatitude,
            double currentLongitude,
            string? verificationPin = null)
        {
            try
            {
                var deliveryNode = _firebaseClient.Child(QRCodesCollection).Child(qrCodeId);
                var delivery = await deliveryNode.OnceSingleAsync<DeliveryQRCode>();

                // 1. QR kod var mı?
                if (delivery == null)
                {
                    return ServiceResult<bool>.FailureResult("QR kod bulunamadı.");
                }

                // 2. Süresi dolmuş mu?
                if (delivery.IsExpired)
                {
                    delivery.DeliveryStatus = DeliveryStatus.Expired;
                    await deliveryNode.PutAsync(delivery);
                    return ServiceResult<bool>.FailureResult("QR kodun süresi dolmuş.");
                }

                // 3. Zaten kullanılmış mı?
                if (delivery.IsUsed)
                {
                    return ServiceResult<bool>.FailureResult("Bu QR kod daha önce kullanılmış.");
                }

                // 4. İptal edilmiş mi?
                if (delivery.DeliveryStatus == DeliveryStatus.Cancelled)
                {
                    return ServiceResult<bool>.FailureResult("Bu teslimat iptal edilmiş.");
                }

                // 5. PIN kontrolü (eski QR kodlar için PIN null olabilir - backward compatibility)
                if (!string.IsNullOrEmpty(delivery.VerificationPin))
                {
                    if (string.IsNullOrEmpty(verificationPin))
                    {
                        return ServiceResult<bool>.FailureResult("PIN kodu gereklidir.");
                    }

                    if (delivery.VerificationPin != verificationPin)
                    {
                        delivery.ScanAttempts++;
                        await deliveryNode.PutAsync(delivery);

                        if (delivery.ScanAttempts >= delivery.MaxScanAttempts)
                        {
                            delivery.DeliveryStatus = DeliveryStatus.Cancelled;
                            delivery.CancellationReason = "Maksimum PIN deneme sayısı aşıldı";
                            delivery.CancelledAt = DateTime.UtcNow;
                            await deliveryNode.PutAsync(delivery);
                            return ServiceResult<bool>.FailureResult("Çok fazla yanlış PIN denemesi. QR kod iptal edildi.");
                        }

                        return ServiceResult<bool>.FailureResult($"Yanlış PIN. Kalan deneme: {delivery.MaxScanAttempts - delivery.ScanAttempts}");
                    }
                }

                // 6. Konum kontrolü (buluşma noktası belirtildiyse)
                if (delivery.MeetingPointLatitude.HasValue && delivery.MeetingPointLongitude.HasValue)
                {
                    var distance = CalculateDistance(
                        delivery.MeetingPointLatitude.Value,
                        delivery.MeetingPointLongitude.Value,
                        currentLatitude,
                        currentLongitude);

                    if (distance > delivery.MaxDistanceMeters)
                    {
                        return ServiceResult<bool>.FailureResult(
                            $"Belirlenen buluşma noktasına çok uzaksınız. Mesafe: {distance:N0} metre (Max: {delivery.MaxDistanceMeters} metre)");
                    }

                    delivery.LocationVerified = true;
                }

                // 7. Fotoğraf kontrolü (FAZ 2)
                if (delivery.PhotoRequired && string.IsNullOrEmpty(delivery.DeliveryPhotoUrl))
                {
                    delivery.DeliveryStatus = DeliveryStatus.WaitingForPhoto;
                    delivery.ActualDeliveryLatitude = currentLatitude;
                    delivery.ActualDeliveryLongitude = currentLongitude;
                    await deliveryNode.PutAsync(delivery);
                    return ServiceResult<bool>.FailureResult("Lütfen teslimat fotoğrafı yükleyin", 
                        new List<string> { "PHOTO_REQUIRED" });
                }

                // 8. Tüm kontroller geçti, teslimatı tamamla
                delivery.IsUsed = true;
                delivery.UsedAt = DateTime.UtcNow;
                delivery.Status = DeliveryStatus.Completed;
                delivery.DeliveryStatus = DeliveryStatus.Completed;
                delivery.ActualDeliveryLatitude = currentLatitude;
                delivery.ActualDeliveryLongitude = currentLongitude;
                await deliveryNode.PutAsync(delivery);

                // Transaction'ı güncelle
                var transaction = await _firebaseClient.Child(Constants.TransactionsCollection).Child(delivery.TransactionId).OnceSingleAsync<Transaction>();
                if (transaction != null)
                {
                    var allCodesResult = await GetQRCodesForTransactionAsync(transaction.TransactionId);

                    if (allCodesResult.Success && allCodesResult.Data.All(c => c.IsUsed))
                    {
                        await MarkProductAsSold(transaction.ProductId);
                        if (!string.IsNullOrEmpty(transaction.OfferedProductId))
                        {
                            await MarkProductAsSold(transaction.OfferedProductId);
                        }

                        transaction.Status = TransactionStatus.Completed;
                        await _firebaseClient.Child(Constants.TransactionsCollection).Child(transaction.TransactionId).PutAsync(transaction);

                        if (transaction.Type == ProductType.Bagis)
                        {
                            await _userProfileService.AddPointsForAction(transaction.SellerId, UserAction.MakeDonation);
                            await _userProfileService.AddPointsForAction(transaction.BuyerId, UserAction.ReceiveDonation);
                        }
                        else
                        {
                            await _userProfileService.AddPointsForAction(transaction.SellerId, UserAction.CompleteTransaction);
                            await _userProfileService.AddPointsForAction(transaction.BuyerId, UserAction.CompleteTransaction);
                        }
                    }
                }

                return ServiceResult<bool>.SuccessResult(true, "Teslimat başarıyla tamamlandı!");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Teslimat doğrulama hatası", ex.Message);
            }
        }

        /// <summary>
        /// QR kod süresini uzatır (1 kez, max 30 dakika)
        /// </summary>
        public async Task<ServiceResult<DateTime>> ExtendQRCodeValidityAsync(string qrCodeId, int additionalMinutes)
        {
            try
            {
                if (additionalMinutes <= 0 || additionalMinutes > MaxExtensionMinutes)
                {
                    return ServiceResult<DateTime>.FailureResult($"Süre uzatma 1-{MaxExtensionMinutes} dakika arasında olmalıdır.");
                }

                var deliveryNode = _firebaseClient.Child(QRCodesCollection).Child(qrCodeId);
                var delivery = await deliveryNode.OnceSingleAsync<DeliveryQRCode>();

                if (delivery == null)
                {
                    return ServiceResult<DateTime>.FailureResult("QR kod bulunamadı.");
                }

                if (delivery.IsUsed)
                {
                    return ServiceResult<DateTime>.FailureResult("Kullanılmış QR kod süre uzatılamaz.");
                }

                if (delivery.HasBeenExtended)
                {
                    return ServiceResult<DateTime>.FailureResult("QR kod süresi daha önce uzatılmış. Sadece 1 kez uzatılabilir.");
                }

                if (delivery.DeliveryStatus == DeliveryStatus.Cancelled)
                {
                    return ServiceResult<DateTime>.FailureResult("İptal edilmiş QR kod süre uzatılamaz.");
                }

                // Süreyi uzat
                delivery.ExpiresAt = delivery.ExpiresAt.AddMinutes(additionalMinutes);
                delivery.HasBeenExtended = true;
                await deliveryNode.PutAsync(delivery);

                return ServiceResult<DateTime>.SuccessResult(delivery.ExpiresAt, $"Süre {additionalMinutes} dakika uzatıldı.");
            }
            catch (Exception ex)
            {
                return ServiceResult<DateTime>.FailureResult("Süre uzatma hatası", ex.Message);
            }
        }

        /// <summary>
        /// QR kodu iptal eder
        /// </summary>
        public async Task<ServiceResult<bool>> CancelDeliveryQRCodeAsync(string qrCodeId, string userId, string reason)
        {
            try
            {
                var deliveryNode = _firebaseClient.Child(QRCodesCollection).Child(qrCodeId);
                var delivery = await deliveryNode.OnceSingleAsync<DeliveryQRCode>();

                if (delivery == null)
                {
                    return ServiceResult<bool>.FailureResult("QR kod bulunamadı.");
                }

                if (delivery.IsUsed)
                {
                    return ServiceResult<bool>.FailureResult("Tamamlanmış teslimat iptal edilemez.");
                }

                // Yetki kontrolü: Sadece satıcı veya alıcı iptal edebilir
                if (delivery.SellerId != userId && delivery.BuyerId != userId)
                {
                    return ServiceResult<bool>.FailureResult("Bu teslimatı iptal etme yetkiniz yok.");
                }

                delivery.DeliveryStatus = DeliveryStatus.Cancelled;
                delivery.Status = DeliveryStatus.Cancelled;
                delivery.CancellationReason = reason;
                delivery.CancelledAt = DateTime.UtcNow;
                delivery.CancelledByUserId = userId;
                await deliveryNode.PutAsync(delivery);

                return ServiceResult<bool>.SuccessResult(true, "Teslimat iptal edildi.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("İptal hatası", ex.Message);
            }
        }

        #endregion

        #region Yardımcı Metodlar

        /// <summary>
        /// Güvenli QR kod verisi oluşturur
        /// </summary>
        private string GenerateSecureQRData()
        {
            return $"KP_{Guid.NewGuid():N}_{DateTime.UtcNow.Ticks}";
        }

        /// <summary>
        /// 6 haneli rastgele PIN oluşturur
        /// </summary>
        private string GeneratePin()
        {
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            var bytes = new byte[4];
            rng.GetBytes(bytes);
            var randomNumber = Math.Abs(BitConverter.ToInt32(bytes, 0)) % 900000 + 100000;
            return randomNumber.ToString();
        }

        /// <summary>
        /// Haversine formülü ile iki koordinat arasındaki mesafeyi metre cinsinden hesaplar
        /// </summary>
        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000; // Dünya'nın yarıçapı (metre)
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        /// <summary>
        /// Dereceyi radyana çevirir
        /// </summary>
        private double ToRadians(double degrees) => degrees * Math.PI / 180;

        private async Task MarkProductAsSold(string productId)
        {
            if (string.IsNullOrEmpty(productId)) return;
            var productNode = _firebaseClient.Child(Constants.ProductsCollection).Child(productId);
            var product = await productNode.OnceSingleAsync<Product>();
            if (product != null)
            {
                product.IsSold = true;
                product.SoldAt = DateTime.UtcNow;
                await productNode.PutAsync(product);
            }
        }

        #endregion

        #region Mevcut Metodlar

        public async Task<ServiceResult<DeliveryQRCode>> GenerateDeliveryQRCodeAsync(string transactionId, string productId, string productTitle, string sellerId, string buyerId)
        {
            try
            {
                var delivery = new DeliveryQRCode { TransactionId = transactionId, ProductId = productId, ProductTitle = productTitle, SellerId = sellerId, BuyerId = buyerId };
                delivery.QRCodeData = GenerateQRCodeData(delivery);
                await _firebaseClient.Child(QRCodesCollection).Child(delivery.QRCodeId).PutAsync(delivery);
                return ServiceResult<DeliveryQRCode>.SuccessResult(delivery, "QR kod oluşturuldu");
            }
            catch (Exception ex) { return ServiceResult<DeliveryQRCode>.FailureResult("QR kod oluşturulamadı", ex.Message); }
        }

        public async Task<ServiceResult<List<DeliveryQRCode>>> GetQRCodesForTransactionAsync(string transactionId)
        {
            try
            {
                var allCodes = await _firebaseClient.Child(QRCodesCollection).OnceAsync<DeliveryQRCode>();
                var qrCodes = allCodes.Select(q => q.Object).Where(q => q.TransactionId == transactionId).ToList();
                return ServiceResult<List<DeliveryQRCode>>.SuccessResult(qrCodes);
            }
            catch (Exception ex) { return ServiceResult<List<DeliveryQRCode>>.FailureResult("QR kodları alınamadı.", ex.Message); }
        }

        public string GenerateQRCodeData(DeliveryQRCode delivery) { return $"KAMPAY|{delivery.QRCodeId}|{delivery.ProductId}|{delivery.CreatedAt.Ticks}"; }

        public async Task<ServiceResult<DeliveryQRCode>> ValidateQRCodeAsync(string qrCodeData)
        {
            try
            {
                if (string.IsNullOrEmpty(qrCodeData) || !qrCodeData.StartsWith("KAMPAY|")) { return ServiceResult<DeliveryQRCode>.FailureResult("Geçersiz QR kod"); }
                var parts = qrCodeData.Split('|');
                if (parts.Length < 3) { return ServiceResult<DeliveryQRCode>.FailureResult("QR kod formatı hatalı"); }
                var qrCodeId = parts[1];
                var delivery = await _firebaseClient.Child(QRCodesCollection).Child(qrCodeId).OnceSingleAsync<DeliveryQRCode>();
                if (delivery == null) { return ServiceResult<DeliveryQRCode>.FailureResult("QR kod bulunamadı"); }
                if (delivery.IsUsed) { return ServiceResult<DeliveryQRCode>.FailureResult("QR kod daha önce kullanılmış"); }
                if (delivery.IsExpired) { return ServiceResult<DeliveryQRCode>.FailureResult("QR kodun süresi dolmuş"); }
                return ServiceResult<DeliveryQRCode>.SuccessResult(delivery, "QR kod geçerli");
            }
            catch (Exception ex) { return ServiceResult<DeliveryQRCode>.FailureResult("Doğrulama hatası", ex.Message); }
        }

        #endregion

        #region FAZ 2: Fotoğraf Yönetimi

        /// <summary>
        /// Teslimat fotoğrafı yükler
        /// </summary>
        public async Task<ServiceResult<string>> UploadDeliveryPhotoAsync(
            string qrCodeId, byte[] photoData, string userId)
        {
            try
            {
                var deliveryNode = _firebaseClient.Child(QRCodesCollection).Child(qrCodeId);
                var delivery = await deliveryNode.OnceSingleAsync<DeliveryQRCode>();

                if (delivery == null)
                {
                    return ServiceResult<string>.FailureResult("QR kod bulunamadı.");
                }

                // Yetki kontrolü: Sadece satıcı veya alıcı fotoğraf yükleyebilir
                if (delivery.SellerId != userId && delivery.BuyerId != userId)
                {
                    return ServiceResult<string>.FailureResult("Bu teslimat için fotoğraf yükleme yetkiniz yok.");
                }

                // Zaten fotoğraf yüklenmişse
                if (!string.IsNullOrEmpty(delivery.DeliveryPhotoUrl))
                {
                    return ServiceResult<string>.FailureResult("Bu teslimat için zaten fotoğraf yüklenmiş.");
                }

                // Fotoğrafı yükle
                var uploadResult = await _storageService.UploadDeliveryPhotoAsync(
                    photoData, delivery.TransactionId, qrCodeId, userId);

                if (!uploadResult.Success || uploadResult.Data == null)
                {
                    return ServiceResult<string>.FailureResult("Fotoğraf yüklenemedi", uploadResult.Message);
                }

                // QR kodu güncelle
                delivery.DeliveryPhotoUrl = uploadResult.Data.FullPhotoUrl;
                delivery.DeliveryPhotoThumbnailUrl = uploadResult.Data.ThumbnailUrl;
                delivery.PhotoUploadedAt = DateTime.UtcNow;
                delivery.PhotoSizeBytes = uploadResult.Data.FileSize;
                delivery.PhotoUploadedByUserId = userId;
                delivery.PhotoWidth = uploadResult.Data.Width;
                delivery.PhotoHeight = uploadResult.Data.Height;

                // Fotoğraf yüklendiyse ve WaitingForPhoto durumundaysa, teslimatı tamamla
                if (delivery.DeliveryStatus == DeliveryStatus.WaitingForPhoto)
                {
                    delivery.IsUsed = true;
                    delivery.UsedAt = DateTime.UtcNow;
                    delivery.Status = DeliveryStatus.Completed;
                    delivery.DeliveryStatus = DeliveryStatus.Completed;
                }

                await deliveryNode.PutAsync(delivery);

                // Transaction'ı kontrol et ve gerekirse güncelle
                var transaction = await _firebaseClient.Child(Constants.TransactionsCollection)
                    .Child(delivery.TransactionId).OnceSingleAsync<Transaction>();
                
                if (transaction != null && delivery.DeliveryStatus == DeliveryStatus.Completed)
                {
                    var allCodesResult = await GetQRCodesForTransactionAsync(transaction.TransactionId);

                    if (allCodesResult.Success && allCodesResult.Data.All(c => c.IsUsed))
                    {
                        await MarkProductAsSold(transaction.ProductId);
                        if (!string.IsNullOrEmpty(transaction.OfferedProductId))
                        {
                            await MarkProductAsSold(transaction.OfferedProductId);
                        }

                        transaction.Status = TransactionStatus.Completed;
                        await _firebaseClient.Child(Constants.TransactionsCollection)
                            .Child(transaction.TransactionId).PutAsync(transaction);

                        if (transaction.Type == ProductType.Bagis)
                        {
                            await _userProfileService.AddPointsForAction(transaction.SellerId, UserAction.MakeDonation);
                            await _userProfileService.AddPointsForAction(transaction.BuyerId, UserAction.ReceiveDonation);
                        }
                        else
                        {
                            await _userProfileService.AddPointsForAction(transaction.SellerId, UserAction.CompleteTransaction);
                            await _userProfileService.AddPointsForAction(transaction.BuyerId, UserAction.CompleteTransaction);
                        }
                    }
                }

                return ServiceResult<string>.SuccessResult(
                    uploadResult.Data.FullPhotoUrl, 
                    "Fotoğraf başarıyla yüklendi");
            }
            catch (Exception ex)
            {
                return ServiceResult<string>.FailureResult("Fotoğraf yükleme hatası", ex.Message);
            }
        }

        /// <summary>
        /// Fotoğraf gerekli mi kontrol eder
        /// </summary>
        public async Task<ServiceResult<bool>> IsPhotoRequiredAsync(string qrCodeId)
        {
            try
            {
                var delivery = await _firebaseClient.Child(QRCodesCollection)
                    .Child(qrCodeId).OnceSingleAsync<DeliveryQRCode>();

                if (delivery == null)
                {
                    return ServiceResult<bool>.FailureResult("QR kod bulunamadı.");
                }

                return ServiceResult<bool>.SuccessResult(delivery.PhotoRequired);
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Kontrol hatası", ex.Message);
            }
        }

        #endregion
    }
}