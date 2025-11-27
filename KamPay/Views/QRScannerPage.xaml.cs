using CommunityToolkit.Mvvm.Messaging;
using KamPay.Models.Messages;
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
    }

    // HATA DÜZELTMESÝ: Metodun adý XAML ile eþleþmesi için "BarcodesDetected" olarak deðiþtirildi.
    private void BarcodesDetected(object sender, BarcodeDetectionEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            // Tekrar tekrar taramayý önlemek için kamerayý durdur
            barcodeReader.IsDetecting = false;

            if (e.Results.Any())
            {
                string qrCodeData = e.Results[0].Value;

                // Taranan QR kod verisini içeren bir mesaj GÖNDER
                WeakReferenceMessenger.Default.Send(new QRCodeScannedMessage(qrCodeData));
            }

            // Bir önceki sayfaya geri dön
            await Shell.Current.GoToAsync("..");
        });
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Sayfa açýldýðýnda taramayý baþlat
        barcodeReader.IsDetecting = true;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Sayfa kapandýðýnda taramayý durdur
        barcodeReader.IsDetecting = false;
    }
}