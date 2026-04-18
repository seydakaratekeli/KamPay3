namespace KamPay.Models.EventMessages
{
    
    // Harita konumunun güncellenmesi gerektiğinde gönderilen mesaj.
    
    public class MapLocationUpdateMessage
    {
        public double Latitude { get; }
        public double Longitude { get; }

        public MapLocationUpdateMessage(double latitude, double longitude)
        {
            Latitude = latitude;
            Longitude = longitude;
        }
    }
}
