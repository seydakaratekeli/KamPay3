using CommunityToolkit.Mvvm.Messaging;
using KamPay.Models.EventMessages;
using ZXing.Net.Maui;

namespace KamPay.Views;

public partial class QRScannerPage : ContentPage
{
    public QRScannerPage()
    {
        InitializeComponent();
        barcodeReader.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormats.All,
            AutoRotate = true,
            Multiple = false
        };

        // Animasyonları başlat
        StartScanAnimations();
    }

    // HATA DÜZELTMESİ: Metodun adı XAML ile eşleşmesi için "BarcodesDetected" olarak değiştirildi.
    private void BarcodesDetected(object sender, BarcodeDetectionEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            // Tekrar tekrar taramayı önlemek için kamerayı durdur
            barcodeReader.IsDetecting = false;

            if (e.Results.Any())
            {
                string qrCodeData = e.Results[0].Value;

                // Başarı animasyonu
                await ShowSuccessAnimation();

                // Taranan QR kod verisini içeren bir mesaj GÖNDER
                WeakReferenceMessenger.Default.Send(new QRCodeScannedMessage(qrCodeData));
            }

            // Bir önceki sayfaya geri dön
            await Shell.Current.GoToAsync("..");
        });
    }

    private async void StartScanAnimations()
    {
        // Scan frame pulse animasyonu
        var pulseAnimation = new Animation(v => ScanFrame.Scale = v, 1, 1.05);
        
        // Scan line yukarı aşağı animasyonu
        var scanLineAnimation = new Animation(v => ScanLine.TranslationY = v, -120, 120);

        // Animasyonları başlat
        while (true)
        {
            pulseAnimation.Commit(this, "Pulse", 16, 1500, Easing.CubicInOut, (v, c) => { }, () => true);
            await Task.Delay(1500);
            
            if (!IsLoaded) break;
        }
    }

    private async Task ShowSuccessAnimation()
    {
        // Frame'i yeşile çevir ve titret
        await ScanFrame.ScaleTo(1.1, 100);
        ScanFrame.Stroke = Color.FromArgb("#4CAF50");
        await ScanFrame.ScaleTo(1.0, 100);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Sayfa açıldığında taramayı başlat
        barcodeReader.IsDetecting = true;
        
        // Scan line animasyonunu başlat
        ScanLine.TranslateTo(0, -120, 0);
        AnimateScanLine();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Sayfa kapandığında taramayı durdur
        barcodeReader.IsDetecting = false;
    }

    private async void AnimateScanLine()
    {
        while (barcodeReader.IsDetecting)
        {
            await ScanLine.TranslateTo(0, 120, 1500, Easing.Linear);
            await ScanLine.TranslateTo(0, -120, 1500, Easing.Linear);
        }
    }
}
