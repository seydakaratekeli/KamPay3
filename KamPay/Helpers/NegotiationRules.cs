using KamPay.Models;
using System;

namespace KamPay.Helpers
{
    /// <summary>
    /// Pazarlık kuralları ve limitleri için helper sınıf
    /// </summary>
    public static class NegotiationRules
    {
        // Maksimum pazarlık turu sayısı
        public const int MaxNegotiationRounds = 10;

        // Pazarlık zaman aşımı (saat cinsinden)
        public const int NegotiationTimeoutHours = 48;

        // Minimum teklif oranı (orijinal fiyatın %'si olarak)
        public const decimal MinOfferPercentage = 50m; // %50

        // Maksimum karşı teklif oranı (orijinal fiyatın %'si olarak)
        public const decimal MaxCounterOfferPercentage = 100m; // %100

        /// <summary>
        /// Pazarlık turlarının maksimum sayıya ulaşıp ulaşmadığını kontrol eder
        /// </summary>
        public static bool HasReachedMaxRounds(int currentRounds)
            => currentRounds >= MaxNegotiationRounds;

        /// <summary>
        /// Pazarlık zaman aşımına uğramış mı kontrol eder
        /// </summary>
        public static bool IsNegotiationExpired(DateTime? negotiationStartedAt)
        {
            if (!negotiationStartedAt.HasValue) return false;
            return (DateTime.UtcNow - negotiationStartedAt.Value).TotalHours > NegotiationTimeoutHours;
        }

