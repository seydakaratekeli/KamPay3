using Firebase.Database;
using Firebase.Database.Query;
using Firebase.Database.Streaming;
using System.Reactive.Linq;

namespace KamPay.Services
{
    /// <summary>
    /// Firebase Realtime Database'den gerçek zamanlı veri anlık görüntüleri almak için servis
    /// ✅ DI ile kullanılabilir - FirebaseClient inject edilir
    /// </summary>
    public class RealtimeSnapshotService<T> : IRealtimeSnapshotService<T>
    {
        private readonly FirebaseClient _client;
        private IDisposable? _subscription;

        // ✅ Constructor DI ile FirebaseClient alıyor
        public RealtimeSnapshotService(FirebaseClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            KamPay.Helpers.AppLogger.DebugLog($"✅ RealtimeSnapshotService<{typeof(T).Name}> oluşturuldu (DI ile)");
        }

        public async Task<Dictionary<string, T>> LoadSnapshotAsync(string path)
        {
            var items = await _client.Child(path).OnceAsync<T>();
            return items.ToDictionary(i => i.Key, i => i.Object);
        }

        public IDisposable Listen(string path, Action<FirebaseEvent<T>> onEvent)
        {
            _subscription = _client.Child(path)
                .AsObservable<T>()
                .Where(e => e.Object != null)
                .Subscribe(onEvent);

            return _subscription;
        }

        public void Stop() => _subscription?.Dispose();
    }
}

