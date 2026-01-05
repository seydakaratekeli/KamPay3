## 2. LİTERATÜR ÖZETİ

Bu bölüm, paylaşım ekonomisinin kuramsal ve ampirik birikimini; dijital platform ekosistemlerini, kampüs ölçeğindeki uygulamaları ve güvenlik–erişilebilirlik–teknik çerçeveleri sentezlemekte; KamPay'in literatürdeki konumunu ve katkısını ortaya koymaktadır. Atıflar ve kaynak numaraları aşağıdaki kaynakça ile birebir eşleştirilmiştir.

### 2.1. Kronolojik Özet

- 2004–2009: Erken bilişim literatürü, öneri sistemlerinde değerlendirme ölçütleri ve faktörizasyon tabanlı yaklaşımlarla kullanıcı etkileşimini artırmanın yöntemlerini olgunlaştırdı [12], [13].  
- 2010–2016: Paylaşım ekonomisinin kuramsal temelleri "sahiplikten erişime" dönüşümü vurguladı; motivasyonlar, güven ve topluluk dinamikleri birlikte ele alındı [1], [2], [3].  
- 2015 sonrası: Sürdürülebilir tüketim ve döngüsel ekonomi kesişiminde, mikro-topluluklarda (kampüs) paylaşım davranışlarının çevresel/sosyal katkıları tartışmaya açıldı [4], [5], [33].  
- 2021–2024: Güvenlik ve erişilebilirlikte OWASP (mobil) [14], [15], NIST (dijital kimlik) [16], WCAG 2.2 [19]; medya/QR standartlarında ISO/IEC 15444‑1 (JPEG 2000) [21] ve ISO/IEC 18004 (QR) [22] gibi teknik referans çerçeveler olgunlaştı. Bulut tabanlı gerçek zamanlı mimariler (Firebase) [23], [24] ve Azure Well‑Architected Framework [26] ölçeklenebilir tasarıma kılavuzluk etti.  

Genel hat: Kampüs özelinde güvenli teslimat, doğrulanmış topluluk üyeliği ve sosyal inovasyon modüllerini bütünleştiren hibrit paylaşım platformlarının gereksinimleri belirginleşti.

### 2.2. Literatürdeki Açıklar ve Bu Çalışmanın Katkısı

Kampüs ölçeğinde kurumsal doğrulama, güvenli fiziksel teslimat, erişilebilirlik gereksinimleri ve oyunlaştırmanın sürdürülebilir davranışa etkisinin uzun vadeli ölçümü çoğunlukla parçalı incelenmiştir. Bu çalışma, yalnızca kurumsal üniversite e‑postasıyla topluluk içi doğrulama; QR + konum doğrulamalı teslimat onayı; WCAG 2.2 uyumlu arayüz ilkeleri; gönüllülük/"İyilik Panosu" gibi sosyal inovasyon modülleri ve sunucusuz gerçek zamanlı mimariyi bütünleştirerek söz konusu açığı kapatmayı hedefler. Amaç, kampüs paylaşım platformlarında güven, erişilebilirlik ve sürdürülebilir katılım arasında köprü kurmaktır [19], [14], [16], [29]–[31], [23], [24], [33].

Metin boyunca "uygulanmaktadır/öngörülmektedir" fiilleri, repoda doğrulanmış olanlar dışında hedeflenen veya tasarlanan özellikleri belirtmek için kullanılmıştır.

### 2.3. Paylaşım Ekonomisi ve Ortaklaşa Tüketim

Paylaşım ekonomisi, atıl kaynakların bireyler arasında paylaşımı/ödünç/takas üzerinden "erişim" odaklı kullanımını esas alır; dijitalleşme bu dönüşümü hızlandırmıştır [1], [2]. Platform tasarım kararlarıyla kullanıcı davranışlarının etkileşimi, çevresel/sosyal çıktıları belirgin biçimde etkiler [3]. Ulusal ve uluslararası politika belgeleri (ör. SKA 12 [5]; Türkiye 2023–2030 Sanayi ve Teknoloji Stratejisi [6]) paylaşım ekosistemlerine yön vererek ölçeklenebilirliği ve toplumsal faydayı öncelemektedir. Belk, ticari işlemlere evrilen çevrimiçi paylaşımın topluluk ruhu ve güven mekanizmalarını zayıflatma riskine dikkat çeker; anti‑tüketim ve mikro-topluluk örnekleri, sosyal teşviklerin kalıcı davranışa dönüşebildiğini gösterir [1], [11].

