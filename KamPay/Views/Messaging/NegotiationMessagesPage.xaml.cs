using KamPay.ViewModels;

namespace KamPay.Views
{
    public partial class NegotiationMessagesPage : ContentPage
    {
        private readonly MessagesViewModel _viewModel;
        private bool _hasAnimated = false;

        public NegotiationMessagesPage(MessagesViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        //  Sayfa her göründüğünde çağrılır
        protected override async void OnAppearing()
        {
            base.OnAppearing();

            if (!_hasAnimated)
            {
                _hasAnimated = true;
                // MAUI Tab geçişinin tamamlanması ve uygulamanın donmaması için UI'a 350ms nefes aldır
                await Task.Delay(350);

                // Önce ekranı akıcı şekilde çiz
                await AnimatePageAsync();

                // Sonra arkaplanda veriyi yüklemeye başla (sadece ilk kez)
                _ = _viewModel.InitializeAsync();
            }
            // ✅ Singleton sayfa: sonraki tab geçişlerinde InitializeAsync çağırma
            // Veri zaten yüklü, listener zaten aktif
        }

        private async Task AnimatePageAsync()
        {
            // Reset states
            HeaderSection.Opacity = 0;
            HeaderSection.TranslationY = -30;
            SearchSection.Opacity = 0;
            SearchSection.TranslationY = 30;

            // Background animation
            AnimateBackgroundCircle();

            // Header animation
            await Task.WhenAll(
                HeaderSection.FadeTo(1, 600, Easing.CubicOut),
                HeaderSection.TranslateTo(0, 0, 600, Easing.CubicOut)
            );

            await Task.Delay(150);

            // Search section animation
            await Task.WhenAll(
                SearchSection.FadeTo(1, 700, Easing.CubicOut),
                SearchSection.TranslateTo(0, 0, 700, Easing.CubicOut)
            );
        }

        private void AnimateBackgroundCircle()
        {
            // ✅ DÜZELTME: while(true) döngüsü yerine güvenli Animation.Commit API'si
            try { Circle1?.AbortAnimation("CircleRotation"); } catch { }
            var animation = new Animation(v => Circle1.Rotation = v, 0, 360);
            animation.Commit(
                owner: Circle1,
                name: "CircleRotation",
                length: 30000,
                easing: Easing.Linear,
                repeat: () => true
            );
        }

        //  Sayfa kaybolduğunda listener'ları temizle
        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            // Dispose otomatik çağrılır, ekstra birşey yapma
        }
    }
}

