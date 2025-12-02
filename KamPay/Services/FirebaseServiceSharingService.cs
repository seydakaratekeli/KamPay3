using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Models;
using KamPay.Helpers;
using System; 
using System.Collections.Generic; 
using System.Linq; 
using System.Threading.Tasks;
using System.Threading; // Delay için

namespace KamPay.Services
{
    public class FirebaseServiceSharingService : IServiceSharingService
    {
        private readonly FirebaseClient _firebaseClient;
        private readonly INotificationService _notificationService; // Bildirim servisini ekleyin
        private readonly IUserProfileService _userProfileService; // YENİ SERVİS
        private readonly IMessagingService _messagingService; // 🔥 YENİ: Mesajlaşma servisi
                                                                  // Basit OTP modeli (geçici koleksiyon için)
        internal class TempOtpModel
        {
            public string Otp { get; set; }
            public DateTime ExpiresAt { get; set; }
        }

        private string GenerateOtp() => new Random().Next(100000, 999999).ToString();
        private string GenerateBankReference() => $"BTX-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString().Substring(0, 6)}";


        // Constructor'ı INotificationService alacak şekilde güncelleyin
        public FirebaseServiceSharingService(INotificationService notificationService, IUserProfileService userProfileService, IMessagingService messagingService)
        {
            _firebaseClient = new FirebaseClient(Constants.FirebaseRealtimeDbUrl);
            _notificationService = notificationService;
            _userProfileService = userProfileService; // Ata
            _messagingService = messagingService; // 🔥 YENİ
        }

