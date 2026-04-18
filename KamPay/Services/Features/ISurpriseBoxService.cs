using KamPay.Models;
using System.Threading.Tasks;

namespace KamPay.Services
{
    public interface ISurpriseBoxService
    {
        // Bu arayüz artýk sadece bu tek metodu içermeli
        Task<ServiceResult<Product>> RedeemSurpriseBoxAsync(string userId);
    }
}