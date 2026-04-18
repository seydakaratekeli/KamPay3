using KamPay.ViewModels;

namespace KamPay.Views
{
    public partial class MainPage : ContentPage
    {
        private bool _hasAnimated = false;
        private readonly MainViewModel? _viewModel;

        public MainPage()
        {
            InitializeComponent();
            _viewModel = App.Current?.Handler?.MauiContext?.Services?.GetService<MainViewModel>();
            BindingContext = _viewModel;
        }

        public MainPage(MainViewModel vm)
        {
            InitializeComponent();
            _viewModel = vm;
            BindingContext = vm;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

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
            QuickActionsGrid.Opacity = 0;
            QuickActionsGrid.TranslationY = 40;

            // Background animation
            AnimateBackgroundCircles();

            // Header animation
            await Task.WhenAll(
                HeaderSection.FadeTo(1, 700, Easing.CubicOut),
                HeaderSection.TranslateTo(0, 0, 700, Easing.CubicOut)
            );

            await Task.Delay(200);

            // Grid animation
            await Task.WhenAll(
                QuickActionsGrid.FadeTo(1, 600, Easing.CubicOut),
                QuickActionsGrid.TranslateTo(0, 0, 600, Easing.CubicOut)
            );
        }

        private void AnimateBackgroundCircles()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            await Task.WhenAll(
                                Circle1.RotateTo(360, 30000, Easing.Linear),
                                Circle2.RotateTo(-360, 35000, Easing.Linear)
                            );

                            Circle1.Rotation = 0;
                            Circle2.Rotation = 0;
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
