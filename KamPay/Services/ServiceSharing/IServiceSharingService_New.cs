using KamPay.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KamPay.Services.ServiceSharing
{
    /// <summary>
    /// ? ISP: Hizmet Paylaþýmý Ana Interface
    /// BACKWARD COMPATIBILITY için tüm alt interface'leri birleþtirir
    /// YENÝ KOD: Sadece ihtiyacýnýz olan küçük interface'i kullanýn
    /// ESKÝ KOD: Bu interface'i kullanmaya devam edebilir (breaking change yok)
    /// </summary>
    public interface IServiceSharingService : 
        IServiceOfferQueryService,
        IServiceOfferCommandService,
        ICustomerRequestQueryService,
        ICustomerRequestCommandService,
        IProviderProposalQueryService,
        IProviderProposalCommandService,
        IServiceRequestManagementService,
        IServiceNegotiationService,
        IServicePaymentService
    {
        // ? Tüm metodlar alt interface'lerden geliyor
        // Bu interface sadece "marker" görevi görüyor - backward compatibility için
    }
}
