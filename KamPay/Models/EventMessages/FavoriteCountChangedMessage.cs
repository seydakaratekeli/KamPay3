using CommunityToolkit.Mvvm.Messaging.Messages;
using KamPay.Models;

namespace KamPay.Models.EventMessages
{
    /// <summary>
    /// ProductDetailViewModel → ProductListViewModel arası mesaj.
    /// Favori eklenince/kaldırılınca gönderilir; listede favori sayısı anında güncellenir.
    /// </summary>
    public class FavoriteCountChangedMessage : ValueChangedMessage<Product>
    {
        public FavoriteCountChangedMessage(Product value) : base(value) { }
    }
}
