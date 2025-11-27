using KamPay.ViewModels;

namespace KamPay.Views
{
    public partial class NotificationsPage : ContentPage
    {
        public NotificationsPage(NotificationsViewModel vm)
        {
            InitializeComponent();
            BindingContext = vm;
        }
    }
}





/*
 * NewMessage (Yeni Mesaj):

Ne zaman oluþur? Baþka bir kullanýcý sana bir ürün hakkýnda veya doðrudan mesaj gönderdiðinde.

Örnek: "Ali Veli, 'Ders Kitabý' ürünün hakkýnda bir mesaj gönderdi."

ProductSold (Ürün Satýldý):

Ne zaman oluþur? Bir alýcý ile anlaþýp QR kod ile teslimatý tamamladýðýnda veya ürünü "Satýldý" olarak iþaretlediðinde.

Örnek: "'Eski Hesap Makinesi' adlý ürünün satýldý olarak iþaretlendi."

NewFavorite (Yeni Favori):

Ne zaman oluþur? Baþka bir kullanýcý, senin listelediðin bir ürünü favorilerine eklediðinde.

Örnek: "Ayþe Yýlmaz, 'Kamp Sandalyesi' ürününü favorilerine ekledi."

BadgeEarned (Rozet Kazanýldý):

Ne zaman oluþur? Belirli bir baþarýya ulaþtýðýnda (örneðin 5. ürününü listelediðinde veya 100 puana ulaþtýðýnda). FirebaseUserProfileService içinde bu mantýðý zaten kurmuþuz.

Örnek: "Tebrikler! 'Paylaþým Kahramaný' rozetini kazandýn."

PointsEarned (Puan Kazanýldý):

Ne zaman oluþur? Puan kazandýracak bir eylem yaptýðýnda (ürün ekleme, baðýþ yapma vb.). Bu mantýk da FirebaseUserProfileService içinde mevcut.

Örnek: "Yeni bir ürün eklediðin için +5 puan kazandýn!"

DonationMade (Baðýþ Yapýldý):

Ne zaman oluþur? Bir ürününü baðýþladýðýnda veya "Sürpriz Kutu"ya eklediðinde.

Örnek: "'Okunmuþ Romanlar' baðýþýn ihtiyaç sahibine ulaþtý."

SystemNotice (Sistem Bildirimi):

Ne zaman oluþur? Uygulama genelinde bir duyuru yapýldýðýnda veya hesabýnla ilgili önemli bir güncelleme olduðunda.

Örnek: "Uygulamamýzdaki yeni 'Zaman Bankasý' özelliðini keþfet!" bu mekanýzmalarý da ekle
 * */