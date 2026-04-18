using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Helpers;
using KamPay.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KamPay.Services.QRCode
{
    public interface IQRCodeService
    {
        Task<ServiceResult<DeliveryQRCode>> GenerateDeliveryQRCodeAsync(string transactionId, string productId, string productTitle, string sellerId, string buyerId);
        Task<ServiceResult<DeliveryQRCode>> ValidateQRCodeAsync(string qrCodeData);
        Task<ServiceResult<bool>> CompleteDeliveryAsync(string qrCodeId);
        string GenerateQRCodeData(DeliveryQRCode delivery);
        Task<ServiceResult<List<DeliveryQRCode>>> GetQRCodesForTransactionAsync(string transactionId);

       
        /// Süre sınırlı ve konum doğrulamalı QR kod oluşturur
       
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

       
        /// QR kodu konum ve PIN doğrulaması ile tarar
       
        Task<ServiceResult<bool>> ScanQRCodeWithLocationAsync(
            string qrCodeId,
            double currentLatitude,
            double currentLongitude,
            string? verificationPin = null);

   
        /// QR kod süresini uzatır (1 kez, max 30 dakika)
       
        Task<ServiceResult<DateTime>> ExtendQRCodeValidityAsync(
            string qrCodeId,
            int additionalMinutes);

       
        /// QR kodu iptal eder
       
        Task<ServiceResult<bool>> CancelDeliveryQRCodeAsync(
            string qrCodeId,
            string userId,
            string reason);

       
        /// Teslimat fotoğrafı yükler ()
       
        Task<ServiceResult<string>> UploadDeliveryPhotoAsync(
            string qrCodeId, byte[] photoData, string userId);

       
        /// Fotoğraf gerekli mi kontrol eder ()
       
        Task<ServiceResult<bool>> IsPhotoRequiredAsync(string qrCodeId);
    }


}