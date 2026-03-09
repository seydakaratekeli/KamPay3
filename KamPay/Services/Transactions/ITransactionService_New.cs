using KamPay.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KamPay.Services.Transactions
{
    /// <summary>
    /// ? ISP: Transaction Ana Interface
    /// BACKWARD COMPATIBILITY için tüm alt interface'leri birleþtirir
    /// 
    /// YENÝ KOD: Sadece ihtiyacýnýz olan küçük interface'i kullanýn:
    /// - Sadece okuma: ITransactionQueryService
    /// - Sadece oluþturma: ITransactionCreationService
    /// - Sadece durum deðiþtirme: ITransactionStatusService
    /// - Sadece SATIÞ pazarlýk: ISaleNegotiationService
    /// - Sadece TAKAS pazarlýk: ITradeNegotiationService
    /// - Sadece ortak pazarlýk: INegotiationCommonService
    /// - Sadece ödeme: ITransactionPaymentService
    /// 
    /// ESKÝ KOD: Bu interface'i kullanmaya devam edebilir (breaking change yok)
    /// </summary>
    public interface ITransactionService :
        ITransactionQueryService,
        ITransactionCreationService,
        ITransactionStatusService,
        ISaleNegotiationService,
        ITradeNegotiationService,
        INegotiationCommonService,
        ITransactionPaymentService
    {
        // ? Tüm metodlar alt interface'lerden geliyor
        // Bu interface sadece "marker" görevi görüyor - backward compatibility için
    }
}
