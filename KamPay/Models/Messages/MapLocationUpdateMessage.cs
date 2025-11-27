namespace KamPay.Models.Messages
{
    /// <summary>
    /// Message sent when the map location needs to be updated
    /// </summary>
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
