using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Models;
using KamPay.Services.Shared; // âœ… FAZ2: OtpGenerator
using KamPay.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using KamPay.Services.Messaging;

namespace KamPay.Services.ServiceSharing
{
    public class ServiceRequestService
    {
        private readonly FirebaseClient _firebaseClient;
        private readonly INotificationService _notificationService;
        private readonly IUserProfileService _userProfileService;
        private readonly IMessagingService _messagingService;

        private readonly ICustomerRequestManager _customerRequestManager; // âœ… EKLE (satÄ±r 17)

        public ServiceRequestService(
            FirebaseClient firebaseClient,
            INotificationService notificationService,
            IUserProfileService userProfileService,
            IMessagingService messagingService,
            ICustomerRequestManager customerRequestManager) // âœ… YENÄ° PARAMETRE
        {
            _firebaseClient = firebaseClient ?? throw new ArgumentNullException(nameof(firebaseClient));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _userProfileService = userProfileService ?? throw new ArgumentNullException(nameof(userProfileService));
            _messagingService = messagingService ?? throw new ArgumentNullException(nameof(messagingService));
            _customerRequestManager = customerRequestManager ?? throw new ArgumentNullException(nameof(customerRequestManager)); // âœ… EKLE

            KamPay.Helpers.AppLogger.DebugLog("âœ… ServiceRequestService oluÅŸturuldu (KoordinatÃ¶r pattern ile)");
        }



        private string GenerateOtp() => OtpGenerator.GenerateSecureOtp(); // âœ… FAZ2: new Random() â†’ kriptografik OTP
        private string GenerateBankReference() => $"BTX-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString().Substring(0, 6)}";


        // Constructor to inject all required services
        
        

        // ServiceOfferService'e taÅŸÄ±ndÄ±.

        public async Task<ServiceResult<ServiceRequest>> RequestServiceAsync(ServiceOffer offer, User requester, string message)
        {
            try
            {
                if (offer == null || requester == null)
                    return ServiceResult<ServiceRequest>.FailureResult("Hizmet veya kullanÄ±cÄ± bilgisi eksik.");

                // ?? Yeni ServiceRequest nesnesi oluÅŸturuluyor
                var request = new ServiceRequest
                {
                    RequestId = Guid.NewGuid().ToString(),
                    ServiceId = offer.ServiceId,            // Hizmet kimliÄŸi
                    ServiceTitle = offer.Title,
                    ProviderId = offer.ProviderId,
                    ProviderName = offer.ProviderName, // âœ… KRÄ°TÄ°K EKSÄ°K: Bu satÄ±rÄ± ekle
                    RequesterId = requester.UserId,
                    RequesterName = requester.FullName,
                    Message = message,
                    Status = ServiceRequestStatus.Pending,
                    RequestedAt = DateTime.UtcNow,
                
                    QuotedPrice = offer.Price,              // Hizmetin o anki fiyatÄ±
                    Price = offer.Price,                    // UI veya raporlama iÃ§in de saklÄ±yoruz
                    TimeCreditValue = offer.TimeCredits,    // Kredi bilgisi (eski sistemle uyumlu)
                    PaymentStatus = ServicePaymentStatus.None,
                    PaymentMethod = PaymentMethodType.None,
                    Currency = "TRY"
                };

                // ?? Firebaseâ€™e kaydet
                await _firebaseClient
                    .Child(Constants.ServiceRequestsCollection)
                    .Child(request.RequestId)
                    .PutAsync(request);

                return ServiceResult<ServiceRequest>.SuccessResult(request, "Hizmet talebiniz baÅŸarÄ±yla oluÅŸturuldu.");
            }
            catch (Exception ex)
            {
                return ServiceResult<ServiceRequest>.FailureResult($"Talep oluÅŸturulamadÄ±: {ex.Message}");
            }
        }

        // FirebaseServiceSharingService.cs iÃ§ine eklenecek
        public async Task<ServiceResult<bool>> CompleteServiceRequestAsync(string transactionId, string providerId)
        {
            try
            {
                // 1. AÄŸ ve Yetki Kontrolleri
                if (!NetworkHelper.HasInternetConnection())
                    return ServiceResult<bool>.FailureResult("Ä°nternet baÄŸlantÄ±sÄ± yok.");

                var transactionNode = _firebaseClient.Child(Constants.TransactionsCollection).Child(transactionId);
                var transaction = await transactionNode.OnceSingleAsync<Transaction>();

                if (transaction == null || transaction.SellerId != providerId)
                    return ServiceResult<bool>.FailureResult("Ä°ÅŸlem bulunamadÄ± veya yetkiniz yok.");

                // 2. Durum KontrolÃ¼: Ã–deme gerekiyorsa Ã¶denmiÅŸ olmalÄ±
                if (transaction.Price > 0 && transaction.PaymentStatus != PaymentStatus.Paid)
                    return ServiceResult<bool>.FailureResult("Hizmetin Ã¶demesi henÃ¼z tamamlanmamÄ±ÅŸ.");

                // 3. Ä°ÅŸlemi Tamamla (Transaction durumunu Completed yapar, puanlarÄ± verir ve bildirim gÃ¶nderir)
                // Not: FirebaseTransactionService iÃ§indeki CompleteTransactionInternalAsync mantÄ±ÄŸÄ±na benzer 
                // bir Ã§aÄŸrÄ± yapmalÄ± veya TransactionService Ã¼zerinden bu akÄ±ÅŸÄ± tetiklemelisiniz.
                transaction.Status = TransactionStatus.Completed;
                transaction.UpdatedAt = DateTime.UtcNow;
                await transactionNode.PutAsync(transaction);

                // 4. KullanÄ±cÄ±ya Puan Ver (Hizmet SaÄŸlayÄ±cÄ±ya)
                await _userProfileService.AddPointsForAction(providerId, UserAction.ProvideService);

                // 5. Alan KiÅŸiye Bildirim GÃ¶nder
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = transaction.BuyerId,
                    Title = "Hizmet TamamlandÄ±",
                    Message = $"{transaction.SellerName}, '{transaction.ProductTitle}' hizmetini tamamladÄ±ÄŸÄ±nÄ± bildirdi.",
                    Type = NotificationType.ServiceCompleted,
                    ActionUrl = nameof(Views.ServiceRequestsPage)
                });

