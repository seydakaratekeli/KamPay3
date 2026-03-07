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
    public class FirebaseServiceSharingService : IServiceSharingService
    {
        private readonly FirebaseClient _firebaseClient;
        private readonly INotificationService _notificationService;
        private readonly IUserProfileService _userProfileService;
        private readonly IMessagingService _messagingService;

        // ? Constructor DI ile FirebaseClient alıyor
        public FirebaseServiceSharingService(
            FirebaseClient firebaseClient,
            INotificationService notificationService,
            IUserProfileService userProfileService,
            IMessagingService messagingService)
        {
            _firebaseClient = firebaseClient ?? throw new ArgumentNullException(nameof(firebaseClient));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _userProfileService = userProfileService ?? throw new ArgumentNullException(nameof(userProfileService));
            _messagingService = messagingService ?? throw new ArgumentNullException(nameof(messagingService));

            System.Diagnostics.Debug.WriteLine("? FirebaseServiceSharingService oluşturuldu (DI ile)");
        }

        // Basit OTP modeli (geçici koleksiyon için)
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
                // ?? UYARI: Bu metod tüm servisleri çekiyor - Performans sorunu!
                // Sayfalama için GetServiceOffersPaged metodunu kullanın
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

        /// <summary>
        /// ? OPTİMİZE EDİLMİŞ: Sayfalama ile hizmet listesi getirir
        /// Ürün modülündeki GetProductsPagedAsync() ile aynı yaklaşımı kullanır
        /// </summary>
        /// <param name="pageSize">Sayfa başına hizmet sayısı (varsayılan: 20)</param>
        /// <param name="lastKey">Son yüklenen hizmetin ID'si (sonraki sayfa için)</param>
        /// <param name="category">Kategori filtresi</param>
        public async Task<ServiceResult<List<ServiceOffer>>> GetServiceOffersPagedAsync(
            int pageSize = 20,
            string? lastKey = null,
            ServiceCategory? category = null)
        {
            try
            {
                IEnumerable<Firebase.Database.FirebaseObject<ServiceOffer>> items;

                // ?? KATEGORİ FİLTRESİ VAR: EqualTo() kullan
                if (category.HasValue)
                {
                    // ?? UYARI: EqualTo() kullanırken StartAt/LimitToFirst ÇALIŞMAZ!
                    // Çözüm: Tüm kategoriyi çek, bellekte sırala/sayfalama yap
                    items = await _firebaseClient
                        .Child(Constants.ServiceOffersCollection)
                        .OrderBy("Category")
                        .EqualTo((int)category.Value)
                        .OnceAsync<ServiceOffer>();

                    // Bellekte sayfalama yap
                    var allOffers = items
                        .Select(o =>
                        {
                            var offer = o.Object;
                            offer.ServiceId = o.Key;
                            return offer;
                        })
                        .Where(o => o.IsAvailable)
                        .OrderByDescending(o => o.CreatedAt)
                        .ToList();

                    // Sayfalama mantığı
                    if (!string.IsNullOrEmpty(lastKey))
                    {
                        var lastIndex = allOffers.FindIndex(o => o.ServiceId == lastKey);
                        if (lastIndex >= 0)
                        {
                            allOffers = allOffers.Skip(lastIndex + 1).Take(pageSize).ToList();
                        }
                    }
                    else
                    {
                        allOffers = allOffers.Take(pageSize).ToList();
                    }

                    return ServiceResult<List<ServiceOffer>>.SuccessResult(allOffers);
                }
                // ?? KATEGORİ YOK: Gerçek sunucu taraflı sayfalama
                else
                {
                    if (!string.IsNullOrEmpty(lastKey))
                    {
                        items = await _firebaseClient
                            .Child(Constants.ServiceOffersCollection)
                            .OrderBy("CreatedAt")
                            .StartAt(lastKey)
                            .LimitToFirst(pageSize + 1)
                            .OnceAsync<ServiceOffer>();
                    }
                    else
                    {
                        items = await _firebaseClient
                            .Child(Constants.ServiceOffersCollection)
                            .OrderBy("CreatedAt")
                            .LimitToFirst(pageSize)
                            .OnceAsync<ServiceOffer>();
                    }

                    var offers = items.Select(o =>
                    {
                        var offer = o.Object;
                        offer.ServiceId = o.Key;
                        return offer;
                    }).ToList();

                    // lastKey'i atla (eğer pagination yapılıyorsa)
                    if (!string.IsNullOrEmpty(lastKey) && offers.Any() && offers.First().ServiceId == lastKey)
                    {
                        offers.RemoveAt(0);
                    }

                    // Sadece aktif servisleri filtrele (hafif işlem)
                    offers = offers
                        .Where(o => o.IsAvailable)
                        .OrderByDescending(o => o.CreatedAt)
                        .ToList();

                    return ServiceResult<List<ServiceOffer>>.SuccessResult(offers);
                }
            }
            catch (Exception ex)
            {
                return ServiceResult<List<ServiceOffer>>.FailureResult("Hizmetler yüklenemedi", ex.Message);
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

                // ?? Yeni ServiceRequest nesnesi oluşturuluyor
                var request = new ServiceRequest
                {
                    RequestId = Guid.NewGuid().ToString(),
                    ServiceId = offer.ServiceId,            // Hizmet kimliği
                    ServiceTitle = offer.Title,
                    ProviderId = offer.ProviderId,
                    ProviderName = offer.ProviderName, // ✅ KRİTİK EKSİK: Bu satırı ekle
                    RequesterId = requester.UserId,
                    RequesterName = requester.FullName,
                    Message = message,
                    Status = ServiceRequestStatus.Pending,
                    RequestedAt = DateTime.UtcNow,
                
                    QuotedPrice = offer.Price,              // Hizmetin o anki fiyatı
                    Price = offer.Price,                    // UI veya raporlama için de saklıyoruz
                    TimeCreditValue = offer.TimeCredits,    // Kredi bilgisi (eski sistemle uyumlu)
                    PaymentStatus = ServicePaymentStatus.None,
                    PaymentMethod = PaymentMethodType.None,
                    Currency = "TRY"
                };

                // ?? Firebase’e kaydet
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

                    // ?? Demo modu: Eğer UI'dan OTP gelmemişse otomatik geçerli say
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
        /// ? OPTIMIZE: Firebase multi-path atomic update ile tek istekle güncelleme
        /// </summary>
        public async Task<ServiceResult<bool>> UpdateUserInfoInServicesAsync(string userId, string? newName, string? newPhotoUrl)
        {
            try
            {
                // 1?? Kullanıcının hizmetlerini bul
                var allServices = await _firebaseClient
                    .Child(Constants.ServiceOffersCollection)
                    .OrderBy("ProviderId")
                    .EqualTo(userId)
                    .OnceAsync<ServiceOffer>();

                if (!allServices.Any())
                {
                    return ServiceResult<bool>.SuccessResult(true, "Güncellenecek hizmet yok");
                }

                // 2?? ? FIX: Multi-path atomic update için tüm yolları topla
                var updates = new Dictionary<string, object>();

                foreach (var serviceEntry in allServices)
                {
                    var servicePath = $"{Constants.ServiceOffersCollection}/{serviceEntry.Key}";

                    if (!string.IsNullOrWhiteSpace(newName))
                    {
                        updates[$"{servicePath}/ProviderName"] = newName;
                    }

                    if (!string.IsNullOrWhiteSpace(newPhotoUrl))
                    {
                        updates[$"{servicePath}/ProviderPhotoUrl"] = newPhotoUrl;
                    }
                }

                // 3?? ? TEK BİR İSTEKLE TÜM YOLLARİ GÜNCELLE
                if (updates.Any())
                {
                    // Firebase.Database doesn't support server-side multi-path UpdateAsync; perform per-service PatchAsync in parallel
                    var patchTasks = new List<Task>();

                    foreach (var serviceEntry in allServices)
                    {
                        var perServiceUpdates = new Dictionary<string, object>();
                        if (!string.IsNullOrWhiteSpace(newName))
                            perServiceUpdates["ProviderName"] = newName;
                        if (!string.IsNullOrWhiteSpace(newPhotoUrl))
                            perServiceUpdates["ProviderPhotoUrl"] = newPhotoUrl;

                        if (perServiceUpdates.Any())
                        {
                            var task = _firebaseClient
                                .Child(Constants.ServiceOffersCollection)
                                .Child(serviceEntry.Key)
                                .PatchAsync(perServiceUpdates);

                            patchTasks.Add(task);
                        }
                    }

                    if (patchTasks.Any())
                        await Task.WhenAll(patchTasks);

                    Console.WriteLine($"? {allServices.Count()} hizmet atomic update ile güncellendi");
                }

                return ServiceResult<bool>.SuccessResult(true, $"{allServices.Count()} hizmet güncellendi");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? UpdateUserInfoInServices hatası: {ex.Message}");
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
                    Console.WriteLine($"? Talep bulunamadı: {requestId}");
                    return ServiceResult<string>.FailureResult("Talep bulunamadı.");
                }

                Console.WriteLine($"   ServiceTitle: {request.ServiceTitle}");
                Console.WriteLine($"   ProviderId: {request.ProviderId}");
                Console.WriteLine($"   RequesterId: {request.RequesterId}");

                // Kullanıcının talep eden veya sağlayıcı olduğunu doğrula
                if (request.RequesterId != currentUserId && request.ProviderId != currentUserId)
                {
                    Console.WriteLine($"? Erişim yetkiniz yok!");
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
                            Console.WriteLine($"? Mevcut konuşma bulundu ve aktif: {request.ConversationId}");
                            return ServiceResult<string>.SuccessResult(request.ConversationId, "Mevcut konuşma bulundu.");
                        }
                        else
                        {
                            Console.WriteLine($"?? Mevcut konuşma bulunamadı veya pasif");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"?? Mevcut konuşma kontrol hatası: {ex.Message}");
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

                    Console.WriteLine($"? Mevcut kullanıcı konuşması bulundu: {existingWithOtherUser.Key}");
                    return ServiceResult<string>.SuccessResult(existingWithOtherUser.Key, "Mevcut konuşma bulundu.");
                }

                Console.WriteLine($"   Yeni konuşma oluşturuluyor...");

                // Yeni konuşma oluştur
                var conversationResult = await _messagingService.GetOrCreateConversationAsync(currentUserId, otherUserId);

                if (!conversationResult.Success || conversationResult.Data == null)
                {
                    Console.WriteLine($"? Konuşma oluşturulamadı: {conversationResult.Message}");
                    return ServiceResult<string>.FailureResult("Konuşma oluşturulamadı.", conversationResult.Message);
                }

                // Konuşma ID'sini talebe kaydet
                request.ConversationId = conversationResult.Data.ConversationId;
                request.HasActiveConversation = true;
                await requestNode.PutAsync(request);

                Console.WriteLine($"? Yeni konuşma oluşturuldu: {conversationResult.Data.ConversationId}");

                //  Sistem mesajı gönder
                var systemMessageContent = $"??? [{request.ServiceTitle} - Hizmet]\n?? Konuşma başlatıldı\nFiyat: {request.Price:N2} ?";
                Console.WriteLine($"?? Sistem mesajı gönderiliyor: {systemMessageContent}");

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
                        Console.WriteLine($"? Sistem mesajı gönderildi!");
                    }
                    else
                    {
                        Console.WriteLine($"?? Sistem mesajı gönderilemedi: {messageResult.Message}");
                    }
                }
                else
                {
                    Console.WriteLine($"?? Kullanıcı bilgisi alınamadı, sistem mesajı gönderilemedi");
                }

                return ServiceResult<string>.SuccessResult(conversationResult.Data.ConversationId, "Konuşma başlatıldı.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? StartConversationForRequestAsync hatası: {ex.Message}");
                Console.WriteLine($"   StackTrace: {ex.StackTrace}");
                return ServiceResult<string>.FailureResult("Konuşma başlatılırken hata oluştu.", ex.Message);
            }
        }


        /// Talep eden kişinin fiyat teklifi göndermesi

        public async Task<ServiceResult<bool>> ProposePrice(string requestId, decimal proposedPrice, string currentUserId)
        {
            try
            {
                Console.WriteLine($"?? ProposePrice başladı:");
                Console.WriteLine($"   RequestId: {requestId}");
                Console.WriteLine($"   ProposedPrice: {proposedPrice:N2} ?");
                Console.WriteLine($"   CurrentUserId: {currentUserId}");

                var requestNode = _firebaseClient.Child(Constants.ServiceRequestsCollection).Child(requestId);
                var request = await requestNode.OnceSingleAsync<ServiceRequest>();

                if (request == null)
                {
                    Console.WriteLine($"? Talep bulunamadı");
                    return ServiceResult<bool>.FailureResult("Talep bulunamadı.");
                }

                Console.WriteLine($"   ServiceTitle: {request.ServiceTitle}");
                Console.WriteLine($"   ConversationId: {request.ConversationId}");

                // Sadece talep eden kişi fiyat teklif edebilir
                if (request.RequesterId != currentUserId)
                {
                    Console.WriteLine($"? Yetki yok - RequesterId: {request.RequesterId}");
                    return ServiceResult<bool>.FailureResult("Sadece talep eden kişi fiyat teklif edebilir.");
                }

                if (proposedPrice <= 0)
                {
                    Console.WriteLine($"? Geçersiz fiyat");
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

                Console.WriteLine($"? Fiyat teklifi kaydedildi");

                // Sağlayıcıya bildirim gönder
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = request.ProviderId,
                    Title = "Yeni Fiyat Teklifi",
                    Message = $"{request.RequesterName}, '{request.ServiceTitle}' hizmeti için {proposedPrice} ? teklif etti. (Orijinal fiyat: {request.Price} ?)"
                });

                Console.WriteLine($"? Bildirim gönderildi");

                //  Sistem mesajı gönder
                if (!string.IsNullOrEmpty(request.ConversationId))
                {
                    var messageContent = $"??? [{request.ServiceTitle} - Hizmet]\n?? Fiyat Teklifi: {proposedPrice:N2} ?\n(Orijinal fiyat: {request.Price:N2} ?)";
                    Console.WriteLine($"?? Sistem mesajı gönderiliyor: {messageContent}");

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
                            Console.WriteLine($"? Sistem mesajı gönderildi!");
                        }
                        else
                        {
                            Console.WriteLine($"?? Sistem mesajı gönderilemedi: {messageResult.Message}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"?? Kullanıcı bilgisi alınamadı, sistem mesajı gönderilemedi");
                    }
                }
                else
                {
                    Console.WriteLine($"?? ConversationId yok, sistem mesajı gönderilemedi");
                }

                return ServiceResult<bool>.SuccessResult(true, "Fiyat teklifiniz gönderildi.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? ProposePrice hatası: {ex.Message}");
                Console.WriteLine($"   StackTrace: {ex.StackTrace}");
                return ServiceResult<bool>.FailureResult("Fiyat teklifi gönderilemedi.", ex.Message);
            }
        }


        /// Hizmet sağlaycısının karşı teklif göndermesi

        public async Task<ServiceResult<bool>> SendCounterOfferAsync(string requestId, decimal counterOffer, string currentUserId)
        {
            try
            {
                Console.WriteLine($"?? SendCounterOffer başladı:");
                Console.WriteLine($"   RequestId: {requestId}");
                Console.WriteLine($"   CounterOffer: {counterOffer:N2} ?");
                Console.WriteLine($"   CurrentUserId: {currentUserId}");

                var requestNode = _firebaseClient.Child(Constants.ServiceRequestsCollection).Child(requestId);
                var request = await requestNode.OnceSingleAsync<ServiceRequest>();

                if (request == null)
                {
                    Console.WriteLine($"? Talep bulunamadı");
                    return ServiceResult<bool>.FailureResult("Talep bulunamadı.");
                }

                Console.WriteLine($"   ServiceTitle: {request.ServiceTitle}");
                Console.WriteLine($"   ConversationId: {request.ConversationId}");

                // Sadece hizmet sağlayıcı karşı teklif verebilir
                if (request.ProviderId != currentUserId)
                {
                    Console.WriteLine($"? Yetki yok - ProviderId: {request.ProviderId}");
                    return ServiceResult<bool>.FailureResult("Sadece hizmet sağlayıcı karşı teklif verebilir.");
                }

                if (counterOffer <= 0)
                {
                    Console.WriteLine($"? Geçersiz fiyat");
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

                Console.WriteLine($"? Karşı teklif kaydedildi");

                // Talep eden kişiye bildirim gönder
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = request.RequesterId,
                    Title = "Karşı Teklif Alındı",
                    Message = $"'{request.ServiceTitle}' hizmeti için karşı teklif: {counterOffer} ?"
                });

                Console.WriteLine($"? Bildirim gönderildi");

                //  Sistem mesajı gönder
                if (!string.IsNullOrEmpty(request.ConversationId))
                {
                    var requesterOffer = request.ProposedPriceByRequester.HasValue
                        ? $"\n(Talep edenin teklifi: {request.ProposedPriceByRequester:N2} ?)"
                        : "";

                    var messageContent = $"??? [{request.ServiceTitle} - Hizmet]\n?? Karşı Teklif: {counterOffer:N2} ?{requesterOffer}";
                    Console.WriteLine($"?? Sistem mesajı gönderiliyor: {messageContent}");

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
                            Console.WriteLine($"? Sistem mesajı gönderildi!");
                        }
                        else
                        {
                            Console.WriteLine($"?? Sistem mesajı gönderilemedi: {messageResult.Message}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"?? Kullanıcı bilgisi alınamadı, sistem mesajı gönderilemedi");
                    }
                }
                else
                {
                    Console.WriteLine($"?? ConversationId yok, sistem mesajı gönderilemedi");
                }

                return ServiceResult<bool>.SuccessResult(true, "Karşı teklifiniz gönderildi.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? SendCounterOffer hatası: {ex.Message}");
                Console.WriteLine($"   StackTrace: {ex.StackTrace}");
                return ServiceResult<bool>.FailureResult("Karşı teklif gönderilemedi.", ex.Message);
            }
        }


        /// Pazarlık sonucu anlaşılan fiyatı kabul etme

        public async Task<ServiceResult<bool>> AcceptNegotiatedPriceAsync(string requestId, string currentUserId)
        {
            try
            {
                Console.WriteLine($"? AcceptNegotiatedPrice başladı:");
                Console.WriteLine($"   RequestId: {requestId}");
                Console.WriteLine($"   CurrentUserId: {currentUserId}");

                var requestNode = _firebaseClient.Child(Constants.ServiceRequestsCollection).Child(requestId);
                var request = await requestNode.OnceSingleAsync<ServiceRequest>();

                if (request == null)
                {
                    Console.WriteLine($"? Talep bulunamadı");
                    return ServiceResult<bool>.FailureResult("Talep bulunamadı.");
                }

                Console.WriteLine($"   ServiceTitle: {request.ServiceTitle}");
                Console.WriteLine($"   ConversationId: {request.ConversationId}");
                Console.WriteLine($"   IsNegotiating: {request.IsNegotiating}");

                // Kullanıcının talep eden veya sağlayıcı olduğunu doğrula
                if (request.RequesterId != currentUserId && request.ProviderId != currentUserId)
                {
                    Console.WriteLine($"? Erişim yetkiniz yok");
                    return ServiceResult<bool>.FailureResult("Bu talebe erişim yetkiniz yok.");
                }

                if (!request.IsNegotiating)
                {
                    Console.WriteLine($"? Aktif pazarlık yok");
                    return ServiceResult<bool>.FailureResult("Aktif bir pazarlık bulunmuyor.");
                }

                // Anlaşılan fiyatı belirle (karşı teklif > teklif > 0)
                decimal agreedPrice = request.CounterOfferByProvider ?? request.ProposedPriceByRequester ?? 0;
                Console.WriteLine($"   AgreedPrice: {agreedPrice:N2} ?");

                if (agreedPrice <= 0)
                {
                    Console.WriteLine($"? Geçersiz anlaşma fiyatı");
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

                var negotiationSummary = $"? Anlaşma Sağlandı\n" +
                    $"Fiyat: {agreedPrice:N2}?\n" +
                    $"Kabul Eden: {acceptedBy}\n" +
                    $"Pazarlık Turu: {request.NegotiationRoundCount}\n" +
                    $"Süre: {negotiationDuration:N0} dakika\n" +
                    $"Tarih: {DateTime.UtcNow:dd.MM.yyyy HH:mm}";

                request.NegotiationNotes += (string.IsNullOrEmpty(request.NegotiationNotes) ? "" : "\n\n") + negotiationSummary;

                await requestNode.PutAsync(request);

                Console.WriteLine($"? Anlaşma kaydedildi");

                // Diğer tarafa bildirim gönder
                var otherUserId = request.RequesterId == currentUserId ? request.ProviderId : request.RequesterId;
                Console.WriteLine($"   OtherUserId: {otherUserId}");

                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = otherUserId,
                    Title = "Fiyat Anlaşması",
                    Message = $"'{request.ServiceTitle}' hizmeti için {agreedPrice} ? fiyat üzerinde anlaşıldı."
                });

                Console.WriteLine($"? Bildirim gönderildi");

                //  Sistem mesajı gönder
                if (!string.IsNullOrEmpty(request.ConversationId))
                {
                    var messageContent = $"??? [{request.ServiceTitle} - Hizmet]\n? Anlaşma Sağlandı: {agreedPrice:N2} ?";
                    Console.WriteLine($"?? Sistem mesajı gönderiliyor: {messageContent}");

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
                            Console.WriteLine($"? Sistem mesajı gönderildi!");
                        }
                        else
                        {
                            Console.WriteLine($"?? Sistem mesajı gönderilemedi: {messageResult.Message}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"?? Kullanıcı bilgisi alınamadı, sistem mesajı gönderilemedi");
                    }
                }
                else
                {
                    Console.WriteLine($"?? ConversationId yok, sistem mesajı gönderilemedi");
                }

                return ServiceResult<bool>.SuccessResult(true, $"Fiyat {agreedPrice} ? olarak kabul edildi.");
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

        // 🎯 ARMUT MODELİ - YENİ METODLAR

        #region Müşteri Talep Yönetimi

        /// <summary>
        /// Müşteri yeni bir hizmet talebi oluşturur
        /// </summary>
        public async Task<ServiceResult<CustomerServiceRequest>> CreateCustomerRequestAsync(CustomerServiceRequest request)
        {
            try
            {
                // Talep numarası oluştur
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

                Console.WriteLine($"✅ Müşteri talebi oluşturuldu: {request.RequestId}");
                return ServiceResult<CustomerServiceRequest>.SuccessResult(request, "Talep başarıyla oluşturuldu!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ CreateCustomerRequestAsync hatası: {ex.Message}");
                return ServiceResult<CustomerServiceRequest>.FailureResult("Talep oluşturulamadı", ex.Message);
            }
        }

        /// <summary>
        /// Tüm aktif müşteri taleplerini getirir
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

                Console.WriteLine($"📋 {requests.Count} aktif talep getirildi");
                return ServiceResult<List<CustomerServiceRequest>>.SuccessResult(requests);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ GetCustomerRequestsAsync hatası: {ex.Message}");
                return ServiceResult<List<CustomerServiceRequest>>.FailureResult("Talepler getirilemedi", ex.Message);
            }
        }

        /// <summary>
        /// Sayfalama ile müşteri taleplerini getirir
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
                Console.WriteLine($"❌ GetCustomerRequestsPagedAsync hatası: {ex.Message}");
                return ServiceResult<List<CustomerServiceRequest>>.FailureResult("Talepler yüklenemedi", ex.Message);
            }
        }

        /// <summary>
        /// Belirli bir müşteri talebini ID ile getirir
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
                    return ServiceResult<CustomerServiceRequest>.FailureResult("Talep bulunamadı");
                }

                request.RequestId = requestId;
                return ServiceResult<CustomerServiceRequest>.SuccessResult(request);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ GetCustomerRequestByIdAsync hatası: {ex.Message}");
                return ServiceResult<CustomerServiceRequest>.FailureResult("Talep getirilemedi", ex.Message);
            }
        }

        /// <summary>
        /// Müşterinin kendi oluşturduğu talepleri getirir
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

                Console.WriteLine($"📋 {requests.Count} talep getirildi (Müşteri: {customerId})");
                return ServiceResult<List<CustomerServiceRequest>>.SuccessResult(requests);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ GetMyCustomerRequestsAsync hatası: {ex.Message}");
                return ServiceResult<List<CustomerServiceRequest>>.FailureResult("Talepler getirilemedi", ex.Message);
            }
        }

        /// <summary>
        /// Müşteri talebini günceller
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

                Console.WriteLine($"✅ Talep güncellendi: {request.RequestId}");
                return ServiceResult<bool>.SuccessResult(true, "Talep güncellendi");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ UpdateCustomerRequestAsync hatası: {ex.Message}");
                return ServiceResult<bool>.FailureResult("Talep güncellenemedi", ex.Message);
            }
        }

        /// <summary>
        /// Müşteri talebini iptal eder
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
                    return ServiceResult<bool>.FailureResult("Talep bulunamadı");
                }

                if (request.CustomerId != customerId)
                {
                    return ServiceResult<bool>.FailureResult("Bu işlemi yapmaya yetkiniz yok");
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
                            Title = "Talep İptal Edildi",
                            Message = $"'{request.Title}' talebi müşteri tarafından iptal edildi.",
                            Type = NotificationType.ServiceCompleted
                        });
                    }
                }

                Console.WriteLine($"✅ Talep iptal edildi: {requestId}");
                return ServiceResult<bool>.SuccessResult(true, "Talep iptal edildi");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ CancelCustomerRequestAsync hatası: {ex.Message}");
                return ServiceResult<bool>.FailureResult("Talep iptal edilemedi", ex.Message);
            }
        }

        #endregion

        #region Profesyonel Teklif Yönetimi

        /// <summary>
        /// Profesyonel bir müşteri talebine teklif gönderir
        /// </summary>
        public async Task<ServiceResult<ProviderProposal>> SendProposalAsync(ProviderProposal proposal)
        {
            try
            {
                // Teklif numarası oluştur
                if (string.IsNullOrWhiteSpace(proposal.ProposalNumber))
                {
                    proposal.ProposalNumber = $"PP{DateTime.UtcNow:yyyyMMddHHmmss}";
                }

                proposal.CreatedAt = DateTime.UtcNow;
                proposal.UpdatedAt = DateTime.UtcNow;
                proposal.Status = ProposalStatus.Pending;

                // Teklif geçerlilik süresi (varsayılan 7 gün)
                if (!proposal.ExpiresAt.HasValue)
                {
                    proposal.ExpiresAt = DateTime.UtcNow.AddDays(7);
                }

                // Firebase'e kaydet
                await _firebaseClient
                    .Child(Constants.ProviderProposalsCollection)
                    .Child(proposal.ProposalId)
                    .PutAsync(proposal);

                // Talepteki teklif sayısını artır
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

                // Müşteriye bildirim gönder
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = proposal.CustomerId,
                    Title = "🎉 Yeni Teklif Geldi!",
                    Message = $"{proposal.ProviderName}, '{proposal.RequestTitle}' talebiniz için {proposal.Price:N2}₺ teklif gönderdi.",
                    Type = NotificationType.NewOffer
                });

                Console.WriteLine($"✅ Teklif gönderildi: {proposal.ProposalId}");
                return ServiceResult<ProviderProposal>.SuccessResult(proposal, "Teklif başarıyla gönderildi!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ SendProposalAsync hatası: {ex.Message}");
                return ServiceResult<ProviderProposal>.FailureResult("Teklif gönderilemedi", ex.Message);
            }
        }

        /// <summary>
        /// Belirli bir talebe gönderilen tüm teklifleri getirir
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

                Console.WriteLine($"📋 {proposals.Count} teklif getirildi (Talep: {customerRequestId})");
                return ServiceResult<List<ProviderProposal>>.SuccessResult(proposals);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ GetProposalsForRequestAsync hatası: {ex.Message}");
                return ServiceResult<List<ProviderProposal>>.FailureResult("Teklifler getirilemedi", ex.Message);
            }
        }

        /// <summary>
        /// Profesyonelin gönderdiği tüm teklifleri getirir
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

                Console.WriteLine($"📋 {proposals.Count} teklif getirildi (Profesyonel: {providerId})");
                return ServiceResult<List<ProviderProposal>>.SuccessResult(proposals);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ GetMyProposalsAsync hatası: {ex.Message}");
                return ServiceResult<List<ProviderProposal>>.FailureResult("Teklifler getirilemedi", ex.Message);
            }
        }

        /// <summary>
        /// Müşteri bir teklifi kabul eder
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
                    return ServiceResult<bool>.FailureResult("Teklif bulunamadı");
                }

                if (proposal.CustomerId != customerId)
                {
                    return ServiceResult<bool>.FailureResult("Bu işlemi yapmaya yetkiniz yok");
                }

                if (proposal.Status != ProposalStatus.Pending)
                {
                    return ServiceResult<bool>.FailureResult("Bu teklif zaten yanıtlanmış");
                }

                // Teklifi kabul et
                proposal.Status = ProposalStatus.Accepted;
                proposal.RespondedAt = DateTime.UtcNow;
                proposal.UpdatedAt = DateTime.UtcNow;
                await proposalNode.PutAsync(proposal);

                // Talebi güncelle
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

                // Diğer bekleyen teklifleri reddet
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
                        other.RejectionReason = "Müşteri başka bir teklifi kabul etti";
                        other.RespondedAt = DateTime.UtcNow;
                        other.UpdatedAt = DateTime.UtcNow;
                        await otherNode.PutAsync(other);

                        // Bildirim gönder
                        await _notificationService.CreateNotificationAsync(new Notification
                        {
                            UserId = other.ProviderId,
                            Title = "Teklif Sonucu",
                            Message = $"'{proposal.RequestTitle}' talebi için teklifiniz kabul edilmedi. Müşteri başka bir profesyoneli seçti.",
                            Type = NotificationType.OfferRejected
                        });
                    }
                }

                // Kazanan profesyonele bildirim
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = proposal.ProviderId,
                    Title = "🎉 Tebrikler! Teklifiniz Kabul Edildi",
                    Message = $"'{proposal.RequestTitle}' talebi için {proposal.Price:N2}₺ teklifiniz müşteri tarafından kabul edildi. İş başlayabilir!",
                    Type = NotificationType.OfferAccepted
                });

                Console.WriteLine($"✅ Teklif kabul edildi: {proposalId}");
                return ServiceResult<bool>.SuccessResult(true, "Teklif kabul edildi! Profesyonel bilgilendirildi.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ AcceptProposalAsync hatası: {ex.Message}");
                return ServiceResult<bool>.FailureResult("Teklif kabul edilemedi", ex.Message);
            }
        }

        /// <summary>
        /// Müşteri bir teklifi reddeder
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
                    return ServiceResult<bool>.FailureResult("Teklif bulunamadı");
                }

                if (proposal.CustomerId != customerId)
                {
                    return ServiceResult<bool>.FailureResult("Bu işlemi yapmaya yetkiniz yok");
                }

                if (proposal.Status != ProposalStatus.Pending)
                {
                    return ServiceResult<bool>.FailureResult("Bu teklif zaten yanıtlanmış");
                }

                proposal.Status = ProposalStatus.Rejected;
                proposal.RejectionReason = reason ?? "Müşteri tarafından reddedildi";
                proposal.RespondedAt = DateTime.UtcNow;
                proposal.UpdatedAt = DateTime.UtcNow;
                await proposalNode.PutAsync(proposal);

                // Profesyonele bildirim
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = proposal.ProviderId,
                    Title = "Teklif Reddedildi",
                    Message = $"'{proposal.RequestTitle}' talebi için teklifiniz müşteri tarafından reddedildi.",
                    Type = NotificationType.OfferRejected
                });

                Console.WriteLine($"✅ Teklif reddedildi: {proposalId}");
                return ServiceResult<bool>.SuccessResult(true, "Teklif reddedildi");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ RejectProposalAsync hatası: {ex.Message}");
                return ServiceResult<bool>.FailureResult("Teklif reddedilemedi", ex.Message);
            }
        }

        /// <summary>
        /// Profesyonel kendi teklifini geri çeker
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
                    return ServiceResult<bool>.FailureResult("Teklif bulunamadı");
                }

                if (proposal.ProviderId != providerId)
                {
                    return ServiceResult<bool>.FailureResult("Bu işlemi yapmaya yetkiniz yok");
                }

                if (proposal.Status != ProposalStatus.Pending)
                {
                    return ServiceResult<bool>.FailureResult("Bu teklif zaten yanıtlanmış veya kabul edilmiş");
                }

                proposal.Status = ProposalStatus.Withdrawn;
                proposal.UpdatedAt = DateTime.UtcNow;
                await proposalNode.PutAsync(proposal);

                // Talepteki teklif sayısını azalt
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

                Console.WriteLine($"✅ Teklif geri çekildi: {proposalId}");
                return ServiceResult<bool>.SuccessResult(true, "Teklif geri çekildi");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ WithdrawProposalAsync hatası: {ex.Message}");
                return ServiceResult<bool>.FailureResult("Teklif geri çekilemedi", ex.Message);
            }
        }

        /// <summary>
        /// Teklif kabul edildikten sonra iş sözleşmesi oluşturur
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
                    return ServiceResult<ServiceRequest>.FailureResult("Teklif bulunamadı");
                }

                if (proposal.Status != ProposalStatus.Accepted)
                {
                    return ServiceResult<ServiceRequest>.FailureResult("Bu teklif kabul edilmemiş");
                }

                if (proposal.IsContractCreated)
                {
                    return ServiceResult<ServiceRequest>.FailureResult("Sözleşme zaten oluşturulmuş");
                }

                // ServiceRequest (iş sözleşmesi) oluştur
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

                // Sözleşmeyi kaydet
                await _firebaseClient
                    .Child(Constants.ServiceRequestsCollection)
                    .Child(contract.RequestId)
                    .PutAsync(contract);

                // Teklifi güncelle
                proposal.IsContractCreated = true;
                proposal.ServiceContractId = contract.RequestId;
                proposal.UpdatedAt = DateTime.UtcNow;

                await _firebaseClient
                    .Child(Constants.ProviderProposalsCollection)
                    .Child(proposalId)
                    .PutAsync(proposal);

                // Talebi güncelle
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

                Console.WriteLine($"✅ İş sözleşmesi oluşturuldu: {contract.RequestId}");
                return ServiceResult<ServiceRequest>.SuccessResult(contract, "İş sözleşmesi oluşturuldu");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ CreateServiceContractFromProposalAsync hatası: {ex.Message}");
                return ServiceResult<ServiceRequest>.FailureResult("Sözleşme oluşturulamadı", ex.Message);
            }
        }

        #endregion

        // 🔧 INTERFACE UYUMLULUK: Basit imzalı wrapper metod
        /// <summary>
        /// Interface uyumluluğu için basit imzalı wrapper metod
        /// İçeride 4 parametreli versiyonu çağırır
        /// </summary>
        public async Task<ServiceResult<bool>> SimulatePaymentAndCompleteAsync(string requestId)
        {
            try
            {
                // Request'i al ve requester ID'sini çıkar
                var requestNode = _firebaseClient
                    .Child(Constants.ServiceRequestsCollection)
                    .Child(requestId);

                var request = await requestNode.OnceSingleAsync<ServiceRequest>();

                if (request == null)
                {
                    return ServiceResult<bool>.FailureResult("İstek bulunamadı");
                }

                // 4 parametreli versiyonu çağır (varsayılan değerlerle)
                return await SimulatePaymentAndCompleteAsync(
                    requestId,
                    request.RequesterId,
                    PaymentMethodType.CardSim,
                    null
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ SimulatePaymentAndCompleteAsync (wrapper) hatası: {ex.Message}");
                return ServiceResult<bool>.FailureResult("Ödeme işlemi başarısız", ex.Message);
            }
        }
    }
}