        // ... CreateServiceOfferAsync ve GetServiceOffersAsync metotları aynı kalacak ...
        public async Task<ServiceResult<ServiceOffer>> CreateServiceOfferAsync(ServiceOffer offer)
        {
            try
            {
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


        // YENİ METODU IMPLEMENTE EDİN
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
                    return ServiceResult<(List<ServiceRequest>, List<ServiceRequest>)>.FailureResult("Kullanıcı ID'si bulunamadı.");
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

                // HATA 1 DÜZELTMESİ: Geri döndürülen Tuple'a doğru isimler veriliyor.
                return ServiceResult<(List<ServiceRequest>, List<ServiceRequest>)>.SuccessResult((Incoming: incoming, Outgoing: outgoing));
            }
            catch (Exception ex)
            {
                return ServiceResult<(List<ServiceRequest>, List<ServiceRequest>)>.FailureResult("Talepler getirilirken bir hata oluştu.", ex.Message);
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

                    // 🔽🔽🔽 BURAYA EKLE:
                    await _firebaseClient
                        .Child(Constants.TempOtpsCollection)
                        .Child(payment.PaymentId)
                        .PutAsync(new TempOtpModel
                        {
                            Otp = otp,
                            ExpiresAt = DateTime.UtcNow.AddMinutes(2)
                        });
                    // 🔼🔼🔼 BURAYA EKLE

                    // İstersen burada log veya debug:
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
    

        /// <summary>
        /// Kullanıcının tüm hizmetlerindeki isim ve profil fotoğrafı bilgilerini günceller
        /// </summary>
        public async Task<ServiceResult<bool>> UpdateUserInfoInServicesAsync(string userId, string newName, string newPhotoUrl)
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
                    service.ProviderName = newName;
                    service.ProviderPhotoUrl = newPhotoUrl;

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

        // 🔥 YENİ METODLAR: Mesajlaşma ve Pazarlık
        
        /// <summary>
        /// Hizmet talebi için konuşma başlatır veya mevcut konuşma ID'sini döndürür
        /// </summary>
        public async Task<ServiceResult<string>> StartConversationForRequestAsync(string requestId, string currentUserId)
        {
            try
            {
                var requestNode = _firebaseClient.Child(Constants.ServiceRequestsCollection).Child(requestId);
                var request = await requestNode.OnceSingleAsync<ServiceRequest>();

                if (request == null)
                    return ServiceResult<string>.FailureResult("Talep bulunamadı.");

                // Kullanıcının talep eden veya sağlayıcı olduğunu doğrula
                if (request.RequesterId != currentUserId && request.ProviderId != currentUserId)
                    return ServiceResult<string>.FailureResult("Bu talebe erişim yetkiniz yok.");

                // Eğer zaten bir konuşma varsa onu döndür
                if (!string.IsNullOrEmpty(request.ConversationId))
                {
                    return ServiceResult<string>.SuccessResult(request.ConversationId, "Mevcut konuşma bulundu.");
                }

                // Yeni konuşma oluştur
                var otherUserId = request.RequesterId == currentUserId ? request.ProviderId : request.RequesterId;
                var conversationResult = await _messagingService.GetOrCreateConversationAsync(currentUserId, otherUserId);

                if (!conversationResult.Success || conversationResult.Data == null)
                    return ServiceResult<string>.FailureResult("Konuşma oluşturulamadı.", conversationResult.Message);

                // Konuşma ID'sini talebe kaydet
                request.ConversationId = conversationResult.Data.ConversationId;
                request.HasActiveConversation = true;
                await requestNode.PutAsync(request);

                // Sistem mesajı gönder
                var systemMessageContent = $"'{request.ServiceTitle}' hizmeti için konuşma başlatıldı. Fiyat: {request.Price} ₺";
                await _messagingService.SendMessageAsync(new SendMessageRequest
                {
                    ReceiverId = otherUserId,
                    Content = systemMessageContent,
                    Type = MessageType.System
                }, await GetUserAsync(currentUserId));

                return ServiceResult<string>.SuccessResult(conversationResult.Data.ConversationId, "Konuşma başlatıldı.");
            }
            catch (Exception ex)
            {
                return ServiceResult<string>.FailureResult("Konuşma başlatılırken hata oluştu.", ex.Message);
            }
        }

        /// <summary>
        /// Talep eden kişinin fiyat teklifi göndermesi
        /// </summary>
        public async Task<ServiceResult<bool>> ProposePrice(string requestId, decimal proposedPrice, string currentUserId)
        {
            try
            {
                var requestNode = _firebaseClient.Child(Constants.ServiceRequestsCollection).Child(requestId);
                var request = await requestNode.OnceSingleAsync<ServiceRequest>();

                if (request == null)
                    return ServiceResult<bool>.FailureResult("Talep bulunamadı.");

                // Sadece talep eden kişi fiyat teklif edebilir
                if (request.RequesterId != currentUserId)
                    return ServiceResult<bool>.FailureResult("Sadece talep eden kişi fiyat teklif edebilir.");

                if (proposedPrice <= 0)
                    return ServiceResult<bool>.FailureResult("Geçerli bir fiyat giriniz.");

                // Fiyat teklifini kaydet
                request.ProposedPriceByRequester = proposedPrice;
                request.IsNegotiating = true;
                request.LastNegotiationDate = DateTime.UtcNow;
                await requestNode.PutAsync(request);

                // Sağlayıcıya bildirim gönder
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = request.ProviderId,
                    Title = "Yeni Fiyat Teklifi",
                    Message = $"{request.RequesterName}, '{request.ServiceTitle}' hizmeti için {proposedPrice} ₺ teklif etti. (Orijinal fiyat: {request.Price} ₺)"
                });

                // Eğer konuşma varsa, mesaj olarak da gönder
                if (!string.IsNullOrEmpty(request.ConversationId))
                {
                    var messageContent = $"💰 Fiyat Teklifi: {proposedPrice} ₺ (Orijinal: {request.Price} ₺)";
                    await _messagingService.SendMessageAsync(new SendMessageRequest
                    {
                        ReceiverId = request.ProviderId,
                        Content = messageContent,
                        Type = MessageType.Text
                    }, await GetUserAsync(currentUserId));
                }

                return ServiceResult<bool>.SuccessResult(true, "Fiyat teklifiniz gönderildi.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Fiyat teklifi gönderilemedi.", ex.Message);
            }
        }

        /// <summary>
        /// Hizmet sağlayıcısının karşı teklif göndermesi
        /// </summary>
        public async Task<ServiceResult<bool>> SendCounterOfferAsync(string requestId, decimal counterOffer, string currentUserId)
        {
            try
            {
                var requestNode = _firebaseClient.Child(Constants.ServiceRequestsCollection).Child(requestId);
                var request = await requestNode.OnceSingleAsync<ServiceRequest>();

                if (request == null)
                    return ServiceResult<bool>.FailureResult("Talep bulunamadı.");

                // Sadece hizmet sağlayıcı karşı teklif verebilir
                if (request.ProviderId != currentUserId)
                    return ServiceResult<bool>.FailureResult("Sadece hizmet sağlayıcı karşı teklif verebilir.");

                if (counterOffer <= 0)
                    return ServiceResult<bool>.FailureResult("Geçerli bir fiyat giriniz.");

                // Karşı teklifi kaydet
                request.CounterOfferByProvider = counterOffer;
                request.IsNegotiating = true;
                request.LastNegotiationDate = DateTime.UtcNow;
                await requestNode.PutAsync(request);

                // Talep eden kişiye bildirim gönder
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = request.RequesterId,
                    Title = "Karşı Teklif Alındı",
                    Message = $"'{request.ServiceTitle}' hizmeti için karşı teklif: {counterOffer} ₺"
                });

