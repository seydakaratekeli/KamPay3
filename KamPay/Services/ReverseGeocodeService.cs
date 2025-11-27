using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// KamPay/Services/ReverseGeocodeService.cs
namespace KamPay.Services
{
    public class ReverseGeocodeService : IReverseGeocodeService
    {
        public async Task<string> GetAddressForLocation(Location location)
        {
            if (location == null)
            {
                return "Geçerli bir konum belirtilmedi.";
            }

            try
            {
                var placemarks = await Geocoding.Default.GetPlacemarksAsync(location.Latitude, location.Longitude);
                var placemark = placemarks?.FirstOrDefault();

                if (placemark == null)
                {
                    return $"Enlem: {location.Latitude:F4}, Boylam: {location.Longitude:F4}";
                }

                // --- BİNA NO EKLENMİŞ ADRES FORMATLAMA ---
                var streetAndNumber = !string.IsNullOrWhiteSpace(placemark.SubThoroughfare)
                    ? $"{placemark.Thoroughfare} No: {placemark.SubThoroughfare}"
                    : placemark.Thoroughfare;

                var addressParts = new[]
                {
                    streetAndNumber,           // Sokak Adı ve Bina No birleştirildi
                    placemark.SubLocality,     // Mahalle/Semt
                    placemark.Locality,        // İlçe/Şehir
                    placemark.AdminArea,       // İl
                    placemark.PostalCode       // Posta Kodu
                }
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .ToArray();
                // --- GÜNCELLEME SONU ---

                if (addressParts.Any())
                {
                    return string.Join(", ", addressParts);
                }
                else
                {
                    return placemark.FeatureName ?? "Adres detayı bulunamadı.";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Adres çözümleme hatası: {ex.Message}");
                return "Adres bilgisi alınamadı.";
            }
        }
    }
}