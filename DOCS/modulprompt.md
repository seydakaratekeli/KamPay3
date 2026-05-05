ROL:
Staff-level yazılım mimarı, mobil geliştirici, backend uzmanı ve security reviewer gibi davran.
Amacın: .NET MAUI + ASP.NET Core Web API + Firebase kullanan hibrit mobil uygulamamı Play Store’a çıkmadan önce production-grade seviyeye taşımak.

PROJE BAĞLAMI:
Uygulama: KamPay
Hedef kullanıcı: Üniversite öğrencileri
Teknoloji:
- Mobil: .NET MAUI, MVVM, CommunityToolkit.Mvvm
- Backend: ASP.NET Core Web API / Minimal API
- Veritabanı: Firebase Realtime Database
- Storage: Firebase Storage
- Auth: Firebase Authentication
- Bildirim: Firebase / push notification altyapısı
- Dil: C#
- UI: Türkçe, çoklu dil desteği mevcut

ÇALIŞMA ŞEKLİ:
Uygulamayı modül modül inceleyeceğiz.
Bu turda sadece aşağıda belirttiğim modülü analiz et.
Diğer modülleri yalnızca bu modülle doğrudan ilişkiliyse değerlendir.
Varsayım yapman gerekiyorsa açıkça belirt.
Kod görmeden kesin hüküm verme; riskleri “olası risk” olarak işaretle.
Yüzeysel yorum yapma. Gerçek bir senior production code review gibi davran.

İNCELEME MODÜLÜ:
[KamPazar/Anasayfa]


BU MODÜLÜN AMACI:
[KamPazar/Anasyafa modülü ile üniversite öğrencilerinin ürün alım,satım,takas,bağış ve ürünlerini görebilecekleri bir arayüz sunar.]

Özellikler: 
- Kategoriler
- Filtreleme
- Sıralama
- Arama
- Ürün detay sayfası
- Ürün ekleme
- Ürün düzenleme
- Ürün silme
- favoriler
- bildirimler

Dikkat etmen gerekenler: 

- ürün satış işlemi hem liste fiyatı üzerinden hem pazarlık yaparak iki farklı şekilde gerçekleşiyor. ödeme yöntemi hem kart ile olabiliyor hemde kampüs içinde elden nakit olarak verebilirler. şu an kart kısmını kullanmıyoruz.(bu seçeneğe daha sonra geliştirilecek yazılabilir uygulama üzerinde) daha sonra paytr gibi bir ödeme sistemi entegrasyonu ile  kullanıma sunacağız. bu yüzden elden nakit ödeme akışı üzerinde duralım
- ürün takas işlemi için kullanıcılar fiziksel olarak buluşup ürünü elden teslim ediyorlar. bu süreçte ürünün hem alıcı hem satıcı tarafından durumunun takibi için bir akış gerekiyor. bu akışta kullanıcılar karşılıklı qr kod okutarak ürünü elden teslim alıp ürünün durumunu değiştirebilirler.
- ürün bağış işleminde kullanıcılar ürünü bağışlamak istedikleri kullanıcıya elden teslim ederek ürünü bağışlamış sayılıyor bağışladıkları kişinin ekranında qr okutarak bağışladım yapabilirler. 


Özellikle şu konulara odaklan:

1. Mimari Uygunluk
- Modül Clean Architecture prensiplerine uygun mu?
- Presentation / ViewModel / Service / Repository / Firebase ayrımı net mi?
- Business logic doğru katmanda mı?
- Bağımlılık yönleri doğru mu?
- Gereksiz coupling var mı?
- Modül başka modüllere fazla mı bağımlı?

2. SOLID Analizi
Her prensibi ayrı değerlendir:
- Single Responsibility Principle
- Open/Closed Principle
- Liskov Substitution Principle
- Interface Segregation Principle
- Dependency Inversion Principle

Her ihlal için:
- Sorun nerede?
- Neden riskli?
- Nasıl düzeltilmeli?
- Gerekirse örnek refactor kodu ver.

3. Mobil / MAUI / MVVM Analizi
- ViewModel çok fazla sorumluluk alıyor mu?
- UI state yönetimi doğru mu?
- Loading / Empty / Error state var mı?
- Command’lerde concurrency guard var mı?
- Async command kullanımı güvenli mi?
- Navigation doğru mu?
- Memory leak riski var mı?
- Event subscription / messenger / realtime listener temizleniyor mu?
- Offline / network failure senaryoları düşünülmüş mü?

4. Backend / API Analizi
- Controller / Endpoint katmanı ince mi?
- Service ve repository ayrımı doğru mu?
- DTO / Entity / Firebase model ayrımı yapılmış mı?
- Validation merkezi mi?
- Exception handling merkezi mi?
- Logging yeterli mi?
- API endpoint naming RESTful mı?
- Pagination / filtering / sorting gerekli mi?
- Rate limiting gerekiyor mu?

5. Firebase Analizi
- Realtime Database veri modeli doğru mu?
- Security rules açısından risk var mı?
- Client-side validation’a fazla güveniliyor mu?
- Index eksikleri olabilir mi?
- Atomic update / transaction ihtiyacı var mı?
- Yetkisiz okuma/yazma riski var mı?
- Kullanıcı sadece kendi verisine erişebiliyor mu?
- Admin / owner / participant kontrolleri net mi?

6. Güvenlik Analizi
- Auth flow güvenli mi?
- Token doğrulama doğru yerde mi?
- Role / permission kontrolü var mı?
- Input validation eksikleri var mı?
- IDOR riski var mı?
- Injection / XSS / CSRF riski var mı?
- Sensitive data loglanıyor mu?
- API URL, key, secret, admin credential gibi değerler production için güvenli mi?

