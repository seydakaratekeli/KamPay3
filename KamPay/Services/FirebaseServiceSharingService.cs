using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Models;
using KamPay.Helpers;
using System; 
using System.Collections.Generic; 
using System.Linq; 
using System.Threading.Tasks;
using System.Threading; 

namespace KamPay.Services
{
    // bu sayfanın amacı Firebase Realtime Database üzerinden hizmet paylaşımı ile ilgili işlemleri gerçekleştirmektir. kullanıcıların hizmet sunmalarını, taleplerini, ödemelerini ve ilgili bildirimleri yönetir. ama ödeme kısmı simülasyon şeklindedir. simulason şu anda tamamlanmamıştır.
    public class FirebaseServiceSharingService : IServiceSharingService
    {
        private readonly FirebaseClient _firebaseClient;
        private readonly INotificationService _notificationService; 
        private readonly IUserProfileService _userProfileService; 
        private readonly IMessagingService _messagingService; 
                                                                  // Basit OTP modeli (geçici koleksiyon için) bunu yaptık ta kullanıcaz mı bakalım ?? TEKRAR BAK
        internal class TempOtpModel
        {
            public string Otp { get; set; } = string.Empty;
            public DateTime ExpiresAt { get; set; }
        }

        private string GenerateOtp() => new Random().Next(100000, 999999).ToString();
        private string GenerateBankReference() => $"BTX-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString().Substring(0, 6)}";


        // Constructor to inject all required services
        public FirebaseServiceSharingService(INotificationService notificationService, IUserProfileService userProfileService, IMessagingService messagingService)
        {
            _firebaseClient = new FirebaseClient(Constants.FirebaseRealtimeDbUrl);
            _notificationService = notificationService;
            _userProfileService = userProfileService; 
            _messagingService = messagingService; 
        }

        // ... CreateServiceOfferAsync ve GetServiceOffersAsync metotları aynı kalacak ...
        public async Task<ServiceResult<ServiceOffer>> CreateServiceOfferAsync(ServiceOffer offer)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(offer.ServiceId))
                    offer.ServiceId = Guid.NewGuid().ToString(); // <-- EKLEYİN

                await _firebaseClient
                    .Child(Constants.ServiceOffersCollection)
                    .Child(offer.ServiceId)
                    .PutAsync(offer);

