using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Helpers;
using KamPay.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace KamPay.Services
{
    public interface IFirebaseObserverService
    {
        IObservable<Product> ObserveProductChanges();
        void Dispose();
    }

    public class FirebaseObserverService : IFirebaseObserverService
    {
        private readonly FirebaseClient _firebaseClient;
        private IDisposable _subscription;

        public FirebaseObserverService()
        {
            _firebaseClient = new FirebaseClient(Constants.FirebaseRealtimeDbUrl);
        }

        public IObservable<Product> ObserveProductChanges()
        {
            return _firebaseClient
                .Child(Constants.ProductsCollection)
                .AsObservable<Product>()
                .Select(change =>
                {
                    var product = change.Object;
                    product.ProductId = change.Key;
                    return product;
                })
                .Where(p => p.IsActive); // Sadece aktif ürünler
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }
    }
}