        /// <summary>
        /// ✅ FAZ 7 YENİ: Pazarlık bitiş tarihine kalan süreyi TimeSpan olarak döndürür.
        /// Süre dolmuşsa veya başlangıç tarihi yoksa TimeSpan.Zero döner.
        /// </summary>
        public static TimeSpan GetRemainingTime(DateTime? negotiationStartedAt)
        {
            if (!negotiationStartedAt.HasValue) return TimeSpan.Zero;

            var deadline = negotiationStartedAt.Value.AddHours(NegotiationTimeoutHours);
            var remaining = deadline - DateTime.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        /// <summary>
        /// ✅ FAZ 7 YENİ: Kalan süreyi kullanıcı dostu metin olarak döndürür.
        /// Örnekler: "⏱️ 23s 45dk", "⏱️ 5dk", "⏰ Süre doldu"
        /// </summary>
        public static string FormatRemainingTime(DateTime? negotiationStartedAt)
        {
            var remaining = GetRemainingTime(negotiationStartedAt);

            if (remaining == TimeSpan.Zero)
                return "⏰ Pazarlık süresi doldu";

            if (remaining.TotalHours >= 1)
                return $"⏱️ {(int)remaining.TotalHours}s {remaining.Minutes}dk kaldı";

            if (remaining.TotalMinutes >= 1)
                return $"⏱️ {(int)remaining.TotalMinutes}dk kaldı";

            return $"⏱️ {(int)remaining.TotalSeconds}sn kaldı";
        }

        /// <summary>
        /// ✅ FAZ 7 YENİ: Sürenin kritik eşiğe (son 2 saat) gelip gelmediğini kontrol eder.
        /// UI'da uyarı rengi göstermek için kullanılabilir.
        /// </summary>
        public static bool IsNearingExpiry(DateTime? negotiationStartedAt, int warningHours = 2)
        {
            var remaining = GetRemainingTime(negotiationStartedAt);
            return remaining > TimeSpan.Zero && remaining.TotalHours <= warningHours;
        }

        /// <summary>
        /// Teklif edilen fiyatın geçerli aralıkta olup olmadığını kontrol eder (SATIŞ için)
        /// </summary>
        public static ValidationResult ValidateProposedPrice(decimal proposedPrice, decimal originalPrice)
        {
            if (proposedPrice <= 0)
                return ValidationResult.Failure("Teklif fiyatı sıfırdan büyük olmalıdır.");

            var minAllowedPrice = originalPrice * (MinOfferPercentage / 100m);

            if (proposedPrice < minAllowedPrice)
                return ValidationResult.Failure(
                    $"Teklif fiyatı çok düşük. Minimum: {minAllowedPrice:N2}₺ (Orijinal fiyatın %{MinOfferPercentage:N0})");

            if (proposedPrice > originalPrice)
                return ValidationResult.Warning(
                    "Teklif fiyatı orijinal fiyattan yüksek. Direkt kabul edebilirsiniz.");

            return ValidationResult.Success();
        }

        /// <summary>
        /// Karşı teklif fiyatının geçerli aralıkta olup olmadığını kontrol eder (SATIŞ için).
        ///
        /// ✅ KURAL DÜZELTMESİ:
        ///   Önceki: counterOffer &lt; proposedPrice → sadece Warning (engellemiyordu)
        ///   Yeni:   counterOffer &lt; proposedPrice → Failure (bloke eder)
        ///
        ///   MANTIK: Satıcı alıcının teklifinden düşük karşı teklif veremez.
        ///   Örnek: Alıcı 400₺ teklif etti, satıcı 350₺ karşı teklif veremez.
        ///   Bu hem pazarlık mantığına aykırı hem de sonsuz döngüye yol açar.
        ///
        ///   Satıcının seçenekleri:
        ///     a) Alıcının teklifini kabul et (AcceptNegotiatedPriceAsync)
        ///     b) Alıcının teklifinden yüksek karşı teklif ver (liste fiyatına kadar)
        ///     c) Teklifi reddet (RejectOffer)
        /// </summary>
        public static ValidationResult ValidateCounterOffer(
            decimal counterOffer,
            decimal originalPrice,
            decimal? proposedPrice)
        {
            if (counterOffer <= 0)
                return ValidationResult.Failure("Karşı teklif fiyatı sıfırdan büyük olmalıdır.");

            if (counterOffer > originalPrice)
                return ValidationResult.Failure(
                    $"Karşı teklif orijinal liste fiyatından ({originalPrice:N2}₺) yüksek olamaz. " +
                    $"Maksimum: {originalPrice:N2}₺");

            // ✅ DÜZELTME: Warning → Failure
            // Satıcı alıcının teklifinden düşük karşı teklif veremez
            if (proposedPrice.HasValue && counterOffer < proposedPrice.Value)
                return ValidationResult.Failure(
                    $"Karşı teklif, alıcının mevcut teklifinden ({proposedPrice:N2}₺) düşük olamaz.\n" +
                    $"• Alıcıyı kabul etmek için 'Teklifi Kabul Et' kullanın.\n" +
                    $"• Daha yüksek bir fiyat önermek için {proposedPrice.Value:N2}₺ üzerinde bir değer girin.");

            return ValidationResult.Success();
        }
        /// <summary>
        /// Takas için ek nakit teklifinin geçerli olup olmadığını kontrol eder
        /// </summary>
        public static ValidationResult ValidateAdditionalCash(decimal additionalCash)
        {
            if (additionalCash < 0)
                return ValidationResult.Failure("Ek nakit negatif olamaz.");

            return ValidationResult.Success();
        }

        /// <summary>
        /// Pazarlığa devam edilip edilemeyeceğini kontrol eder
        /// </summary>
        public static ValidationResult CanContinueNegotiation(int currentRounds, DateTime? negotiationStartedAt)
        {
            if (HasReachedMaxRounds(currentRounds))
                return ValidationResult.Failure(
                    $"Maksimum pazarlık turu sayısına ({MaxNegotiationRounds}) ulaşıldı. " +
                    "Lütfen mevcut teklifi kabul edin veya reddedin.");

            if (IsNegotiationExpired(negotiationStartedAt))
                return ValidationResult.Failure(
                    $"Pazarlık süresi doldu ({NegotiationTimeoutHours} saat). Bu pazarlık artık aktif değil.");

            return ValidationResult.Success();
        }

        /// <summary>
        /// Pazarlık özeti mesajı oluşturur
        /// </summary>
        public static string GetNegotiationSummary(
            int roundCount,
            DateTime? startedAt,
            decimal? originalPrice = null,
            decimal? finalPrice = null)
        {
            var duration = startedAt.HasValue
                ? (DateTime.UtcNow - startedAt.Value).TotalMinutes
                : 0;

            var summary = "📊 Pazarlık İstatistikleri\n";
            summary += "━━━━━━━━━━━━━━━━━━━━\n";
            summary += $"🔄 Tur Sayısı: {roundCount}\n";
            summary += $"⏱️ Süre: {duration:N0} dakika\n";

            if (originalPrice.HasValue && finalPrice.HasValue && originalPrice.Value > 0)
            {
                var discount = ((originalPrice.Value - finalPrice.Value) / originalPrice.Value) * 100m;
                summary += $"💰 Orijinal Fiyat: {originalPrice.Value:N2}₺\n";
                summary += $"✅ Anlaşılan Fiyat: {finalPrice.Value:N2}₺\n";
                summary += $"📉 İndirim: %{discount:N1}\n";
            }

            summary += $"📅 Tarih: {DateTime.UtcNow:dd.MM.yyyy HH:mm}\n";

            return summary;
        }

        /// <summary>
        /// Akıllı öneri: Ortalama indirim oranına göre fiyat önerir
        /// </summary>
        public static decimal SuggestPrice(
            decimal originalPrice,
            decimal? buyerOffer = null,
            decimal? sellerCounter = null)
        {
            if (buyerOffer.HasValue && sellerCounter.HasValue)
                return Math.Round((buyerOffer.Value + sellerCounter.Value) / 2m, 2);

            if (buyerOffer.HasValue)
                return Math.Round((buyerOffer.Value + originalPrice) / 2m, 2);

            return Math.Round(originalPrice * 0.85m, 2);
        }
    }
}