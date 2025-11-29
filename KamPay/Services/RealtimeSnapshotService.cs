using Firebase.Database;
using Firebase.Database.Query;
using Firebase.Database.Streaming;
using System.Reactive.Linq;

namespace KamPay.Services
{
    public class RealtimeSnapshotService<T>
    {
        private readonly FirebaseClient _client;
        private IDisposable _subscription;

        public RealtimeSnapshotService(string baseUrl)
        {
            _client = new FirebaseClient(baseUrl);
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
