**ROL:**
Staff-level (çok kıdemli) yazılım mimarı, mobil geliştirici ve backend uzmanı gibi davran.
Amacın: Bir uygulamayı Play Store’a çıkmadan önce production seviyesine getirmek.

---

**GÖREV:**
.NET MAUI (mobil), ASP.NET Core (backend API) ve Firebase (auth / realtime / notification vb.) kullanan hibrit modüllü  bir mobil uygulamayı **uçtan uca değerlendir, riskleri tespit et ve production-ready hale getirmek için kapsamlı bir iyileştirme planı oluştur.**

---

**UYGULAMA BAĞLAMI:**

* Mobil: .NET MAUI (MVVM mimarisi)
* Backend: ASP.NET Core Web API
* Firebase: Authentication / Realtime Database veya Firestore / Push Notifications
* Uygulama adı: KamPay
* Uygulama hedefi: Üniversite öğrencilerine yönelik ürün,hizmet paylaşım,alım,satım,takas,bağış uygulaması ve sosyal dayanışma platformu
* Uygulama Özellikleri: Üniversite öğrencileri Kampazar modülü ile ürün satış,takas,bağış yapabilecekler.
KamHizmet modülü ile mikro-hizmet sistemi hizmet talepi,hizmet ilanı yapabilecekler. 
KamAvantaj modülü ile kampüs içi işletmelerin kuponlarını,kampanyalarını,fırsatlarını görebilecekler.
KamSosyal modülü ile duyuruları,etkinlikleri,kampüs duyurularını görebilecekler yardımlaşma yapabilecekler.
Sohbet modülü ile pazarlık ve genel sohbet yapabilecekler.Kullanıcılar mesajlaşabilecekler.
favoriler modülü ile favori ürünlerini görebilecekler.
profil modülü ile profil bilgilerini görebilecekler.



---

**ANALİZİ GENİŞ KAPSAMDA YAP:**

### 1. Mimari Değerlendirme (Clean Architecture)

* Katmanlı yapı doğru mu?
* Presentation / Application / Domain / Infrastructure ayrımı var mı?
* Bağımlılıklar doğru yönde mi?
* Coupling / cohesion durumu nasıl?

---

### 2. SOLID Prensipleri Analizi

Her prensibi tek tek değerlendir:

* Single Responsibility
* Open/Closed
* Liskov Substitution
* Interface Segregation
* Dependency Inversion

İhlalleri bul ve düzeltme öner.

---

### 3. Mobil (MAUI) Katmanı İncelemesi

* MVVM doğru uygulanmış mı?
* State management sağlıklı mı?
* UI → ViewModel → Service akışı temiz mi?
* Navigation yönetimi doğru mu?
* Memory leak riski var mı?
* Offline destek var mı?

---

### 4. Backend (ASP.NET Core) İncelemesi

* Controller → Service → Repository ayrımı var mı?
* Business logic doğru katmanda mı?
* Validation (FluentValidation vb.) var mı?
* Exception handling merkezi mi?
* Logging (Serilog vb.) var mı?
* DTO / Entity ayrımı yapılmış mı?

---

### 5. Firebase Kullanımı

* Authentication güvenli mi?
* Firestore / Realtime Database doğru yapılandırılmış mı?
* Security rules doğru mu?
* Client-side validation’a fazla güveniliyor mu?
* Push notification akışı doğru mu?

---

### 6. Veritabanı & Veri Modeli

* Normalizasyon doğru mu?
* İlişkiler doğru kurulmuş mu?
* Gereksiz veri tekrarları var mı?
* Index kullanımı var mı?
* Offer (pazarlık) sistemi doğru modellenmiş mi?

---

### 7. API Tasarımı

* RESTful mı?
* Endpoint naming doğru mu?
* Versioning var mı?
* Pagination / filtering var mı?
* Rate limiting gerekli mi?

---

### 8. Güvenlik (KRİTİK)

* JWT / auth akışı güvenli mi?
* Role / permission sistemi var mı?
* Input validation eksikleri var mı?
* Injection / XSS / CSRF riskleri?
* Firebase rules açık mı?

---

### 9. Performans & Ölçeklenebilirlik

* N+1 query problemleri var mı?
* Cache kullanımı (Memory / Redis)?
* Lazy loading vs eager loading?
* Büyük liste yükleme nasıl yönetiliyor?
* Realtime update sistemi verimli mi?

---

### 10. Concurrency & State Problemleri

* Race condition var mı?
* Aynı anda işlem yapılınca veri bozulur mu?
* Offer sistemi concurrency-safe mi?

---

### 11. Hata Yönetimi & Logging

* Global exception handling var mı?
* Kullanıcıya gösterilen hata ile log ayrılmış mı?
* Log seviyeleri doğru mu?

---

### 12. Test Altyapısı

* Unit test var mı?
* Integration test var mı?
* Kritik business logic test ediliyor mu?

---

### 13. UI/UX & Product Perspektifi

* Kullanıcı akışı mantıklı mı?
* Edge case’lerde UI ne yapıyor?
* Loading / empty / error state’ler var mı?

---

### 14. DevOps & Release Hazırlığı

* CI/CD var mı?
* Environment ayrımı (dev / prod)?
* Config yönetimi güvenli mi?
* API URL hardcoded mı?

---

### 15. Play Store Öncesi Kritik Checklist

* Crash riskleri
* Permission kullanımı
* Network failure handling
* Offline senaryolar
* Analytics entegrasyonu

---

**ÇIKTI FORMATI:**

1. Genel sistem değerlendirmesi
2. Kritik hatalar (yüksek öncelik)
3. Mimari problemler
4. Güvenlik açıkları
5. Performans problemleri
6. Kod kalitesi sorunları
7. İyileştirme roadmap’i (öncelik sırasına göre)
8. Refactor önerileri (örnek kodlarla)
9. Production readiness skoru (0-100)
10. Yayına çıkmadan önce yapılması gereken SON 10 şey

---

**KALİTE BEKLENTİSİ:**

* Yüzeysel değil, derin analiz yap
* Varsayım yapman gerekiyorsa belirt
* Gerçek bir senior code review gibi davran
* Gerektiğinde mimariyi yeniden tasarlamaktan çekinme
* Amaç: sistemi “çalışır” değil, “production-grade” yapmak
