using CommunityToolkit.Mvvm.Messaging.Messages;

namespace KamPay.Models.Messages
{
    /// <summary>
    /// Message sent when the map location needs to be updated
    /// </summary>
    public class MapLocationUpdateMessage : ValueChangedMessage<(double Latitude, double Longitude)>
    {
        public double Latitude { get; }
        public double Longitude { get; }

        public MapLocationUpdateMessage(double latitude, double longitude) 
            : base((latitude, longitude))
        {
            Latitude = latitude;
            Longitude = longitude;
        }
    }
}
