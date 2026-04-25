using KamPay.Models;
using KamPay.Services;
using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace KamPay.Converters
{
    /// <summary>
    /// Sıranın mevcut kullanıcıda olup olmadığını kontrol eder.
    /// LastActionBy != currentUserId → sıra bu kullanıcıda (teklif verebilir)
    /// LastActionBy == currentUserId → karşı tarafın sırası (buton disable)
    ///
    /// ConverterParameter:
    ///   null / "text" → "Sıra Sizde" veya "Bekleniyor..." string döner
    ///   "bool"        → true/false döner (IsEnabled için)
    ///   "opacity"     → 1.0 veya 0.45 döner
    /// </summary>
    public class IsMyTurnConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not Transaction transaction)
                return parameter?.ToString() == "bool" ? false : (object)"?";

            var currentUserId = ConverterHelpers.GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
                return parameter?.ToString() == "bool" ? true : (object)"";

            // LastActionBy boşsa → henüz hiç teklif yapılmamış → alıcının sırası
            bool isMyTurn = string.IsNullOrEmpty(transaction.LastActionBy)
                || transaction.LastActionBy != currentUserId;

            return parameter?.ToString() switch
            {
                "bool" => isMyTurn,
                "opacity" => isMyTurn ? 1.0 : 0.45,
                _ => isMyTurn ? "🟢 Sıra Sizde" : "⏳ Bekleniyor..."
            };
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// NegotiationRoundCount'u 0..1 arasında ProgressBar değerine dönüştürür.
    /// MaxNegotiationRounds = 10 → Progress = roundCount / 10
    /// </summary>
    public class NegotiationRoundsLeftConverter : IValueConverter
    {
        private const int MaxRounds = 10;

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int rounds)
                return Math.Min(1.0, rounds / (double)MaxRounds);
            return 0.0;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// ✅ Satıcı fiyat kuralı: Satıcı karşı teklif verirken
    ///    alıcının teklifinden DÜŞÜK veremez (sadece Warning değil, hata).
    ///    OffersViewModel.SendCounterOfferAsync içinde kullanılır.
    ///
    ///    KURAL ÖZETİ:
    ///    - Alıcı: orijinal fiyatın %50'sinden az teklif veremez
    ///    - Satıcı: alıcının mevcut teklifinden düşük karşı teklif veremez
    ///              (mantıklı değil, pazarlık hiç ilerlemez)
    ///    - Her iki taraf: üst üste teklif gönderemez (LastActionBy kontrolü)
    /// </summary>
    public static class NegotiationRuleExtensions
    {
        /// <summary>
        /// Satıcının karşı teklifini validate eder.
        /// </summary>
        public static (bool IsValid, string ErrorMessage) ValidateSellerCounterOffer(
            decimal counterOffer,
            decimal originalPrice,
            decimal? buyerOffer)
        {
            if (counterOffer <= 0)
                return (false, "Karşı teklif 0'dan büyük olmalı.");

            if (counterOffer > originalPrice)
                return (false, $"Karşı teklif liste fiyatından ({originalPrice:N2}₺) yüksek olamaz.");

            // ✅ DÜZELTME: Warning → Error; alıcının teklifinden düşük karşı teklif verilmez
            if (buyerOffer.HasValue && counterOffer < buyerOffer.Value)
                return (false,
                    $"Karşı teklif alıcının teklifinden ({buyerOffer:N2}₺) düşük olamaz. " +
                    "Fiyatı kabul etmek veya daha yüksek karşı teklif vermek için seçin.");

            return (true, string.Empty);
        }
    }
}