                return ServiceResult<bool>.SuccessResult(true, "Hizmet baÅŸarÄ±yla tamamlandÄ±.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Hata oluÅŸtu.", NetworkHelper.GetUserFriendlyErrorMessage(ex));
            }
        }
        public async Task<ServiceResult<bool>> CompleteRequestAsync(string requestId, string currentUserId)
        {
            try
            {
                var requestNode = _firebaseClient.Child(Constants.ServiceRequestsCollection).Child(requestId);
                var request = await requestNode.OnceSingleAsync<ServiceRequest>();

                if (request == null) return ServiceResult<bool>.FailureResult("Talep bulunamadÄ±.");

                // Sadece hizmeti talep eden kiÅŸi tamamlandÄ± olarak iÅŸaretleyebilir
                if (request.RequesterId != currentUserId)
                    return ServiceResult<bool>.FailureResult("Bu iÅŸlemi yapmaya yetkiniz yok.");

                if (request.Status != ServiceRequestStatus.Accepted)
                    return ServiceResult<bool>.FailureResult("Bu talep henÃ¼z onaylanmamÄ±ÅŸ veya zaten tamamlanmÄ±ÅŸ.");

                // 1. Kredi transferini yap
                var transferResult = await _userProfileService.TransferTimeCreditsAsync(
                    request.RequesterId,
                    request.ProviderId,
                    request.TimeCreditValue,
                    $"Hizmet tamamlandÄ±: {request.ServiceTitle}"
                );

                if (!transferResult.Success)
                {
                    return ServiceResult<bool>.FailureResult($"Kredi transferi baÅŸarÄ±sÄ±z: {transferResult.Message}");
                }

                // 2. Talebin durumunu gÃ¼ncelle
                request.Status = ServiceRequestStatus.Completed;
                await requestNode.PutAsync(request);

                // 3. Hizmeti sunan kiÅŸiye bildirim gÃ¶nder
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = request.ProviderId,
                    Title = "Hizmet TamamlandÄ± ve Kredi KazandÄ±n!",
                    Message = $"{request.RequesterName}, '{request.ServiceTitle}' hizmetini tamamlandÄ± olarak iÅŸaretledi. HesabÄ±na {request.TimeCreditValue} saat kredi eklendi."
                });

                return ServiceResult<bool>.SuccessResult(true, "Hizmet baÅŸarÄ±yla tamamlandÄ±.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Ä°ÅŸlem sÄ±rasÄ±nda hata oluÅŸtu.", ex.Message);
            }
        }


        public async Task<ServiceResult<(List<ServiceRequest> Incoming, List<ServiceRequest> Outgoing)>> GetMyServiceRequestsAsync(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    return ServiceResult<(List<ServiceRequest> Incoming, List<ServiceRequest> Outgoing)>.FailureResult("KullanÄ±cÄ± ID'si bulunamadÄ±.");
                }

                var incomingRequestsTask = _firebaseClient
                    .Child(Constants.ServiceRequestsCollection)
                    .OrderBy("ProviderId")
                    .EqualTo(userId)
                    .OnceAsync<ServiceRequest>();

                var outgoingRequestsTask = _firebaseClient
                    .Child(Constants.ServiceRequestsCollection)
                    .OrderBy("RequesterId")
                    .EqualTo(userId)
                    .OnceAsync<ServiceRequest>();

                await Task.WhenAll(incomingRequestsTask, outgoingRequestsTask);

                // HATA 2 ve 3 DÃœZELTMESÄ°: 'CreatedAt' yerine 'RequestedAt' kullanÄ±lÄ±yor.
                var incoming = incomingRequestsTask.Result
                    .Select(item => { item.Object.RequestId = item.Key; return item.Object; })
                    .OrderByDescending(r => r.RequestedAt)
                    .ToList();

                var outgoing = outgoingRequestsTask.Result
                    .Select(item => { item.Object.RequestId = item.Key; return item.Object; })
                    .OrderByDescending(r => r.RequestedAt)
                    .ToList();

                // HATA 1 DÃœZELTMESÄ°: Tuple element names kullanÄ±lÄ±yor
                return ServiceResult<(List<ServiceRequest> Incoming, List<ServiceRequest> Outgoing)>.SuccessResult((incoming, outgoing));
            }
            catch (Exception ex)
            {
                return ServiceResult<(List<ServiceRequest> Incoming, List<ServiceRequest> Outgoing)>.FailureResult("Talepler getirilirken bir hata oluÅŸtu.", ex.Message);
            }
        }


        // Ã–deme simÃ¼lasyon metodlarÄ± (CreatePaymentSimulationAsync, vb.) TransactionPaymentService iÃ§ine taÅŸÄ±ndÄ±ÄŸÄ± iÃ§in silindi.

        // Bu metot ÅŸu an kullanÄ±lmÄ±yor ama ileride talepleri yanÄ±tlarken gerekecek.
        public async Task<ServiceResult<bool>> RespondToRequestAsync(string requestId, bool accept)
        {
            try
            {
                var requestNode = _firebaseClient.Child(Constants.ServiceRequestsCollection).Child(requestId);
                var request = await requestNode.OnceSingleAsync<ServiceRequest>();

                if (request == null)
                {
                    return ServiceResult<bool>.FailureResult("Talep bulunamadÄ±.");
                }

                request.Status = accept ? ServiceRequestStatus.Accepted : ServiceRequestStatus.Declined;
                await requestNode.PutAsync(request);

                // Talebi gÃ¶nderen kiÅŸiye bildirim gÃ¶nder
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = request.RequesterId,
                    Type = accept ? NotificationType.OfferAccepted : NotificationType.OfferRejected,
                    Title = accept ? "Hizmet Talebin OnaylandÄ±!" : "Hizmet Talebin Reddedildi",
                    Message = $"'{request.ServiceTitle}' hizmeti iÃ§in talebin {(accept ? "kabul edildi." : "reddedildi.")}",
                    ActionUrl = "///ServiceSharingPage"
                });

                return ServiceResult<bool>.SuccessResult(true, "Talep yanÄ±tlandÄ±.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Ä°ÅŸlem sÄ±rasÄ±nda hata oluÅŸtu.", ex.Message);
            }
        }



        // UpdateUserInfoInServicesAsync taÅŸÄ±ndÄ±.

        //  MesajlaÅŸma ve PazarlÄ±k


        // Hizmet talebi iÃ§in konuÅŸma baÅŸlatÄ±r veya mevcut konuÅŸma ID'sini dÃ¶ndÃ¼rÃ¼r

        public async Task<ServiceResult<string>> StartConversationForRequestAsync(string requestId, string currentUserId)
        {
            try
            {
                KamPay.Helpers.AppLogger.DebugLog($" StartConversationForRequestAsync baÅŸladÄ±:");
                KamPay.Helpers.AppLogger.DebugLog($"   RequestId: {requestId}");
                KamPay.Helpers.AppLogger.DebugLog($"   CurrentUserId: {currentUserId}");

                var requestNode = _firebaseClient.Child(Constants.ServiceRequestsCollection).Child(requestId);
                var request = await requestNode.OnceSingleAsync<ServiceRequest>();

                if (request == null)
                {
                    KamPay.Helpers.AppLogger.DebugLog($"? Talep bulunamadÄ±: {requestId}");
                    return ServiceResult<string>.FailureResult("Talep bulunamadÄ±.");
                }

                KamPay.Helpers.AppLogger.DebugLog($"   ServiceTitle: {request.ServiceTitle}");
                KamPay.Helpers.AppLogger.DebugLog($"   ProviderId: {request.ProviderId}");
                KamPay.Helpers.AppLogger.DebugLog($"   RequesterId: {request.RequesterId}");

                // KullanÄ±cÄ±nÄ±n talep eden veya saÄŸlayÄ±cÄ± olduÄŸunu doÄŸrula
                if (request.RequesterId != currentUserId && request.ProviderId != currentUserId)
                {
                    KamPay.Helpers.AppLogger.DebugLog($"? EriÅŸim yetkiniz yok!");
                    return ServiceResult<string>.FailureResult("Bu talebe eriÅŸim yetkiniz yok.");
                }

                // Mevcut ConversationId'yi kontrol et
                if (!string.IsNullOrEmpty(request.ConversationId))
                {
                    KamPay.Helpers.AppLogger.DebugLog($"   Mevcut ConversationId: {request.ConversationId}");

                    // KonuÅŸmanÄ±n hala aktif olduÄŸunu doÄŸrula
                    try
                    {
                        var existingConversation = await _firebaseClient
                            .Child(Constants.ConversationsCollection)
                            .Child(request.ConversationId)
                            .OnceSingleAsync<Conversation>();

                        if (existingConversation != null && existingConversation.IsActive)
                        {
                            KamPay.Helpers.AppLogger.DebugLog($"? Mevcut konuÅŸma bulundu ve aktif: {request.ConversationId}");
                            return ServiceResult<string>.SuccessResult(request.ConversationId, "Mevcut konuÅŸma bulundu.");
                        }
                        else
                        {
                            KamPay.Helpers.AppLogger.DebugLog($"?? Mevcut konuÅŸma bulunamadÄ± veya pasif");
                        }
                    }
                    catch (Exception ex)
                    {
                        KamPay.Helpers.AppLogger.DebugLog($"?? Mevcut konuÅŸma kontrol hatasÄ±: {ex.Message}");
                    }
                }
                else
                {
                    KamPay.Helpers.AppLogger.DebugLog($"   ConversationId yok, yeni oluÅŸturulacak");
                }

                //  EÄŸer ConversationId yoksa veya konuÅŸma geÃ§ersizse, yeni oluÅŸtur
                var otherUserId = request.RequesterId == currentUserId ? request.ProviderId : request.RequesterId;
                KamPay.Helpers.AppLogger.DebugLog($"   OtherUserId: {otherUserId}");

                // Ã–nce bu iki kullanÄ±cÄ± arasÄ±nda aktif konuÅŸma var mÄ± kontrol et
                var existingConversations = await _firebaseClient
                    .Child(Constants.ConversationsCollection)
                    .OrderBy("User1Id")
                    .EqualTo(currentUserId)
                    .OnceAsync<Conversation>();

                KamPay.Helpers.AppLogger.DebugLog($"   Mevcut konuÅŸma sorgusu: {existingConversations.Count()} sonuÃ§");

                var existingWithOtherUser = existingConversations
                    .FirstOrDefault(c => c.Object != null &&
                                        c.Object.IsActive &&
                                        (c.Object.User2Id == otherUserId || c.Object.User1Id == otherUserId));

                if (existingWithOtherUser != null)
                {
                    // Mevcut konuÅŸma bulundu - request'e kaydet
                    request.ConversationId = existingWithOtherUser.Key;
                    request.HasActiveConversation = true;
                    await requestNode.PutAsync(request);

                    KamPay.Helpers.AppLogger.DebugLog($"? Mevcut kullanÄ±cÄ± konuÅŸmasÄ± bulundu: {existingWithOtherUser.Key}");
                    return ServiceResult<string>.SuccessResult(existingWithOtherUser.Key, "Mevcut konuÅŸma bulundu.");
                }

                KamPay.Helpers.AppLogger.DebugLog($"   Yeni konuÅŸma oluÅŸturuluyor...");

                // Yeni konuÅŸma oluÅŸtur
                var conversationResult = await _messagingService.GetOrCreateConversationAsync(currentUserId, otherUserId);

                if (!conversationResult.Success || conversationResult.Data == null)
                {
                    KamPay.Helpers.AppLogger.DebugLog($"? KonuÅŸma oluÅŸturulamadÄ±: {conversationResult.Message}");
                    return ServiceResult<string>.FailureResult("KonuÅŸma oluÅŸturulamadÄ±.", conversationResult.Message);
                }

                // KonuÅŸma ID'sini talebe kaydet
                request.ConversationId = conversationResult.Data.ConversationId;
                request.HasActiveConversation = true;
                await requestNode.PutAsync(request);

                KamPay.Helpers.AppLogger.DebugLog($"? Yeni konuÅŸma oluÅŸturuldu: {conversationResult.Data.ConversationId}");

                //  Sistem mesajÄ± gÃ¶nder
                var systemMessageContent = $"??? [{request.ServiceTitle} - Hizmet]\n?? KonuÅŸma baÅŸlatÄ±ldÄ±\nFiyat: {request.Price:N2} ?";
                KamPay.Helpers.AppLogger.DebugLog($"?? Sistem mesajÄ± gÃ¶nderiliyor: {systemMessageContent}");

                var currentUserObj = await GetUserAsync(currentUserId);
                if (currentUserObj != null)
                {
                    var messageResult = await _messagingService.SendMessageAsync(new SendMessageRequest
                    {
                        ReceiverId = otherUserId,
                        Content = systemMessageContent,
                        Type = MessageType.System
                    }, currentUserObj);

                    if (messageResult.Success)
                    {
                        KamPay.Helpers.AppLogger.DebugLog($"? Sistem mesajÄ± gÃ¶nderildi!");
                    }
                    else
                    {
                        KamPay.Helpers.AppLogger.DebugLog($"?? Sistem mesajÄ± gÃ¶nderilemedi: {messageResult.Message}");
                    }
                }
                else
                {
                    KamPay.Helpers.AppLogger.DebugLog($"?? KullanÄ±cÄ± bilgisi alÄ±namadÄ±, sistem mesajÄ± gÃ¶nderilemedi");
                }

                return ServiceResult<string>.SuccessResult(conversationResult.Data.ConversationId, "KonuÅŸma baÅŸlatÄ±ldÄ±.");
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"? StartConversationForRequestAsync hatasÄ±: {ex.Message}");
                KamPay.Helpers.AppLogger.DebugLog($"   StackTrace: {ex.StackTrace}");
                return ServiceResult<string>.FailureResult("KonuÅŸma baÅŸlatÄ±lÄ±rken hata oluÅŸtu.", ex.Message);
            }
        }


        /// Talep eden kiÅŸinin fiyat teklifi gÃ¶ndermesi

        public async Task<ServiceResult<bool>> ProposePrice(string requestId, decimal proposedPrice, string currentUserId)
        {
            try
            {
                KamPay.Helpers.AppLogger.DebugLog($"?? ProposePrice baÅŸladÄ±:");
                KamPay.Helpers.AppLogger.DebugLog($"   RequestId: {requestId}");
                KamPay.Helpers.AppLogger.DebugLog($"   ProposedPrice: {proposedPrice:N2} ?");
                KamPay.Helpers.AppLogger.DebugLog($"   CurrentUserId: {currentUserId}");

                var requestNode = _firebaseClient.Child(Constants.ServiceRequestsCollection).Child(requestId);
                var request = await requestNode.OnceSingleAsync<ServiceRequest>();

                if (request == null)
                {
                    KamPay.Helpers.AppLogger.DebugLog($"? Talep bulunamadÄ±");
                    return ServiceResult<bool>.FailureResult("Talep bulunamadÄ±.");
                }

                KamPay.Helpers.AppLogger.DebugLog($"   ServiceTitle: {request.ServiceTitle}");
                KamPay.Helpers.AppLogger.DebugLog($"   ConversationId: {request.ConversationId}");

                // Sadece talep eden kiÅŸi fiyat teklif edebilir
                if (request.RequesterId != currentUserId)
                {
                    KamPay.Helpers.AppLogger.DebugLog($"? Yetki yok - RequesterId: {request.RequesterId}");
                    return ServiceResult<bool>.FailureResult("Sadece talep eden kiÅŸi fiyat teklif edebilir.");
                }

                if (proposedPrice <= 0)
                {
                    KamPay.Helpers.AppLogger.DebugLog($"? GeÃ§ersiz fiyat");
                    return ServiceResult<bool>.FailureResult("GeÃ§erli bir fiyat giriniz.");
                }

                // Fiyat teklifini kaydet
                request.ProposedPriceByRequester = proposedPrice;
                request.IsNegotiating = true;
                request.LastNegotiationDate = DateTime.UtcNow;

                // Ä°lk teklif ise baÅŸlangÄ±Ã§ tarihini ayarla
                if (!request.NegotiationStartedAt.HasValue)
                {
                    request.NegotiationStartedAt = DateTime.UtcNow;
                }

                // PazarlÄ±k turu sayÄ±sÄ±nÄ± artÄ±r
                request.NegotiationRoundCount++;

                await requestNode.PutAsync(request);

                KamPay.Helpers.AppLogger.DebugLog($"? Fiyat teklifi kaydedildi");

                // SaÄŸlayÄ±cÄ±ya bildirim gÃ¶nder
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = request.ProviderId,
                    Title = "Yeni Fiyat Teklifi",
                    Message = $"{request.RequesterName}, '{request.ServiceTitle}' hizmeti iÃ§in {proposedPrice} ? teklif etti. (Orijinal fiyat: {request.Price} ?)"
                });

                KamPay.Helpers.AppLogger.DebugLog($"? Bildirim gÃ¶nderildi");

                //  Sistem mesajÄ± gÃ¶nder
                if (!string.IsNullOrEmpty(request.ConversationId))
                {
                    var messageContent = $"??? [{request.ServiceTitle} - Hizmet]\n?? Fiyat Teklifi: {proposedPrice:N2} ?\n(Orijinal fiyat: {request.Price:N2} ?)";
                    KamPay.Helpers.AppLogger.DebugLog($"?? Sistem mesajÄ± gÃ¶nderiliyor: {messageContent}");

                    var currentUserObj = await GetUserAsync(currentUserId);
                    if (currentUserObj != null)
                    {
                        var messageResult = await _messagingService.SendMessageAsync(new SendMessageRequest
                        {
                            ReceiverId = request.ProviderId,
                            Content = messageContent,
                            Type = MessageType.System
                        }, currentUserObj);

                        if (messageResult.Success)
                        {
                            KamPay.Helpers.AppLogger.DebugLog($"? Sistem mesajÄ± gÃ¶nderildi!");
                        }
                        else
                        {
                            KamPay.Helpers.AppLogger.DebugLog($"?? Sistem mesajÄ± gÃ¶nderilemedi: {messageResult.Message}");
                        }
                    }
                    else
                    {
                        KamPay.Helpers.AppLogger.DebugLog($"?? KullanÄ±cÄ± bilgisi alÄ±namadÄ±, sistem mesajÄ± gÃ¶nderilemedi");
                    }
                }
                else
                {
                    KamPay.Helpers.AppLogger.DebugLog($"?? ConversationId yok, sistem mesajÄ± gÃ¶nderilemedi");
                }

                return ServiceResult<bool>.SuccessResult(true, "Fiyat teklifiniz gÃ¶nderildi.");
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"? ProposePrice hatasÄ±: {ex.Message}");
                KamPay.Helpers.AppLogger.DebugLog($"   StackTrace: {ex.StackTrace}");
                return ServiceResult<bool>.FailureResult("Fiyat teklifi gÃ¶nderilemedi.", ex.Message);
            }
        }


        /// Hizmet saÄŸlaycÄ±sÄ±nÄ±n karÅŸÄ± teklif gÃ¶ndermesi

        public async Task<ServiceResult<bool>> SendCounterOfferAsync(string requestId, decimal counterOffer, string currentUserId)
        {
            try
            {
                KamPay.Helpers.AppLogger.DebugLog($"?? SendCounterOffer baÅŸladÄ±:");
                KamPay.Helpers.AppLogger.DebugLog($"   RequestId: {requestId}");
                KamPay.Helpers.AppLogger.DebugLog($"   CounterOffer: {counterOffer:N2} ?");
                KamPay.Helpers.AppLogger.DebugLog($"   CurrentUserId: {currentUserId}");

                var requestNode = _firebaseClient.Child(Constants.ServiceRequestsCollection).Child(requestId);
                var request = await requestNode.OnceSingleAsync<ServiceRequest>();

                if (request == null)
                {
                    KamPay.Helpers.AppLogger.DebugLog($"? Talep bulunamadÄ±");
                    return ServiceResult<bool>.FailureResult("Talep bulunamadÄ±.");
                }

                KamPay.Helpers.AppLogger.DebugLog($"   ServiceTitle: {request.ServiceTitle}");
                KamPay.Helpers.AppLogger.DebugLog($"   ConversationId: {request.ConversationId}");

                // Sadece hizmet saÄŸlayÄ±cÄ± karÅŸÄ± teklif verebilir
                if (request.ProviderId != currentUserId)
                {
                    KamPay.Helpers.AppLogger.DebugLog($"? Yetki yok - ProviderId: {request.ProviderId}");
                    return ServiceResult<bool>.FailureResult("Sadece hizmet saÄŸlayÄ±cÄ± karÅŸÄ± teklif verebilir.");
                }

                if (counterOffer <= 0)
                {
                    KamPay.Helpers.AppLogger.DebugLog($"? GeÃ§ersiz fiyat");
                    return ServiceResult<bool>.FailureResult("GeÃ§erli bir fiyat giriniz.");
                }

                // KarÅŸÄ± teklifi kaydet
                request.CounterOfferByProvider = counterOffer;
                request.IsNegotiating = true;
                request.LastNegotiationDate = DateTime.UtcNow;

                // Ä°lk karÅŸÄ± teklif ise baÅŸlangÄ±Ã§ tarihini ayarla
                if (!request.NegotiationStartedAt.HasValue)
                {
                    request.NegotiationStartedAt = DateTime.UtcNow;
                }

                // PazarlÄ±k turu sayÄ±sÄ±nÄ± artÄ±r
                request.NegotiationRoundCount++;

                await requestNode.PutAsync(request);

                KamPay.Helpers.AppLogger.DebugLog($"? KarÅŸÄ± teklif kaydedildi");

                // Talep eden kiÅŸiye bildirim gÃ¶nder
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = request.RequesterId,
                    Title = "KarÅŸÄ± Teklif AlÄ±ndÄ±",
                    Message = $"'{request.ServiceTitle}' hizmeti iÃ§in karÅŸÄ± teklif: {counterOffer} ?"
                });

                KamPay.Helpers.AppLogger.DebugLog($"? Bildirim gÃ¶nderildi");

                //  Sistem mesajÄ± gÃ¶nder
                if (!string.IsNullOrEmpty(request.ConversationId))
                {
                    var requesterOffer = request.ProposedPriceByRequester.HasValue
                        ? $"\n(Talep edenin teklifi: {request.ProposedPriceByRequester:N2} ?)"
                        : "";

                    var messageContent = $"??? [{request.ServiceTitle} - Hizmet]\n?? KarÅŸÄ± Teklif: {counterOffer:N2} ?{requesterOffer}";
                    KamPay.Helpers.AppLogger.DebugLog($"?? Sistem mesajÄ± gÃ¶nderiliyor: {messageContent}");

                    var currentUserObj = await GetUserAsync(currentUserId);
                    if (currentUserObj != null)
                    {
                        var messageResult = await _messagingService.SendMessageAsync(new SendMessageRequest
                        {
                            ReceiverId = request.RequesterId,
                            Content = messageContent,
                            Type = MessageType.System
                        }, currentUserObj);

                        if (messageResult.Success)
                        {
                            KamPay.Helpers.AppLogger.DebugLog($"? Sistem mesajÄ± gÃ¶nderildi!");
                        }
                        else
                        {
                            KamPay.Helpers.AppLogger.DebugLog($"?? Sistem mesajÄ± gÃ¶nderilemedi: {messageResult.Message}");
                        }
                    }
                    else
                    {
                        KamPay.Helpers.AppLogger.DebugLog($"?? KullanÄ±cÄ± bilgisi alÄ±namadÄ±, sistem mesajÄ± gÃ¶nderilemedi");
                    }
                }
                else
                {
                    KamPay.Helpers.AppLogger.DebugLog($"?? ConversationId yok, sistem mesajÄ± gÃ¶nderilemedi");
                }

                return ServiceResult<bool>.SuccessResult(true, "KarÅŸÄ± teklifiniz gÃ¶nderildi.");
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"? SendCounterOffer hatasÄ±: {ex.Message}");
                KamPay.Helpers.AppLogger.DebugLog($"   StackTrace: {ex.StackTrace}");
                return ServiceResult<bool>.FailureResult("KarÅŸÄ± teklif gÃ¶nderilemedi.", ex.Message);
            }
        }


        /// PazarlÄ±k sonucu anlaÅŸÄ±lan fiyatÄ± kabul etme

        public async Task<ServiceResult<bool>> AcceptNegotiatedPriceAsync(string requestId, string currentUserId)
        {
            try
            {
                KamPay.Helpers.AppLogger.DebugLog($"? AcceptNegotiatedPrice baÅŸladÄ±:");
                KamPay.Helpers.AppLogger.DebugLog($"   RequestId: {requestId}");
                KamPay.Helpers.AppLogger.DebugLog($"   CurrentUserId: {currentUserId}");

                var requestNode = _firebaseClient.Child(Constants.ServiceRequestsCollection).Child(requestId);
                var request = await requestNode.OnceSingleAsync<ServiceRequest>();

                if (request == null)
                {
                    KamPay.Helpers.AppLogger.DebugLog($"? Talep bulunamadÄ±");
                    return ServiceResult<bool>.FailureResult("Talep bulunamadÄ±.");
                }

                KamPay.Helpers.AppLogger.DebugLog($"   ServiceTitle: {request.ServiceTitle}");
                KamPay.Helpers.AppLogger.DebugLog($"   ConversationId: {request.ConversationId}");
                KamPay.Helpers.AppLogger.DebugLog($"   IsNegotiating: {request.IsNegotiating}");

                // KullanÄ±cÄ±nÄ±n talep eden veya saÄŸlayÄ±cÄ± olduÄŸunu doÄŸrula
                if (request.RequesterId != currentUserId && request.ProviderId != currentUserId)
                {
                    KamPay.Helpers.AppLogger.DebugLog($"? EriÅŸim yetkiniz yok");
                    return ServiceResult<bool>.FailureResult("Bu talebe eriÅŸim yetkiniz yok.");
                }

                if (!request.IsNegotiating)
                {
                    KamPay.Helpers.AppLogger.DebugLog($"? Aktif pazarlÄ±k yok");
                    return ServiceResult<bool>.FailureResult("Aktif bir pazarlÄ±k bulunmuyor.");
                }

                // AnlaÅŸÄ±lan fiyatÄ± belirle (karÅŸÄ± teklif > teklif > 0)
                decimal agreedPrice = request.CounterOfferByProvider ?? request.ProposedPriceByRequester ?? 0;
                KamPay.Helpers.AppLogger.DebugLog($"   AgreedPrice: {agreedPrice:N2} ?");

                if (agreedPrice <= 0)
                {
                    KamPay.Helpers.AppLogger.DebugLog($"? GeÃ§ersiz anlaÅŸma fiyatÄ±");
                    return ServiceResult<bool>.FailureResult("Kabul edilecek bir teklif bulunamadÄ±.");
                }

                // AnlaÅŸÄ±lan fiyatÄ± kaydet
                request.QuotedPrice = agreedPrice;
                request.Price = agreedPrice;
                request.IsNegotiating = false;

                // DetaylÄ± pazarlÄ±k Ã¶zeti oluÅŸtur
                var acceptedBy = request.RequesterId == currentUserId ? "Talep Eden" : "SaÄŸlayÄ±cÄ±";
                var negotiationDuration = request.NegotiationStartedAt.HasValue
                    ? (DateTime.UtcNow - request.NegotiationStartedAt.Value).TotalMinutes
                    : 0;

                var negotiationSummary = $"? AnlaÅŸma SaÄŸlandÄ±\n" +
                    $"Fiyat: {agreedPrice:N2}?\n" +
                    $"Kabul Eden: {acceptedBy}\n" +
                    $"PazarlÄ±k Turu: {request.NegotiationRoundCount}\n" +
                    $"SÃ¼re: {negotiationDuration:N0} dakika\n" +
                    $"Tarih: {DateTime.UtcNow:dd.MM.yyyy HH:mm}";

                request.NegotiationNotes += (string.IsNullOrEmpty(request.NegotiationNotes) ? "" : "\n\n") + negotiationSummary;

                await requestNode.PutAsync(request);

                KamPay.Helpers.AppLogger.DebugLog($"? AnlaÅŸma kaydedildi");

                // DiÄŸer tarafa bildirim gÃ¶nder
                var otherUserId = request.RequesterId == currentUserId ? request.ProviderId : request.RequesterId;
                KamPay.Helpers.AppLogger.DebugLog($"   OtherUserId: {otherUserId}");

                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = otherUserId,
                    Title = "Fiyat AnlaÅŸmasÄ±",
                    Message = $"'{request.ServiceTitle}' hizmeti iÃ§in {agreedPrice} ? fiyat Ã¼zerinde anlaÅŸÄ±ldÄ±."
                });

                KamPay.Helpers.AppLogger.DebugLog($"? Bildirim gÃ¶nderildi");

                //  Sistem mesajÄ± gÃ¶nder
                if (!string.IsNullOrEmpty(request.ConversationId))
                {
                    var messageContent = $"??? [{request.ServiceTitle} - Hizmet]\n? AnlaÅŸma SaÄŸlandÄ±: {agreedPrice:N2} ?";
                    KamPay.Helpers.AppLogger.DebugLog($"?? Sistem mesajÄ± gÃ¶nderiliyor: {messageContent}");

                    var currentUserObj = await GetUserAsync(currentUserId);
                    if (currentUserObj != null)
                    {
                        var messageResult = await _messagingService.SendMessageAsync(new SendMessageRequest
                        {
                            ReceiverId = otherUserId,
                            Content = messageContent,
                            Type = MessageType.System
                        }, currentUserObj);

                        if (messageResult.Success)
                        {
                            KamPay.Helpers.AppLogger.DebugLog($"? Sistem mesajÄ± gÃ¶nderildi!");
                        }
                        else
                        {
                            KamPay.Helpers.AppLogger.DebugLog($"?? Sistem mesajÄ± gÃ¶nderilemedi: {messageResult.Message}");
                        }
                    }
                    else
                    {
                        KamPay.Helpers.AppLogger.DebugLog($"?? KullanÄ±cÄ± bilgisi alÄ±namadÄ±, sistem mesajÄ± gÃ¶nderilemedi");
                    }
                }
                else
                {
                    KamPay.Helpers.AppLogger.DebugLog($"?? ConversationId yok, sistem mesajÄ± gÃ¶nderilemedi");
                }

                return ServiceResult<bool>.SuccessResult(true, $"Fiyat {agreedPrice} ? olarak kabul edildi.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Fiyat kabulÃ¼ sÄ±rasÄ±nda hata oluÅŸtu.", ex.Message);
            }
        }
        // 1. SAÄLAYICI: "Hizmeti TamamladÄ±m" (Ä°ÅŸi bitirdim mesajÄ±)
        public async Task<ServiceResult<bool>> ProviderFinishServiceAsync(string requestId, string providerId)
        {
            try
            {
                // AÄŸ KontrolÃ¼
                if (!NetworkHelper.HasInternetConnection())
                    return ServiceResult<bool>.FailureResult("Ä°nternet baÄŸlantÄ±sÄ± yok.");

                var requestNode = _firebaseClient.Child(Constants.ServiceRequestsCollection).Child(requestId);
                var request = await requestNode.OnceSingleAsync<ServiceRequest>();

                if (request == null || request.ProviderId != providerId)
                    return ServiceResult<bool>.FailureResult("Yetkisiz iÅŸlem veya talep bulunamadÄ±.");

                // Durum kontrolÃ¼: Sadece onaylanmÄ±ÅŸ hizmetler bitirilebilir
                if (request.Status != ServiceRequestStatus.Accepted)
                    return ServiceResult<bool>.FailureResult("Bu hizmetin durumu 'TamamlandÄ±' olarak iÅŸaretlenmeye uygun deÄŸil.");

                // Ã–deme kontrolÃ¼ (EÄŸer Ã¼cretliyse)
                if (request.Price > 0 && request.PaymentStatus != ServicePaymentStatus.Paid)
                    return ServiceResult<bool>.FailureResult("Hizmet bedeli henÃ¼z Ã¶denmemiÅŸ.");

                // Durumu gÃ¼ncelle (Yeni bir status veya flag eklenebilir, ÅŸimdilik beklemede tutuyoruz)
                request.UpdatedAt = DateTime.UtcNow;
                // Not: Burada status'Ã¼ hemen 'Completed' yapmÄ±yoruz, talep edenin onayÄ±nÄ± bekliyoruz.
                await requestNode.PutAsync(request);

                // Talep edene bildirim gÃ¶nder
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = request.RequesterId,
                    Title = "Hizmet TamamlandÄ± mÄ±?",
                    Message = $"{request.ProviderName}, '{request.ServiceTitle}' hizmetini tamamladÄ±ÄŸÄ±nÄ± bildirdi. LÃ¼tfen onaylayÄ±n.",
                    Type = NotificationType.ServiceCompleted,
                    ActionUrl = nameof(Views.ServiceRequestsPage)
                });

                return ServiceResult<bool>.SuccessResult(true, "Hizmet tamamlandÄ± bildirimi gÃ¶nderildi.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Hata oluÅŸtu.", NetworkHelper.GetUserFriendlyErrorMessage(ex));
            }
        }

        // 2. TALEP EDEN: "Hizmeti AldÄ±m / Onayla" (Krediyi/Ã–demeyi serbest bÄ±rakÄ±r)
        public async Task<ServiceResult<bool>> RequesterConfirmServiceAsync(string requestId, string requesterId)
        {
            try
            {
                var requestNode = _firebaseClient.Child(Constants.ServiceRequestsCollection).Child(requestId);
                var request = await requestNode.OnceSingleAsync<ServiceRequest>();

                if (request == null || request.RequesterId != requesterId)
                    return ServiceResult<bool>.FailureResult("Yetkisiz iÅŸlem.");

                // 1. Kredi Transferi (Zaman bankasÄ± sistemi iÃ§in)
                if (request.TimeCreditValue > 0)
                {
                    var transfer = await _userProfileService.TransferTimeCreditsAsync(
                        request.RequesterId, request.ProviderId, request.TimeCreditValue, $"Hizmet OnayÄ±: {request.ServiceTitle}");

                    if (!transfer.Success) return ServiceResult<bool>.FailureResult("Kredi transferi baÅŸarÄ±sÄ±z: " + transfer.Message);
                }

                // 2. Durumu Kapat
                request.Status = ServiceRequestStatus.Completed;
                request.UpdatedAt = DateTime.UtcNow;
                await requestNode.PutAsync(request);

                // 3. Puan Ver (Her iki tarafa da)
                await _userProfileService.AddPointsForAction(request.ProviderId, UserAction.ProvideService);
                await _userProfileService.AddPointsForAction(request.RequesterId, UserAction.ReceiveService);

                // 4. SaÄŸlayÄ±cÄ±ya Bildirim
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = request.ProviderId,
                    Title = "Ä°ÅŸlem BaÅŸarÄ±yla KapatÄ±ldÄ±",
                    Message = $"{request.RequesterName} hizmeti onayladÄ±. Puan ve krediler hesabÄ±nÄ±za eklendi.",
                    Type = NotificationType.ServiceCompleted
                });

                return ServiceResult<bool>.SuccessResult(true, "Hizmet baÅŸarÄ±yla onaylandÄ± ve tamamlandÄ±.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Hata.", NetworkHelper.GetUserFriendlyErrorMessage(ex));
            }
        }
        // YardÄ±mcÄ± metod: KullanÄ±cÄ± bilgisini getir
        private async Task<User?> GetUserAsync(string userId)
        {
            try
            {
                var user = await _firebaseClient
                    .Child(Constants.UsersCollection)
                    .Child(userId)
                    .OnceSingleAsync<User>();
                return user;
            }
            catch
            {
                return null;
            }
        }

        // ğŸ¯ ARMUT MODELÄ° - YENÄ° METODLAR

        #region MÃ¼ÅŸteri Talep YÃ¶netimi

        /// <summary>
        /// MÃ¼ÅŸteri yeni bir hizmet talebi oluÅŸturur
        /// </summary>
        public async Task<ServiceResult<CustomerServiceRequest>> CreateCustomerRequestAsync(CustomerServiceRequest request)
        {
            try
            {
                // Talep numarasÄ± oluÅŸtur
                if (string.IsNullOrWhiteSpace(request.RequestNumber))
                {
                    request.RequestNumber = $"CSR{DateTime.UtcNow:yyyyMMddHHmmss}";
                }

                request.CreatedAt = DateTime.UtcNow;
                request.UpdatedAt = DateTime.UtcNow;
                request.Status = CustomerRequestStatus.Open;
                request.IsActive = true;

                await _firebaseClient
                    .Child(Constants.CustomerServiceRequestsCollection)
                    .Child(request.RequestId)
                    .PutAsync(request);

                KamPay.Helpers.AppLogger.DebugLog($"âœ… MÃ¼ÅŸteri talebi oluÅŸturuldu: {request.RequestId}");
                return ServiceResult<CustomerServiceRequest>.SuccessResult(request, "Talep baÅŸarÄ±yla oluÅŸturuldu!");
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âŒ CreateCustomerRequestAsync hatasÄ±: {ex.Message}");
                return ServiceResult<CustomerServiceRequest>.FailureResult("Talep oluÅŸturulamadÄ±", ex.Message);
            }
        }

        /// <summary>
        /// TÃ¼m aktif mÃ¼ÅŸteri taleplerini getirir
        /// </summary>
        public async Task<ServiceResult<List<CustomerServiceRequest>>> GetCustomerRequestsAsync(
            ServiceCategory? category = null,
            string? location = null)
        {
            try
            {
                var allRequests = await _firebaseClient
                    .Child(Constants.CustomerServiceRequestsCollection)
                    .OnceAsync<CustomerServiceRequest>();

                var requests = allRequests
                    .Select(r => {
                        var req = r.Object;
                        req.RequestId = r.Key;
                        return req;
                    })
                    .Where(r => r.IsActive && r.Status == CustomerRequestStatus.Open)
                    .ToList();

                // Kategori filtresi
                if (category.HasValue)
                {
                    requests = requests.Where(r => r.Category == category.Value).ToList();
                }

                // Konum filtresi (basit string match)
                if (!string.IsNullOrWhiteSpace(location))
                {
                    requests = requests.Where(r => 
                        r.Location.Contains(location, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                requests = requests.OrderByDescending(r => r.CreatedAt).ToList();

                KamPay.Helpers.AppLogger.DebugLog($"ğŸ“‹ {requests.Count} aktif talep getirildi");
                return ServiceResult<List<CustomerServiceRequest>>.SuccessResult(requests);
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âŒ GetCustomerRequestsAsync hatasÄ±: {ex.Message}");
                return ServiceResult<List<CustomerServiceRequest>>.FailureResult("Talepler getirilemedi", ex.Message);
            }
        }

        /// <summary>
        /// Sayfalama ile mÃ¼ÅŸteri taleplerini getirir
        /// </summary>
        public async Task<ServiceResult<List<CustomerServiceRequest>>> GetCustomerRequestsPagedAsync(
            int pageSize = 20,
            string? lastKey = null,
            ServiceCategory? category = null)
        {
            try
            {
                IEnumerable<Firebase.Database.FirebaseObject<CustomerServiceRequest>> items;

                if (category.HasValue)
                {
                    items = await _firebaseClient
                        .Child(Constants.CustomerServiceRequestsCollection)
                        .OrderBy("Category")
                        .EqualTo((int)category.Value)
                        .OnceAsync<CustomerServiceRequest>();

                    var allRequests = items
                        .Select(r => {
                            var req = r.Object;
                            req.RequestId = r.Key;
                            return req;
                        })
                        .Where(r => r.IsActive && r.Status == CustomerRequestStatus.Open)
                        .OrderByDescending(r => r.CreatedAt)
                        .ToList();

                    if (!string.IsNullOrEmpty(lastKey))
                    {
                        var lastIndex = allRequests.FindIndex(r => r.RequestId == lastKey);
                        if (lastIndex >= 0)
                        {
                            allRequests = allRequests.Skip(lastIndex + 1).Take(pageSize).ToList();
                        }
                    }
                    else
                    {
                        allRequests = allRequests.Take(pageSize).ToList();
                    }

                    return ServiceResult<List<CustomerServiceRequest>>.SuccessResult(allRequests);
                }
                else
                {
                    if (!string.IsNullOrEmpty(lastKey))
                    {
                        items = await _firebaseClient
                            .Child(Constants.CustomerServiceRequestsCollection)
                            .OrderBy("CreatedAt")
                            .StartAt(lastKey)
                            .LimitToFirst(pageSize + 1)
                            .OnceAsync<CustomerServiceRequest>();
                    }
                    else
                    {
                        items = await _firebaseClient
                            .Child(Constants.CustomerServiceRequestsCollection)
                            .OrderBy("CreatedAt")
                            .LimitToFirst(pageSize)
                            .OnceAsync<CustomerServiceRequest>();
                    }

                    var requests = items.Select(r => {
                        var req = r.Object;
                        req.RequestId = r.Key;
                        return req;
                    }).ToList();

                    if (!string.IsNullOrEmpty(lastKey) && requests.Any() && requests.First().RequestId == lastKey)
                    {
                        requests.RemoveAt(0);
                    }

                    requests = requests
                        .Where(r => r.IsActive && r.Status == CustomerRequestStatus.Open)
                        .OrderByDescending(r => r.CreatedAt)
                        .ToList();

                    return ServiceResult<List<CustomerServiceRequest>>.SuccessResult(requests);
                }
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âŒ GetCustomerRequestsPagedAsync hatasÄ±: {ex.Message}");
                return ServiceResult<List<CustomerServiceRequest>>.FailureResult("Talepler yÃ¼klenemedi", ex.Message);
            }
        }

        /// <summary>
        /// Belirli bir mÃ¼ÅŸteri talebini ID ile getirir
        /// </summary>
        public async Task<ServiceResult<CustomerServiceRequest>> GetCustomerRequestByIdAsync(string requestId)
        {
            try
            {
                var request = await _firebaseClient
                    .Child(Constants.CustomerServiceRequestsCollection)
                    .Child(requestId)
                    .OnceSingleAsync<CustomerServiceRequest>();

                if (request == null)
                {
                    return ServiceResult<CustomerServiceRequest>.FailureResult("Talep bulunamadÄ±");
                }

                request.RequestId = requestId;
                return ServiceResult<CustomerServiceRequest>.SuccessResult(request);
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âŒ GetCustomerRequestByIdAsync hatasÄ±: {ex.Message}");
                return ServiceResult<CustomerServiceRequest>.FailureResult("Talep getirilemedi", ex.Message);
            }
        }

        /// <summary>
        /// MÃ¼ÅŸterinin kendi oluÅŸturduÄŸu talepleri getirir
        /// </summary>
        public async Task<ServiceResult<List<CustomerServiceRequest>>> GetMyCustomerRequestsAsync(string customerId)
        {
            try
            {
                var allRequests = await _firebaseClient
                    .Child(Constants.CustomerServiceRequestsCollection)
                    .OrderBy("CustomerId")
                    .EqualTo(customerId)
                    .OnceAsync<CustomerServiceRequest>();

                var requests = allRequests
                    .Select(r => {
                        var req = r.Object;
                        req.RequestId = r.Key;
                        return req;
                    })
                    .OrderByDescending(r => r.CreatedAt)
                    .ToList();

                KamPay.Helpers.AppLogger.DebugLog($"ğŸ“‹ {requests.Count} talep getirildi (MÃ¼ÅŸteri: {customerId})");
                return ServiceResult<List<CustomerServiceRequest>>.SuccessResult(requests);
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âŒ GetMyCustomerRequestsAsync hatasÄ±: {ex.Message}");
                return ServiceResult<List<CustomerServiceRequest>>.FailureResult("Talepler getirilemedi", ex.Message);
            }
        }

        /// <summary>
        /// MÃ¼ÅŸteri talebini gÃ¼nceller
        /// </summary>
        public async Task<ServiceResult<bool>> UpdateCustomerRequestAsync(CustomerServiceRequest request)
        {
            try
            {
                request.UpdatedAt = DateTime.UtcNow;

                await _firebaseClient
                    .Child(Constants.CustomerServiceRequestsCollection)
                    .Child(request.RequestId)
                    .PutAsync(request);

                KamPay.Helpers.AppLogger.DebugLog($"âœ… Talep gÃ¼ncellendi: {request.RequestId}");
                return ServiceResult<bool>.SuccessResult(true, "Talep gÃ¼ncellendi");
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âŒ UpdateCustomerRequestAsync hatasÄ±: {ex.Message}");
                return ServiceResult<bool>.FailureResult("Talep gÃ¼ncellenemedi", ex.Message);
            }
        }

        /// <summary>
        /// MÃ¼ÅŸteri talebini iptal eder
        /// </summary>
        public async Task<ServiceResult<bool>> CancelCustomerRequestAsync(string requestId, string customerId)
        {
            try
            {
                var requestNode = _firebaseClient
                    .Child(Constants.CustomerServiceRequestsCollection)
                    .Child(requestId);

                var request = await requestNode.OnceSingleAsync<CustomerServiceRequest>();

                if (request == null)
                {
                    return ServiceResult<bool>.FailureResult("Talep bulunamadÄ±");
                }

                if (request.CustomerId != customerId)
                {
                    return ServiceResult<bool>.FailureResult("Bu iÅŸlemi yapmaya yetkiniz yok");
                }

                request.Status = CustomerRequestStatus.Cancelled;
                request.IsActive = false;
                request.UpdatedAt = DateTime.UtcNow;

                await requestNode.PutAsync(request);

                // Bekleyen teklifleri bilgilendir
                var proposals = await GetProposalsForRequestAsync(requestId);
                if (proposals.Success && proposals.Data != null)
                {
                    foreach (var proposal in proposals.Data.Where(p => p.Status == ProposalStatus.Pending))
                    {
                        await _notificationService.CreateNotificationAsync(new Notification
                        {
                            UserId = proposal.ProviderId,
                            Title = "Talep Ä°ptal Edildi",
                            Message = $"'{request.Title}' talebi mÃ¼ÅŸteri tarafÄ±ndan iptal edildi.",
                            Type = NotificationType.ServiceCompleted
                        });
                    }
                }

                KamPay.Helpers.AppLogger.DebugLog($"âœ… Talep iptal edildi: {requestId}");
                return ServiceResult<bool>.SuccessResult(true, "Talep iptal edildi");
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âŒ CancelCustomerRequestAsync hatasÄ±: {ex.Message}");
                return ServiceResult<bool>.FailureResult("Talep iptal edilemedi", ex.Message);
            }
        }

        #endregion

        #region Profesyonel Teklif YÃ¶netimi

        /// <summary>
        /// Profesyonel bir mÃ¼ÅŸteri talebine teklif gÃ¶nderir
        /// </summary>
        public async Task<ServiceResult<ProviderProposal>> SendProposalAsync(ProviderProposal proposal)
        {
            try
            {
                // Teklif numarasÄ± oluÅŸtur
                if (string.IsNullOrWhiteSpace(proposal.ProposalNumber))
                {
                    proposal.ProposalNumber = $"PP{DateTime.UtcNow:yyyyMMddHHmmss}";
                }

                proposal.CreatedAt = DateTime.UtcNow;
                proposal.UpdatedAt = DateTime.UtcNow;
                proposal.Status = ProposalStatus.Pending;

                // Teklif geÃ§erlilik sÃ¼resi (varsayÄ±lan 7 gÃ¼n)
                if (!proposal.ExpiresAt.HasValue)
                {
                    proposal.ExpiresAt = DateTime.UtcNow.AddDays(7);
                }

                // Firebase'e kaydet
                await _firebaseClient
                    .Child(Constants.ProviderProposalsCollection)
                    .Child(proposal.ProposalId)
                    .PutAsync(proposal);

                // Talepteki teklif sayÄ±sÄ±nÄ± artÄ±r
                var requestNode = _firebaseClient
                    .Child(Constants.CustomerServiceRequestsCollection)
                    .Child(proposal.CustomerRequestId);

                var request = await requestNode.OnceSingleAsync<CustomerServiceRequest>();
                if (request != null)
                {
                    request.ProposalCount++;
                    request.UpdatedAt = DateTime.UtcNow;
                    await requestNode.PutAsync(request);
                }

                // MÃ¼ÅŸteriye bildirim gÃ¶nder
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = proposal.CustomerId,
                    Title = "ğŸ‰ Yeni Teklif Geldi!",
                    Message = $"{proposal.ProviderName}, '{proposal.RequestTitle}' talebiniz iÃ§in {proposal.Price:N2}â‚º teklif gÃ¶nderdi.",
                    Type = NotificationType.NewOffer
                });

                KamPay.Helpers.AppLogger.DebugLog($"âœ… Teklif gÃ¶nderildi: {proposal.ProposalId}");
                return ServiceResult<ProviderProposal>.SuccessResult(proposal, "Teklif baÅŸarÄ±yla gÃ¶nderildi!");
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âŒ SendProposalAsync hatasÄ±: {ex.Message}");
                return ServiceResult<ProviderProposal>.FailureResult("Teklif gÃ¶nderilemedi", ex.Message);
            }
        }

        /// <summary>
        /// Belirli bir talebe gÃ¶nderilen tÃ¼m teklifleri getirir
        /// </summary>
        public async Task<ServiceResult<List<ProviderProposal>>> GetProposalsForRequestAsync(string customerRequestId)
        {
            try
            {
                var allProposals = await _firebaseClient
                    .Child(Constants.ProviderProposalsCollection)
                    .OrderBy("CustomerRequestId")
                    .EqualTo(customerRequestId)
                    .OnceAsync<ProviderProposal>();

                var proposals = allProposals
                    .Select(p => {
                        var prop = p.Object;
                        prop.ProposalId = p.Key;
                        return prop;
                    })
                    .OrderByDescending(p => p.CreatedAt)
                    .ToList();

                KamPay.Helpers.AppLogger.DebugLog($"ğŸ“‹ {proposals.Count} teklif getirildi (Talep: {customerRequestId})");
                return ServiceResult<List<ProviderProposal>>.SuccessResult(proposals);
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âŒ GetProposalsForRequestAsync hatasÄ±: {ex.Message}");
                return ServiceResult<List<ProviderProposal>>.FailureResult("Teklifler getirilemedi", ex.Message);
            }
        }

        /// <summary>
        /// Profesyonelin gÃ¶nderdiÄŸi tÃ¼m teklifleri getirir
        /// </summary>
        public async Task<ServiceResult<List<ProviderProposal>>> GetMyProposalsAsync(string providerId)
        {
            try
            {
                var allProposals = await _firebaseClient
                    .Child(Constants.ProviderProposalsCollection)
                    .OrderBy("ProviderId")
                    .EqualTo(providerId)
                    .OnceAsync<ProviderProposal>();

                var proposals = allProposals
                    .Select(p => {
                        var prop = p.Object;
                        prop.ProposalId = p.Key;
                        return prop;
                    })
                    .OrderByDescending(p => p.CreatedAt)
                    .ToList();

                KamPay.Helpers.AppLogger.DebugLog($"ğŸ“‹ {proposals.Count} teklif getirildi (Profesyonel: {providerId})");
                return ServiceResult<List<ProviderProposal>>.SuccessResult(proposals);
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âŒ GetMyProposalsAsync hatasÄ±: {ex.Message}");
                return ServiceResult<List<ProviderProposal>>.FailureResult("Teklifler getirilemedi", ex.Message);
            }
        }

        /// <summary>
        /// MÃ¼ÅŸteri bir teklifi kabul eder
        /// </summary>
        public async Task<ServiceResult<bool>> AcceptProposalAsync(string proposalId, string customerId)
        {
            try
            {
                var proposalNode = _firebaseClient
                    .Child(Constants.ProviderProposalsCollection)
                    .Child(proposalId);

                var proposal = await proposalNode.OnceSingleAsync<ProviderProposal>();

                if (proposal == null)
                {
                    return ServiceResult<bool>.FailureResult("Teklif bulunamadÄ±");
                }

                if (proposal.CustomerId != customerId)
                {
                    return ServiceResult<bool>.FailureResult("Bu iÅŸlemi yapmaya yetkiniz yok");
                }

                if (proposal.Status != ProposalStatus.Pending)
                {
                    return ServiceResult<bool>.FailureResult("Bu teklif zaten yanÄ±tlanmÄ±ÅŸ");
                }

                // Teklifi kabul et
                proposal.Status = ProposalStatus.Accepted;
                proposal.RespondedAt = DateTime.UtcNow;
                proposal.UpdatedAt = DateTime.UtcNow;
                await proposalNode.PutAsync(proposal);

                // Talebi gÃ¼ncelle
                var requestNode = _firebaseClient
                    .Child(Constants.CustomerServiceRequestsCollection)
                    .Child(proposal.CustomerRequestId);

                var request = await requestNode.OnceSingleAsync<CustomerServiceRequest>();
                if (request != null)
                {
                    request.Status = CustomerRequestStatus.ProviderSelected;
                    request.SelectedProposalId = proposalId;
                    request.UpdatedAt = DateTime.UtcNow;
                    await requestNode.PutAsync(request);
                }

                // DiÄŸer bekleyen teklifleri reddet
                var otherProposals = await GetProposalsForRequestAsync(proposal.CustomerRequestId);
                if (otherProposals.Success && otherProposals.Data != null)
                {
                    foreach (var other in otherProposals.Data.Where(p => 
                        p.ProposalId != proposalId && 
                        p.Status == ProposalStatus.Pending))
                    {
                        var otherNode = _firebaseClient
                            .Child(Constants.ProviderProposalsCollection)
                            .Child(other.ProposalId);

                        other.Status = ProposalStatus.Rejected;
                        other.RejectionReason = "MÃ¼ÅŸteri baÅŸka bir teklifi kabul etti";
                        other.RespondedAt = DateTime.UtcNow;
                        other.UpdatedAt = DateTime.UtcNow;
                        await otherNode.PutAsync(other);

                        // Bildirim gÃ¶nder
                        await _notificationService.CreateNotificationAsync(new Notification
                        {
                            UserId = other.ProviderId,
                            Title = "Teklif Sonucu",
                            Message = $"'{proposal.RequestTitle}' talebi iÃ§in teklifiniz kabul edilmedi. MÃ¼ÅŸteri baÅŸka bir profesyoneli seÃ§ti.",
                            Type = NotificationType.OfferRejected
                        });
                    }
                }

                // Kazanan profesyonele bildirim
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = proposal.ProviderId,
                    Title = "ğŸ‰ Tebrikler! Teklifiniz Kabul Edildi",
                    Message = $"'{proposal.RequestTitle}' talebi iÃ§in {proposal.Price:N2}â‚º teklifiniz mÃ¼ÅŸteri tarafÄ±ndan kabul edildi. Ä°ÅŸ baÅŸlayabilir!",
                    Type = NotificationType.OfferAccepted
                });

                KamPay.Helpers.AppLogger.DebugLog($"âœ… Teklif kabul edildi: {proposalId}");
                return ServiceResult<bool>.SuccessResult(true, "Teklif kabul edildi! Profesyonel bilgilendirildi.");
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âŒ AcceptProposalAsync hatasÄ±: {ex.Message}");
                return ServiceResult<bool>.FailureResult("Teklif kabul edilemedi", ex.Message);
            }
        }

        /// <summary>
        /// MÃ¼ÅŸteri bir teklifi reddeder
        /// </summary>
        public async Task<ServiceResult<bool>> RejectProposalAsync(string proposalId, string customerId, string? reason = null)
        {
            try
            {
                var proposalNode = _firebaseClient
                    .Child(Constants.ProviderProposalsCollection)
                    .Child(proposalId);

                var proposal = await proposalNode.OnceSingleAsync<ProviderProposal>();

                if (proposal == null)
                {
                    return ServiceResult<bool>.FailureResult("Teklif bulunamadÄ±");
                }

                if (proposal.CustomerId != customerId)
                {
                    return ServiceResult<bool>.FailureResult("Bu iÅŸlemi yapmaya yetkiniz yok");
                }

                if (proposal.Status != ProposalStatus.Pending)
                {
                    return ServiceResult<bool>.FailureResult("Bu teklif zaten yanÄ±tlanmÄ±ÅŸ");
                }

                proposal.Status = ProposalStatus.Rejected;
                proposal.RejectionReason = reason ?? "MÃ¼ÅŸteri tarafÄ±ndan reddedildi";
                proposal.RespondedAt = DateTime.UtcNow;
                proposal.UpdatedAt = DateTime.UtcNow;
                await proposalNode.PutAsync(proposal);

                // Profesyonele bildirim
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = proposal.ProviderId,
                    Title = "Teklif Reddedildi",
                    Message = $"'{proposal.RequestTitle}' talebi iÃ§in teklifiniz mÃ¼ÅŸteri tarafÄ±ndan reddedildi.",
                    Type = NotificationType.OfferRejected
                });

                KamPay.Helpers.AppLogger.DebugLog($"âœ… Teklif reddedildi: {proposalId}");
                return ServiceResult<bool>.SuccessResult(true, "Teklif reddedildi");
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âŒ RejectProposalAsync hatasÄ±: {ex.Message}");
                return ServiceResult<bool>.FailureResult("Teklif reddedilemedi", ex.Message);
            }
        }

        /// <summary>
        /// Profesyonel kendi teklifini geri Ã§eker
        /// </summary>
        public async Task<ServiceResult<bool>> WithdrawProposalAsync(string proposalId, string providerId)
        {
            try
            {
                var proposalNode = _firebaseClient
                    .Child(Constants.ProviderProposalsCollection)
                    .Child(proposalId);

                var proposal = await proposalNode.OnceSingleAsync<ProviderProposal>();

                if (proposal == null)
                {
                    return ServiceResult<bool>.FailureResult("Teklif bulunamadÄ±");
                }

                if (proposal.ProviderId != providerId)
                {
                    return ServiceResult<bool>.FailureResult("Bu iÅŸlemi yapmaya yetkiniz yok");
                }

                if (proposal.Status != ProposalStatus.Pending)
                {
                    return ServiceResult<bool>.FailureResult("Bu teklif zaten yanÄ±tlanmÄ±ÅŸ veya kabul edilmiÅŸ");
                }

                proposal.Status = ProposalStatus.Withdrawn;
                proposal.UpdatedAt = DateTime.UtcNow;
                await proposalNode.PutAsync(proposal);

                // Talepteki teklif sayÄ±sÄ±nÄ± azalt
                var requestNode = _firebaseClient
                    .Child(Constants.CustomerServiceRequestsCollection)
                    .Child(proposal.CustomerRequestId);

                var request = await requestNode.OnceSingleAsync<CustomerServiceRequest>();
                if (request != null && request.ProposalCount > 0)
                {
                    request.ProposalCount--;
                    request.UpdatedAt = DateTime.UtcNow;
                    await requestNode.PutAsync(request);
                }

                KamPay.Helpers.AppLogger.DebugLog($"âœ… Teklif geri Ã§ekildi: {proposalId}");
                return ServiceResult<bool>.SuccessResult(true, "Teklif geri Ã§ekildi");
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âŒ WithdrawProposalAsync hatasÄ±: {ex.Message}");
                return ServiceResult<bool>.FailureResult("Teklif geri Ã§ekilemedi", ex.Message);
            }
        }

        /// <summary>
        /// Teklif kabul edildikten sonra iÅŸ sÃ¶zleÅŸmesi oluÅŸturur
        /// </summary>
        public async Task<ServiceResult<ServiceRequest>> CreateServiceContractFromProposalAsync(string proposalId)
        {
            try
            {
                var proposal = await _firebaseClient
                    .Child(Constants.ProviderProposalsCollection)
                    .Child(proposalId)
                    .OnceSingleAsync<ProviderProposal>();

                if (proposal == null)
                {
                    return ServiceResult<ServiceRequest>.FailureResult("Teklif bulunamadÄ±");
                }

                if (proposal.Status != ProposalStatus.Accepted)
                {
                    return ServiceResult<ServiceRequest>.FailureResult("Bu teklif kabul edilmemiÅŸ");
                }

                if (proposal.IsContractCreated)
                {
                    return ServiceResult<ServiceRequest>.FailureResult("SÃ¶zleÅŸme zaten oluÅŸturulmuÅŸ");
                }

                // ServiceRequest (iÅŸ sÃ¶zleÅŸmesi) oluÅŸtur
                var contract = new ServiceRequest
                {
                    RequestId = Guid.NewGuid().ToString(),
                    ServiceId = proposal.CustomerRequestId,
                    ServiceTitle = proposal.RequestTitle,
                    ProviderId = proposal.ProviderId,
                    ProviderName = proposal.ProviderName,
                    RequesterId = proposal.CustomerId,
                    RequesterName = proposal.CustomerName,
                    Price = proposal.Price,
                    QuotedPrice = proposal.Price,
                    Currency = proposal.Currency,
                    TimeCreditValue = 0,
                    Message = proposal.Message,
                    Status = ServiceRequestStatus.Accepted,
                    PaymentStatus = ServicePaymentStatus.None,
                    RequestedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                // SÃ¶zleÅŸmeyi kaydet
                await _firebaseClient
                    .Child(Constants.ServiceRequestsCollection)
                    .Child(contract.RequestId)
                    .PutAsync(contract);

                // Teklifi gÃ¼ncelle
                proposal.IsContractCreated = true;
                proposal.ServiceContractId = contract.RequestId;
                proposal.UpdatedAt = DateTime.UtcNow;

                await _firebaseClient
                    .Child(Constants.ProviderProposalsCollection)
                    .Child(proposalId)
                    .PutAsync(proposal);

                // Talebi gÃ¼ncelle
                var request = await _firebaseClient
                    .Child(Constants.CustomerServiceRequestsCollection)
                    .Child(proposal.CustomerRequestId)
                    .OnceSingleAsync<CustomerServiceRequest>();

                if (request != null)
                {
                    request.Status = CustomerRequestStatus.InProgress;
                    request.UpdatedAt = DateTime.UtcNow;
                    await _firebaseClient
                        .Child(Constants.CustomerServiceRequestsCollection)
                        .Child(proposal.CustomerRequestId)
                        .PutAsync(request);
                }

                KamPay.Helpers.AppLogger.DebugLog($"âœ… Ä°ÅŸ sÃ¶zleÅŸmesi oluÅŸturuldu: {contract.RequestId}");
                return ServiceResult<ServiceRequest>.SuccessResult(contract, "Ä°ÅŸ sÃ¶zleÅŸmesi oluÅŸturuldu");
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âŒ CreateServiceContractFromProposalAsync hatasÄ±: {ex.Message}");
                return ServiceResult<ServiceRequest>.FailureResult("SÃ¶zleÅŸme oluÅŸturulamadÄ±", ex.Message);
            }
        }

        #endregion

    }
}
