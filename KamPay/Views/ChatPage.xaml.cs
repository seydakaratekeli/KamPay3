using KamPay.ViewModels;
using KamPay.Models;
using CommunityToolkit.Mvvm.Messaging;

namespace KamPay.Views
{
    public partial class ChatPage : ContentPage
    {
        private readonly ChatViewModel _viewModel;

        public ChatPage(ChatViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;

            // 🔥 Yeni mesaj geldiğinde scroll mesajını dinle
            WeakReferenceMessenger.Default.Register<ScrollToChatMessage>(this, (r, message) =>
            {
                ScrollToLastMessage();
            });
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            // Sayfa göründüğünde son mesaja kaydır (biraz gecikmeyle)
            _ = Task.Run(async () =>
            {
                await Task.Delay(500);
                ScrollToLastMessage();
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

        // 🔥 Son mesaja otomatik kaydırma
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
                    Console.WriteLine($"Scroll hatası: {ex.Message}");
                }
            });
        }
    }
}