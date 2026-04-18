using KamPay.ViewModels;

namespace KamPay.Views
{
    public partial class NotificationsPage : ContentPage
    {
        private readonly NotificationsViewModel _viewModel;
        private bool _hasAnimated = false;

        public NotificationsPage(NotificationsViewModel vm)
        {
            InitializeComponent();
            _viewModel = vm;
            BindingContext = _viewModel;
        }

        //  Sayfa göründüðünde verileri yükle
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (_viewModel != null)
            {
                await _viewModel.InitializeAsync();
            }

            // Animasyonlarý çalýþtýr
            if (!_hasAnimated)
            {
                _hasAnimated = true;
                await Task.Delay(100);
                await AnimatePageAsync();
            }
        }

        private async Task AnimatePageAsync()
        {
            // Reset states
            HeaderSection.Opacity = 0;
            HeaderSection.TranslationY = -30;
            ContentSection.Opacity = 0;
            ContentSection.TranslationY = 40;

            // Background animation
            AnimateBackgroundCircle();

            // Header animation
            await Task.WhenAll(
                HeaderSection.FadeTo(1, 600, Easing.CubicOut),
                HeaderSection.TranslateTo(0, 0, 600, Easing.CubicOut)
            );

            await Task.Delay(150);

            // Content animation
            await Task.WhenAll(
                ContentSection.FadeTo(1, 700, Easing.CubicOut),
                ContentSection.TranslateTo(0, 0, 700, Easing.CubicOut)
            );
        }

        private void AnimateBackgroundCircle()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            await Circle1.RotateTo(360, 30000, Easing.Linear);
                            Circle1.Rotation = 0;
                        });
                    }
                    catch
                    {
                        break;
                    }
                }
            });
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