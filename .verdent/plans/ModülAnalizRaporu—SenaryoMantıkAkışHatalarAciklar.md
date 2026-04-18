Modül Analiz Raporu — Senaryo, Mantık Akışı, Hatalar ve Açıklar
________________________________________
MODÜL 1 — Ürün Detay (ProductDetailViewModel)
BUG-01 — Kritik: Rapor hiçbir yere kaydedilmiyor
[RelayCommand]
private async Task ReportProductAsync()
{
    ...
    if (reason != null && reason != Res["Cancel"])
    {
        await Application.Current.MainPage.DisplayAlert(Res["Info"], Res["ReportReceived"], Res["Ok"]);
    }
}
Kullanıcı "Uygunsuz İçerik" seçiyor, "Şikayetiniz alındı" diyor ama Firebase'e hiç yazılmıyor. Tamamen sahte moderasyon.
________________________________________
BUG-02 — Kritik: MarkAsSoldAsync tüm transaction koleksiyonunu yüklüyor
var transactions = await _firebaseClient
    .Child(Constants.TransactionsCollection)
    .OnceAsync<Transaction>();
    
var productTransactions = transactions
    .Select(t => t.Object)
    .Where(t => t.ProductId == ProductId && ...)
    .ToList();
1000 kullanıcının tüm transaction'ları belleğe çekiliyor, sonra client-side filtre. Doğrusu: Firebase query ile OrderBy("ProductId").EqualTo(productId).
________________________________________
BUG-03 — Mantık Hatası: Pazarlık kabul akışında yanlış taraf kontrolü
AcceptNegotiatedPriceAsync (ProductDetailViewModel satır 581) AgreedAmount'a bakıyor. Ama satıcı karşı teklif verdi, alıcı kabul edecek, satıcı da bu butona basabilir — kim bastı kontrolü eksik. Sonuç: satıcı kendi karşı teklifini kendisi "kabul etmiş" sayılabilir.
________________________________________
BUG-04 — Senaryo Açığı: Satış talebi oluşturma akışında fiyat kaybı
var saleResult = await _transactionService.CreateRequestAsync(Product, currentUser);

