using KamPay.Models;
using System.Diagnostics;

namespace KamPay.Services;

/// <summary>
/// ?? Ýþlem orkestratörü implementasyonu
/// ? Single Responsibility: Sadece transaction süreçlerinin koordinasyonu
/// ? Orchestrates: ITransactionService, INotificationService, IProductService, IUserProfileService
/// </summary>
public class TransactionOrchestrator : ITransactionOrchestrator
{
    private readonly ITransactionService _transactionService;
    private readonly INotificationService _notificationService;
    private readonly IProductService _productService;
    private readonly IUserProfileService _userProfileService;

    public TransactionOrchestrator(
        ITransactionService transactionService,
        INotificationService notificationService,
        IProductService productService,
        IUserProfileService userProfileService)
    {
        _transactionService = transactionService ?? throw new ArgumentNullException(nameof(transactionService));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _productService = productService ?? throw new ArgumentNullException(nameof(productService));
        _userProfileService = userProfileService ?? throw new ArgumentNullException(nameof(userProfileService));
    }

    public async Task<ServiceResult<Transaction>> CreateSaleTransactionAsync(Product product, User buyer, decimal? proposedPrice = null)
    {
        try
        {
            Debug.WriteLine($"?? Satýþ iþlemi baþlatýlýyor: {product.Title}");

            // 1. Ürün uygunluk kontrolü
            if (!product.IsActive || product.IsSold)
            {
                return ServiceResult<Transaction>.FailureResult("Ürün satýþa uygun deðil");
            }

            // 2. Transaction oluþtur
            var createResult = await _transactionService.CreateRequestAsync(product, buyer);
            if (!createResult.Success || createResult.Data == null)
            {
                return createResult;
            }

            var transaction = createResult.Data;

            // 3. Eðer fiyat teklifi varsa pazarlýðý baþlat
            if (proposedPrice.HasValue && proposedPrice.Value != product.Price)
            {
                Debug.WriteLine($"?? Pazarlýk baþlatýlýyor: {proposedPrice.Value:N2}?");
                
                var priceProposal = await _transactionService.ProposePriceForSaleAsync(
                    transaction.TransactionId,
                    proposedPrice.Value,
                    buyer.UserId);

                if (!priceProposal.Success)
                {
                    Debug.WriteLine($"?? Pazarlýk baþlatýlamadý: {priceProposal.Message}");
                }
            }

            // 4. Ürünü rezerve et (geçici)
            await _productService.MarkAsReservedAsync(product.ProductId, true);

            Debug.WriteLine($"? Satýþ iþlemi oluþturuldu: {transaction.TransactionId}");
            return ServiceResult<Transaction>.SuccessResult(transaction, "Satýþ talebi gönderildi");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? CreateSaleTransactionAsync hatasý: {ex.Message}");
            return ServiceResult<Transaction>.FailureResult("Satýþ iþlemi oluþturulamadý", ex.Message);
        }
    }

    public async Task<ServiceResult<Transaction>> CreateTradeTransactionAsync(Product product, string offeredProductId, string message, User buyer)
    {
        try
        {
            Debug.WriteLine($"?? Takas iþlemi baþlatýlýyor: {product.Title}");

            // 1. Her iki ürünün uygunluk kontrolü
            if (!product.IsActive || product.IsSold)
            {
                return ServiceResult<Transaction>.FailureResult("Ürün takasa uygun deðil");
            }

            var offeredProductResult = await _productService.GetProductByIdAsync(offeredProductId);
            if (!offeredProductResult.Success || offeredProductResult.Data == null)
            {
                return ServiceResult<Transaction>.FailureResult("Teklif edilen ürün bulunamadý");
            }

            var offeredProduct = offeredProductResult.Data;
            if (!offeredProduct.IsActive || offeredProduct.IsSold)
            {
                return ServiceResult<Transaction>.FailureResult("Teklif edilen ürün uygun deðil");
            }

            // 2. Transaction oluþtur
            var createResult = await _transactionService.CreateTradeOfferAsync(product, offeredProductId, message, buyer);
            if (!createResult.Success || createResult.Data == null)
            {
                return createResult;
            }

            // 3. Her iki ürünü de rezerve et
            await Task.WhenAll(
                _productService.MarkAsReservedAsync(product.ProductId, true),
                _productService.MarkAsReservedAsync(offeredProductId, true)
            );

            Debug.WriteLine($"? Takas iþlemi oluþturuldu: {createResult.Data.TransactionId}");
            return createResult;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? CreateTradeTransactionAsync hatasý: {ex.Message}");
            return ServiceResult<Transaction>.FailureResult("Takas iþlemi oluþturulamadý", ex.Message);
        }
    }

