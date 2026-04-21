# 🔧 Ürün Detay Sayfası — Güncelleme & Refresh Düzeltme Planı

> **Tarih:** 2026-04-21  
> **Kapsam:** `ProductDetailViewModel`, `EditProductViewModel`, `ProductDetailPage`, `ProductListViewModel`

---

## 📋 Sorun Özeti

| # | Sorun | Şiddet |
|---|-------|--------|
| **S1** | Ürün güncellendikten sonra `ProductDetailPage`'e geri dönüldüğünde **eski veriler** görünüyor | 🔴 Kritik |
| **S2** | `ProductDetailPage`'de **pull-to-refresh** (aşağı çekerek yenileme) desteği yok | 🟡 Orta |
| **S3** | `ProductListPage`'de de güncelleme ancak **manuel refresh** ile görünüyor | 🟠 Yüksek |

---

## 🔍 Kök Neden Analizi

### S1: Güncelleme sonrası eski veriler görünüyor

**Navigasyon akışı:**
```
ProductDetailPage → EditProductPage (güncelle) → Kaydet → GoToAsync("..") → ProductDetailPage
```

**Sorun zinciri:**

1. `EditProductViewModel.SaveProductAsync()` ([EditProductViewModel.cs:249-279](file:///c:/Users/seyda/source/repos/seydakaratekeli/KamPay3/KamPay/ViewModels/Products/EditProductViewModel.cs#L249-L279)) güncellemeyi API'ye gönderir, ardından `Shell.Current.GoToAsync("..")` ile geri döner.

2. **Hiçbir mesaj yayınlanmaz** — `EditProductViewModel` güncelleme sonrası herhangi bir `WeakReferenceMessenger` mesajı göndermez. `ProductUpdatedMessage` diye bir sınıf **projede mevcut değil**.

3. `ProductDetailViewModel`'de `OnProductIdChanged()` ([ProductDetailViewModel.cs:145-152](file:///c:/Users/seyda/source/repos/seydakaratekeli/KamPay3/KamPay/ViewModels/Products/ProductDetailViewModel.cs#L145-L152)) şu kontrolü yapar:
   ```csharp
   if (!string.IsNullOrEmpty(value) && value != _lastLoadedProductId)
   ```
   Geri dönüşte `ProductId` **aynı değere** set edilir → `_lastLoadedProductId` ile eşleşir → **`LoadProductAsync()` çağrılmaz!**

4. `ProductDetailPage.OnAppearing()` ([ProductDetailPage.xaml.cs:57-66](file:///c:/Users/seyda/source/repos/seydakaratekeli/KamPay3/KamPay/Views/Products/ProductDetailPage.xaml.cs#L57-L66)) sadece `PropertyChanged` event'ine subscribe olur — **`LoadProductAsync()` çağrısı yoktur**.

> [!CAUTION]
> **Kök Neden:** `OnAppearing()`'de ürün verisini yeniden yükleme mekanizması yok VE `EditProductViewModel` güncelleme sonrası bildirim göndermediği için `ProductDetailViewModel` güncellemeyi asla öğrenmiyor.

### S2: Pull-to-refresh desteği yok

`ProductDetailPage.xaml`'de ([ProductDetailPage.xaml:34](file:///c:/Users/seyda/source/repos/seydakaratekeli/KamPay3/KamPay/Views/Products/ProductDetailPage.xaml#L34)) düz bir `ScrollView` kullanılıyor:
```xml
<ScrollView Grid.Row="0">
```
`RefreshView` sarmalayıcısı yok → pull-to-refresh mümkün değil.

### S3: ProductListPage'de de güncelleme geç yansıyor

`ProductListViewModel`'deki realtime listener ([ProductListViewModel.cs:283-303](file:///c:/Users/seyda/source/repos/seydakaratekeli/KamPay3/KamPay/ViewModels/Products/ProductListViewModel.cs#L283-L303)) sadece `LimitToLast(1)` kullanıyor:
```csharp
.OrderByKey()
.LimitToLast(1)
```
Bu listener **sadece yeni eklenen** ürünleri algılar. **Mevcut bir ürünün güncellenmesi** (başlık, fiyat, açıklama vs. değişikliği) bu listener tarafından **yakalanamaz** çünkü key değişmez ve `LimitToLast(1)` sadece son key'i izler.

---

## 🏗️ Düzeltme Planı

### Faz 1: Mesajlaşma Altyapısı (Önkoşul)

**Dosya:** Yeni dosya — `Models/EventMessages/ProductUpdatedMessage.cs`

```csharp
using CommunityToolkit.Mvvm.Messaging.Messages;
using KamPay.Models;

namespace KamPay.Models.EventMessages
{
    public class ProductUpdatedMessage : ValueChangedMessage<Product>
    {
        public ProductUpdatedMessage(Product value) : base(value) { }
    }
}
```

> [!NOTE]
> Bu mesaj sınıfı, ürün güncelleme/silme/satış gibi tüm değişiklik senaryolarında sayfalar arası iletişimi sağlayacak merkezi mekanizma olacak.

---

### Faz 2: EditProductViewModel — Güncelleme Bildirimi

**Dosya:** [EditProductViewModel.cs](file:///c:/Users/seyda/source/repos/seydakaratekeli/KamPay3/KamPay/ViewModels/Products/EditProductViewModel.cs)

**Değişiklik:** `SaveProductAsync()` metodu güncellendikten sonra mesaj yayınlayacak.

```diff
 if (result.Success)
 {
+    // Güncel ürün verisini API'den çek ve mesaj olarak yayınla
+    var updatedResult = await _productService.GetProductByIdAsync(ProductId);
+    if (updatedResult.Success && updatedResult.Data != null)
+    {
+        WeakReferenceMessenger.Default.Send(new ProductUpdatedMessage(updatedResult.Data));
+    }
+
     await Application.Current!.MainPage!.DisplayAlert("Başarılı", "Ürün güncellendi.", "Tamam");
     await Shell.Current.GoToAsync("..");
 }
```

**Gerekli using:**
```diff
+using KamPay.Models.EventMessages;
```

---

### Faz 3: ProductDetailViewModel — Güncelleme Mesajını Dinleme + Refresh Desteği

**Dosya:** [ProductDetailViewModel.cs](file:///c:/Users/seyda/source/repos/seydakaratekeli/KamPay3/KamPay/ViewModels/Products/ProductDetailViewModel.cs)

#### 3.1 — IsRefreshing property eklenmesi
```diff
 [ObservableProperty]
 private bool isLoading;

+[ObservableProperty]
+private bool isRefreshing;
```

#### 3.2 — Constructor'da mesaj dinleyici kaydı
```diff
 public ProductDetailViewModel(...)
 {
     // ... mevcut kod ...
     
+    // Ürün güncelleme mesajını dinle
+    WeakReferenceMessenger.Default.Register<ProductUpdatedMessage>(this, (r, m) =>
+    {
+        if (m.Value.ProductId == ProductId)
+        {
+            MainThread.BeginInvokeOnMainThread(() =>
+            {
+                Product = m.Value;
+                ProductImages.Clear();
+                if (m.Value.ImageUrls != null)
+                {
+                    foreach (var url in m.Value.ImageUrls)
+                        ProductImages.Add(url);
+                }
+                CurrentImageIndex = 0;
+                OnPropertyChanged(nameof(HasLocation));
+            });
+        }
+    });
     
     KamPay.Helpers.AppLogger.DebugLog("✅ ProductDetailViewModel oluşturuldu");
 }
```

#### 3.3 — RefreshProduct komutu eklenmesi
```diff
+[RelayCommand]
+private async Task RefreshProductAsync()
+{
+    IsRefreshing = true;
+    try
+    {
+        _lastLoadedProductId = null; // Guard'ı sıfırla
+        await LoadProductAsync();
+    }
+    finally
+    {
+        IsRefreshing = false;
+    }
+}
```

#### 3.4 — Dispose'da mesaj kaydını temizleme
```diff
 protected virtual void Dispose(bool disposing)
 {
     if (!_disposed)
     {
         if (disposing)
         {
             _userStateService.UserProfileChanged -= OnUserProfileChanged;
             _transactionListener?.Dispose();
             _transactionListener = null;
+            WeakReferenceMessenger.Default.UnregisterAll(this);
         }
         _disposed = true;
     }
 }
```

#### 3.5 — Gerekli using
```diff
+using KamPay.Models.EventMessages;
```

---

### Faz 4: ProductDetailPage.xaml — RefreshView Sarmalayıcısı

**Dosya:** [ProductDetailPage.xaml](file:///c:/Users/seyda/source/repos/seydakaratekeli/KamPay3/KamPay/Views/Products/ProductDetailPage.xaml)

**Değişiklik:** `ScrollView`'i `RefreshView` ile sarmala.

```diff
 <Grid RowDefinitions="*,Auto">

-    <ScrollView Grid.Row="0">
+    <RefreshView Grid.Row="0"
+                 Command="{Binding RefreshProductCommand}"
+                 IsRefreshing="{Binding IsRefreshing}"
+                 RefreshColor="{StaticResource Primary}">
+        <ScrollView>
             <VerticalStackLayout Spacing="0">
                 <!-- ... mevcut içerik ... -->
             </VerticalStackLayout>
-    </ScrollView>
+        </ScrollView>
+    </RefreshView>
```

---

### Faz 5: ProductDetailPage.xaml.cs — OnAppearing Yeniden Yükleme

**Dosya:** [ProductDetailPage.xaml.cs](file:///c:/Users/seyda/source/repos/seydakaratekeli/KamPay3/KamPay/Views/Products/ProductDetailPage.xaml.cs)

**Değişiklik:** `OnAppearing()`'de harita durumunu sıfırla ve ürün verisini yeniden yükle.

```diff
 protected override void OnAppearing()
 {
     base.OnAppearing();

     // Subscribe to property changes to know when product is loaded
     _viewModel.PropertyChanged += OnViewModelPropertyChanged;

+    // Sayfaya her dönüşte ürün verisini yeniden yükle (düzenleme sonrası güncel veri garantisi)
+    if (!string.IsNullOrEmpty(_viewModel.ProductId))
+    {
+        _isMapInitialized = false; // Haritanın yeniden çizilmesini sağla
+        _viewModel.RefreshProductCommand.Execute(null);
+    }
 }
```

---

### Faz 6: ProductListViewModel — Güncelleme Mesajını Dinleme

**Dosya:** [ProductListViewModel.cs](file:///c:/Users/seyda/source/repos/seydakaratekeli/KamPay3/KamPay/ViewModels/Products/ProductListViewModel.cs)

**Değişiklik:** Constructor'da `ProductUpdatedMessage`'ı dinle, listeyi güncelle.

```diff
 // Constructor'daki mevcut WeakReferenceMessenger kayıtlarından sonra:

+WeakReferenceMessenger.Default.Register<ProductUpdatedMessage>(this, (_, m) =>
+{
+    var updated = m.Value;
+    MainThread.BeginInvokeOnMainThread(() =>
+    {
+        lock (_allProductsLock)
+        {
+            var idx = _allLoadedProducts.FindIndex(p => p.ProductId == updated.ProductId);
+            if (idx >= 0) _allLoadedProducts[idx] = updated;
+        }
+
+        var visible = Products.FirstOrDefault(p => p.ProductId == updated.ProductId);
+        if (visible != null)
+        {
+            var visIdx = Products.IndexOf(visible);
+            if (visIdx >= 0)
+            {
+                Products[visIdx] = updated;
+            }
+        }
+    });
+});
```

**Gerekli using:**
```diff
+using KamPay.Models.EventMessages;
```

---

## 🐛 Keşfedilen Ek Hatalar

### EK-1: `_lastLoadedProductId` Guard Sorunu

**Dosya:** [ProductDetailViewModel.cs:145-152](file:///c:/Users/seyda/source/repos/seydakaratekeli/KamPay3/KamPay/ViewModels/Products/ProductDetailViewModel.cs#L145-L152)

```csharp
partial void OnProductIdChanged(string? value)
{
    if (!string.IsNullOrEmpty(value) && value != _lastLoadedProductId)
    {
        _lastLoadedProductId = value;
        _ = LoadProductAsync();
    }
}
```

> [!WARNING]
> **Sorun:** Eğer kullanıcı A ürününe bakar → listeeye döner → aynı A ürününe tekrar tıklarsa, `_lastLoadedProductId` hâlâ "A" olduğundan **ürün yüklenmez**. ViewModel `Transient` olarak kayıtlı olduğundan bu teoride sorun olmamalı, ancak Shell navigasyonu bazen aynı ViewModel instance'ını yeniden kullanabilir.
> 
> **Çözüm:** Faz 5'teki `OnAppearing` düzeltmesi bunu da çözer (her sayfaya dönüşte `_lastLoadedProductId`'yi sıfırlayıp yeniden yükler).

### EK-2: Harita Tekrar İnişialize Edilmiyor

**Dosya:** [ProductDetailPage.xaml.cs:94](file:///c:/Users/seyda/source/repos/seydakaratekeli/KamPay3/KamPay/Views/Products/ProductDetailPage.xaml.cs#L94)

```csharp
if (_isMapInitialized || ProductMap?.Map == null) return Task.CompletedTask;
```

> [!NOTE]
> **Sorun:** `_isMapInitialized` bir kez `true` olduktan sonra, ürün güncellendikten sonra harita konum bilgisi değişse bile harita yeniden çizilmez.
> 
> **Çözüm:** Faz 5'te `_isMapInitialized = false` yapılarak bu da otomatik çözülür.

### EK-3: Dispose Eksikliği — WeakReferenceMessenger Sızıntısı

**Dosya:** [ProductDetailViewModel.cs:125-143](file:///c:/Users/seyda/source/repos/seydakaratekeli/KamPay3/KamPay/ViewModels/Products/ProductDetailViewModel.cs#L125-L143)

> [!WARNING]
> **Sorun:** `Dispose()` metodunda `WeakReferenceMessenger.Default.UnregisterAll(this)` çağrısı yok. `FavoriteCountChangedMessage` gibi mesajlar constructor'da register edilmese de, Faz 3'te eklenen `ProductUpdatedMessage` listener'ı temizlenmezse **memory leak** olur.
> 
> **Çözüm:** Faz 3.4'te düzeltildi.

### EK-4: EditProductPage — OnDisappearing'de ViewModel PropertyChanged Temizleme Zamanlaması

**Dosya:** [EditProductPage.xaml.cs:43-48](file:///c:/Users/seyda/source/repos/seydakaratekeli/KamPay3/KamPay/Views/Products/EditProductPage.xaml.cs#L43-L48)

> [!NOTE]
> **Potansiyel Risk:** `SaveProductAsync()` çalışırken `OnDisappearing` tetiklenebilir ve `PropertyChanged` handler'ı erken kaldırılabilir. Bu, konum güncellemelerinin kaybolmasına neden olabilir. Ancak bu düşük riskli bir konudur ve mevcut sorunla doğrudan ilgili değildir.

---

## 📊 Uygulama Sırası ve Tahmini Etki

| Faz | Dosya | Değişiklik Tipi | Risk | Tahmini Süre |
|-----|-------|-----------------|------|-------------|
| **Faz 1** | `ProductUpdatedMessage.cs` | Yeni dosya | 🟢 Düşük | 2 dk |
| **Faz 2** | `EditProductViewModel.cs` | Mevcut metod düzenleme | 🟢 Düşük | 5 dk |
| **Faz 3** | `ProductDetailViewModel.cs` | Property + Command + Register | 🟡 Orta | 10 dk |
| **Faz 4** | `ProductDetailPage.xaml` | XAML sarmalama | 🟢 Düşük | 3 dk |
| **Faz 5** | `ProductDetailPage.xaml.cs` | OnAppearing düzenleme | 🟡 Orta | 5 dk |
| **Faz 6** | `ProductListViewModel.cs` | Mesaj dinleyici ekleme | 🟢 Düşük | 5 dk |

> **Toplam:** ~30 dk — 6 dosya değişikliği (1 yeni, 5 düzenleme)

---

## ✅ Doğrulama Senaryoları

Düzeltme sonrası test edilecek senaryolar:

1. **Temel Senaryo:** Detay → Düzenle → Başlık değiştir → Kaydet → Detay sayfasında yeni başlık **anında** görünmeli
2. **Fiyat Güncellemesi:** Fiyat değiştir → Kaydet → Detay ve Liste'de yeni fiyat görünmeli
3. **Görsel Değişikliği:** Yeni görsel ekle → Kaydet → Carousel yeni görseli göstermeli
4. **Konum Değişikliği:** Konum güncelle → Kaydet → Harita yeni konumu göstermeli
5. **Pull-to-Refresh:** Detay sayfasında aşağı çek → Veriler yeniden yüklenmeli
6. **Liste Güncellemesi:** Ürün güncelle → Listeye dön → Güncellenen bilgiler **refresh'siz** görünmeli
