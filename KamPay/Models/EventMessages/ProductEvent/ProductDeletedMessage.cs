using CommunityToolkit.Mvvm.Messaging.Messages;

namespace KamPay.Models.EventMessages.ProductEvent
{
    /// <summary>
    /// ProductDetailViewModel → ProductListViewModel arası mesaj.
    /// Ürün başarıyla silinince gönderilir; liste yenileme gerektirmeden ürünü kaldırır.
    /// </summary>
    public class ProductDeletedMessage : ValueChangedMessage<string>
    {
        public ProductDeletedMessage(string productId) : base(productId) { }
    }
}
