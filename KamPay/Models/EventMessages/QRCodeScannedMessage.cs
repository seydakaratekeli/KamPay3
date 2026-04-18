using CommunityToolkit.Mvvm.Messaging.Messages;

namespace KamPay.Models.EventMessages
{
    // Bu sınıf, QR kod tarandığında gönderilecek mesajı temsil eder.
    // İçinde taranan QR kodun metnini (string) taşır.
    //qrın içine zaman felan bir şeyler ekle
    public class QRCodeScannedMessage : ValueChangedMessage<string>
    {
        public QRCodeScannedMessage(string value) : base(value)
        {
        }
    }
}
