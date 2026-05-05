using System;
using System.Globalization;
using KamPay.Models;
using KamPay.Services;
using Microsoft.Maui.Controls;

namespace KamPay.Converters
{
    public class CanAcceptNegotiationConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not Transaction transaction)
                return false;

            if (!transaction.IsNegotiating)
                return false;

            var currentUserId = ConverterHelpers.GetCurrentUserId();
            if (!string.IsNullOrEmpty(currentUserId) &&
                transaction.LastActionBy == currentUserId)
            {
                return false;
            }

            if (transaction.Type == ProductType.Satis)
            {
                return !string.IsNullOrEmpty(transaction.CurrentActiveOfferId)
                    || (transaction.ProposedPriceByBuyer.HasValue && transaction.ProposedPriceByBuyer.Value > 0)
                    || (transaction.CounterOfferBySeller.HasValue && transaction.CounterOfferBySeller.Value > 0);
            }
            else if (transaction.Type == ProductType.Takas)
            {
                return transaction.CounterCashByOwner.HasValue && transaction.CounterCashByOwner.Value >= 0;
            }

            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
