using KamPay.ViewModels;

namespace KamPay.Views
{
    public partial class ImageViewerPage : ContentPage
    {
        public ImageViewerPage(ImageViewerViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}
