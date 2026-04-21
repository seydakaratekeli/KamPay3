using CommunityToolkit.Mvvm.Messaging.Messages;
using KamPay.Models;

namespace KamPay.Models.EventMessages.ProductEvent
{
    /// <summary>
    /// EditProductViewModel → ProductDetailViewModel + ProductListViewModel arası mesaj.
    /// Ürün başarıyla güncellenince gönderilir; detay sayfası ve liste anında güncellenir.
    /// </summary>
    public class ProductUpdatedMessage : ValueChangedMessage<Product>
    {
        public ProductUpdatedMessage(Product product) : base(product) { }
    }
}