### 2.4. Dijital Paylaşım Platformları ve Kampüs Ekosistemleri

Karşılıksız/atık odaklı ağlar (Freecycle [7], OLIO [8]) ile işletme–tüketici arayüzünde israfı azaltan uygulamalar (Too Good To Go [9]) paylaşım ekonomisinin çevresel/sosyal boyutlarını güçlendirir. Genel amaçlı ikinci el pazar yerleri (Letgo, Sahibinden vb.) geniş kitlelere ulaşsa da, ölçek büyüdükçe kimlik doğrulama, lojistik ve güven yönetimi güçleşir; bu nedenle mikro-toplulukların (kampüs) özgün gereksinimlerini tam karşılamayabilir [10]. Kampüs bağlamında benzer sosyo‑ekonomik koşullar, kurumsal doğrulama ve yerel teslimat avantajları sayesinde yüksek adaptasyon görülür; ancak çözümler çoğu kez tek paylaşım türüne odaklanır ve bütüncül ekosistem eksiği sürer [3], [11], [4].

### 2.5. Güvenlik, Erişilebilirlik ve Teknik Çerçeveler

- Güvenlik/kimlik: OWASP MASVS ve Mobile Top 10 mobil tehdit modelleri ve doğrulanabilir gereksinimler sunar [14], [15]; NIST SP 800‑63‑3 kimlik doğrulama/güven seviyelerini tanımlar [16]. GDPR [17] ve KVKK [18], rıza–minimizasyon–şeffaflık ilkelerini zorunlu kılar.  
- Erişilebilirlik/kalite: WCAG 2.2 (örn. 1.4.3 Kontrast, 2.4.7 Odak Görünümü) kapsayıcı tasarım ölçütleri sunar [19]; ISO/IEC 25010 yazılım kalite özelliklerini (güvenlik, kullanılabilirlik, performans verimliliği, taşınabilirlik vb.) sistematikleştirir [20].  
- Gerçek zamanlı mimari: Firebase Realtime Database/Storage, istemci odaklı, olay güdümlü senaryolarda hızlı geliştirme ve anlık senkronizasyon sağlar [23], [24]; serverless yaklaşımın trade‑off'ları literatürde ayrıntılı incelenmiştir [25]. Azure Well‑Architected Framework, ölçek, güvenilirlik ve maliyet için tasarım ilkeleri sunar [26].  
- İletişim/kimlik belirteçleri: WebSocket düşük gecikmeli çift yönlü iletişim sağlar [27]; JWT stateless yetkilendirme için yaygındır [28].  
- Medya/QR standartları ve performans: JPEG 2000 (ISO/IEC 15444‑1) çok çözünürlüklü saklama ve oran verimliliği sunar [21]; QR veri taşıyıcıları ISO/IEC 18004 ile tanımlıdır [22].  
- Moderasyon/oyunlaştırma: Topluluk sağlığı için politika ve moderasyon süreçleri [32]; oyunlaştırmanın tanımı ve kullanıcı etkilerine ilişkin kuramsal ve deneysel birikim mevcuttur [29]–[31].  
- Konum gizliliği: Kampüs uygulamalarında mahremiyet riskleri ve koruma teknikleri kapsamlı olarak incelenmiştir [34], [35].  
- Bildirim ve ürün geliştirme pratikleri: Bulut mesajlaşma (FCM) altyapıları ölçek/teslim güvenilirliği metrikleri sunar [36]; Hot Reload ve MVVM araç takımları geliştirici verimini ve bakım yapılabilirliği artırır [37], [38].

### 2.6. KamPay'in Literatürdeki Konumu ve Katkısı