                // Eğer konuşma varsa, mesaj olarak da gönder
                if (!string.IsNullOrEmpty(request.ConversationId))
                {
                    var messageContent = $"💰 Karşı Teklif: {counterOffer} ₺";
                    await _messagingService.SendMessageAsync(new SendMessageRequest
                    {
                        ReceiverId = request.RequesterId,
                        Content = messageContent,
                        Type = MessageType.Text
                    }, await GetUserAsync(currentUserId));
                }

                return ServiceResult<bool>.SuccessResult(true, "Karşı teklifiniz gönderildi.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Karşı teklif gönderilemedi.", ex.Message);
            }
        }

        /// <summary>
        /// Pazarlık sonucu anlaşılan fiyatı kabul etme
        /// </summary>
        public async Task<ServiceResult<bool>> AcceptNegotiatedPriceAsync(string requestId, string currentUserId)
        {
            try
            {
                var requestNode = _firebaseClient.Child(Constants.ServiceRequestsCollection).Child(requestId);
                var request = await requestNode.OnceSingleAsync<ServiceRequest>();

                if (request == null)
                    return ServiceResult<bool>.FailureResult("Talep bulunamadı.");

                // Kullanıcının talep eden veya sağlayıcı olduğunu doğrula
                if (request.RequesterId != currentUserId && request.ProviderId != currentUserId)
                    return ServiceResult<bool>.FailureResult("Bu talebe erişim yetkiniz yok.");

                if (!request.IsNegotiating)
                    return ServiceResult<bool>.FailureResult("Aktif bir pazarlık bulunmuyor.");

                // Son teklif edilen fiyatı belirle
                decimal agreedPrice = 0;
                if (request.CounterOfferByProvider.HasValue && request.CounterOfferByProvider.Value > 0)
                {
                    agreedPrice = request.CounterOfferByProvider.Value;
                }
                else if (request.ProposedPriceByRequester.HasValue && request.ProposedPriceByRequester.Value > 0)
                {
                    agreedPrice = request.ProposedPriceByRequester.Value;
                }
                else
                {
                    return ServiceResult<bool>.FailureResult("Kabul edilecek bir teklif bulunamadı.");
                }

                // Anlaşılan fiyatı kaydet
                request.QuotedPrice = agreedPrice;
                request.Price = agreedPrice;
                request.IsNegotiating = false;
                request.NegotiationNotes = $"Fiyat {agreedPrice} ₺ olarak anlaşıldı. Kabul eden: {currentUserId}";
                await requestNode.PutAsync(request);

                // Diğer tarafa bildirim gönder
                var otherUserId = request.RequesterId == currentUserId ? request.ProviderId : request.RequesterId;
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = otherUserId,
                    Title = "Fiyat Anlaşması",
                    Message = $"'{request.ServiceTitle}' hizmeti için {agreedPrice} ₺ fiyat üzerinde anlaşıldı."
                });

                // Konuşmaya sistem mesajı ekle
                if (!string.IsNullOrEmpty(request.ConversationId))
                {
                    var messageContent = $"✅ Fiyat anlaşıldı: {agreedPrice} ₺";
                    await _messagingService.SendMessageAsync(new SendMessageRequest
                    {
                        ReceiverId = otherUserId,
                        Content = messageContent,
                        Type = MessageType.System
                    }, await GetUserAsync(currentUserId));
                }

                return ServiceResult<bool>.SuccessResult(true, $"Fiyat {agreedPrice} ₺ olarak kabul edildi.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Fiyat kabulü sırasında hata oluştu.", ex.Message);
            }
        }

        // Yardımcı metod: Kullanıcı bilgisini getir
        private async Task<User> GetUserAsync(string userId)
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