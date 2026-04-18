# KamPay — Data Binding Kýlavuzu

Bu belge, KamPay .NET MAUI projesinde gerçekleþtirilen data binding ile ilgili tüm tasarým kararlarýný, desenleri ve deðiþiklikleri dokümante eder.

---

## Ýçindekiler

1. [Genel Mimari](#genel-mimari)
2. [ObservableObject ve CommunityToolkit.Mvvm](#observableobject-ve-communitytoolkitmvvm)
3. [Converter Katmaný](#converter-katmaný)
4. [Behavior ile CollectionView Optimizasyonu](#behavior-ile-collectionview-optimizasyonu)
5. [Lokalizasyon Binding'i](#lokalizasyon-bindingi)
6. [Realtime Veri Binding'i](#realtime-veri-bindingi)
7. [Mesaj Ekraný Binding Detaylarý](#mesaj-ekraný-binding-detaylarý)
8. [Ürün Listesi Binding Detaylarý](#ürün-listesi-binding-detaylarý)
9. [Güvenlik & Oturum Binding'i](#güvenlik--oturum-bindingi)
10. [Bilinen Kýsýtlamalar ve Geçici Çözümler](#bilinen-kýsýtlamalar-ve-geçici-çözümler)

---

## Genel Mimari

Uygulama **MVVM** desenini kullanýr:

```
View (.xaml)  ??  ViewModel (ObservableObject)  ??  Service / Repository
```

- **View ? ViewModel**: `{Binding PropertyName}` veya `Command="{Binding XyzCommand}"` ile.
- **ViewModel ? View**: `[ObservableProperty]` veya `INotifyPropertyChanged` aracýlýðýyla otomatik bildirim.
- **ViewModel ? ViewModel**: `WeakReferenceMessenger` ile gevþek baðlý mesajlar.

---

## ObservableObject ve CommunityToolkit.Mvvm

Tüm ViewModel'lar `CommunityToolkit.Mvvm.ComponentModel.ObservableObject` sýnýfýndan türetilir.

### `[ObservableProperty]` Kullanýmý

Otomatik `INotifyPropertyChanged` bildirimi için kullanýlýr. Örnek (`LoginViewModel.cs`):

```csharp
[ObservableProperty]
private string email = string.Empty;

[ObservableProperty]
private bool isLoading;
```

> Derleme zamanýnda `Email`, `IsLoading` gibi public property'ler üretilir; XAML doðrudan bu isimlerle baðlanýr.

### `partial void OnXyzChanged` Kancalarý

Bir property deðiþtiðinde yan etki tetiklemek için:

```csharp
// ProductListViewModel.cs
async partial void OnUserIdChanged(string? value)
{
    // UserId deðiþtiðinde ürün listesini yeniden yükle
    ...
    await UltraFastLoadAsync();
}

partial void OnSelectedSortOptionChanged(ProductSortOption value)
{
    ExecuteFiltering();
}
```

### `[RelayCommand]`

Komutlar `[RelayCommand]` niteliði ile tanýmlanýr; XAML'de `XyzCommand` adýyla kullanýlýr:

```csharp
[RelayCommand]
private async Task LoginAsync() { ... }
```

```xaml
<Button Command="{Binding LoginCommand}" />
```

---

## Converter Katmaný

Tüm converter'lar `KamPay/Converters/` dizinindedir ve XAML `ResourceDictionary`'de kayýt edilir.

### Temel Converter'lar

| Converter | Giriþ | Çýkýþ | Açýklama |
|---|---|---|---|
| `BoolToColorConverter` | `bool` | `Color` | `"#FF5252\|#4CAF50"` parametresi ile iki renk arasý seçim |
| `InvertedBoolConverter` | `bool` | `bool` | Görünürlük tersine çevirmek için |
| `IsNotNullOrEmptyConverter` | `string?` | `bool` | Boþ string kontrolü |
| `EnumToBoolConverter` | `Enum` | `bool` | `ConverterParameter="System"` veya `"System,Invert=True"` ile enum karþýlaþtýrma |
| `EqualityToBoolConverter` | `object` | `bool` | Seçili kategori/tab vurgulama |

### Ürün Converter'larý (`ProductConverters.cs`)

| Converter | Kullaným Yeri |
|---|---|
| `ProductTypeToBadgeColorConverter` | Satýþ/Baðýþ/Takas rozeti rengi |
| `ProductTypeConverter` | Enum ? yerelleþtirilmiþ metin |
| `ProductConditionConverter` | Ürün durumu ? metin |
| `ProductCategoryToTextConverter` | Firebase'deki Türkçe kategori adý ? yerelleþtirilmiþ metin |
| `MessageBubbleColorConverter` | Gönderen/alýcý balonu rengi |
| `MessageBubbleAlignmentConverter` | Balon hizalama (`End`/`Start`) |
| `EnumToBoolConverter` (System mesajý) | Sistem mesajý görünürlük kontrolü |

### `BoolToColorConverter` Parametresi

```xaml
<Label TextColor="{Binding IsActive,
    Converter={StaticResource BoolToColorConverter},
    ConverterParameter='#4CAF50|#EF5350'}" />
```

### `EnumToBoolConverter` ile Sistem Mesajý Gösterimi

```xaml
<!-- Sistem mesajý görünür -->
<Border IsVisible="{Binding Type,
    Converter={StaticResource EnumToBoolConverter},
    ConverterParameter=System}" />

<!-- Normal mesaj görünür (System DEÐÝL ise) -->
<Border IsVisible="{Binding Type,
    Converter={StaticResource EnumToBoolConverter},
    ConverterParameter='System,Invert=True'}" />
```

### Müzakere (Negotiation) Converter'larý

`NegotiationStatusTextConverter` — `Transaction` nesnesini alýr, `Preferences`'dan mevcut kullanýcý ID'sini okur ve kullanýcýya göre farklý metin döndürür:

- **Satýcý görünümü**: "Alýcýnýn Teklifi: X?"
- **Alýcý görünümü**: "Sizin Teklifiniz: X? / Satýcýnýn Karþý Teklifi: Y?"

`CanAcceptNegotiationConverter` — Pazarlýk kabul butonunun aktifliðini belirler:

```csharp
// Sadece karþý teklif varsa kabul edilebilir
return transaction.CounterOfferBySeller.HasValue && transaction.CounterOfferBySeller.Value > 0;
```

### `MultiBinding` — `AllTrueConverter`

Birden fazla koþulun ayný anda doðru olmasýný gerektiren binding'ler için:

```xaml
<Button.IsVisible>
    <MultiBinding Converter="{StaticResource AllTrueConverter}">
        <Binding Path="IsNegotiating" />
        <Binding Path="IsSeller" />
    </MultiBinding>
</Button.IsVisible>
```

---

## Behavior ile CollectionView Optimizasyonu

`OptimizedCollectionViewBehavior` (`Behaviors/OptimizedCollectionViewBehavior.cs`) — Android'de `RecyclerView` performans ayarlarýný XAML'den yapýlandýrýlabilir hale getirir.

### Baðlanabilir Özellikler (BindableProperty)

```csharp
public static readonly BindableProperty ItemCacheSizeProperty = ...  // varsayýlan: 20
public static readonly BindableProperty PrefetchItemCountProperty = ... // varsayýlan: 4
```

### XAML Kullanýmý

**Ürün listesi** (`ProductListPage.xaml`):
```xaml
<CollectionView.Behaviors>
    <behaviors:OptimizedCollectionViewBehavior
        ItemCacheSize="20"
        PrefetchItemCount="4" />
</CollectionView.Behaviors>
```

**Sohbet mesaj listesi** (`ChatPage.xaml`):
```xaml
<CollectionView.Behaviors>
    <behaviors:OptimizedCollectionViewBehavior
        ItemCacheSize="30"
        PrefetchItemCount="6" />
</CollectionView.Behaviors>
```

> Sohbet sayfasýnda mesajlarýn sýk güncellendiði göz önüne alýnarak `ItemCacheSize` 30, `PrefetchItemCount` 6 olarak ayarlanmýþtýr.

### Uygulanan Android Optimizasyonlarý

- `SetItemViewCacheSize(ItemCacheSize)` — View geri dönüþüm havuzu geniþletildi.
- `NestedScrollingEnabled = false` — Yuvalanmýþ kaydýrma devre dýþý.
- `RecycledViewPool` — Havuz sýnýrý `ItemCacheSize + 10`.
- `InitialPrefetchItemCount` — `LinearLayoutManager` ön yükleme sayýsý.
- Animasyon süreleri 150ms'e düþürüldü (`AddDuration`, `RemoveDuration`, vb.).
- `OverScrollMode = Never` — Aþýrý kaydýrma efekti kapatýldý.

---

## Lokalizasyon Binding'i

`LocalizationResourceManager` bir singleton'dýr ve `INotifyPropertyChanged` uygular; dil deðiþtiðinde UI otomatik güncellenir.

### XAML Yöntemleri

**Doðrudan indexer binding** (dinamik, dil deðiþimine duyarlý):
```xaml
<Label Text="{Binding Source={x:Static res:LocalizationResourceManager.Instance}, Path=[NoMessageYet]}" />
```

**Markup Extension** (`extensions:Translate`):
```xaml
<Label Text="{extensions:Translate SearchProduct}" />
<Button Text="{extensions:Translate Apply}" />
```

**Sayfa baþlýðý**:
```xaml
Title="{Binding Source={x:Static res:LocalizationResourceManager.Instance}, Path=[Products]}"
```

### ViewModel Ýçinde

```csharp
private static LocalizationResourceManager Res => LocalizationResourceManager.Instance;

// Kullaným:
ErrorMessage = Res["LoginFailed"];
```

### Dil Deðiþimine Dinamik Tepki (`ProductListViewModel.cs`)

```csharp
LocalizationResourceManager.Instance.PropertyChanged += (sender, e) =>
{
    OnPropertyChanged(nameof(SortOptionStrings)); // Picker metinleri güncellenir
    OnPropertyChanged(nameof(SelectedSortIndex));
    ExecuteFiltering();
};
```

---

## Realtime Veri Binding'i

### Firebase Realtime Listener ? ObservableCollection

`ProductListViewModel` Firebase `AsObservable<T>()` ile anlýk veri deðiþimlerini dinler ve `ObservableCollection<Product> Products`'a yansýtýr:

```csharp
_listener = _loader.Listen(Constants.ProductsCollection, evt =>
{
    MainThread.BeginInvokeOnMainThread(() => ApplyRealtimeEvent(evt));
});
```

`ApplyRealtimeEvent` — `InsertOrUpdate` ve `Delete` olaylarýný iþler; `ExecuteFiltering()` ile UI'yi yeniler.

### `UpdateProductsCollection` — Minimal UI Güncellemesi

Tüm listeyi silip yeniden eklemek yerine fark algoritmasý kullanýlýr:

```csharp
// 1. Artýk mevcut olmayanlarý kaldýr
var toRemove = Products.Where(p => !newProducts.Any(np => np.ProductId == p.ProductId)).ToList();
foreach (var item in toRemove) Products.Remove(item);

// 2. Yeni gelenleri doðru konuma ekle veya mevcut olanlarý güncelle
for (int i = 0; i < newProducts.Count; i++) { ... }
```

> Bu yaklaþým `CollectionView` scroll pozisyonunu ve animasyonlarý korur.

### Sohbet Mesajlarý — Buffer + Batch

```csharp
_firebaseClient
    .Child(Constants.MessagesCollection)
    .Child(ConversationId)
    .AsObservable<Message>()
    .Buffer(TimeSpan.FromMilliseconds(200)) // 200ms batching
    .Where(batch => batch.Any())
    .Subscribe(events => MainThread.BeginInvokeOnMainThread(() => ProcessMessageBatch(events)));
```

`InsertMessageSorted` — Binary search ile kronolojik sýra korunur; geçici (temp) mesajlar gerçek mesajla deðiþtirilir.

---

## Mesaj Ekraný Binding Detaylarý

### `ChatPage.xaml` — Dinamik Balon Stili

Gönderen/alýcý ayrýmý `DataTrigger` ile yapýlýr (converter kullanýlmaz; basitlik için):

```xaml
<Border.Style>
    <Style TargetType="Border">
        <Setter Property="HorizontalOptions" Value="Start" />
        <Setter Property="BackgroundColor" Value="#FFFFFF" />
        <Style.Triggers>
            <DataTrigger TargetType="Border" Binding="{Binding IsSentByMe}" Value="True">
                <Setter Property="HorizontalOptions" Value="End" />
                <Setter Property="BackgroundColor" Value="#1E88E5" />
            </DataTrigger>
        </Style.Triggers>
    </Style>
</Border.Style>
```

### Teslim Durumu Ýkonu

```xaml
<Label IsVisible="{Binding IsSentByMe}">
    <Label.Triggers>
        <DataTrigger Binding="{Binding IsDelivered}" Value="False">
            <Setter Property="Text" Value="?" />
        </DataTrigger>
        <DataTrigger Binding="{Binding IsDelivered}" Value="True">
            <Setter Property="Text" Value="??" />
        </DataTrigger>
    </Label.Triggers>
</Label>
```

### Profil Fotoðrafý Fallback Zinciri

`ChatViewModel.LoadChatAsync()` þu sýrayla profil fotoðrafý çözümler:

1. Navigation parametresinden URL-decode edilmiþ fotoðraf.
2. `Conversation.GetOtherUserPhotoUrl()` (konuþma nesnesinden).
3. Firebase `Users` koleksiyonundan `ProfileImageUrl`.
4. `IUserProfileService.GetUserProfileAsync()` (son çare).

```csharp
private async Task EnsureOtherUserPhotoAsync()
{
    if (!string.IsNullOrEmpty(OtherUserPhoto) && OtherUserPhoto != "person_icon.svg")
        return;
    // ... IUserProfileService ile yükle
}
```

### Kullanýcý Profili Deðiþimi — Anlýk Güncelleme

`IUserStateService.UserProfileChanged` olayý dinlenir; mesajlardaki gönderen/alýcý bilgileri anýnda güncellenir:

```csharp
private void OnUserProfileChanged(object? sender, User updatedUser)
{
    MainThread.BeginInvokeOnMainThread(() =>
    {
        foreach (var message in Messages.Where(m => m.SenderId == updatedUser.UserId))
        {
            message.SenderName = updatedUser.FullName;
        }
    });
}
```

---

## Ürün Listesi Binding Detaylarý

### Picker ? Enum Köprüsü

`Picker` yalnýzca string listesiyle çalýþýr; ViewModel iki property ile köprü kurar:

```csharp
// Picker'ýn gösterdiði metin listesi
public List<string> SortOptionStrings => _sortOptionEnums.Select(GetSortOptionText).ToList();

// Picker'ýn seçili indeksi
public int SelectedSortIndex
{
    get => _selectedSortIndex;
    set
    {
        if (SetProperty(ref _selectedSortIndex, value) && value >= 0)
            SelectedSortOption = _sortOptionEnums[value]; // Enum'a çevir
    }
}
```

```xaml
<Picker ItemsSource="{Binding SortOptionStrings}"
        SelectedIndex="{Binding SelectedSortIndex}" />
```

### Kategori Seçimi — `RelativeSource` ile AncestorType

`CollectionView` içindeki `DataTemplate`'den ViewModel komutuna eriþmek için `RelativeSource`:

```xaml
<TapGestureRecognizer
    Command="{Binding Source={RelativeSource AncestorType={x:Type vm:ProductListViewModel}},
              Path=CategoryTappedCommand}"
    CommandParameter="{Binding .}" />
```

### Sýralama Filtresi Deðiþim Zinciri

```
Picker.SelectedIndex (kullanýcý etkileþimi)
    ? SelectedSortIndex setter
        ? SelectedSortOption = _sortOptionEnums[value]
            ? partial void OnSelectedSortOptionChanged()
                ? ExecuteFiltering()
                    ? UpdateProductsCollection()
                        ? CollectionView güncellenir
```

### Arama Debounce

`Entry.Text` ? `SearchText` [ObservableProperty] ? `OnSearchTextChanged` 500ms debounce ile sunucu aramasý:

```csharp
async partial void OnSearchTextChanged(string? value)
{
    _searchCancellationTokenSource?.Cancel();
    _searchCancellationTokenSource = new CancellationTokenSource();
    await Task.Delay(500, _searchCancellationTokenSource.Token);
    await ReloadWithFilterAsync();
}
```

### Favori/Görüntülenme Sayýsý Anlýk Güncelleme

`WeakReferenceMessenger` ile ürün kartlarýndaki sayaçlar güncellenirken ScrollView pozisyonu korunur:

```csharp
WeakReferenceMessenger.Default.Register<FavoriteCountChangedMessage>(this, (r, m) =>
{
    MainThread.BeginInvokeOnMainThread(() =>
    {
        var product = Products.FirstOrDefault(p => p.ProductId == m.Value.ProductId);
        if (product != null)
        {
            product.FavoriteCount = m.Value.FavoriteCount;
            product.ViewCount = m.Value.ViewCount;
        }
    });
});
```

---

## Güvenlik & Oturum Binding'i

### `IsUserLoggedIn()` — Senkron Kontrol

`LoginViewModel` ve shell routing için senkron oturum kontrolü; `SecureStorage.GetAsync().Result` ile (UI thread dýþýnda çaðrýlmalýdýr):

```csharp
public bool IsUserLoggedIn()
{
    if (_currentUser != null) return true;
    try
    {
        var userId = SecureStorage.GetAsync(KEY_USER_ID).Result;
        return !string.IsNullOrEmpty(userId);
    }
    catch { return false; }
}
```

### `UserSessionChangedMessage` ile Shell Yönlendirme

Oturum açma/kapama sonrasý UI akýþýný `WeakReferenceMessenger` ile tetikler:

```csharp
// FirebaseAuthService.cs — Login baþarýlý
WeakReferenceMessenger.Default.Send(new UserSessionChangedMessage(true));

// FirebaseAuthService.cs — Logout
WeakReferenceMessenger.Default.Send(new UserSessionChangedMessage(false));
```

`AppShell` veya `App.xaml.cs` bu mesajý alarak `MainPage`'i deðiþtirir.

### `LoginViewModel` — Cache Temizleme

Yeni kullanýcý giriþinde önceki kullanýcýya ait cache verilerinin görünmesini engellemek için:

```csharp
if (result.Success)
{
    ChatViewModel.ClearCache();
    var productCacheService = Application.Current?.Handler?.MauiContext?.Services.GetService<IProductCacheService>();
    await productCacheService?.InvalidateCacheAsync();
    await Shell.Current.GoToAsync("//MainApp");
}
```

---

## Bilinen Kýsýtlamalar ve Geçici Çözümler

### 1. `DataTrigger` ile `null` ConverterParameter

`DataTrigger` içinde `{x:Null}` parametre geçmek XAML'de desteklenir:

```xaml
<DataTrigger TargetType="Button" Binding="{Binding SelectedType}" Value="{x:Null}">
    <Setter Property="BackgroundColor" Value="{StaticResource Primary}" />
</DataTrigger>
```

### 2. `NegotiationStatusTextConverter` — `Preferences` Baðýmlýlýðý

Bu converter `IValueConverter` olup DI almaz; mevcut kullanýcý ID'sine `Preferences.Get("current_user_id", ...)` ile eriþir. Kullanýcý deðiþtiðinde `Preferences` anahtarýnýn güncel tutulmasý gerekir.

### 3. `CanAcceptNegotiationConverter` — Takas için `>= 0` Kontrolü

Takas iþleminde ek ücret sýfýr da geçerlidir; bu nedenle `CounterCashByOwner >= 0` kontrolü kullanýlýr (satýþ iþlemindeki `> 0` ile farklýdýr).

### 4. `CollectionView` Ýçinden Komut Binding

`DataTemplate` içindeki kontroller, `RelativeSource AncestorType` olmadan ViewModel komutlarýna eriþemez. Tüm sýnýr aþan komutlar bu þablonla tanýmlanmýþtýr:

```xaml
Command="{Binding Source={RelativeSource AncestorType={x:Type vm:ProductListViewModel}},
          Path=ProductTappedCommand}"
```

### 5. `OptimizedCollectionViewBehavior` — Yalnýzca Android

iOS ve diðer platformlarda behavior `else` dalýna düþer ve optimizasyon uygulanmaz. Platform özelinde ek optimizasyon gerekiyorsa `#if IOS` bloðu eklenmelidir.

---

*Son güncelleme: Proje devop branch'i — KamPay v3*
