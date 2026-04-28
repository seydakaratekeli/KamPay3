using KamPay.Models;
using System;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Linq;

namespace KamPay.Models
{
    // Bir ürün satışı, takası veya bağışı sürecini takip eden ana model
    public class Transaction
    {
        public string TransactionId { get; set; } = Guid.NewGuid().ToString();

        // İlgili Ana Ürün Bilgileri
        public string ProductId { get; set; } = "";
        public string ProductTitle { get; set; } = "";
        public string ProductThumbnailUrl { get; set; } = "";
        public ProductType Type { get; set; } // Satış, Takas, Bağış

        [JsonIgnore]
        public bool IsChipSelected { get; set; }

        // Taraflar
        public string SellerId { get; set; } = "";
        public string SellerName { get; set; } = "";
        public string SellerPhotoUrl { get; set; } = "";
        public string BuyerId { get; set; } = "";
        public string BuyerName { get; set; } = "";
        public string BuyerPhotoUrl { get; set; } = "";

        // Ödeme Durumu
        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

        // Ödeme bilgileri (simülasyon desteği)
        public PaymentMethodType PaymentMethod { get; set; } = PaymentMethodType.None;
        public string? PaymentSimulationId { get; set; }

        public decimal Price { get; set; }       // Ürünün orijinal liste fiyatı
        public decimal QuotedPrice { get; set; } // Satış anındaki kilitli fiyat
        public string Currency { get; set; } = "TRY";

        public DateTime? PaymentCompletedAt { get; set; }

        public string? Message { get; set; }

