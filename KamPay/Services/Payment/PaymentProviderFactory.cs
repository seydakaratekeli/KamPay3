using KamPay.Models;

namespace KamPay.Services.Payment;

/// <summary>
/// ? Ödeme sağlayıcı fabrikası - Open/Closed Principle uyumlu
/// Yeni provider eklerken sadece factory'ye kayıt yapılır, switch/case değişmez!
/// </summary>
public interface IPaymentProviderFactory
{
    /// <summary>
    /// Ödeme yöntemi string'ine göre doğru provider'ı döndürür
    /// </summary>
    IPaymentProvider GetProvider(string method);
    
    /// <summary>
    /// PaymentMethodType'a göre provider döndürür
    /// </summary>
    IPaymentProvider GetProvider(PaymentMethodType methodType);
    
    /// <summary>
    /// Tüm mevcut provider'ları listeler
    /// </summary>
    IEnumerable<IPaymentProvider> GetAllProviders();
}

/// <summary>
/// ? Factory implementasyonu - DI ile provider'ları alır
/// </summary>
public class PaymentProviderFactory : IPaymentProviderFactory
{
    private readonly IEnumerable<IPaymentProvider> _providers;
    
    /// <summary>
    /// Constructor - DI ile tüm IPaymentProvider implementasyonlarını alır
    /// </summary>
    public PaymentProviderFactory(IEnumerable<IPaymentProvider> providers)
    {
        _providers = providers ?? throw new ArgumentNullException(nameof(providers));
        
        KamPay.Helpers.AppLogger.DebugLog($"? PaymentProviderFactory: {_providers.Count()} provider kaydedildi");
        foreach (var provider in _providers)
        {
            KamPay.Helpers.AppLogger.DebugLog($"   - {provider.ProviderName} ({provider.MethodType})");
        }
    }
    
    public IPaymentProvider GetProvider(string method)
    {
        var methodType = ParseMethodString(method);
        return GetProvider(methodType);
    }
    
    public IPaymentProvider GetProvider(PaymentMethodType methodType)
    {
        var provider = _providers.FirstOrDefault(p => p.MethodType == methodType);
        
        if (provider == null)
        {
            throw new NotSupportedException(
                $"Ödeme yöntemi desteklenmiyor: {methodType}. " +
                $"Mevcut yöntemler: {string.Join(", ", _providers.Select(p => p.ProviderName))}");
        }
        
        return provider;
    }
    
    public IEnumerable<IPaymentProvider> GetAllProviders() => _providers;
    
    private static PaymentMethodType ParseMethodString(string method)
    {
        return method?.ToLower() switch
        {
            "cardsim" or "card" => PaymentMethodType.CardSim,
            "banktransfersim" or "eft" or "havale" or "transfer" => PaymentMethodType.BankTransferSim,
            "walletsim" or "wallet" => PaymentMethodType.WalletSim,
            _ => throw new ArgumentException($"Geçersiz ödeme yöntemi: {method}")
        };
    }
}

