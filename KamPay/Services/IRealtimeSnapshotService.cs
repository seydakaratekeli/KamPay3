using Firebase.Database.Streaming;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KamPay.Services
{
    /// <summary>
    /// Firebase Realtime Database'den gerçek zamanlý veri anlýk görüntüleri almak için interface
    /// </summary>
    /// <typeparam name="T">Veri modeli tipi</typeparam>
    public interface IRealtimeSnapshotService<T>
    {
        /// <summary>
        /// Belirtilen path'ten snapshot yükler
        /// </summary>
        Task<Dictionary<string, T>> LoadSnapshotAsync(string path);

        /// <summary>
        /// Belirtilen path'i gerçek zamanlý dinlemeye baþlar
        /// </summary>
        IDisposable Listen(string path, Action<FirebaseEvent<T>> onEvent);

        /// <summary>
        /// Aktif dinleyiciyi durdurur
        /// </summary>
        void Stop();
    }
}
