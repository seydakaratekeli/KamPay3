using System;
using System.IO;
using System.Threading.Tasks;
using Firebase.Storage;
using KamPay.Helpers;
using KamPay.Models;

namespace KamPay.Services
{
    public interface IStorageService
    {
        Task<ServiceResult<string>> UploadProductImageAsync(string localPath, string productId, int imageIndex);
        Task<ServiceResult<string>> UploadProfileImageAsync(string localPath, string userId);
        Task<ServiceResult<bool>> DeleteImageAsync(string imageUrl);
        Task<long> GetFileSizeAsync(string localPath);
    }

   


}