KamPay, kampüs ölçeğinde çok boyutlu bir paylaşım ekosistemi sunar: ürün satışı, takas, karşılıksız bağış ve hizmet paylaşımı tek bir platformda bütünleştirilir. Kurumsal e‑posta doğrulamasıyla kapalı ve güvenilir bir topluluk kurgulanması öngörülmektedir [6]. QR + (gerektiğinde) konum doğrulamalı fiziksel teslimat onayı ile yüz yüze teslimlerde güven artırılır [22]. WCAG 2.2 uyumlu arayüz ilkeleri ve .resx tabanlı yerelleştirme kapsayıcılığı gözetir [19]. "İyilik Panosu" ve oyunlaştırma modülleri sürdürülebilir katılım ve sosyal etkileşimi destekler [29]–[31], [33]. Sunucusuz/gerçek zamanlı mimari (Firebase) ile düşük gecikme ve bakım kolaylığı hedeflenirken; güvenlik (OWASP/NIST), gizlilik (GDPR/KVKK) ve kalite (ISO/IEC 25010) gereksinimleri tasarımın ayrılmaz parçası olarak ele alınır [14]–[20], [23]–[26].

**Tablo 2.1. KamPay ve Mevcut Pazar Yeri Platformlarının Fonksiyonel Karşılaştırması**

| Özellik                    | Ölçüt Açıklaması                                     | Letgo | Sahibinden | Dolap | KamPay | Not/Kanıt (teslimde doğrulanacak)              |
| -------------------------- | ---------------------------------------------------- | ----- | ---------- | ----- | ------ | ---------------------------------------------- |
| Kampüs Odaklı              | Kurumsal e‑posta/ID ile kapalı topluluk erişimi      | Yok   | Yok        | Yok   | Var    | Genel pazar yerleri herkese açıktır            |
| Kurumsal E‑posta Doğrulama | .edu.tr vb. zorunlu alan/kurum doğrulaması          | Yok   | Yok        | Yok   | Var    | Üyelik/kimlik politikaları                     |
| QR Teslimat Onayı          | QR ile alım‑teslim doğrulaması                       | Yok   | Yok        | Kısmi | Var    | "Kısmi" kargo/3P çözümlerle sınırlı olabilir   |
| İyilik/Bağış Modülü        | Ücretsiz bağış/iyilik paylaşımı                      | Kısıtlı | Yok      | Yok   | Var    | KamPay'de "İyilik Panosu"                      |
| Hizmet Paylaşımı           | Zaman/uzmanlık/hizmet ilanları                       | Yok   | Kısıtlı    | Yok   | Var    | Kategori kapsamı sınırlı (genel pazar yerleri) |
| Oyunlaştırma               | Rozet/puan/sıralama vb.                              | Yok   | Yok        | Yok   | Var    | Kullanıcı bağlılığına dönük tasarım             |

Dipnot: Tablo beyanları platformların güncel özellikleriyle teslim öncesi doğrulanmalı; mümkünse resmî SSS/yardım sayfalarına URL verilmelidir.

---

## Kaynakça (IEEE)

[1] R. Belk, "You are what you can access? Sharing and collaborative consumption online," Journal of Business Research, vol. 67, no. 8, pp. 1595–1600, 2014.

[2] R. Botsman and R. Rogers, What's Mine Is Yours: The Rise of Collaborative Consumption. HarperCollins, 2010.

[3] J. Hamari, M. Sjöklint, and A. Ukkonen, "The sharing economy: Why people participate in collaborative consumption," Journal of the Association for Information Science and Technology, vol. 67, no. 9, pp. 2047–2059, 2016.

[4] M. Geissdoerfer, P. Savaget, N. M. P. Bocken, and E. J. Hultink, "The Circular Economy – A new sustainability paradigm?," Journal of Cleaner Production, vol. 143, pp. 757–768, 2017.

[5] United Nations, "Sustainable Development Goal 12: Responsible Consumption and Production," https://sdgs.un.org/goals/goal12 (erişim: 2026‑01‑05).

[6] T.C. Sanayi ve Teknoloji Bakanlığı, "2023–2030 Sanayi ve Teknoloji Stratejisi," (resmî belge/rapor) (erişim: 2026‑01‑05).

[7] The Freecycle Network, https://www.freecycle.org/ (erişim: 2026‑01‑05).

[8] OLIO, https://olioex.com/ (erişim: 2026‑01‑05).

[9] Too Good To Go, https://www.toogoodtogo.com/ (erişim: 2026‑01‑05).

[10] A. Sundararajan, The Sharing Economy: The End of Employment and the Rise of Crowd‑Based Capitalism. MIT Press, 2016.