7. Veri Tutarlılığı ve Concurrency
- Aynı anda iki işlem yapılırsa veri bozulur mu?
- Race condition riski var mı?
- Stale write kontrolü var mı?
- Idempotency gerekli mi?
- Status geçişleri state machine gibi tanımlı mı?
- Aynı aksiyon iki kez tetiklenirse ne olur?
- Firebase transaction / ETag / optimistic concurrency gerekiyor mu?

8. Performans ve Ölçeklenebilirlik
- Büyük liste yüklemelerinde sorun olur mu?
- Pagination var mı?
- Gereksiz realtime listener var mı?
- N+1 benzeri veri çekme sorunu var mı?
- Cache stratejisi doğru mu?
- Görsel / medya yüklemeleri optimize mi?
- Network çağrıları gereksiz tekrar ediyor mu?
- Mobil cihaz bellek tüketimi açısından risk var mı?

9. Hata Yönetimi ve Logging
- Kullanıcıya gösterilen hata ile teknik log ayrılmış mı?
- Try/catch kullanımı doğru mu?
- Sessiz hata yutma var mı?
- Log seviyeleri doğru mu?
- Kritik işlemler audit log gerektiriyor mu?
- Firebase/API hataları normalize ediliyor mu?

10. Test Edilebilirlik
- Unit test yazılabilir mi?
- Integration test gerekli mi?
- Mock edilebilir interface’ler var mı?
- Kritik business rule’lar test edilmiş mi?
- Eksik test senaryolarını listele.
- Edge case test matrisi oluştur.

11. UI/UX Edge Case Analizi
- Loading, empty, error state var mı?
- Kullanıcı hızlı hızlı butona basarsa ne olur?
- İnternet giderse ne olur?
- Veri değişmiş/silinmişse UI nasıl tepki verir?
- Yetkisiz kullanıcı aksiyon görebiliyor mu?
- Kullanıcıya anlaşılır mesaj gösteriliyor mu?

12. Production Readiness
Bu modül Play Store öncesi production’a hazır mı?
Aşağıdaki seviyelerden birini ver:
- Blocker: Yayına çıkamaz
- High Risk: Yayına çıkmadan önce düzeltilmeli
- Medium Risk: Yayından önce önerilir
- Low Risk: Teknik borç
- Ready: Production için uygun

ÇIKTI FORMATI:

1. Kısa Modül Özeti
- Bu modül ne yapıyor?
- Genel kalite seviyesi nedir?
- En büyük risk nedir?

2. Production Readiness Skoru
0-100 arası puan ver.
Puanı gerekçelendir.

3. Kritik Bulgular
Tablo formatında ver:
| Öncelik | Bulgu | Etki | Kanıt / Dosya | Önerilen Çözüm |

Öncelik değerleri:
- P0: Yayına çıkışı engeller
- P1: Yayına çıkmadan önce düzeltilmeli
- P2: Kısa vadede düzeltilmeli
- P3: Teknik borç / iyileştirme

4. Mimari Problemler
Her problemi şu formatta yaz:
- Problem:
- Etki:
- Neden önemli:
- Önerilen refactor:
- Örnek kod:

5. Güvenlik Açıkları
Her riski ayrı değerlendir:
- Risk:
- Saldırı / kötüye kullanım senaryosu:
- Etki:
- Çözüm:
- Firebase rule / backend validation önerisi:

6. Concurrency & State Machine Analizi
- Mevcut status/state geçişlerini çıkar.
- Eksik veya tehlikeli geçişleri işaretle.
- Gerekirse önerilen state machine tablosu oluştur.
- Aynı işlemin iki kez yapılması senaryosunu analiz et.

7. Performans Problemleri
- Mobil taraf
- Backend taraf
- Firebase taraf
- Realtime listener taraf
- Cache tarafı

8. Kod Kalitesi Sorunları
- Fazla büyük class/method
- Tekrarlı kod
- Yanlış abstraction
- Interface şişmesi
- Magic string
- Hardcoded config
- Null handling
- Async/await hataları

9. Test Matrisi
Aşağıdaki formatta test senaryoları oluştur:
| Test | Senaryo | Beklenen Sonuç | Test Tipi | Öncelik |

10. Refactor Roadmap
Öncelik sırasına göre ver:
- Hemen yapılacaklar
- Bu sprint yapılacaklar
- Yayından önce yapılacaklar
- Yayından sonra teknik borç olarak kalabilecekler

11. Örnek Kodlarla İyileştirme
Gerekirse şu tür kodlar üret:
- Interface ayrımı
- Service refactor
- Validator örneği
- State machine örneği
- Firebase atomic update örneği
- Guard clause örneği
- Result pattern örneği
- Centralized exception handling örneği
- MAUI ViewModel command guard örneği

12. Yayına Çıkmadan Önce Bu Modül İçin Son 10 Kontrol
Numaralı checklist ver.

DİL VE ÜSLUP:
- Türkçe yaz.
- Net, teknik ve doğrudan konuş.
- Gereksiz övgü yapma.
- Kod kötüyse açıkça söyle.
- Ancak çözüm odaklı ol.
- Senior code review ciddiyetinde davran.
- “Muhtemelen sorun yoktur” gibi belirsiz ifadelerden kaçın.
- Emin değilsen “Kodda bunu doğrulamak gerekir” diye belirt.

ŞİMDİ ANALİZ ETMENİ İSTEDİĞİM MODÜL:
[Modül adını buraya yaz]

DOSYALAR:
[İlgili kodları buraya yapıştır]