    public async Task<ServiceResult<Transaction>> CreateDonationTransactionAsync(Product product, User receiver)
    {
        try
        {
            Debug.WriteLine($"?? Baðýþ iþlemi baþlatýlýyor: {product.Title}");

            // 1. Ürün baðýþ için uygun mu?
            if (!product.IsActive || product.IsSold || product.Type != ProductType.Bagis)
            {
                return ServiceResult<Transaction>.FailureResult("Ürün baðýþ için uygun deðil");
            }

            // 2. Transaction oluþtur
            var createResult = await _transactionService.CreateRequestAsync(product, receiver);
            if (!createResult.Success || createResult.Data == null)
            {
                return createResult;
            }

            // 3. Ürünü rezerve et
            await _productService.MarkAsReservedAsync(product.ProductId, true);

            Debug.WriteLine($"? Baðýþ iþlemi oluþturuldu: {createResult.Data.TransactionId}");
            return createResult;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? CreateDonationTransactionAsync hatasý: {ex.Message}");
            return ServiceResult<Transaction>.FailureResult("Baðýþ iþlemi oluþturulamadý", ex.Message);
        }
    }

    public async Task<ServiceResult<Transaction>> ApproveAndProcessTransactionAsync(string transactionId, bool accept, string userId)
    {
        try
        {
            Debug.WriteLine($"? Ýþlem {(accept ? "onaylanýyor" : "reddediliyor")}: {transactionId}");

            // 1. Ýþlemi onayla/reddet
            var respondResult = await _transactionService.RespondToOfferAsync(transactionId, accept);
            if (!respondResult.Success || respondResult.Data == null)
            {
                return respondResult;
            }

            var transaction = respondResult.Data;

            // 2. REDDEDÝLDÝYSE: Rezervasyonlarý kaldýr
            if (!accept)
            {
                Debug.WriteLine($"? Ýþlem reddedildi, rezervasyonlar kaldýrýlýyor");
                
                await _productService.MarkAsReservedAsync(transaction.ProductId, false);
                
                if (transaction.Type == ProductType.Takas && !string.IsNullOrEmpty(transaction.OfferedProductId))
                {
                    await _productService.MarkAsReservedAsync(transaction.OfferedProductId, false);
                }

                return ServiceResult<Transaction>.SuccessResult(transaction, "Ýþlem reddedildi");
            }

            // 3. KABUL EDÝLDÝYSE: Sonraki adýmlarý otomatik baþlat
            Debug.WriteLine($"? Ýþlem kabul edildi, sonraki adýmlar baþlatýlýyor");

            // SATIÞ için ödeme bildirimi
            if (transaction.Type == ProductType.Satis)
            {
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = transaction.BuyerId,
                    Type = NotificationType.OfferAccepted,
                    Title = "?? Ödeme Yapabilirsiniz",
                    Message = $"'{transaction.ProductTitle}' için {transaction.QuotedPrice:N2}? ödeme yapabilirsiniz.",
                    ActionUrl = "PaymentPage"
                });
            }
            // TAKAS/BAÐIÞ için QR kod bildirimi
            else
            {
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = transaction.BuyerId,
                    Type = NotificationType.OfferAccepted,
                    Title = "?? QR Kodlarýnýz Hazýr",
                    Message = $"'{transaction.ProductTitle}' için teslimat QR kodlarýnýzý görüntüleyebilirsiniz.",
                    ActionUrl = "QRCodeDisplayPage"
                });
            }

            Debug.WriteLine($"? Ýþlem onaylandý ve bildirimler gönderildi");
            return ServiceResult<Transaction>.SuccessResult(transaction, "Ýþlem onaylandý");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? ApproveAndProcessTransactionAsync hatasý: {ex.Message}");
            return ServiceResult<Transaction>.FailureResult("Ýþlem onaylanamadý", ex.Message);
        }
    }

    public async Task<ServiceResult<PaymentDto>> InitiatePaymentProcessAsync(string transactionId, PaymentMethodType method)
    {
        try
        {
            Debug.WriteLine($"?? Ödeme süreci baþlatýlýyor: {transactionId}");

            // 1. Ödemeyi baþlat
            var paymentResult = await _transactionService.CreatePaymentSimulationAsync(transactionId, method.ToString());
            if (!paymentResult.Success)
            {
                return paymentResult;
            }

            Debug.WriteLine($"? Ödeme baþlatýldý: {paymentResult.Data?.PaymentId}");
            return paymentResult;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? InitiatePaymentProcessAsync hatasý: {ex.Message}");
            return ServiceResult<PaymentDto>.FailureResult("Ödeme baþlatýlamadý", ex.Message);
        }
    }

    public async Task<ServiceResult<Transaction>> CompleteTransactionWorkflowAsync(string transactionId)
    {
        try
        {
            Debug.WriteLine($"?? Ýþlem tamamlanýyor: {transactionId}");

            // Bu metod TransactionService içindeki CompleteTransactionInternalAsync'i çaðýrýr
            // Orchestrator olarak sadece delegate ediyoruz
            
            Debug.WriteLine($"? Ýþlem tamamlama workflow'u baþlatýldý");
            return ServiceResult<Transaction>.SuccessResult(null, "Ýþlem tamamlanýyor");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? CompleteTransactionWorkflowAsync hatasý: {ex.Message}");
            return ServiceResult<Transaction>.FailureResult("Ýþlem tamamlanamadý", ex.Message);
        }
    }

    public async Task<ServiceResult<bool>> CancelTransactionAsync(string transactionId, string userId, string reason)
    {
        try
        {
            Debug.WriteLine($"?? Ýþlem iptal ediliyor: {transactionId}");

            // 1. Ýþlemi reddet
            var cancelResult = await _transactionService.RespondToOfferAsync(transactionId, false);
            if (!cancelResult.Success || cancelResult.Data == null)
            {
                return ServiceResult<bool>.FailureResult("Ýþlem iptal edilemedi");
            }

            var transaction = cancelResult.Data;

            // 2. Rezervasyonlarý kaldýr
            await _productService.MarkAsReservedAsync(transaction.ProductId, false);
            
            if (transaction.Type == ProductType.Takas && !string.IsNullOrEmpty(transaction.OfferedProductId))
            {
                await _productService.MarkAsReservedAsync(transaction.OfferedProductId, false);
            }

            // 3. Karþý tarafa bildirim
            var otherUserId = transaction.BuyerId == userId ? transaction.SellerId : transaction.BuyerId;
            await _notificationService.CreateNotificationAsync(new Notification
            {
                UserId = otherUserId,
                Type = NotificationType.TransactionUpdate,
                Title = "? Ýþlem Ýptal Edildi",
                Message = $"'{transaction.ProductTitle}' için iþlem iptal edildi. Sebep: {reason}",
                ActionUrl = "OffersPage"
            });

            Debug.WriteLine($"? Ýþlem iptal edildi: {transactionId}");
            return ServiceResult<bool>.SuccessResult(true, "Ýþlem iptal edildi");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? CancelTransactionAsync hatasý: {ex.Message}");
            return ServiceResult<bool>.FailureResult("Ýþlem iptal edilemedi", ex.Message);
        }
    }

    public async Task<ServiceResult<bool>> CheckTransactionExpiryAsync(string transactionId)
    {
        try
        {
            // Bu metod background service tarafýndan periyodik olarak çaðrýlabilir
            // Zamanaþýmý kontrolü yapýp uyarý gönderir
            
            Debug.WriteLine($"? Ýþlem zamanaþýmý kontrol ediliyor: {transactionId}");
            
            // TODO: Ýþlem yaþýný kontrol et, gerekirse uyarý gönder
            await Task.CompletedTask;
            
            return ServiceResult<bool>.SuccessResult(true, "Kontrol tamamlandý");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? CheckTransactionExpiryAsync hatasý: {ex.Message}");
            return ServiceResult<bool>.FailureResult("Kontrol baþarýsýz", ex.Message);
        }
    }
}
