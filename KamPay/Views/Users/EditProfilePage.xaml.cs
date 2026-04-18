using KamPay.ViewModels;

namespace KamPay.Views;

public partial class EditProfilePage : ContentPage
{
    public EditProfilePage(EditProfileViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Sayfa açýldýðýnda içeriklerin animasyonlu gelmesi
        var content = this.Content;
        content.Opacity = 0;
        content.TranslationY = 30;

        await Task.WhenAll(
            content.FadeTo(1, 400, Easing.CubicOut),
            content.TranslateTo(0, 0, 400, Easing.CubicOut)
        );
    }
}