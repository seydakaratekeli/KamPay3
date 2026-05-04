# Kampüs Rehberi Modülü

## Mimari Genel Bakış

Kampüs Rehberi, KamPay mobil uygulamasına read-only tanıtım modülü olarak eklenmiştir. Modül MVVM akışını korur:

- `Views/CampusGuide` sadece UI ve kullanıcı etkileşimini taşır.
- `ViewModels/CampusGuide` listeleme, filtreleme, refresh ve navigasyon durumunu yönetir.
- `Services/CampusGuide` Firebase Realtime Database okuma, doğrulanmış veri filtresi ve cache fallback sorumluluğunu üstlenir.
- `Models/CampusGuide` Firebase veri sözleşmesini ve UI için güvenli computed property'leri içerir.

Uygulama içinden ödeme, sipariş veya rezervasyon başlatılmaz. İşletmeler pasif içerik sağlayıcıdır; admin doğrulaması Firebase verisi üzerinden `VerificationStatus`, `IsVerified` ve `IsActive` alanlarıyla temsil edilir.

## Firebase Şeması

```json
{
  "micro_businesses": {
    "{businessId}": {
      "BusinessId": "business-1",
      "Name": "Kampüs Kafe",
      "Description": "Kütüphane yanında kahve ve atıştırmalık noktası.",
      "Category": 0,
      "CategoryName": "Yeme İçme",
      "Location": "Merkez Kampüs, Kütüphane yanı",
      "PhoneNumber": "+90...",
      "InstagramUrl": "https://instagram.com/...",
      "WebsiteUrl": "",
      "LogoUrl": "https://...",
      "CoverImageUrl": "https://...",
      "IsVerified": true,
      "VerificationStatus": 2,
      "OwnerUserId": "firebase-user-id",
      "OwnerEmail": "owner@example.com",
      "IsActive": true,
      "DisplayOrder": 10,
      "Tags": ["kahve", "öğrenci indirimi"],
      "WorkingHours": {
        "weekday": "08:30-18:00",
        "weekend": "Kapalı"
      },
      "CreatedAt": "2026-04-28T09:00:00Z",
      "VerifiedAt": "2026-04-28T09:30:00Z"
    }
  },
  "campaigns": {
    "{campaignId}": {
      "CampaignId": "campaign-1",
      "BusinessId": "business-1",
      "Title": "Öğrenci Kahvesi",
      "Description": "Öğrenci kartını gösterene filtre kahvede indirim.",
      "BadgeText": "%15",
      "ImageUrl": "",
      "IsActive": true,
      "Status": 1,
      "CreatedByUserId": "firebase-user-id",
      "DisplayOrder": 1,
      "StartsAt": "2026-04-20T00:00:00Z",
      "EndsAt": "2026-05-20T23:59:59Z"
    }
  }
}
```

## Gerekli Indexler

```json
{
  "rules": {
    "micro_businesses": {
      ".indexOn": ["IsVerified", "IsActive", "Category", "DisplayOrder", "VerificationStatus", "OwnerUserId"]
    },
    "campaigns": {
      ".indexOn": ["BusinessId", "CreatedByUserId", "IsActive", "Status", "StartsAt", "EndsAt", "DisplayOrder"]
    }
  }
}
```

## Veri Akışı

1. `CampusGuidePage.OnAppearing` liste ViewModel'inin `LoadCommand` komutunu çalıştırır.
2. `CampusGuideViewModel`, `IMicroBusinessService.GetVerifiedBusinessesAsync` ile sadece `IsVerified && IsActive` işletmeleri alır.
3. Arama ve kategori filtreleri Firebase'e tekrar gitmeden bellekte uygulanır.
4. Kullanıcı işletmeye dokununca `BusinessDetailPage` route'una seçili `MicroBusiness` nesnesi aktarılır.
5. `BusinessDetailViewModel`, `ICampaignService.GetActiveCampaignsByBusinessIdAsync` ile yalnızca aktif ve tarih aralığı geçerli kampanyaları gösterir.

## Edge Case ve Fallback

- Veri yoksa liste boş durum metni gösterir.
- Süresi dolmuş kampanyalar servis katmanında elenir.
- Network hatasında son başarılı cache `Preferences` üzerinden gösterilir.
- Bozuk veya eksik işletme verisi `Name`, `BusinessId`, `IsVerified`, `IsActive` kontrollerinden geçemezse UI'a çıkmaz.
- Client tarafındaki doğrulama sadece UX ve savunma katmanıdır; üretimde Firebase rules/admin paneli doğrulanmamış veriyi public read kapsamından çıkarmalıdır.

## CRUD ve Rol Akışı

- Öğrenci hesabı `Student` rolüyle mevcut kayıt akışını kullanır ve sadece public rehberi görür.
- İşletme hesabı ayrı başvuru ekranından oluşturulur. Bu akış `@bartin.edu.tr` zorunluluğunu uygulamaz ve kullanıcıyı `BusinessOwner` rolüyle kaydeder.
- Yeni işletme `PendingReview` durumunda oluşur; öğrencilere görünmez.
- Admin kullanıcıları normal tab içinde ayrı bir ekran görmez. Login sonrası admin onay kuyruğuna yönlenir.
- Admin onaylayınca işletme `Verified` olur; işletme sahibi kampanya oluşturabilir, düzenleyebilir ve silebilir.
- Kampanyalar ek admin onayına gitmez; çünkü sadece doğrulanmış işletme sahibi tarafından oluşturulabilir.
- BusinessOwner sadece kendi `BusinessId` değerine bağlı işletme profilini ve kampanyalarını yönetebilir.
