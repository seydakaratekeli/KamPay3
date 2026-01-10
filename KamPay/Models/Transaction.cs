using KamPay.Models;
using System;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Linq;

namespace KamPay.Models
{
    //  Bir ürün satışı, takası veya bağışı sürecini takip eden ana model
    public class Transaction
    {
        public string TransactionId { get; set; } = Guid.NewGuid().ToString();

        //  İlgili Ana Ürün Bilgileri
        public string ProductId { get; set; } = "";
        public string ProductTitle { get; set; } = "";
        public string ProductThumbnailUrl { get; set; } = "";
        public ProductType Type { get; set; } // Satış, Takas, Bağış

        //  Taraflar
        public string SellerId { get; set; } = ""; // Ürünü sunan kişi
        public string SellerName { get; set; } = "";
        public string SellerPhotoUrl { get; set; } = "";
        public string BuyerId { get; set; } = "";  // Teklifi yapan/isteği gönderen kişi
        public string BuyerName { get; set; } = "";
        public string BuyerPhotoUrl { get; set; } = "";

        // Ödeme Durumu
        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

        //  Yeni eklenen ödeme bilgileri (simülasyon desteği için)
        public PaymentMethodType PaymentMethod { get; set; } = PaymentMethodType.None;
        public string? PaymentSimulationId { get; set; }

        public decimal Price { get; set; } // Ürünün orijinal liste fiyatı
        public decimal QuotedPrice { get; set; } // Satış anındaki kilitli fiyat (pazarlık vb.)
        public string Currency { get; set; } = "TRY";
        public DateTime? PaymentCompletedAt { get; set; }

        public string? Message { get; set; }

        //  Durum ve Zaman Bilgileri
        public TransactionStatus Status { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        //  QR Kod Teslimat Takibi
        [JsonIgnore]
        public List<DeliveryQRCode> DeliveryQRCodes { get; set; } = new();

        //  Takas'a özel alanlar
        public string? OfferedProductId { get; set; }
        public string? OfferedProductTitle { get; set; }
        public string? OfferedProductThumbnailUrl { get; set; } // ✅ EKLENDI: Takas edilen ürünün görseli
        public string? OfferMessage { get; set; }

        //  Pazarlık Özellikleri
        
       
        // Alıcının teklif ettiği fiyat (Satış için)
       
        public decimal? ProposedPriceByBuyer { get; set; }
        
        
        // Satıcının karşı teklifi (Satış için)
      
        public decimal? CounterOfferBySeller { get; set; }
        
       
        // Talep edenin teklif ettiği ek nakit (Takas için)
        
        public decimal? AdditionalCashByRequester { get; set; }
        
       
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
        
       
        // Pazarlık turu sayısı (kaç kez teklif/karşı teklif yapıldı)
        
        public int NegotiationRoundCount { get; set; } = 0;
        
       
        // Mesajlaşma için conversation ID
      
        public string ConversationId { get; set; } = "";
        
      
        // Aktif konuşma var mı?
        
        public bool HasActiveConversation { get; set; } = false;

        //  Görsel durum metni
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

        //  Hesaplanan Özellikler
        
        
        // Pazarlık durumu metni
       
        public string NegotiationStatusText
        {
            get
            {
                if (!IsNegotiating)
                    return string.Empty;
                
                if (Type == ProductType.Satis)
                {
                    // SATIŞ için fiyat pazarlığı
                    if (CounterOfferBySeller.HasValue && ProposedPriceByBuyer.HasValue)
                    {
                        return $"Sizin: {ProposedPriceByBuyer:N2}₺ / Karşı: {CounterOfferBySeller:N2}₺";
                    }
                    else if (ProposedPriceByBuyer.HasValue)
                    {
                        return $"Teklifiniz: {ProposedPriceByBuyer:N2}₺";
                    }
                    else if (CounterOfferBySeller.HasValue)
                    {
                        return $"Karşı Teklif: {CounterOfferBySeller:N2}₺";
                    }
                }
                else if (Type == ProductType.Takas)
                {
                    // TAKAS için ek nakit pazarlığı
                    if (CounterCashByOwner.HasValue && AdditionalCashByRequester.HasValue)
                    {
                        return $"Sizin: {AdditionalCashByRequester:N2}₺ / Karşı: {CounterCashByOwner:N2}₺";
                    }
                    else if (AdditionalCashByRequester.HasValue)
                    {
                        return $"Ek Nakit Teklifiniz: {AdditionalCashByRequester:N2}₺";
                    }
                    else if (CounterCashByOwner.HasValue)
                    {
                        return $"İstenen Ek Nakit: {CounterCashByOwner:N2}₺";
                    }
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
                {
                    // Satış: En son teklif edilen fiyat
                    return CounterOfferBySeller ?? ProposedPriceByBuyer ?? QuotedPrice;
                }
                else if (Type == ProductType.Takas)
                {
                    // Takas: En son teklif edilen ek nakit
                    return CounterCashByOwner ?? AdditionalCashByRequester ?? 0;
                }
                
                return QuotedPrice;
            }
        }
    }

    //  İşlem Durumu
    public enum TransactionStatus
    {
        Pending,     // Teklif yapıldı, satıcının onayı bekliyor
        Accepted,    // Teklif kabul edildi, teslimat/ödeme süreci bekleniyor
        Rejected,    // Teklif reddedildi
        Completed,   // İşlem (ödeme/teslimat) tamamlandı ve kapandı
        Cancelled    // Taraflardan biri iptal etti
    }

    //  Ödeme Durumu
    public enum PaymentStatus
    {
        Pending, // Ödeme bekleniyor
        Paid,    // Ödendi (simülasyon)
        Failed   // Başarısız
    }
}