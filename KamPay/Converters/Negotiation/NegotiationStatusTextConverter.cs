using KamPay.Helpers;
using KamPay.Models;
using KamPay.Services;
using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace KamPay.Converters
{
    /// <summary>
    /// Transaction'ın pazarlık durumu metnini kullanıcıya göre döndürür.
    /// SATICI: Alıcının teklifini görür
    /// ALICI:  Satıcının karşı teklifini görür
    ///
    /// ✅ FAZ 7: Kalan süre bilgisi eklendi. Süre kritik eşiğe gelince uyarı rengi için
    ///           ConverterParameter="TimeOnly" kullanılarak sadece süre metni alınabilir.
    /// </summary>
    public class NegotiationStatusTextConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not Transaction transaction)
                return string.Empty;

            if (!transaction.IsNegotiating)
                return string.Empty;

            var currentUserId = ConverterHelpers.GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
                return "Pazarlık devam ediyor";

            // ✅ FAZ 7: Sadece süre metni isteniyorsa (ConverterParameter="TimeOnly")
            if (parameter?.ToString() == "TimeOnly")
                return NegotiationRules.FormatRemainingTime(transaction.NegotiationStartedAt);

            // ✅ FAZ 7: Süre bilgisini hesapla — dolmuşsa erken dön
            var timeText = NegotiationRules.FormatRemainingTime(transaction.NegotiationStartedAt);
            bool isExpired = NegotiationRules.IsNegotiationExpired(transaction.NegotiationStartedAt);

            if (isExpired)
                return $"⏰ Pazarlık süresi doldu. Lütfen teklifi kabul edin veya reddedin.";

            string statusText = BuildStatusText(transaction, currentUserId);

            // ✅ FAZ 7: Kalan süreyi durum metnine ekle
            if (transaction.NegotiationStartedAt.HasValue)
                statusText += $"\n{timeText}";

            return statusText;
        }

        private static string BuildStatusText(Transaction transaction, string currentUserId)
        {
            // ── SATIŞ ──
            if (transaction.Type == ProductType.Satis)
            {
                // Satıcı perspektifi
                if (transaction.SellerId == currentUserId)
                {
                    if (transaction.ProposedPriceByBuyer.HasValue && transaction.CounterOfferBySeller.HasValue)
                        return $"💰 Alıcının Teklifi: {transaction.ProposedPriceByBuyer:N2}₺\n🔄 Sizin Karşı Teklifiniz: {transaction.CounterOfferBySeller:N2}₺";
                    else if (transaction.ProposedPriceByBuyer.HasValue)
                        return $"💰 Alıcının Teklifi: {transaction.ProposedPriceByBuyer:N2}₺";
                    else if (transaction.CounterOfferBySeller.HasValue)
                        return $"🔄 Sizin Karşı Teklifiniz: {transaction.CounterOfferBySeller:N2}₺";
                }
                // Alıcı perspektifi
                else if (transaction.BuyerId == currentUserId)
                {
                    if (transaction.ProposedPriceByBuyer.HasValue && transaction.CounterOfferBySeller.HasValue)
                        return $"💰 Sizin Teklifiniz: {transaction.ProposedPriceByBuyer:N2}₺\n🔄 Satıcının Karşı Teklifi: {transaction.CounterOfferBySeller:N2}₺";
                    else if (transaction.ProposedPriceByBuyer.HasValue)
                        return $"💰 Sizin Teklifiniz: {transaction.ProposedPriceByBuyer:N2}₺";
                    else if (transaction.CounterOfferBySeller.HasValue)
                        return $"🔄 Satıcının Karşı Teklifi: {transaction.CounterOfferBySeller:N2}₺";
                }
            }
            // ── TAKAS ──
            else if (transaction.Type == ProductType.Takas)
            {
                // Sahip (Satıcı) perspektifi
                if (transaction.SellerId == currentUserId)
                {
                    if (transaction.AdditionalCashByRequester.HasValue && transaction.CounterCashByOwner.HasValue)
                        return $"💰 Talep Edenin Teklifi: {transaction.AdditionalCashByRequester:N2}₺\n🔄 Sizin Karşı Teklifiniz: {transaction.CounterCashByOwner:N2}₺";
                    else if (transaction.AdditionalCashByRequester.HasValue)
                        return $"💰 Talep Edenin Teklifi: {transaction.AdditionalCashByRequester:N2}₺";
                    else if (transaction.CounterCashByOwner.HasValue)
                        return $"🔄 Sizin Karşı Teklifiniz: {transaction.CounterCashByOwner:N2}₺";
                }
                // Talep Eden (Alıcı) perspektifi
                else if (transaction.BuyerId == currentUserId)
                {
                    if (transaction.AdditionalCashByRequester.HasValue && transaction.CounterCashByOwner.HasValue)
                        return $"💰 Sizin Teklifiniz: {transaction.AdditionalCashByRequester:N2}₺\n🔄 Sahip'in Karşı Teklifi: {transaction.CounterCashByOwner:N2}₺";
                    else if (transaction.AdditionalCashByRequester.HasValue)
                        return $"💰 Sizin Teklifiniz: {transaction.AdditionalCashByRequester:N2}₺";
                    else if (transaction.CounterCashByOwner.HasValue)
                        return $"🔄 Sahip'in Karşı Teklifi: {transaction.CounterCashByOwner:N2}₺";
                }
            }

            return "Pazarlık devam ediyor";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// ✅ FAZ 7 YENİ: Pazarlık süresinin kritik eşiğe gelip gelmediğine göre uyarı rengi döndürür.
    /// Normal → AccentOrange  |  Son 2 saat → Error (kırmızı)  |  Dolmuş → Gray
    /// Binding: {Binding ., Converter={StaticResource NegotiationTimerColorConverter}}
    /// </summary>
    public class NegotiationTimerColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not Transaction transaction || !transaction.IsNegotiating)
                return Colors.Gray;

            if (NegotiationRules.IsNegotiationExpired(transaction.NegotiationStartedAt))
                return Colors.Gray;

            if (NegotiationRules.IsNearingExpiry(transaction.NegotiationStartedAt, warningHours: 2))
                return (Color)Application.Current.Resources["Error"]; // Kırmızı

            return (Color)Application.Current.Resources["AccentOrange"]; // Normal turuncu
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}