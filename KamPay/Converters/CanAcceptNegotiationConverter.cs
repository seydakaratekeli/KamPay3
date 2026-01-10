using System;
using System.Globalization;
using KamPay.Models;
using Microsoft.Maui.Controls;

namespace KamPay.Converters
{
    /// <summary>
    /// Pazarlýk sonucu onaylanabilir mi? (Karþý tarafýn son teklifi varsa ve pazarlýk devam ediyorsa)
    /// ALICI için: Satýcýnýn karþý teklifi varsa
    /// </summary>
    public class CanAcceptNegotiationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not Transaction transaction)
                return false;

            // Pazarlýk devam etmiyor mu?
            if (!transaction.IsNegotiating)
                return false;

            if (transaction.Type == ProductType.Satis)
            {
                // SATIÞ: Satýcýnýn karþý teklifi varsa alýcý onaylayabilir
                return transaction.CounterOfferBySeller.HasValue && transaction.CounterOfferBySeller.Value > 0;
            }
            else if (transaction.Type == ProductType.Takas)
            {
                // TAKAS: Sahip'in karþý nakit teklifi varsa talep eden onaylayabilir
                return transaction.CounterCashByOwner.HasValue && transaction.CounterCashByOwner.Value >= 0;
            }

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
