using KamPay.Models;
using KamPay.Services;
using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace KamPay.Converters
{
    /// <summary>
    /// Transaction'ın pazarlık durumu metnini kullanıcıya göre döndürür
    /// SATICI: Alıcının teklifini görür
    /// ALICI: Satıcının karşı teklifini görür
    /// </summary>
    public class NegotiationStatusTextConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not Transaction transaction)
                return string.Empty;

            if (!transaction.IsNegotiating)
                return string.Empty;

            // ✅ Preferences üzerinden mevcut kullanıcı ID'sini al
            var currentUserId = Preferences.Get("current_user_id", string.Empty);
            
            if (string.IsNullOrEmpty(currentUserId))
                return "Pazarlık devam ediyor";

            // SATIŞ işlemi için
            if (transaction.Type == ProductType.Satis)
            {
                // Kullanıcı SATICI ise
                if (transaction.SellerId == currentUserId)
                {
                    if (transaction.ProposedPriceByBuyer.HasValue && transaction.CounterOfferBySeller.HasValue)
                    {
                        return $"💰 Alıcının Teklifi: {transaction.ProposedPriceByBuyer:N2}₺\n🔄 Sizin Karşı Teklifiniz: {transaction.CounterOfferBySeller:N2}₺";
                    }
                    else if (transaction.ProposedPriceByBuyer.HasValue)
                    {
                        return $"💰 Alıcının Teklifi: {transaction.ProposedPriceByBuyer:N2}₺";
                    }
                    else if (transaction.CounterOfferBySeller.HasValue)
                    {
                        return $"🔄 Sizin Karşı Teklifiniz: {transaction.CounterOfferBySeller:N2}₺";
                    }
                }
                // Kullanıcı ALICI ise
                else if (transaction.BuyerId == currentUserId)
                {
                    if (transaction.ProposedPriceByBuyer.HasValue && transaction.CounterOfferBySeller.HasValue)
                    {
                        return $"💰 Sizin Teklifiniz: {transaction.ProposedPriceByBuyer:N2}₺\n🔄 Satıcının Karşı Teklifi: {transaction.CounterOfferBySeller:N2}₺";
                    }
                    else if (transaction.ProposedPriceByBuyer.HasValue)
                    {
                        return $"💰 Sizin Teklifiniz: {transaction.ProposedPriceByBuyer:N2}₺";
                    }
                    else if (transaction.CounterOfferBySeller.HasValue)
                    {
                        return $"🔄 Satıcının Karşı Teklifi: {transaction.CounterOfferBySeller:N2}₺";
                    }
                }
            }
            // TAKAS işlemi için
            else if (transaction.Type == ProductType.Takas)
            {
                // Kullanıcı SAHİP (Seller) ise
                if (transaction.SellerId == currentUserId)
                {
                    if (transaction.AdditionalCashByRequester.HasValue && transaction.CounterCashByOwner.HasValue)
                    {
                        return $"💰 Talep Edenin Teklifi: {transaction.AdditionalCashByRequester:N2}₺\n🔄 Sizin Karşı Teklifiniz: {transaction.CounterCashByOwner:N2}₺";
                    }
                    else if (transaction.AdditionalCashByRequester.HasValue)
                    {
                        return $"💰 Talep Edenin Teklifi: {transaction.AdditionalCashByRequester:N2}₺";
                    }
                    else if (transaction.CounterCashByOwner.HasValue)
                    {
                        return $"🔄 Sizin Karşı Teklifiniz: {transaction.CounterCashByOwner:N2}₺";
                    }
                }
                // Kullanıcı TALEP EDEN (Buyer) ise
                else if (transaction.BuyerId == currentUserId)
                {
                    if (transaction.AdditionalCashByRequester.HasValue && transaction.CounterCashByOwner.HasValue)
                    {
                        return $"💰 Sizin Teklifiniz: {transaction.AdditionalCashByRequester:N2}₺\n🔄 Sahip'in Karşı Teklifi: {transaction.CounterCashByOwner:N2}₺";
                    }
                    else if (transaction.AdditionalCashByRequester.HasValue)
                    {
                        return $"💰 Sizin Teklifiniz: {transaction.AdditionalCashByRequester:N2}₺";
                    }
                    else if (transaction.CounterCashByOwner.HasValue)
                    {
                        return $"🔄 Sahip'in Karşı Teklif: {transaction.CounterCashByOwner:N2}₺";
                    }
                }
            }

            return "Pazarlık devam ediyor";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
