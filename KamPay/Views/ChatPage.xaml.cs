using KamPay.ViewModels;
using KamPay.Models;
using CommunityToolkit.Mvvm.Messaging;

namespace KamPay.Views
{
    public partial class ChatPage : ContentPage
    {
        private readonly ChatViewModel _viewModel;
        private bool _hasAnimated = false;

        public ChatPage(ChatViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;

            //  Yeni mesaj geldiğinde scroll mesajını dinle
            WeakReferenceMessenger.Default.Register<ScrollToChatMessage>(this, (r, message) =>
            {
                ScrollToLastMessage();
            });
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Animasyonları çalıştır
            if (!_hasAnimated)
            {
                _hasAnimated = true;
                await Task.Delay(100);
                await AnimatePageAsync();
            }

            // Sayfa göründüğünde son mesaja kaydır (biraz gecikmeyle)
            _ = Task.Run(async () =>
            {
                await Task.Delay(500);
                ScrollToLastMessage();
            });
        }

        private async Task AnimatePageAsync()
        {
            // Reset states
            HeaderSection.Opacity = 0;
            HeaderSection.TranslationY = -20;
            MessageInputSection.Opacity = 0;
            MessageInputSection.TranslationY = 20;

            // Background animation
            AnimateBackgroundCircle();

            // Header animation
            await Task.WhenAll(
                HeaderSection.FadeTo(1, 500, Easing.CubicOut),
                HeaderSection.TranslateTo(0, 0, 500, Easing.CubicOut)
            );

            await Task.Delay(100);

            // Message input animation
            await Task.WhenAll(
                MessageInputSection.FadeTo(1, 500, Easing.CubicOut),
                MessageInputSection.TranslateTo(0, 0, 500, Easing.CubicOut)
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
                            await Circle1.RotateTo(360, 25000, Easing.Linear);
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

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            // Messenger'ı temizle
            WeakReferenceMessenger.Default.Unregister<ScrollToChatMessage>(this);

            // ViewModel'i dispose et
            (_viewModel as IDisposable)?.Dispose();
        }

        //  Son mesaja otomatik kaydırma
        private void ScrollToLastMessage()
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    // Biraz bekle, mesajların yüklenmesi için
                    await Task.Delay(100);

                    if (_viewModel.Messages.Count > 0)
                    {
                        var lastMessage = _viewModel.Messages.Last();
                        MessagesCollectionView.ScrollTo(lastMessage, position: ScrollToPosition.End, animate: true);
                    }
                }
                catch (Exception ex)
                {
                    KamPay.Helpers.AppLogger.DebugLog($"Scroll hatası: {ex.Message}");
                }
            });
        }
    }
}
