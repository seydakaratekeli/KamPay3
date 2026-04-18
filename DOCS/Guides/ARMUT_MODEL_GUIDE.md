# ?? ARMUT MODELÝ - KAMPAY KULLANIM KILAVUZU

## ?? Ýçindekiler
1. [Genel Bakýþ](#genel-bakýþ)
2. [Mevcut Sistem vs Yeni Sistem](#mevcut-sistem-vs-yeni-sistem)
3. [Kod Yapýsý](#kod-yapýsý)
4. [Firebase Yapýlandýrmasý](#firebase-yapýlandýrmasý)
5. [Kullaným Senaryolarý](#kullaným-senaryolarý)
6. [UI Entegrasyonu](#ui-entegrasyonu)
7. [Test Adýmlarý](#test-adýmlarý)

---

## ?? Genel Bakýþ

KamPay'e **Armut modeli** eklendi. Artýk **iki farklý hizmet paylaþým sistemi** var:

### **?? Sistem 1: Eski Sistem (Hala Aktif)**
- Profesyoneller hizmet paylaþýr
- Müþteriler bu hizmetlere talep gönderir
- Direkt iletiþim kurulur

### **?? Sistem 2: Armut Modeli (YENÝ)**
- Müþteriler ihtiyaçlarýný talep olarak yayýnlar
- Profesyoneller bu taleplere **teklif gönderir**
- Müþteri teklifleri **karþýlaþtýrýp** en iyisini seçer

---

## ?? Mevcut Sistem vs Yeni Sistem

| Özellik | Mevcut Sistem | Armut Modeli |
|---------|---------------|--------------|
| **Baþlatýcý** | Profesyonel | Müþteri |
| **Ýlk Adým** | Hizmet paylaþýmý | Talep oluþturma |
| **Fiyat** | Sabit | Teklif bazlý |
| **Rekabet** | Yok | Profesyoneller arasý |
| **Konum** | Opsiyonel | Zorunlu |
| **Tarih** | Opsiyonel | Tercih edilen tarih |
| **Görseller** | Hizmet görseli | Ýþ yeri görselleri |

---

## ?? Kod Yapýsý

### **Yeni Modeller**

```
KamPay/Models/
??? CustomerServiceRequest.cs   ? Müþteri talepleri
??? ProviderProposal.cs         ? Profesyonel teklifleri
```

#### **CustomerServiceRequest** Özellikleri:
```csharp
- RequestId              // Benzersiz ID
- CustomerId            // Müþteri kimliði
- Category              // Hizmet kategorisi
- Title                 // Talep baþlýðý
- Description           // Detaylý açýklama
- Location              // Ýþ yeri konumu
- Latitude/Longitude    // Koordinatlar
- BudgetMin/Max         // Bütçe aralýðý
- PreferredDate         // Tercih edilen tarih
- ImageUrls             // Ýþ yeri fotoðraflarý
- ProposalCount         // Gelen teklif sayýsý
- Status                // Talep durumu
```

#### **ProviderProposal** Özellikleri:
```csharp
- ProposalId            // Benzersiz ID
- CustomerRequestId     // Ýlgili talep
- ProviderId           // Profesyonel kimliði
- Price                // Teklif edilen fiyat
- Message              // Teklif mesajý
- EstimatedDays        // Tahmini tamamlanma süresi
- PortfolioImageUrls   // Referans çalýþmalar
- Rating               // Profesyonel puaný
- CompletedJobsCount   // Tamamlanan iþ sayýsý
- Status               // Teklif durumu (Pending/Accepted/Rejected)
```

---

### **Yeni Servisler**

```
KamPay/Services/
??? IServiceSharingService.cs          ? Interface güncellendi
??? FirebaseServiceSharingService.cs   ? 15+ yeni metod eklendi
```

#### **Yeni Metodlar:**

**Müþteri Ýþlemleri:**
```csharp
- CreateCustomerRequestAsync()           // Talep oluþtur
- GetCustomerRequestsAsync()             // Tüm talepleri getir
- GetCustomerRequestsPagedAsync()        // Sayfalama ile getir
- GetMyCustomerRequestsAsync()           // Kendi taleplerim
- UpdateCustomerRequestAsync()           // Talep güncelle
- CancelCustomerRequestAsync()           // Talep iptal et
```

**Profesyonel Ýþlemleri:**
```csharp
- SendProposalAsync()                    // Teklif gönder
- GetProposalsForRequestAsync()          // Talebe gelen teklifler
- GetMyProposalsAsync()                  // Kendi tekliflerim
- WithdrawProposalAsync()                // Teklifi geri çek
```

**Teklif Yönetimi:**
```csharp
- AcceptProposalAsync()                  // Teklifi kabul et
- RejectProposalAsync()                  // Teklifi reddet
- CreateServiceContractFromProposalAsync() // Ýþ sözleþmesi oluþtur
```

---

### **Yeni ViewModels**

```
KamPay/ViewModels/
??? CreateCustomerRequestViewModel.cs      ? Müþteri talep formu
??? CustomerRequestsListViewModel.cs       ? Talep listesi
??? CustomerRequestDetailsViewModel.cs     ? Talep detay & teklif
```

---

## ?? Firebase Yapýlandýrmasý

### **1. Yeni Koleksiyonlar**

`Constants.cs` dosyasýna eklendi:
```csharp
public const string CustomerServiceRequestsCollection = "customer_service_requests";
public const string ProviderProposalsCollection = "provider_proposals";
```

### **2. Firebase Rules**

Firebase Console ? Realtime Database ? Rules:

```json
{
  "rules": {
    "customer_service_requests": {
      ".indexOn": ["Category", "CreatedAt", "CustomerId", "Status"],
      ".read": "auth != null",
      ".write": "auth != null"
    },
    "provider_proposals": {
      ".indexOn": ["CustomerRequestId", "ProviderId", "Status", "CreatedAt"],
      ".read": "auth != null",
      ".write": "auth != null"
    }
  }
}
```

?? **UYARI:** Bu indeksler olmadan sayfalama ve filtreleme **ÇALIÞMAZ**!

---

## ?? Kullaným Senaryolarý

### **?? Senaryo 1: Müþteri Talep Oluþturur**

```csharp
// CreateCustomerRequestViewModel
var request = new CustomerServiceRequest
{
    CustomerId = currentUser.UserId,
    Category = ServiceCategory.Technical,
    Title = "50m² Salon Boyasý",
    Description = "Salon duvarlarý boyanacak, tavan dahil",
    Location = "Bartýn, Merkez",
    BudgetMin = 2000,
    BudgetMax = 3500,
    PreferredDate = new DateTime(2025, 5, 15)
};

var result = await _serviceService.CreateCustomerRequestAsync(request);
```

### **?? Senaryo 2: Profesyoneller Talepleri Görür**

```csharp
// CustomerRequestsListViewModel
var requests = await _serviceService.GetCustomerRequestsPagedAsync(
    pageSize: 20,
    category: ServiceCategory.Technical
);

// Filtreli liste
FilteredRequests.Clear();
foreach (var req in requests.Data)
{
    FilteredRequests.Add(req);
}
```

### **?? Senaryo 3: Profesyonel Teklif Gönderir**

```csharp
// CustomerRequestDetailsViewModel
var proposal = new ProviderProposal
{
    CustomerRequestId = requestId,
    ProviderId = currentUser.UserId,
    Price = 2800,
    Message = "10 yýllýk deneyim, garantili iþçilik",
    EstimatedDays = 2
};

var result = await _serviceService.SendProposalAsync(proposal);
```

### **?? Senaryo 4: Müþteri Teklifleri Karþýlaþtýrýr**

```csharp
// CustomerRequestDetailsViewModel
var proposals = await _serviceService.GetProposalsForRequestAsync(requestId);

// UI'da göster:
// - Profesyonel puaný: ? 4.8 (23 iþ)
// - Fiyat: 2800?
// - Süre: 2 gün
// - Mesaj: "10 yýllýk deneyim..."
```

### **?? Senaryo 5: Müþteri Teklifi Kabul Eder**

```csharp
// Teklifi kabul et
await _serviceService.AcceptProposalAsync(proposalId, customerId);

// Otomatik iþlemler:
// ? Teklif durumu ? Accepted
// ? Diðer teklifler ? Rejected (auto)
// ? Talep durumu ? ProviderSelected
// ? Bildirimler gönderilir
```

### **?? Senaryo 6: Ýþ Sözleþmesi Oluþturulur**

```csharp
// Teklif kabul edildikten sonra
var contract = await _serviceService.CreateServiceContractFromProposalAsync(proposalId);

// ServiceRequest oluþturulur (mevcut sistemle uyumlu)
// Ödeme ve tamamlanma mevcut akýþ ile devam eder
```

---

## ?? UI Entegrasyonu

### **1. Müþteri Talep Formu (XAML)**

```xml
<!-- CreateCustomerRequestPage.xaml -->
<ContentPage>
    <ScrollView>
        <VerticalStackLayout Padding="20">
            <!-- Baþlýk -->
            <Entry Placeholder="Baþlýk" 
                   Text="{Binding Title}" />
            
            <!-- Açýklama -->
            <Editor Placeholder="Detaylý açýklama" 
                    Text="{Binding Description}" 
                    HeightRequest="100" />
            
            <!-- Kategori -->
            <Picker ItemsSource="{Binding Categories}" 
                    SelectedItem="{Binding SelectedCategory}" />
            
            <!-- Konum -->
            <Entry Placeholder="Konum" 
                   Text="{Binding Location}" />
            
            <!-- Bütçe -->
            <HorizontalStackLayout>
                <Entry Placeholder="Min" 
                       Text="{Binding BudgetMin}" 
                       Keyboard="Numeric" />
                <Entry Placeholder="Max" 
                       Text="{Binding BudgetMax}" 
                       Keyboard="Numeric" />
            </HorizontalStackLayout>
            
            <!-- Tarih -->
            <DatePicker Date="{Binding PreferredDate}" />
            
            <!-- Fotoðraflar -->
            <CollectionView ItemsSource="{Binding ImageUrls}">
                <CollectionView.ItemTemplate>
                    <DataTemplate>
                        <Image Source="{Binding .}" 
                               HeightRequest="100" 
                               WidthRequest="100" />
                    </DataTemplate>
                </CollectionView.ItemTemplate>
            </CollectionView>
            
            <Button Text="Fotoðraf Ekle" 
                    Command="{Binding AddPhotoCommand}" />
            
            <!-- Gönder -->
            <Button Text="Talep Oluþtur" 
                    Command="{Binding CreateRequestCommand}" 
                    IsEnabled="{Binding IsPosting, Converter={StaticResource InverseBoolConverter}}" />
        </VerticalStackLayout>
    </ScrollView>
</ContentPage>
```

### **2. Talep Listesi (XAML)**

```xml
<!-- CustomerRequestsListPage.xaml -->
<ContentPage>
    <RefreshView IsRefreshing="{Binding IsRefreshing}" 
                 Command="{Binding RefreshRequestsCommand}">
        <CollectionView ItemsSource="{Binding FilteredRequests}">
            <CollectionView.ItemTemplate>
                <DataTemplate x:DataType="models:CustomerServiceRequest">
                    <Frame Padding="15" Margin="10">
                        <Grid RowDefinitions="Auto,Auto,Auto,Auto" 
                              ColumnDefinitions="*,Auto">
                            
                            <!-- Baþlýk -->
                            <Label Grid.Row="0" Grid.Column="0"
                                   Text="{Binding Title}" 
                                   FontSize="16" 
                                   FontAttributes="Bold" />
                            
                            <!-- Teklif sayýsý -->
                            <Label Grid.Row="0" Grid.Column="1"
                                   Text="{Binding ProposalCount, StringFormat='{0} teklif'}" 
                                   FontSize="12" />
                            
                            <!-- Açýklama -->
                            <Label Grid.Row="1" Grid.ColumnSpan="2"
                                   Text="{Binding Description}" 
                                   MaxLines="2" 
                                   LineBreakMode="TailTruncation" />
                            
                            <!-- Konum & Bütçe -->
                            <HorizontalStackLayout Grid.Row="2" Grid.ColumnSpan="2" 
                                                   Spacing="10">
                                <Label Text="??" />
                                <Label Text="{Binding Location}" />
                                <Label Text="??" />
                                <Label Text="{Binding BudgetText}" />
                            </HorizontalStackLayout>
                            
                            <!-- Tarih -->
                            <Label Grid.Row="3" Grid.ColumnSpan="2"
                                   Text="{Binding PreferredDateText}" 
                                   FontSize="12" 
                                   TextColor="Gray" />
                        </Grid>
                        
                        <Frame.GestureRecognizers>
                            <TapGestureRecognizer 
                                Command="{Binding Source={RelativeSource AncestorType={x:Type vm:CustomerRequestsListViewModel}}, Path=ViewRequestDetailsCommand}"
                                CommandParameter="{Binding .}" />
                        </Frame.GestureRecognizers>
                    </Frame>
                </DataTemplate>
            </CollectionView.ItemTemplate>
        </CollectionView>
    </RefreshView>
</ContentPage>
```

### **3. Teklif Gönderme (XAML)**

```xml
<!-- CustomerRequestDetailsPage.xaml - Teklif Formu -->
<Frame IsVisible="{Binding IsProposalFormVisible}">
    <VerticalStackLayout Spacing="15">
        <Label Text="Teklif Gönder" 
               FontSize="18" 
               FontAttributes="Bold" />
        
        <!-- Fiyat -->
        <Entry Placeholder="Teklif Fiyatýnýz (?)" 
               Text="{Binding ProposalPrice}" 
               Keyboard="Numeric" />
        
        <!-- Mesaj -->
        <Editor Placeholder="Teklif mesajýnýz" 
                Text="{Binding ProposalMessage}" 
                HeightRequest="100" />
        
        <!-- Tahmini süre -->
        <HorizontalStackLayout>
            <Label Text="Tahmini Tamamlanma:" 
                   VerticalOptions="Center" />
            <Entry Text="{Binding EstimatedDays}" 
                   Keyboard="Numeric" 
                   WidthRequest="60" />
            <Label Text="gün" 
                   VerticalOptions="Center" />
        </HorizontalStackLayout>
        
        <!-- Butonlar -->
        <HorizontalStackLayout Spacing="10">
            <Button Text="Gönder" 
                    Command="{Binding SendProposalCommand}" 
                    BackgroundColor="Green" />
            <Button Text="Ýptal" 
                    Command="{Binding CloseProposalFormCommand}" 
                    BackgroundColor="Gray" />
        </HorizontalStackLayout>
    </VerticalStackLayout>
</Frame>
```

---

## ?? Test Adýmlarý

### **1. Manuel Test**

```
1. ? Müþteri giriþi yap
2. ? "Talep Oluþtur" sayfasýna git
3. ? Form doldur:
   - Baþlýk: "Salon Boyasý"
   - Açýklama: "50m² salon boyanacak"
   - Kategori: Technical
   - Konum: "Bartýn"
   - Bütçe: 2000-3500?
4. ? Fotoðraf ekle
5. ? "Talep Oluþtur" butonuna bas
6. ? Firebase'de "customer_service_requests" koleksiyonunu kontrol et

7. ? Profesyonel giriþi yap
8. ? "Talepler" sayfasýna git
9. ? Oluþturulan talebi gör
10. ? Talep detayýna git
11. ? "Teklif Gönder" butonuna bas
12. ? Form doldur:
    - Fiyat: 2800?
    - Mesaj: "10 yýllýk deneyim"
    - Süre: 2 gün
13. ? "Gönder" butonuna bas
14. ? Firebase'de "provider_proposals" koleksiyonunu kontrol et

15. ? Müþteri giriþi yap
16. ? "Taleplerim" sayfasýna git
17. ? Talebi aç
18. ? Gelen teklifleri gör
19. ? "Kabul Et" butonuna bas
20. ? Diðer tekliflerin otomatik reddedildiðini kontrol et
```

### **2. Realtime Test**

```
1. ? Ýki cihazda aç (veya iki tarayýcý sekmesi)
2. ? Birinde müþteri, diðerinde profesyonel gir
3. ? Müþteri talep oluþtursun
4. ? Profesyonelin ekranýnda anlýk görünsün mü?
5. ? Profesyonel teklif göndersin
6. ? Müþterinin ekranýnda anlýk görünsün mü?
7. ? Müþteri teklifi kabul etsin
8. ? Profesyonelin ekranýnda durum deðiþsin mi?
```

### **3. Edge Cases**

```
? Ayný profesyonel iki kez teklif gönderebilir mi? ? HAYIR
? Müþteri kendi talebine teklif gönderebilir mi? ? HAYIR
? Ýptal edilen talebe teklif gönderilebilir mi? ? HAYIR
? Kabul edilen teklif geri çekilebilir mi? ? HAYIR
? Reddedilen teklif tekrar kabul edilebilir mi? ? HAYIR
```

---

## ?? Deployment Checklist

### **Firebase Console**

- [ ] Yeni koleksiyonlar oluþturuldu
- [ ] Ýndeksler tanýmlandý
- [ ] Read/Write kurallarý eklendi
- [ ] Storage kurallarý güncellendi

### **Kod Tarafý**

- [ ] Tüm ViewModels `MauiProgram.cs`'e eklendi
- [ ] Tüm sayfalar `AppShell.xaml`'e route edildi
- [ ] Navigation parametreleri doðru
- [ ] Localization keyleri eklendi

### **Test**

- [ ] Manuel testler geçti
- [ ] Realtime güncellemeler çalýþýyor
- [ ] Bildirimler gönderiliyor
- [ ] Edge cases kontrol edildi

---

## ?? Destek

Sorun yaþarsanýz:
1. Firebase Console'da Realtime Database loglarýný kontrol edin
2. Debug modda console çýktýlarýný inceleyin
3. ViewModels'deki `Console.WriteLine` mesajlarýný takip edin

**Kritik Noktalar:**
- Firebase indeksleri **MUTLAKA** tanýmlanmalý
- `IsActive` ve `Status` kontrolleri önemli
- Realtime listener temizliði unutulmamalý (`Dispose`)

---

? **Sistem Hazýr!** Artýk kullanýcýlar Armut gibi teklif alabilir!
