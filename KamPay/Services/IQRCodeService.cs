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
    }

    public class FirebaseQRCodeService : IQRCodeService
    {
        private readonly FirebaseClient _firebaseClient;
        private readonly IUserProfileService _userProfileService;
        private const string QRCodesCollection = "delivery_qrcodes";

        public FirebaseQRCodeService(IUserProfileService userProfileService)
        {
            _firebaseClient = new FirebaseClient(Constants.FirebaseRealtimeDbUrl);
            _userProfileService = userProfileService; // Gelen servisi kendi alanýmýza atýyoruz.
        }

        public async Task<ServiceResult<bool>> CompleteDeliveryAsync(string qrCodeId)
        {
            try
            {
                var deliveryNode = _firebaseClient.Child(QRCodesCollection).Child(qrCodeId);
                var delivery = await deliveryNode.OnceSingleAsync<DeliveryQRCode>();

                if (delivery == null || delivery.IsUsed)
                {
                    return ServiceResult<bool>.FailureResult("Teslimat bulunamadý veya zaten tamamlanmýþ.");
                }

                delivery.IsUsed = true;
                delivery.UsedAt = DateTime.UtcNow;
                delivery.Status = DeliveryStatus.Completed;
                await deliveryNode.PutAsync(delivery);

                var transaction = await _firebaseClient.Child(Constants.TransactionsCollection).Child(delivery.TransactionId).OnceSingleAsync<Transaction>();
                if (transaction != null)
                {
                    var allCodesResult = await GetQRCodesForTransactionAsync(transaction.TransactionId);

                    // YENÝ: Ýþlemin tamamlanma kontrolü ve puan ekleme
                    if (allCodesResult.Success && allCodesResult.Data.All(c => c.IsUsed))
                    {
                        // TÜM TESLÝMATLAR BÝTTÝYSE:
                        await MarkProductAsSold(transaction.ProductId);
                        if (!string.IsNullOrEmpty(transaction.OfferedProductId))
                        {
                            await MarkProductAsSold(transaction.OfferedProductId);
                        }

                        transaction.Status = TransactionStatus.Completed;
                        await _firebaseClient.Child(Constants.TransactionsCollection).Child(transaction.TransactionId).PutAsync(transaction);

                        // PUANLARI EKLE
                        if (transaction.Type == ProductType.Bagis)
                        {
                            // Baðýþý yapan kiþi (Seller) ve alan kiþi (Buyer) için puanlar
                            await _userProfileService.AddPointsForAction(transaction.SellerId, UserAction.MakeDonation);
                            await _userProfileService.AddPointsForAction(transaction.BuyerId, UserAction.ReceiveDonation);
                        }
                        else // Satýþ veya Takas ise
                        {
                            await _userProfileService.AddPointsForAction(transaction.SellerId, UserAction.CompleteTransaction);
                            await _userProfileService.AddPointsForAction(transaction.BuyerId, UserAction.CompleteTransaction);
                        }
                    }
                }

                return ServiceResult<bool>.SuccessResult(true, "Teslimat tamamlandý!");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Teslimat tamamlanamadý", ex.Message);
            }
        }
       

        #region Diðer Metotlar
        private async Task MarkProductAsSold(string productId)
        {
            if (string.IsNullOrEmpty(productId)) return;
            var productNode = _firebaseClient.Child(Constants.ProductsCollection).Child(productId);
            var product = await productNode.OnceSingleAsync<Product>();
            if (product != null)
            {
                product.IsSold = true;
                product.SoldAt = DateTime.UtcNow;
               // product.IsActive = false;
                await productNode.PutAsync(product);
            }
        }
        public async Task<ServiceResult<DeliveryQRCode>> GenerateDeliveryQRCodeAsync(string transactionId, string productId, string productTitle, string sellerId, string buyerId)
        {
            try
            {
                var delivery = new DeliveryQRCode { TransactionId = transactionId, ProductId = productId, ProductTitle = productTitle, SellerId = sellerId, BuyerId = buyerId };
                delivery.QRCodeData = GenerateQRCodeData(delivery);
                await _firebaseClient.Child(QRCodesCollection).Child(delivery.QRCodeId).PutAsync(delivery);
                return ServiceResult<DeliveryQRCode>.SuccessResult(delivery, "QR kod oluþturuldu");
            }
            catch (Exception ex) { return ServiceResult<DeliveryQRCode>.FailureResult("QR kod oluþturulamadý", ex.Message); }
        }
        public async Task<ServiceResult<List<DeliveryQRCode>>> GetQRCodesForTransactionAsync(string transactionId)
        {
            try
            {
                var allCodes = await _firebaseClient.Child(QRCodesCollection).OnceAsync<DeliveryQRCode>();
                var qrCodes = allCodes.Select(q => q.Object).Where(q => q.TransactionId == transactionId).ToList();
                return ServiceResult<List<DeliveryQRCode>>.SuccessResult(qrCodes);
            }
            catch (Exception ex) { return ServiceResult<List<DeliveryQRCode>>.FailureResult("QR kodlarý alýnamadý.", ex.Message); }
        }
        public string GenerateQRCodeData(DeliveryQRCode delivery) { return $"KAMPAY|{delivery.QRCodeId}|{delivery.ProductId}|{delivery.CreatedAt.Ticks}"; }
        public async Task<ServiceResult<DeliveryQRCode>> ValidateQRCodeAsync(string qrCodeData)
        {
            try
            {
                if (string.IsNullOrEmpty(qrCodeData) || !qrCodeData.StartsWith("KAMPAY|")) { return ServiceResult<DeliveryQRCode>.FailureResult("Geçersiz QR kod"); }
                var parts = qrCodeData.Split('|');
                if (parts.Length < 3) { return ServiceResult<DeliveryQRCode>.FailureResult("QR kod formatý hatalý"); }
                var qrCodeId = parts[1];
                var delivery = await _firebaseClient.Child(QRCodesCollection).Child(qrCodeId).OnceSingleAsync<DeliveryQRCode>();
                if (delivery == null) { return ServiceResult<DeliveryQRCode>.FailureResult("QR kod bulunamadý"); }
                if (delivery.IsUsed) { return ServiceResult<DeliveryQRCode>.FailureResult("QR kod daha önce kullanýlmýþ"); }
                if (delivery.IsExpired) { return ServiceResult<DeliveryQRCode>.FailureResult("QR kodun süresi dolmuþ"); }
                return ServiceResult<DeliveryQRCode>.SuccessResult(delivery, "QR kod geçerli");
            }
            catch (Exception ex) { return ServiceResult<DeliveryQRCode>.FailureResult("Doðrulama hatasý", ex.Message); }
        }
        #endregion
    }
}