if (saleResult.Success)
{
    ...
    await _transactionService.ProposePriceForSaleAsync(ActiveTransaction.TransactionId, targetPrice, ...);
CreateRequestAsync hiçbir fiyat parametresi almıyor — transaction QuotedPrice = 0 ile oluşuyor. Sonra ProposePriceForSaleAsync çağırılıyor ama araya network hatası girerse: transaction var, fiyat yok, satıcı 0₺'lık talep görüyor.
________________________________________
MODÜL 2 — Teklifler (OffersViewModel)
BUG-05 — Kritik: DI bypass, new FirebaseClient() manuel oluşturuluyor
_firebaseClient = new FirebaseClient(Constants.FirebaseRealtimeDbUrl);
ProductDetailViewModel'de bu DIP ihlali düzeltilmiş (yorum satırında "DIP FIX" yazıyor) ama OffersViewModel'de hâlâ var. Her OffersViewModel oluşturulduğunda yeni bir Firebase WebSocket bağlantısı açılıyor.
________________________________________
BUG-06 — Kritik Performans: Tüm transactions koleksiyonu stream ediliyor
_allOffersSubscription = _firebaseClient
    .Child(Constants.TransactionsCollection)
    .AsObservable<Transaction>()
Tüm kullanıcıların tüm transaction'larını WebSocket üzerinden akıyor, sonra .Where(sellerId == userId || buyerId == userId) ile filtreniyor. 10.000 transaction → 10.000 event gelir, 9.980'i atılır.
________________________________________
BUG-07 — Aynı veri çift yükleme riski
StartListeningForOffers hem LoadInitialSnapshotAsync() çağırıyor hem de AsObservable listener başlatıyor. İkisi paralel çalışıyor. Snapshot gelirken listener da event fırlatırsa idTracker kontrolü race condition'a giriyor (ana thread'de çözülüyor ama timing riski var).
________________________________________
BUG-08 — Anti-pattern: Busy-wait ile refresh bekleme
while (!_initialLoadComplete && waitedTime < maxWaitTime)
{
    await Task.Delay(checkInterval);
    waitedTime += checkInterval;
}
200ms aralıklarla polling. TaskCompletionSource veya SemaphoreSlim ile event-driven yapılmalı.
________________________________________
BUG-09 — IsLoading finally bloğuna alınmamış
RespondToOfferInternalAsync içinde her branch (Satis, Takas, Bagis) sonunda IsLoading = false ayrı ayrı set ediliyor. Bir exception olursa ya da erken return atılırsa IsLoading true kalır, sayfa sonsuza kadar yüklenme gösterir:
IsLoading = true;
var result = await _transactionService.RespondToOfferAsync(...);
...
IsLoading = false;  // ❌ finally'de değil
return;
________________________________________
BUG-10 — Interface kırılıyor: Hard cast ile servis erişimi
if (_transactionService is FirebaseTransactionService firebaseService)
{
    var result = await firebaseService.ConfirmDonationAsync(...);
ITransactionService interface'ini bypass ediyor. ConfirmDonationAsync interface'e eklenmeli; aksi hâlde mock/test veya farklı implementasyon kullanılamaz.
________________________________________
MODÜL 3 — Hizmet Paylaşımı (FirebaseServiceSharingService)
BUG-11 — Kritik Güvenlik: OTP doğrulama production'da bypass ediliyor
// Demo modu: Eğer UI'dan OTP gelmemişse otomatik geçerli say
if (string.IsNullOrWhiteSpace(otp))
{
    otp = saved.Otp; // demo için doğru kabul
}
Bu kod release build'de çalışırsa OTP girilmeden ödeme geçiyor. #if DEBUG içine alınmadıkça production'da canlı güvenlik açığı.
________________________________________
BUG-12 — Kritik Mantık Hatası: Pagination'da yanlış parametre tipi
items = await _firebaseClient
    .Child(Constants.ServiceOffersCollection)
    .OrderBy("CreatedAt")
    .StartAt(lastKey)       // ❌ lastKey bir UUID string
    .LimitToFirst(pageSize + 1)
lastKey bir ServiceId (UUID), ama OrderBy("CreatedAt") ile kullanılıyor. Firebase StartAt burada timestamp değeri bekliyor. Sayfalama hiç çalışmıyor — ya hep ilk sayfayı döndürür ya da exception verir.
Aynı hata GetCustomerRequestsPagedAsync'te de var (satır 1440-1443).
________________________________________
BUG-13 — Konuşma duplikasyonu: Yalnızca tek yönlü arama
var existingConversations = await _firebaseClient
    .Child(Constants.ConversationsCollection)
    .OrderBy("User1Id")
    .EqualTo(currentUserId)
    .OnceAsync<Conversation>();
Eğer konuşma User1Id = B, User2Id = A olarak oluşturulduysa, A'nın gözünden arama User1Id = A diye yapılıyor, bulunamıyor — yeni bir duplicate konuşma oluşturuluyor. User2Id'ye göre de sorgu yapılmalı.
________________________________________
BUG-14 — AcceptProposalAsync — Atomik değil, N yazma işlemi
foreach (var other in otherProposals.Data.Where(p => 
    p.ProposalId != proposalId && 
    p.Status == ProposalStatus.Pending))
{
    ...
    await otherNode.PutAsync(other); // N ayrı Firebase yazma
}
10 teklif varsa 10 ayrı HTTP isteği. İkincisinde ağ hatası olursa bazı teklifler Pending kalır, diğerleri Rejected olur — tutarsız veri.
________________________________________
BUG-15 — ProviderFinishServiceAsync — Durum güncelleme eksik
// Not: Burada status'ü hemen 'Completed' yapmıyoruz, talep edenin onayını bekliyoruz.
await requestNode.PutAsync(request);
UpdatedAt değiştirilip kaydediliyor ama status değiştirilmiyor ve alıcıya bildirilen "Hizmet tamamlandı mı?" sorusuna cevap verdikten sonra hangi duruma geçileceği belirtilmiyor. RequesterConfirmServiceAsync doğru akışa bağlanmıyor — ikisi bağımsız metot, orchestration yok.
________________________________________
MODÜL 4 — Genel / Cross-Cutting
BUG-16 — GenerateOtp() zayıf rastgelelik
private string GenerateOtp() => new Random().Next(100000, 999999).ToString();
Her çağrıda new Random() — kısa süre içinde aynı seed alabilir, OTP öngörülebilir. Random.Shared.Next() veya RandomNumberGenerator kullan.
________________________________________
BUG-17 — Senaryo Açığı: Bağış talebi sonrası kullanıcı bildirimde bırakılıyor
case ProductType.Bagis:
    var donationResult = await _transactionService.CreateRequestAsync(Product, currentUser);
    
    if (donationResult.Success)
    {
        ...
        await Application.Current.MainPage.DisplayAlert(...);
        // ❌ Chat veya Offers ekranına yönlendirme yok
    }
Satış ve Takas akışlarında chat'e yönlendirme var. Bağışta sadece alert gösteriliyor, kullanıcı nereye gideceğini bilmiyor.
________________________________________
BUG-18 — Senaryo Açığı: Teklif sahibi başka alıcıya sattım → bekleyen tekliflere bildirim yok
MarkAsSoldAsync'te "Uygulama dışından birine sattım" seçildiğinde MarkProductAsSoldDirectlyAsync çalışıyor. Bu akışta bekleyen Pending transaction'lar iptal edilmiyor, alıcılara "ürün satıldı" bildirimi gitmiyor.
________________________________________
Özet Tablo
#	Modül	Tür	Şiddet	Kısa Açıklama
01	ProductDetail	Bug	Kritik	Rapor hiçbir yere kaydedilmiyor
02	ProductDetail	Bug	Kritik	MarkAsSold tüm koleksiyonu yüklüyor
03	ProductDetail	Mantık	Orta	Pazarlık kabul tarafı kontrolsüz
04	ProductDetail	Senaryo	Yüksek	Satış talebi fiyatı atomik değil
05	Offers	Bug	Yüksek	DI bypass, yeni Firebase bağlantısı
06	Offers	Performans	Kritik	Tüm transactions koleksiyonu stream
07	Offers	Bug	Orta	Snapshot + listener race condition
08	Offers	Code Quality	Düşük	Busy-wait anti-pattern
09	Offers	Bug	Yüksek	IsLoading finally'de değil
10	Offers	Mimari	Orta	Interface bypass, hard cast
11	ServiceSharing	Güvenlik	Kritik	OTP production'da bypass
12	ServiceSharing	Bug	Kritik	Pagination hiç çalışmıyor (yanlış parametre tipi)
13	ServiceSharing	Bug	Yüksek	Konuşma duplikasyonu (tek yönlü sorgu)
14	ServiceSharing	Bug	Orta	Teklif reddi atomik değil
15	ServiceSharing	Senaryo	Orta	ProviderFinish → RequesterConfirm bağlantısı yok
16	Genel	Güvenlik	Orta	GenerateOtp zayıf rastgelelik
17	ProductDetail	UX	Orta	Bağış sonrası yönlendirme yok
18	ProductDetail	Senaryo	Yüksek	Dış satışta bekleyen teklifler iptal edilmiyor