        // Durum ve Zaman Bilgileri
        public TransactionStatus Status { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // QR Kod Teslimat Takibi
        [JsonIgnore]
        public List<DeliveryQRCode> DeliveryQRCodes { get; set; } = new();

        // Takas'a özel alanlar
        public string? OfferedProductId { get; set; }
        public string? OfferedProductTitle { get; set; }
        public string? OfferedProductThumbnailUrl { get; set; }
        public string? OfferMessage { get; set; }

        // Pazarlık Özellikleri
        [JsonIgnore]
        public decimal FinalPrice => QuotedPrice > 0 ? QuotedPrice : Price;

        [JsonIgnore]
        public bool HasDiscount => QuotedPrice > 0 && QuotedPrice < Price;

        // Alıcının teklif ettiği fiyat (Satış için)
        public decimal? ProposedPriceByBuyer { get; set; }

        // Satıcının karşı teklifi (Satış için)
        public decimal? CounterOfferBySeller { get; set; }

        // Talep edenin teklif ettiği ek nakit (Takas için)
        public decimal? AdditionalCashByRequester { get; set; }

        // ✅ SORUN 1 FIX: Liste fiyatıyla satın alma talebi mi?
        // CreateRequestAsync'te true set edilir → pazarlık butonları gizlenir
        public bool IsFixedPriceRequest { get; set; } = false;

        /// <summary>
        /// ✅ Computed: Pazarlığa izin veriliyor mu?
        /// IsFixedPriceRequest=true ise pazarlık butonları tamamen devre dışı kalır.
        /// XAML binding'lerde InvertedBoolConverter yerine doğrudan kullanılabilir.
        /// </summary>
        [JsonIgnore]
        public bool IsNegotiationAllowed => !IsFixedPriceRequest;

        // Sahip'in istediği ek nakit (Takas için)
        public decimal? CounterCashByOwner { get; set; }

        // Pazarlık devam ediyor mu?
        public bool IsNegotiating { get; set; } = false;

        // Son pazarlık tarihi
        public DateTime? LastNegotiationDate { get; set; }

        // Pazarlık başlangıç tarihi
        public DateTime? NegotiationStartedAt { get; set; }

        // Pazarlık notları
        public string NegotiationNotes { get; set; } = "";

        // Pazarlık turu sayısı
        public int NegotiationRoundCount { get; set; } = 0;

        [JsonIgnore]
        public string RemainingRoundsText =>
            IsFixedPriceRequest
                ? "✅ Liste fiyatı ile satın alma talebi"
                : $"Kalan Teklif Hakkı: {Math.Max(0, Helpers.NegotiationRules.MaxNegotiationRounds - NegotiationRoundCount)}";

        // ✅ FAZ 2: Sıra kontrolü için — son teklifi/karşı teklifi gönderen kullanıcı ID'si.
        // Alıcı arka arkaya teklif gönderememesi için, satıcı yanıt vermeden alıcının
        // tekrar teklif yapmasını engellemek amacıyla kullanılır.
        public string LastActionBy { get; set; } = "";

        // Mesajlaşma için conversation ID
        public string ConversationId { get; set; } = "";

        // Aktif konuşma var mı?
        public bool HasActiveConversation { get; set; } = false;

        // Görsel durum metni
        public string StatusText
        {
            get
            {
                if (Status == TransactionStatus.Accepted && DeliveryQRCodes.Any() && DeliveryQRCodes.All(qr => qr.IsUsed))
                    return "Tamamlandı";

                return Status switch
                {
                    TransactionStatus.Pending => "Onay Bekliyor",
                    TransactionStatus.Accepted => "Kabul Edildi",
                    TransactionStatus.Rejected => "Reddedildi",
                    TransactionStatus.Completed => "Tamamlandı",
                    TransactionStatus.Cancelled => "İptal Edildi",
                    _ => "Bilinmiyor"
                };
            }
        }

        public bool CanManageDelivery =>
            Type == ProductType.Takas &&
            Status == TransactionStatus.Accepted &&
            !(DeliveryQRCodes.Any() && DeliveryQRCodes.All(qr => qr.IsUsed));

        public bool IsDeliveryCompleted =>
            Status == TransactionStatus.Completed ||
            (Status == TransactionStatus.Accepted && DeliveryQRCodes.Any() && DeliveryQRCodes.All(qr => qr.IsUsed));

        // Pazarlık durumu metni
        public string NegotiationStatusText
        {
            get
            {
                if (!IsNegotiating)
                    return string.Empty;

                if (Type == ProductType.Satis)
                {
                    if (CounterOfferBySeller.HasValue && ProposedPriceByBuyer.HasValue)
                        return $"Sizin: {ProposedPriceByBuyer:N2}₺ / Karşı: {CounterOfferBySeller:N2}₺";
                    else if (ProposedPriceByBuyer.HasValue)
                        return $"Teklifiniz: {ProposedPriceByBuyer:N2}₺";
                    else if (CounterOfferBySeller.HasValue)
                        return $"Karşı Teklif: {CounterOfferBySeller:N2}₺";
                }
                else if (Type == ProductType.Takas)
                {
                    if (CounterCashByOwner.HasValue && AdditionalCashByRequester.HasValue)
                        return $"Sizin: {AdditionalCashByRequester:N2}₺ / Karşı: {CounterCashByOwner:N2}₺";
                    else if (AdditionalCashByRequester.HasValue)
                        return $"Ek Nakit Teklifiniz: {AdditionalCashByRequester:N2}₺";
                    else if (CounterCashByOwner.HasValue)
                        return $"İstenen Ek Nakit: {CounterCashByOwner:N2}₺";
                }

                return "Pazarlık devam ediyor";
            }
        }

        // Hangi fiyat/tutar üzerinde anlaşıldı?
        public decimal AgreedAmount
        {
            get
            {
                if (Type == ProductType.Satis)
                    return CounterOfferBySeller ?? ProposedPriceByBuyer ?? QuotedPrice;
                else if (Type == ProductType.Takas)
                    return CounterCashByOwner ?? AdditionalCashByRequester ?? 0;

                return QuotedPrice;
            }
        }
    }

    // İşlem Durumu
    public enum TransactionStatus
    {
        Pending,     // Teklif yapıldı, satıcının onayı bekliyor
        Accepted,    // Teklif kabul edildi, teslimat/ödeme süreci bekleniyor
        Rejected,    // Teklif reddedildi
        Completed,   // İşlem (ödeme/teslimat) tamamlandı ve kapandı
        Cancelled    // Taraflardan biri iptal etti
    }

    // Ödeme Durumu
    public enum PaymentStatus
    {
        Pending, // Ödeme bekleniyor
        Paid,    // Ödendi (simülasyon)
        Failed   // Başarısız
    }
}