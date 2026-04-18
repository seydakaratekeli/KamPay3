using KamPay.Models;
using System.Collections.Generic;
using System.Linq;

namespace KamPay.Services
{
    /// <summary>
    /// ? OCP (Open/Closed Principle): Hizmet türü handler'ları için factory
    /// Yeni hizmet türü eklerken sadece yeni handler eklenir, factory değişmez
    /// </summary>
    public interface IServiceTypeHandlerFactory
    {
        IServiceTypeHandler GetHandler(ServiceCategory category);
        IEnumerable<IServiceTypeHandler> GetAllHandlers();
    }
    
    /// <summary>
    /// ? Concrete Factory Implementation
    /// </summary>
    public class ServiceTypeHandlerFactory : IServiceTypeHandlerFactory
    {
        private readonly IEnumerable<IServiceTypeHandler> _handlers;
        
        public ServiceTypeHandlerFactory(IEnumerable<IServiceTypeHandler> handlers)
        {
            _handlers = handlers ?? throw new System.ArgumentNullException(nameof(handlers));
            KamPay.Helpers.AppLogger.DebugLog($"? ServiceTypeHandlerFactory oluşturuldu - {_handlers.Count()} handler kayıtlı");
        }
        
        public IServiceTypeHandler GetHandler(ServiceCategory category)
        {
            var handler = _handlers.FirstOrDefault(h => h.SupportedCategory == category);
            
            if (handler == null)
            {
                return new DefaultServiceTypeHandler();
            }
            
            return handler;
        }
        
        public IEnumerable<IServiceTypeHandler> GetAllHandlers()
        {
            return _handlers;
        }
    }
    
    /// <summary>
    /// ? Varsayılan handler - özel handler olmayan kategoriler için
    /// </summary>
    public class DefaultServiceTypeHandler : IServiceTypeHandler
    {
        public ServiceCategory SupportedCategory => ServiceCategory.Other;
        
        public async Task<ServiceResult<bool>> ValidateServiceOfferAsync(ServiceOffer offer)
        {
            // Temel validasyon - model'de olan property'leri kullan
            if (string.IsNullOrWhiteSpace(offer.Title))
                return ServiceResult<bool>.FailureResult("Başlık boş olamaz");
            
            if (string.IsNullOrWhiteSpace(offer.Description))
                return ServiceResult<bool>.FailureResult("Açıklama boş olamaz");
            
            // ServiceOffer'da Price property'si var
            if (offer.Price < 0)
                return ServiceResult<bool>.FailureResult("Fiyat negatif olamaz");
            
            return await Task.FromResult(ServiceResult<bool>.SuccessResult(true));
        }
        
        public async Task<ServiceResult<bool>> OnServiceCompletedAsync(ServiceRequest request)
        {
            // Varsayılan davranış - özel işlem yok
            return await Task.FromResult(ServiceResult<bool>.SuccessResult(true));
        }
    }
}

