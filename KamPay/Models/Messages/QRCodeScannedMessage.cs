using CommunityToolkit.Mvvm.Messaging.Messages;

namespace KamPay.Models.Messages
{
    // Bu sýnýf, QR kod tarandýðýnda gönderilecek mesajý temsil eder.
    // Ýçinde taranan QR kodun metnini (string) taþýr.
    //qrýn içine zaman felan bir þeyler ekle
    public class QRCodeScannedMessage : ValueChangedMessage<string>
    {
        public QRCodeScannedMessage(string value) : base(value)
        {
        }
    }
}