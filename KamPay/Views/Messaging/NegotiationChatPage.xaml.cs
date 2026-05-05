using CommunityToolkit.Mvvm.Messaging;
using KamPay.Helpers;
using KamPay.Models;
using KamPay.ViewModels;

namespace KamPay.Views
{
    public partial class NegotiationChatPage : ContentPage
    {
        private readonly NegotiationChatViewModel _viewModel;
        private bool _hasAnimated;

        public NegotiationChatPage(NegotiationChatViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
            RegisterScrollMessenger();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            RegisterScrollMessenger();
            _viewModel.ResumeRealtimeListeners();

            if (!_hasAnimated)
            {
                _hasAnimated = true;
                await Task.Delay(100);
                await AnimatePageAsync();
            }

            _ = Task.Run(async () =>
            {
                await Task.Delay(500);
                ScrollToLastMessage();
            });
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            WeakReferenceMessenger.Default.Unregister<ScrollToChatMessage>(this);
            _viewModel.PauseRealtimeListeners();
        }

        private async Task AnimatePageAsync()
        {
            HeaderSection.Opacity = 0;
            HeaderSection.TranslationY = -20;
            MessageInputSection.Opacity = 0;
            MessageInputSection.TranslationY = 20;
            AnimateBackgroundCircle();

            await Task.WhenAll(
                HeaderSection.FadeTo(1, 500, Easing.CubicOut),
                HeaderSection.TranslateTo(0, 0, 500, Easing.CubicOut));

            await Task.Delay(100);

            await Task.WhenAll(
                MessageInputSection.FadeTo(1, 500, Easing.CubicOut),
                MessageInputSection.TranslateTo(0, 0, 500, Easing.CubicOut));
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

        private void RegisterScrollMessenger()
        {
            WeakReferenceMessenger.Default.Unregister<ScrollToChatMessage>(this);
            WeakReferenceMessenger.Default.Register<ScrollToChatMessage>(this, (_, _) => ScrollToLastMessage());
        }

        private void ScrollToLastMessage()
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    await Task.Delay(100);
                    var count = _viewModel.FilteredMessages.Cast<Message>().Count();
                    if (count > 0)
                    {
                        MessagesListView.ItemsLayout.ScrollToRowIndex(
                            count - 1,
                            Microsoft.Maui.Controls.ScrollToPosition.End,
                            true);
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.DebugLog($"Negotiation scroll hatasi: {ex.Message}");
                }
            });
        }
    }
}