                return ServiceResult<ServiceOffer>.SuccessResult(offer, "Hizmet paylaşıldı!");
            }
            catch (Exception ex)
            {
                return ServiceResult<ServiceOffer>.FailureResult("Hata", ex.Message);
            }
        }

        public async Task<ServiceResult<List<ServiceOffer>>> GetServiceOffersAsync(ServiceCategory? category = null)
        {
            try
            {
                var allOffers = await _firebaseClient
                    .Child(Constants.ServiceOffersCollection)
                    .OnceAsync<ServiceOffer>();

                var offers = allOffers
                    .Select(o => o.Object)
                    .Where(o => o.IsAvailable && (!category.HasValue || o.Category == category.Value))
                    .OrderByDescending(o => o.CreatedAt)
                    .ToList();

                return ServiceResult<List<ServiceOffer>>.SuccessResult(offers);
            }
            catch (Exception ex)
            {
                return ServiceResult<List<ServiceOffer>>.FailureResult("Hata", ex.Message);
            }
        }

        public async Task<ServiceResult<ServiceRequest>> RequestServiceAsync(ServiceOffer offer, User requester, string message)
        {
            try
            {
                if (offer == null || requester == null)
                    return new ServiceResult<ServiceRequest>
                    {
                        Success = false,
                        Message = "Hizmet veya kullanıcı bilgisi eksik."
                    };

                // 🟢 Yeni ServiceRequest nesnesi oluşturuluyor
                var request = new ServiceRequest
                {
                    RequestId = Guid.NewGuid().ToString(),
                    ServiceId = offer.ServiceId,            // Hizmet kimliği
                    ServiceTitle = offer.Title,
                    ProviderId = offer.ProviderId,
                    RequesterId = requester.UserId,
                    RequesterName = requester.FullName,
                    Message = message,
                    Status = ServiceRequestStatus.Pending,
                    RequestedAt = DateTime.UtcNow,

                    // 🟢 Otomatik atanacak alanlar:
                    QuotedPrice = offer.Price,              // Hizmetin o anki fiyatı
                    Price = offer.Price,                    // UI veya raporlama için de saklıyoruz
                    TimeCreditValue = offer.TimeCredits,    // Kredi bilgisi (eski sistemle uyumlu)
                    PaymentStatus = ServicePaymentStatus.None,
                    PaymentMethod = PaymentMethodType.None,
                    Currency = "TRY"
                };

                // 🧾 Firebase’e kaydet
                await _firebaseClient
                    .Child(Constants.ServiceRequestsCollection)
                    .Child(request.RequestId)
                    .PutAsync(request);

                return new ServiceResult<ServiceRequest>
                {
                    Success = true,
                    Message = "Hizmet talebiniz başarıyla oluşturuldu.",
                    Data = request
                };
            }
            catch (Exception ex)
            {
                return new ServiceResult<ServiceRequest>
                {
                    Success = false,
                    Message = $"Talep oluşturulamadı: {ex.Message}"
                };
            }
        }

        // FirebaseServiceSharingService.cs içine eklenecek
        public async Task<ServiceResult<bool>> CompleteServiceRequestAsync(string transactionId, string providerId)
        {
            try
            {
                // 1. Ağ ve Yetki Kontrolleri
                if (!NetworkHelper.HasInternetConnection())
                    return ServiceResult<bool>.FailureResult("İnternet bağlantısı yok.");

                var transactionNode = _firebaseClient.Child(Constants.TransactionsCollection).Child(transactionId);
                var transaction = await transactionNode.OnceSingleAsync<Transaction>();

                if (transaction == null || transaction.SellerId != providerId)
                    return ServiceResult<bool>.FailureResult("İşlem bulunamadı veya yetkiniz yok.");

                // 2. Durum Kontrolü: Ödeme gerekiyorsa ödenmiş olmalı
                if (transaction.Price > 0 && transaction.PaymentStatus != PaymentStatus.Paid)
                    return ServiceResult<bool>.FailureResult("Hizmetin ödemesi henüz tamamlanmamış.");

                // 3. İşlemi Tamamla (Transaction durumunu Completed yapar, puanları verir ve bildirim gönderir)
                // Not: FirebaseTransactionService içindeki CompleteTransactionInternalAsync mantığına benzer 
                // bir çağrı yapmalı veya TransactionService üzerinden bu akışı tetiklemelisiniz.
                transaction.Status = TransactionStatus.Completed;
                transaction.UpdatedAt = DateTime.UtcNow;
                await transactionNode.PutAsync(transaction);

                // 4. Kullanıcıya Puan Ver (Hizmet Sağlayıcıya)
                await _userProfileService.AddPointsForAction(providerId, UserAction.ProvideService);

                // 5. Alan Kişiye Bildirim Gönder
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = transaction.BuyerId,
                    Title = "Hizmet Tamamlandı",
                    Message = $"{transaction.SellerName}, '{transaction.ProductTitle}' hizmetini tamamladığını bildirdi.",
                    Type = NotificationType.ServiceCompleted,
                    ActionUrl = nameof(Views.ServiceRequestsPage)
                });

                return ServiceResult<bool>.SuccessResult(true, "Hizmet başarıyla tamamlandı.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Hata oluştu.", NetworkHelper.GetUserFriendlyErrorMessage(ex));
            }
        }
        public async Task<ServiceResult<bool>> CompleteRequestAsync(string requestId, string currentUserId)
        {
            try
            {
                var requestNode = _firebaseClient.Child(Constants.ServiceRequestsCollection).Child(requestId);
                var request = await requestNode.OnceSingleAsync<ServiceRequest>();

                if (request == null) return ServiceResult<bool>.FailureResult("Talep bulunamadı.");

                // Sadece hizmeti talep eden kişi tamamlandı olarak işaretleyebilir
                if (request.RequesterId != currentUserId)
                    return ServiceResult<bool>.FailureResult("Bu işlemi yapmaya yetkiniz yok.");

                if (request.Status != ServiceRequestStatus.Accepted)
                    return ServiceResult<bool>.FailureResult("Bu talep henüz onaylanmamış veya zaten tamamlanmış.");

                // 1. Kredi transferini yap
                var transferResult = await _userProfileService.TransferTimeCreditsAsync(
                    request.RequesterId,
                    request.ProviderId,
                    request.TimeCreditValue,
                    $"Hizmet tamamlandı: {request.ServiceTitle}"
                );

                if (!transferResult.Success)
                {
                    return ServiceResult<bool>.FailureResult($"Kredi transferi başarısız: {transferResult.Message}");
                }

                // 2. Talebin durumunu güncelle
                request.Status = ServiceRequestStatus.Completed;
                await requestNode.PutAsync(request);

                // 3. Hizmeti sunan kişiye bildirim gönder
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = request.ProviderId,
                    Title = "Hizmet Tamamlandı ve Kredi Kazandın!",
                    Message = $"{request.RequesterName}, '{request.ServiceTitle}' hizmetini tamamlandı olarak işaretledi. Hesabına {request.TimeCreditValue} saat kredi eklendi."
                });

                return ServiceResult<bool>.SuccessResult(true, "Hizmet başarıyla tamamlandı.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("İşlem sırasında hata oluştu.", ex.Message);
            }
        }


        public async Task<ServiceResult<(List<ServiceRequest> Incoming, List<ServiceRequest> Outgoing)>> GetMyServiceRequestsAsync(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    return ServiceResult<(List<ServiceRequest> Incoming, List<ServiceRequest> Outgoing)>.FailureResult("Kullanıcı ID'si bulunamadı.");
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

                // HATA 2 ve 3 DÜZELTMESİ: 'CreatedAt' yerine 'RequestedAt' kullanılıyor.
                var incoming = incomingRequestsTask.Result
                    .Select(item => { item.Object.RequestId = item.Key; return item.Object; })
                    .OrderByDescending(r => r.RequestedAt)
                    .ToList();

                var outgoing = outgoingRequestsTask.Result
                    .Select(item => { item.Object.RequestId = item.Key; return item.Object; })
                    .OrderByDescending(r => r.RequestedAt)
                    .ToList();

                // HATA 1 DÜZELTMESİ: Tuple element names kullanılıyor
                return ServiceResult<(List<ServiceRequest> Incoming, List<ServiceRequest> Outgoing)>.SuccessResult((incoming, outgoing));
            }
            catch (Exception ex)
            {
                return ServiceResult<(List<ServiceRequest> Incoming, List<ServiceRequest> Outgoing)>.FailureResult("Talepler getirilirken bir hata oluştu.", ex.Message);
            }
        }


        // 3.1 Ödeme başlat (simülasyon)
        public async Task<ServiceResult<PaymentDto>> CreatePaymentSimulationAsync(string requestId, string method)
        {
            try
            {
                var requestNode = _firebaseClient.Child(Constants.ServiceRequestsCollection).Child(requestId);
                var request = await requestNode.OnceSingleAsync<ServiceRequest>();
                if (request == null) return ServiceResult<PaymentDto>.FailureResult("Talep bulunamadı.");

                if (request.PaymentStatus != ServicePaymentStatus.None && request.PaymentStatus != ServicePaymentStatus.Failed)
                    return ServiceResult<PaymentDto>.FailureResult("Bu talep için ödeme zaten başlatılmış.");

                // Miktarı belirle (QuotedPrice varsa onu kullan)
                var amount = (decimal)(request.QuotedPrice ?? request.TimeCreditValue);

                var payment = new PaymentDto
                {
                    Amount = amount,
                    Currency = "TRY",
                    Status = ServicePaymentStatus.Initiated,
                    Method = method?.ToLower() switch
                    {
                        "cardsim" => PaymentMethodType.CardSim,
                        "banktransfersim" or "eft" or "havale" => PaymentMethodType.BankTransferSim,
                        "walletsim" => PaymentMethodType.WalletSim,
                        _ => PaymentMethodType.CardSim
                    }
                };

                // Kart ise OTP üretip kısa süreli saklayalım (gerçekçi his)
                if (payment.Method == PaymentMethodType.CardSim)
                {
                    var otp = GenerateOtp();

                   
                    await _firebaseClient
                        .Child(Constants.TempOtpsCollection)
                        .Child(payment.PaymentId)
                        .PutAsync(new TempOtpModel
                        {
                            Otp = otp,
                            ExpiresAt = DateTime.UtcNow.AddMinutes(2)
                        });
                    

                    // burada log veya debug:
                    // Console.WriteLine($"OTP oluşturuldu: {otp}");
                }

                // EFT ise simüle bir referans üret
                if (payment.Method == PaymentMethodType.BankTransferSim)
                {
                    payment.BankName = "Ziraat Bankası";
                    payment.BankReference = GenerateBankReference();
                }

                // Request üzerinde ödeme bilgilerini işaretle
                request.PaymentStatus = ServicePaymentStatus.Initiated;

                request.PaymentSimulationId = payment.PaymentId;
                request.PaymentMethod = payment.Method;
                await requestNode.PutAsync(request);

                return ServiceResult<PaymentDto>.SuccessResult(payment, "Ödeme başlatıldı (simülasyon).");
            }
            catch (Exception ex)
            {
                return ServiceResult<PaymentDto>.FailureResult("Simülasyon başlatılırken hata.", ex.Message);
            }
        }

        // 3.2 Ödeme onayla (simülasyon)
        // Kartta OTP doğrular; EFT'de başarı/başarısız simüle edebilir.
        public async Task<ServiceResult<bool>> ConfirmPaymentSimulationAsync(string requestId, string paymentId, string? otp = null)
        {
            try
            {
                var requestNode = _firebaseClient.Child(Constants.ServiceRequestsCollection).Child(requestId);
                var request = await requestNode.OnceSingleAsync<ServiceRequest>();
                if (request == null) return ServiceResult<bool>.FailureResult("Talep bulunamadı.");

                if (request.PaymentSimulationId != paymentId)
                    return ServiceResult<bool>.FailureResult("Geçersiz ödeme kimliği.");

                if (request.PaymentStatus == ServicePaymentStatus.Paid)
                    return ServiceResult<bool>.SuccessResult(true, "Ödeme zaten onaylanmış.");

                // Kart için OTP kontrolü
                if (request.PaymentMethod == PaymentMethodType.CardSim)
                {
                    var otpNode = _firebaseClient.Child(Constants.TempOtpsCollection).Child(paymentId);
                    var saved = await otpNode.OnceSingleAsync<TempOtpModel>();
                    if (saved == null) return ServiceResult<bool>.FailureResult("OTP bulunamadı.");

                    if (DateTime.UtcNow > saved.ExpiresAt)
                        return ServiceResult<bool>.FailureResult("OTP süresi doldu.");

                    // 🔄 Demo modu: Eğer UI'dan OTP gelmemişse otomatik geçerli say
                    if (string.IsNullOrWhiteSpace(otp))
                    {
                        otp = saved.Otp; // demo için doğru kabul
                    }

                    // Şimdi kontrol et
                    if (saved.Otp != otp)
                        return ServiceResult<bool>.FailureResult("OTP geçersiz.");
                }

                // EFT ise bu noktada direkt onaylayabilir veya ayrı bir "beklemede" süreci de kurgulanabilir
                request.PaymentStatus = ServicePaymentStatus.Paid;
                await requestNode.PutAsync(request);

                return ServiceResult<bool>.SuccessResult(true, "Ödeme onaylandı (simülasyon).");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Ödeme onayında hata.", ex.Message);
            }
        }

        // 3.3 Tek adımda: Ödeme simülasyonu + Tamamlama
        public async Task<ServiceResult<bool>> SimulatePaymentAndCompleteAsync(string requestId, string currentUserId, PaymentMethodType method = PaymentMethodType.CardSim, string? maskedCardLast4 = null)
        {
            try
            {
                var requestNode = _firebaseClient.Child(Constants.ServiceRequestsCollection).Child(requestId);
                var request = await requestNode.OnceSingleAsync<ServiceRequest>();
                if (request == null) return ServiceResult<bool>.FailureResult("Talep bulunamadı.");

                // Yetki & durum kontrolleri
                if (request.RequesterId != currentUserId)
                    return ServiceResult<bool>.FailureResult("Bu işlemi yapmaya yetkiniz yok.");
                if (request.Status != ServiceRequestStatus.Accepted)
                    return ServiceResult<bool>.FailureResult("Talep henüz onaylanmamış veya tamamlanmış.");

                // 1) Ödeme başlat
                var createResult = await CreatePaymentSimulationAsync(requestId, method.ToString());
                if (!createResult.Success) return ServiceResult<bool>.FailureResult(createResult.Message);
                var payment = createResult.Data;

                // Kartsa UI üzerinden OTP toplanmasını beklediğini varsayabiliriz.
                // Burada gerçek projende ya:
                //  - A) UI, ConfirmPaymentSimulationAsync'i ayrı çağırır (önerilen)
                //  - B) veya burada kısa bir beklemenin ardından "otomatik onay" yapılır (demo için):
                if (payment.Method == PaymentMethodType.CardSim)
                {
                    await Task.Delay(1000);
                    // Demo için otomatik OTP = doğru kabul:
                    // OTP parametresi null gönderilirse, metod içindeki otomatik demo doğrulaması çalışır
                    var confirm = await ConfirmPaymentSimulationAsync(requestId, payment.PaymentId, otp: null);
                    if (!confirm.Success) return ServiceResult<bool>.FailureResult(confirm.Message);
                }
                else if (payment.Method == PaymentMethodType.BankTransferSim)
                {
                    // EFT/havale simülasyonu: kısa bekleme + doğrudan onay (demo)
                    await Task.Delay(new Random().Next(1200, 3000));
                    var confirm = await ConfirmPaymentSimulationAsync(requestId, payment.PaymentId);
                    if (!confirm.Success) return ServiceResult<bool>.FailureResult(confirm.Message);
                }

                // 2) Tamamlama
                request.PaymentStatus = ServicePaymentStatus.Paid;
                request.Status = ServiceRequestStatus.Completed;
                if (!string.IsNullOrWhiteSpace(maskedCardLast4))
                {
                    // masked last4 bilgisini saklamak istersen PaymentDto tarafında tutup loglayabilirsin
                }
                await requestNode.PutAsync(request);

                // Bildirim
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = request.ProviderId,
                    Title = "Hizmet Ücreti Simüle Edildi!",
                    Message = $"{request.RequesterName}, '{request.ServiceTitle}' hizmeti için ödemeyi simüle etti. Hizmet tamamlandı."
                });

                return ServiceResult<bool>.SuccessResult(true, "Ödeme simüle edildi ve hizmet tamamlandı.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Simülasyon tamamlanamadı.", ex.Message);
            }
        }

        // Bu metot şu an kullanılmıyor ama ileride talepleri yanıtlarken gerekecek.
        public async Task<ServiceResult<bool>> RespondToRequestAsync(string requestId, bool accept)
        {
            try
            {
                var requestNode = _firebaseClient.Child(Constants.ServiceRequestsCollection).Child(requestId);
                var request = await requestNode.OnceSingleAsync<ServiceRequest>();

                if (request == null)
                {
                    return ServiceResult<bool>.FailureResult("Talep bulunamadı.");
                }

                request.Status = accept ? ServiceRequestStatus.Accepted : ServiceRequestStatus.Declined;
                await requestNode.PutAsync(request);

                // Talebi gönderen kişiye bildirim gönder
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = request.RequesterId,
                    Type = accept ? NotificationType.OfferAccepted : NotificationType.OfferRejected,
                    Title = accept ? "Hizmet Talebin Onaylandı!" : "Hizmet Talebin Reddedildi",
                    Message = $"'{request.ServiceTitle}' hizmeti için talebin {(accept ? "kabul edildi." : "reddedildi.")}",
                    ActionUrl = "///ServiceSharingPage"
                });

                return ServiceResult<bool>.SuccessResult(true, "Talep yanıtlandı.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("İşlem sırasında hata oluştu.", ex.Message);
            }
        }
    

     
        /// Kullanıcının tüm hizmetlerindeki isim ve profil fotoğrafı bilgilerini günceller
        
        public async Task<ServiceResult<bool>> UpdateUserInfoInServicesAsync(string userId, string? newName, string? newPhotoUrl)
        {
            try
            {
                var allServices = await _firebaseClient
                    .Child(Constants.ServiceOffersCollection)
                    .OrderBy("ProviderId")
                    .EqualTo(userId)
                    .OnceAsync<ServiceOffer>();

                foreach (var serviceEntry in allServices)
                {
                    var service = serviceEntry.Object;
                    service.ServiceId = serviceEntry.Key;
                    service.ProviderName = newName ?? string.Empty;
                    service.ProviderPhotoUrl = newPhotoUrl ?? string.Empty;

                    await _firebaseClient
                        .Child(Constants.ServiceOffersCollection)
                        .Child(serviceEntry.Key)
                        .PutAsync(service);
                }

                return ServiceResult<bool>.SuccessResult(true, $"{allServices.Count()} hizmet güncellendi");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Hizmetler güncellenemedi", ex.Message);
            }
        }

        //  Mesajlaşma ve Pazarlık
        
        
        // Hizmet talebi için konuşma başlatır veya mevcut konuşma ID'sini döndürür
       
        public async Task<ServiceResult<string>> StartConversationForRequestAsync(string requestId, string currentUserId)
        {
            try
            {
                Console.WriteLine($" StartConversationForRequestAsync başladı:");
                Console.WriteLine($"   RequestId: {requestId}");
                Console.WriteLine($"   CurrentUserId: {currentUserId}");

                var requestNode = _firebaseClient.Child(Constants.ServiceRequestsCollection).Child(requestId);
                var request = await requestNode.OnceSingleAsync<ServiceRequest>();

                if (request == null)
                {
                    Console.WriteLine($"❌ Talep bulunamadı: {requestId}");
                    return ServiceResult<string>.FailureResult("Talep bulunamadı.");
                }

                Console.WriteLine($"   ServiceTitle: {request.ServiceTitle}");
                Console.WriteLine($"   ProviderId: {request.ProviderId}");
                Console.WriteLine($"   RequesterId: {request.RequesterId}");

                // Kullanıcının talep eden veya sağlayıcı olduğunu doğrula
                if (request.RequesterId != currentUserId && request.ProviderId != currentUserId)
                {
                    Console.WriteLine($"❌ Erişim yetkiniz yok!");
                    return ServiceResult<string>.FailureResult("Bu talebe erişim yetkiniz yok.");
                }

                // Mevcut ConversationId'yi kontrol et
                if (!string.IsNullOrEmpty(request.ConversationId))
                {
                    Console.WriteLine($"   Mevcut ConversationId: {request.ConversationId}");
                    
                    // Konuşmanın hala aktif olduğunu doğrula
                    try
                    {
                        var existingConversation = await _firebaseClient
                            .Child(Constants.ConversationsCollection)
                            .Child(request.ConversationId)
                            .OnceSingleAsync<Conversation>();

                        if (existingConversation != null && existingConversation.IsActive)
                        {
                            Console.WriteLine($"✅ Mevcut konuşma bulundu ve aktif: {request.ConversationId}");
                            return ServiceResult<string>.SuccessResult(request.ConversationId, "Mevcut konuşma bulundu.");
                        }
                        else
                        {
                            Console.WriteLine($"⚠️ Mevcut konuşma bulunamadı veya pasif");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Mevcut konuşma kontrol hatası: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine($"   ConversationId yok, yeni oluşturulacak");
                }

                //  Eğer ConversationId yoksa veya konuşma geçersizse, yeni oluştur
                var otherUserId = request.RequesterId == currentUserId ? request.ProviderId : request.RequesterId;
                Console.WriteLine($"   OtherUserId: {otherUserId}");
                
                // Önce bu iki kullanıcı arasında aktif konuşma var mı kontrol et
                var existingConversations = await _firebaseClient
                    .Child(Constants.ConversationsCollection)
                    .OrderBy("User1Id")
                    .EqualTo(currentUserId)
                    .OnceAsync<Conversation>();

                Console.WriteLine($"   Mevcut konuşma sorgusu: {existingConversations.Count()} sonuç");

                var existingWithOtherUser = existingConversations
                    .FirstOrDefault(c => c.Object != null && 
                                        c.Object.IsActive &&
                                        (c.Object.User2Id == otherUserId || c.Object.User1Id == otherUserId));

                if (existingWithOtherUser != null)
                {
                    // Mevcut konuşma bulundu - request'e kaydet
                    request.ConversationId = existingWithOtherUser.Key;
                    request.HasActiveConversation = true;
                    await requestNode.PutAsync(request);
                    
                    Console.WriteLine($"✅ Mevcut kullanıcı konuşması bulundu: {existingWithOtherUser.Key}");
                    return ServiceResult<string>.SuccessResult(existingWithOtherUser.Key, "Mevcut konuşma bulundu.");
                }

                Console.WriteLine($"   Yeni konuşma oluşturuluyor...");

                // Yeni konuşma oluştur
                var conversationResult = await _messagingService.GetOrCreateConversationAsync(currentUserId, otherUserId);

                if (!conversationResult.Success || conversationResult.Data == null)
                {
                    Console.WriteLine($"❌ Konuşma oluşturulamadı: {conversationResult.Message}");
                    return ServiceResult<string>.FailureResult("Konuşma oluşturulamadı.", conversationResult.Message);
                }

                // Konuşma ID'sini talebe kaydet
                request.ConversationId = conversationResult.Data.ConversationId;
                request.HasActiveConversation = true;
                await requestNode.PutAsync(request);

                Console.WriteLine($"✅ Yeni konuşma oluşturuldu: {conversationResult.Data.ConversationId}");

                //  Sistem mesajı gönder
                var systemMessageContent = $"🛠️ [{request.ServiceTitle} - Hizmet]\n📝 Konuşma başlatıldı\nFiyat: {request.Price:N2} ₺";
                Console.WriteLine($"📝 Sistem mesajı gönderiliyor: {systemMessageContent}");

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
                        Console.WriteLine($"✅ Sistem mesajı gönderildi!");
                    }
                    else
                    {
                        Console.WriteLine($"⚠️ Sistem mesajı gönderilemedi: {messageResult.Message}");
                    }
                }
                else
                {
                    Console.WriteLine($"⚠️ Kullanıcı bilgisi alınamadı, sistem mesajı gönderilemedi");
                }

                return ServiceResult<string>.SuccessResult(conversationResult.Data.ConversationId, "Konuşma başlatıldı.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ StartConversationForRequestAsync hatası: {ex.Message}");
                Console.WriteLine($"   StackTrace: {ex.StackTrace}");
                return ServiceResult<string>.FailureResult("Konuşma başlatılırken hata oluştu.", ex.Message);
            }
        }

     
        /// Talep eden kişinin fiyat teklifi göndermesi
        
        public async Task<ServiceResult<bool>> ProposePrice(string requestId, decimal proposedPrice, string currentUserId)
        {
            try
            {
                Console.WriteLine($"💰 ProposePrice başladı:");
                Console.WriteLine($"   RequestId: {requestId}");
                Console.WriteLine($"   ProposedPrice: {proposedPrice:N2} ₺");
                Console.WriteLine($"   CurrentUserId: {currentUserId}");

                var requestNode = _firebaseClient.Child(Constants.ServiceRequestsCollection).Child(requestId);
                var request = await requestNode.OnceSingleAsync<ServiceRequest>();

                if (request == null)
                {
                    Console.WriteLine($"❌ Talep bulunamadı");
                    return ServiceResult<bool>.FailureResult("Talep bulunamadı.");
                }

                Console.WriteLine($"   ServiceTitle: {request.ServiceTitle}");
                Console.WriteLine($"   ConversationId: {request.ConversationId}");

                // Sadece talep eden kişi fiyat teklif edebilir
                if (request.RequesterId != currentUserId)
                {
                    Console.WriteLine($"❌ Yetki yok - RequesterId: {request.RequesterId}");
                    return ServiceResult<bool>.FailureResult("Sadece talep eden kişi fiyat teklif edebilir.");
                }

                if (proposedPrice <= 0)
                {
                    Console.WriteLine($"❌ Geçersiz fiyat");
                    return ServiceResult<bool>.FailureResult("Geçerli bir fiyat giriniz.");
                }

                // Fiyat teklifini kaydet
                request.ProposedPriceByRequester = proposedPrice;
                request.IsNegotiating = true;
                request.LastNegotiationDate = DateTime.UtcNow;
                
                // İlk teklif ise başlangıç tarihini ayarla
                if (!request.NegotiationStartedAt.HasValue)
                {
                    request.NegotiationStartedAt = DateTime.UtcNow;
                }
                
                // Pazarlık turu sayısını artır
                request.NegotiationRoundCount++;
                
                await requestNode.PutAsync(request);

                Console.WriteLine($"✅ Fiyat teklifi kaydedildi");

                // Sağlayıcıya bildirim gönder
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = request.ProviderId,
                    Title = "Yeni Fiyat Teklifi",
                    Message = $"{request.RequesterName}, '{request.ServiceTitle}' hizmeti için {proposedPrice} ₺ teklif etti. (Orijinal fiyat: {request.Price} ₺)"
                });

                Console.WriteLine($"✅ Bildirim gönderildi");

                //  Sistem mesajı gönder
                if (!string.IsNullOrEmpty(request.ConversationId))
                {
                    var messageContent = $"🛠️ [{request.ServiceTitle} - Hizmet]\n💰 Fiyat Teklifi: {proposedPrice:N2} ₺\n(Orijinal fiyat: {request.Price:N2} ₺)";
                    Console.WriteLine($"📝 Sistem mesajı gönderiliyor: {messageContent}");

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
                            Console.WriteLine($"✅ Sistem mesajı gönderildi!");
                        }
                        else
                        {
                            Console.WriteLine($"⚠️ Sistem mesajı gönderilemedi: {messageResult.Message}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"⚠️ Kullanıcı bilgisi alınamadı, sistem mesajı gönderilemedi");
                    }
                }
                else
                {
                    Console.WriteLine($"⚠️ ConversationId yok, sistem mesajı gönderilemedi");
                }

                return ServiceResult<bool>.SuccessResult(true, "Fiyat teklifiniz gönderildi.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ProposePrice hatası: {ex.Message}");
                Console.WriteLine($"   StackTrace: {ex.StackTrace}");
                return ServiceResult<bool>.FailureResult("Fiyat teklifi gönderilemedi.", ex.Message);
            }
        }

     
        /// Hizmet sağlayıcısının karşı teklif göndermesi
        
        public async Task<ServiceResult<bool>> SendCounterOfferAsync(string requestId, decimal counterOffer, string currentUserId)
        {
            try
            {
                Console.WriteLine($"💰 SendCounterOffer başladı:");
                Console.WriteLine($"   RequestId: {requestId}");
                Console.WriteLine($"   CounterOffer: {counterOffer:N2} ₺");
                Console.WriteLine($"   CurrentUserId: {currentUserId}");

                var requestNode = _firebaseClient.Child(Constants.ServiceRequestsCollection).Child(requestId);
                var request = await requestNode.OnceSingleAsync<ServiceRequest>();

                if (request == null)
                {
                    Console.WriteLine($"❌ Talep bulunamadı");
                    return ServiceResult<bool>.FailureResult("Talep bulunamadı.");
                }

                Console.WriteLine($"   ServiceTitle: {request.ServiceTitle}");
                Console.WriteLine($"   ConversationId: {request.ConversationId}");

                // Sadece hizmet sağlayıcı karşı teklif verebilir
                if (request.ProviderId != currentUserId)
                {
                    Console.WriteLine($"❌ Yetki yok - ProviderId: {request.ProviderId}");
                    return ServiceResult<bool>.FailureResult("Sadece hizmet sağlayıcı karşı teklif verebilir.");
                }

                if (counterOffer <= 0)
                {
                    Console.WriteLine($"❌ Geçersiz fiyat");
                    return ServiceResult<bool>.FailureResult("Geçerli bir fiyat giriniz.");
                }

                // Karşı teklifi kaydet
                request.CounterOfferByProvider = counterOffer;
                request.IsNegotiating = true;
                request.LastNegotiationDate = DateTime.UtcNow;
                
                // İlk karşı teklif ise başlangıç tarihini ayarla
                if (!request.NegotiationStartedAt.HasValue)
                {
                    request.NegotiationStartedAt = DateTime.UtcNow;
                }
                
                // Pazarlık turu sayısını artır
                request.NegotiationRoundCount++;
                
                await requestNode.PutAsync(request);

                Console.WriteLine($"✅ Karşı teklif kaydedildi");

                // Talep eden kişiye bildirim gönder
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = request.RequesterId,
                    Title = "Karşı Teklif Alındı",
                    Message = $"'{request.ServiceTitle}' hizmeti için karşı teklif: {counterOffer} ₺"
                });

                Console.WriteLine($"✅ Bildirim gönderildi");

                //  Sistem mesajı gönder
                if (!string.IsNullOrEmpty(request.ConversationId))
                {
                    var requesterOffer = request.ProposedPriceByRequester.HasValue
                        ? $"\n(Talep edenin teklifi: {request.ProposedPriceByRequester:N2} ₺)"
                        : "";
                    
                    var messageContent = $"🛠️ [{request.ServiceTitle} - Hizmet]\n💰 Karşı Teklif: {counterOffer:N2} ₺{requesterOffer}";
                    Console.WriteLine($"📝 Sistem mesajı gönderiliyor: {messageContent}");

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
                            Console.WriteLine($"✅ Sistem mesajı gönderildi!");
                        }
                        else
                        {
                            Console.WriteLine($"⚠️ Sistem mesajı gönderilemedi: {messageResult.Message}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"⚠️ Kullanıcı bilgisi alınamadı, sistem mesajı gönderilemedi");
                    }
                }
                else
                {
                    Console.WriteLine($"⚠️ ConversationId yok, sistem mesajı gönderilemedi");
                }

                return ServiceResult<bool>.SuccessResult(true, "Karşı teklifiniz gönderildi.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ SendCounterOffer hatası: {ex.Message}");
                Console.WriteLine($"   StackTrace: {ex.StackTrace}");
                return ServiceResult<bool>.FailureResult("Karşı teklif gönderilemedi.", ex.Message);
            }
        }

     
        /// Pazarlık sonucu anlaşılan fiyatı kabul etme
        
        public async Task<ServiceResult<bool>> AcceptNegotiatedPriceAsync(string requestId, string currentUserId)
        {
            try
            {
                Console.WriteLine($"✅ AcceptNegotiatedPrice başladı:");
                Console.WriteLine($"   RequestId: {requestId}");
                Console.WriteLine($"   CurrentUserId: {currentUserId}");

                var requestNode = _firebaseClient.Child(Constants.ServiceRequestsCollection).Child(requestId);
                var request = await requestNode.OnceSingleAsync<ServiceRequest>();

                if (request == null)
                {
                    Console.WriteLine($"❌ Talep bulunamadı");
                    return ServiceResult<bool>.FailureResult("Talep bulunamadı.");
                }

                Console.WriteLine($"   ServiceTitle: {request.ServiceTitle}");
                Console.WriteLine($"   ConversationId: {request.ConversationId}");
                Console.WriteLine($"   IsNegotiating: {request.IsNegotiating}");

                // Kullanıcının talep eden veya sağlayıcı olduğunu doğrula
                if (request.RequesterId != currentUserId && request.ProviderId != currentUserId)
                {
                    Console.WriteLine($"❌ Erişim yetkiniz yok");
                    return ServiceResult<bool>.FailureResult("Bu talebe erişim yetkiniz yok.");
                }

                if (!request.IsNegotiating)
                {
                    Console.WriteLine($"❌ Aktif pazarlık yok");
                    return ServiceResult<bool>.FailureResult("Aktif bir pazarlık bulunmuyor.");
                }

                // Anlaşılan fiyatı belirle (karşı teklif > teklif > 0)
                decimal agreedPrice = request.CounterOfferByProvider ?? request.ProposedPriceByRequester ?? 0;
                Console.WriteLine($"   AgreedPrice: {agreedPrice:N2} ₺");
                
                if (agreedPrice <= 0)
                {
                    Console.WriteLine($"❌ Geçersiz anlaşma fiyatı");
                    return ServiceResult<bool>.FailureResult("Kabul edilecek bir teklif bulunamadı.");
                }

                // Anlaşılan fiyatı kaydet
                request.QuotedPrice = agreedPrice;
                request.Price = agreedPrice;
                request.IsNegotiating = false;
                
                // Detaylı pazarlık özeti oluştur
                var acceptedBy = request.RequesterId == currentUserId ? "Talep Eden" : "Sağlayıcı";
                var negotiationDuration = request.NegotiationStartedAt.HasValue 
                    ? (DateTime.UtcNow - request.NegotiationStartedAt.Value).TotalMinutes 
                    : 0;
                
                var negotiationSummary = $"✅ Anlaşma Sağlandı\n" +
                    $"Fiyat: {agreedPrice:N2}₺\n" +
                    $"Kabul Eden: {acceptedBy}\n" +
                    $"Pazarlık Turu: {request.NegotiationRoundCount}\n" +
                    $"Süre: {negotiationDuration:N0} dakika\n" +
                    $"Tarih: {DateTime.UtcNow:dd.MM.yyyy HH:mm}";
                
                request.NegotiationNotes += (string.IsNullOrEmpty(request.NegotiationNotes) ? "" : "\n\n") + negotiationSummary;
                
                await requestNode.PutAsync(request);

                Console.WriteLine($"✅ Anlaşma kaydedildi");

                // Diğer tarafa bildirim gönder
                var otherUserId = request.RequesterId == currentUserId ? request.ProviderId : request.RequesterId;
                Console.WriteLine($"   OtherUserId: {otherUserId}");

                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = otherUserId,
                    Title = "Fiyat Anlaşması",
                    Message = $"'{request.ServiceTitle}' hizmeti için {agreedPrice} ₺ fiyat üzerinde anlaşıldı."
                });

                Console.WriteLine($"✅ Bildirim gönderildi");

                //  Sistem mesajı gönder
                if (!string.IsNullOrEmpty(request.ConversationId))
                {
                    var messageContent = $"🛠️ [{request.ServiceTitle} - Hizmet]\n✅ Anlaşma Sağlandı: {agreedPrice:N2} ₺";
                    Console.WriteLine($"📝 Sistem mesajı gönderiliyor: {messageContent}");

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
                            Console.WriteLine($"✅ Sistem mesajı gönderildi!");
                        }
                        else
                        {
                            Console.WriteLine($"⚠️ Sistem mesajı gönderilemedi: {messageResult.Message}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"⚠️ Kullanıcı bilgisi alınamadı, sistem mesajı gönderilemedi");
                    }
                }
                else
                {
                    Console.WriteLine($"⚠️ ConversationId yok, sistem mesajı gönderilemedi");
                }

                return ServiceResult<bool>.SuccessResult(true, $"Fiyat {agreedPrice} ₺ olarak kabul edildi.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Fiyat kabulü sırasında hata oluştu.", ex.Message);
            }
        }
        // 1. SAĞLAYICI: "Hizmeti Tamamladım" (İşi bitirdim mesajı)
        public async Task<ServiceResult<bool>> ProviderFinishServiceAsync(string requestId, string providerId)
        {
            try
            {
                // Ağ Kontrolü
                if (!NetworkHelper.HasInternetConnection())
                    return ServiceResult<bool>.FailureResult("İnternet bağlantısı yok.");

                var requestNode = _firebaseClient.Child(Constants.ServiceRequestsCollection).Child(requestId);
                var request = await requestNode.OnceSingleAsync<ServiceRequest>();

                if (request == null || request.ProviderId != providerId)
                    return ServiceResult<bool>.FailureResult("Yetkisiz işlem veya talep bulunamadı.");

                // Durum kontrolü: Sadece onaylanmış hizmetler bitirilebilir
                if (request.Status != ServiceRequestStatus.Accepted)
                    return ServiceResult<bool>.FailureResult("Bu hizmetin durumu 'Tamamlandı' olarak işaretlenmeye uygun değil.");

                // Ödeme kontrolü (Eğer ücretliyse)
                if (request.Price > 0 && request.PaymentStatus != ServicePaymentStatus.Paid)
                    return ServiceResult<bool>.FailureResult("Hizmet bedeli henüz ödenmemiş.");

                // Durumu güncelle (Yeni bir status veya flag eklenebilir, şimdilik beklemede tutuyoruz)
                request.UpdatedAt = DateTime.UtcNow;
                // Not: Burada status'ü hemen 'Completed' yapmıyoruz, talep edenin onayını bekliyoruz.
                await requestNode.PutAsync(request);

                // Talep edene bildirim gönder
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = request.RequesterId,
                    Title = "Hizmet Tamamlandı mı?",
                    Message = $"{request.ProviderName}, '{request.ServiceTitle}' hizmetini tamamladığını bildirdi. Lütfen onaylayın.",
                    Type = NotificationType.ServiceCompleted,
                    ActionUrl = nameof(Views.ServiceRequestsPage)
                });

                return ServiceResult<bool>.SuccessResult(true, "Hizmet tamamlandı bildirimi gönderildi.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Hata oluştu.", NetworkHelper.GetUserFriendlyErrorMessage(ex));
            }
        }

        // 2. TALEP EDEN: "Hizmeti Aldım / Onayla" (Krediyi/Ödemeyi serbest bırakır)
        public async Task<ServiceResult<bool>> RequesterConfirmServiceAsync(string requestId, string requesterId)
        {
            try
            {
                var requestNode = _firebaseClient.Child(Constants.ServiceRequestsCollection).Child(requestId);
                var request = await requestNode.OnceSingleAsync<ServiceRequest>();

                if (request == null || request.RequesterId != requesterId)
                    return ServiceResult<bool>.FailureResult("Yetkisiz işlem.");

                // 1. Kredi Transferi (Zaman bankası sistemi için)
                if (request.TimeCreditValue > 0)
                {
                    var transfer = await _userProfileService.TransferTimeCreditsAsync(
                        request.RequesterId, request.ProviderId, request.TimeCreditValue, $"Hizmet Onayı: {request.ServiceTitle}");

                    if (!transfer.Success) return ServiceResult<bool>.FailureResult("Kredi transferi başarısız: " + transfer.Message);
                }

                // 2. Durumu Kapat
                request.Status = ServiceRequestStatus.Completed;
                request.UpdatedAt = DateTime.UtcNow;
                await requestNode.PutAsync(request);

                // 3. Puan Ver (Her iki tarafa da)
                await _userProfileService.AddPointsForAction(request.ProviderId, UserAction.ProvideService);
                await _userProfileService.AddPointsForAction(request.RequesterId, UserAction.ReceiveService);

                // 4. Sağlayıcıya Bildirim
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = request.ProviderId,
                    Title = "İşlem Başarıyla Kapatıldı",
                    Message = $"{request.RequesterName} hizmeti onayladı. Puan ve krediler hesabınıza eklendi.",
                    Type = NotificationType.ServiceCompleted
                });

                return ServiceResult<bool>.SuccessResult(true, "Hizmet başarıyla onaylandı ve tamamlandı.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Hata.", NetworkHelper.GetUserFriendlyErrorMessage(ex));
            }
        }
        // Yardımcı metod: Kullanıcı bilgisini getir
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
    }
}