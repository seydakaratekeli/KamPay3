# Language Switching Test Plan / Dil Değiştirme Test Planı

## Test Senaryoları / Test Scenarios

### Test 1: Başlangıç Dili / Initial Language
**Adımlar / Steps:**
1. Uygulamayı ilk kez çalıştır / Launch app for the first time
2. Varsayılan dili (Türkçe) kontrol et / Check default language (Turkish)

**Beklenen Sonuç / Expected Result:**
- AppShell sekmeleri: "Ana Sayfa", "Hizmetler", "İyilik Panosu", "Mesajlar", "Profil"
- Tüm içerik Türkçe olmalı / All content should be in Turkish

### Test 2: Türkçe'den İngilizce'ye Geçiş / Switch from Turkish to English
**Adımlar / Steps:**
1. Uygulamayı Türkçe dilinde aç / Open app in Turkish
2. "Profil" sekmesine git / Navigate to Profile tab
3. "Dil Değiştir" butonuna tıkla / Click "Change Language" button
4. "English" seç / Select "English"
5. Onay mesajını kapat / Close confirmation message

**Beklenen Sonuç / Expected Result:**
- ✅ AppShell sekmeleri ANINDA değişmeli: "Home", "Services", "Good Deed Board", "Messages", "Profile"
- ✅ Profil sayfası içeriği İngilizce olmalı
- ✅ Diğer sekmelere geçince İngilizce içerik görülmeli
- ✅ Button'lar, label'lar, placeholder'lar İngilizce olmalı

### Test 3: İngilizce'den Türkçe'ye Geçiş / Switch from English to Turkish
**Adımlar / Steps:**
1. Uygulamayı İngilizce dilinde aç / Open app in English
2. "Profile" sekmesine git / Navigate to Profile tab
3. "Change Language" butonuna tıkla / Click "Change Language" button
4. "Türkçe" seç / Select "Türkçe"
5. Onay mesajını kapat / Close confirmation message

**Beklenen Sonuç / Expected Result:**
- ✅ AppShell sekmeleri ANINDA değişmeli: "Ana Sayfa", "Hizmetler", "İyilik Panosu", "Mesajlar", "Profil"
- ✅ Profil sayfası içeriği Türkçe olmalı
- ✅ Diğer sekmelere geçince Türkçe içerik görülmeli
- ✅ Button'lar, label'lar, placeholder'lar Türkçe olmalı

### Test 4: Dil Tercihinin Saklanması / Language Preference Persistence
**Adımlar / Steps:**
1. Dili İngilizce olarak ayarla / Set language to English
2. Uygulamayı kapat / Close the app
3. Uygulamayı yeniden aç / Reopen the app

**Beklenen Sonuç / Expected Result:**
- ✅ Uygulama İngilizce dilinde açılmalı / App should open in English
- ✅ Tüm sekme başlıkları ve içerik İngilizce olmalı / All tab titles and content should be in English

### Test 5: Tüm Sayfalarda Dil Değişimi / Language Change on All Pages
**Adımlar / Steps:**
1. Dili değiştir / Change language
2. Her sekmeyi ziyaret et: / Visit each tab:
   - Ana Sayfa / Home (ProductListPage)
   - Hizmetler / Services (ServiceSharingPage)
   - İyilik Panosu / Good Deed Board (GoodDeedBoardPage)
   - Mesajlar / Messages (MessagesPage)
   - Profil / Profile (ProfilePage)

**Beklenen Sonuç / Expected Result:**
- ✅ Her sayfanın başlığı seçilen dilde olmalı / Each page title should be in selected language
- ✅ Her sayfanın içeriği seçilen dilde olmalı / Each page content should be in selected language
- ✅ Button'lar ve label'lar seçilen dilde olmalı / Buttons and labels should be in selected language

## Teknik Doğrulama / Technical Verification

### Kontrol Edilecek Dosyalar / Files to Check:
1. **AppShellViewModel.cs**: 
   - ✅ HomeTitle, ServicesTitle, etc. property'leri var
   - ✅ LanguageChangedMessage listener var
   - ✅ UpdateTabTitles() metodu var

2. **AppShell.xaml**:
   - ✅ Tab Title binding'leri ViewModel property'lerine bağlı
   - ✅ x:DataType="vm:AppShellViewModel" mevcut

3. **LocalizationResourceManager.cs**:
   - ✅ PropertyChanged(null) çağrılıyor
   - ✅ LanguageChangedMessage gönderiliyor
   - ✅ SetCulture metodu doğru çalışıyor

## Bilinen Sınırlamalar / Known Limitations

1. **Converter Değerleri / Converter Values:**
   - DateTimeToTimeAgoConverter ve ProductConverters gibi converter'lar binding re-evaluation'da güncellenir
   - Çoğu durumda veri değiştiğinde veya sayfa yenilendiğinde güncellenir
   - Bu normal bir davranıştır ve sorun oluşturmaz

2. **Sayfa Geçişleri / Page Transitions:**
   - Bazı eski sayfalarda cache'lenmiş veriler olabilir
   - Yeni sayfalara geçiş yapıldığında her zaman güncel dil kullanılır

## Başarı Kriterleri / Success Criteria

✅ AppShell sekme başlıkları dil değiştiğinde ANINDA güncellenir
✅ Türkçe seçildiğinde tüm arayüz Türkçe olur
✅ İngilizce seçildiğinde tüm arayüz İngilizce olur
✅ Dil tercihi kalıcıdır (uygulama kapatılıp açıldığında korunur)
✅ Mevcut işlevsellik bozulmaz
✅ Minimal kod değişikliği yapılmıştır
