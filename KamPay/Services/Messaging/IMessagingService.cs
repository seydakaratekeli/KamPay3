using KamPay.Models;

namespace KamPay.Services.Messaging
{
    /// <summary>
    /// ✅ SOLID İYİLEŞTİRME: IMessagingService artık küçük interface'leri birleştiriyor
    /// - ISP: Query ve Command işlemleri ayrıldı
    /// - SRP: Her interface'in tek sorumluluğu var
    /// - LSP: Alt interface'lerden herhangi biri yerine kullanılabilir
    /// </summary>
    public interface IMessagingService : IMessageQueryService, IMessageCommandService
    {
        // ✅ Tüm metodlar alt interface'lerden geliyor
    }
}