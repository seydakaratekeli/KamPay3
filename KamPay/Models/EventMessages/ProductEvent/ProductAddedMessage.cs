using CommunityToolkit.Mvvm.Messaging.Messages;
using KamPay.Models;

namespace KamPay.Models.EventMessages.ProductEvent
{
    /// <summary>
    /// AddProductViewModel → ProductListViewModel arası mesaj.
    /// Ürün başarıyla kaydedilince gönderilir; liste scroll-to-top yapar.
    /// </summary>
    public class ProductAddedMessage : ValueChangedMessage<Product>
    {
        public ProductAddedMessage(Product product) : base(product) { }
    }
}
