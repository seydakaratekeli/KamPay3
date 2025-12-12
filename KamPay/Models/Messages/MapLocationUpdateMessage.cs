namespace KamPay.Models.Messages
{
    
    // Harita konumunun güncellenmesi gerektiðinde gönderilen mesaj.
    
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