[11] L. Ozanne and P. Ballantine, "Sharing as a form of anti‑consumption: Lessons from a toy library," Journal of Consumer Behaviour, vol. 9, no. 6, pp. 485–493, 2010.

[12] J. L. Herlocker, J. A. Konstan, L. G. Terveen, and J. T. Riedl, "Evaluating collaborative filtering recommender systems," ACM TOIS, vol. 22, no. 1, pp. 5–53, 2004.

[13] Y. Koren, R. Bell, and C. Volinsky, "Matrix factorization techniques for recommender systems," IEEE Computer, vol. 42, no. 8, pp. 30–37, 2009.

[14] OWASP, "Mobile Application Security Verification Standard (MASVS)," https://mas.owasp.org/ (erişim: 2026‑01‑05).

[15] OWASP, "Mobile Top 10 Risks," https://owasp.org/www-project-mobile-top-10/ (erişim: 2026‑01‑05).

[16] NIST, "SP 800‑63‑3: Digital Identity Guidelines," https://pages.nist.gov/800-63-3/ (erişim: 2026‑01‑05).

[17] European Union, "General Data Protection Regulation (EU 2016/679)," https://gdpr.eu/ (erişim: 2026‑01‑05).

[18] Türkiye Cumhuriyeti, "Kişisel Verilerin Korunması Kanunu (KVKK) No. 6698," https://www.kvkk.gov.tr/ (erişim: 2026‑01‑05).

[19] W3C, "Web Content Accessibility Guidelines (WCAG) 2.2," https://www.w3.org/TR/WCAG22/ (erişim: 2026‑01‑05).

[20] ISO/IEC 25010:2011, Systems and software engineering — System and software quality models.

[21] ISO/IEC 15444‑1:2019, Information technology — JPEG 2000 image coding system — Part 1: Core coding system.

[22] ISO/IEC 18004:2015, Information technology — Automatic identification and data capture techniques — QR Code bar code symbology specification.

[23] Google, "Firebase Realtime Database," https://firebase.google.com/docs/database (erişim: 2026‑01‑05).

[24] Google, "Firebase Storage," https://firebase.google.com/docs/storage (erişim: 2026‑01‑05).

[25] I. Baldini et al., "Serverless Computing: Current Trends and Open Problems," in Research Advances in Cloud Computing, Springer, 2017; arXiv:1706.03178.

[26] Microsoft, "Azure Well‑Architected Framework," https://learn.microsoft.com/azure/well-architected/ (erişim: 2026‑01‑05).

[27] IETF, "RFC 6455: The WebSocket Protocol," https://www.rfc-editor.org/rfc/rfc6455 (erişim: 2026‑01‑05).

[28] IETF, "RFC 7519: JSON Web Token (JWT)," https://www.rfc-editor.org/rfc/rfc7519 (erişim: 2026‑01‑05).

[29] S. Deterding, D. Dixon, R. Khaled, and L. Nacke, "From game design elements to gamefulness: Defining 'gamification'," Proc. MindTrek, 2011.

[30] K. Werbach and D. Hunter, For the Win: How Game Thinking Can Revolutionize Your Business. Wharton Digital Press, 2012.

[31] J. Hamari, J. Koivisto, and T. Sarsa, "Does gamification work? — A literature review of empirical studies," Proc. HICSS, 2014.

[32] T. Gillespie, Custodians of the Internet: Platforms, Content Moderation, and the Hidden Decisions that Shape Social Media. Yale Univ. Press, 2018.

[33] G. Mulgan, "The process of social innovation," Innovations, vol. 1, no. 2, pp. 145–162, 2006.

[34] J. Krumm, "A survey of computational location privacy," Personal and Ubiquitous Computing, vol. 13, no. 6, pp. 391–399, 2009.

[35] R. Shokri, G. Theodorakopoulos, J. Freudiger, M. F. Kaafar, and J.‑P. Hubaux, "Quantifying location privacy," Proc. IEEE S&P, 2011.

[36] Google, "Firebase Cloud Messaging," https://firebase.google.com/docs/cloud-messaging (erişim: 2026‑01‑05).

[37] Microsoft, ".NET Hot Reload," https://learn.microsoft.com/dotnet/core/deploying/hot-reload (erişim: 2026‑01‑05).

[38] Microsoft, ".NET CommunityToolkit.Mvvm," https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/ (erişim: 2026‑01‑05).
