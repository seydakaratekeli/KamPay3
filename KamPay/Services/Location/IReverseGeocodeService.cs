using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// KamPay/Services/IReverseGeocodeService.cs
namespace KamPay.Services
{
    public interface IReverseGeocodeService
    {
        Task<string> GetAddressForLocation(Location location);
    }
}