// KamPay/ViewModels/AppShellViewModel.cs

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using KamPay.Services;
using KamPay.Helpers;
using Firebase.Database;
using Firebase.Database.Query;
using System.Reactive.Linq;
using KamPay.Models;

namespace KamPay.ViewModels
{
    public partial class AppShellViewModel : ObservableObject, IDisposable
    {
        [ObservableProperty]
        private bool hasUnreadNotifications;

        [ObservableProperty]
        private bool hasUnreadMessages;

        private readonly IAuthenticationService _authService;
        private readonly IMessagingService _messagingService;
        private IDisposable? _messageSubscription;

        // FirebaseClient'ý her seferinde yeniden oluþturmak yerine bir kere oluþturup kullanmak daha verimlidir.
        private readonly FirebaseClient _firebaseClient = new(Constants.FirebaseRealtimeDbUrl);


        public AppShellViewModel(IAuthenticationService authService, IMessagingService messagingService)
        {
            _authService = authService;
            _messagingService = messagingService;

            // Genel bildirimleri dinle
            WeakReferenceMessenger.Default.Register<UnreadGeneralNotificationStatusMessage>(this, (r, m) =>
            {
                HasUnreadNotifications = m.Value;
            });

            // Mesaj bildirimlerini dinle (Bu mesaj þu anki kodda kullanýlmýyor, ancak gelecekte kullanýlabilir)
            WeakReferenceMessenger.Default.Register<UnreadMessageStatusMessage>(this, (r, m) =>
            {
                HasUnreadMessages = m.Value;
            });

            // GÜNCELLENDÝ: Kullanýcý giriþ / çýkýþ yaptýðýnda asenkron olarak tepki ver
            WeakReferenceMessenger.Default.Register<UserSessionChangedMessage>(this, async (r, m) =>
            {
                if (m.Value) // Giriþ yapýldý
                {
                    // Artýk metodu güvenle 'await' edebiliriz
                    await StartListeningForMessagesAsync();
                }
                else // Çýkýþ yapýldý
                {
                    StopListeningForMessages();
                    HasUnreadMessages = false;
                }
            });
        }

        // GÜNCELLENDÝ: Metodun imzasý async Task olarak deðiþtirildi
        private async Task StartListeningForMessagesAsync()
        {
            StopListeningForMessages(); // Önceki dinleyiciyi durdur

            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser == null) return;

            // Uygulama açýldýðýnda ilk kontrolü yap
            var initialCheckResult = await _messagingService.GetTotalUnreadMessageCountAsync(currentUser.UserId);
            if (initialCheckResult.Success)
            {
                HasUnreadMessages = initialCheckResult.Data > 0;
            }

            // Gerçek zamanlý dinleyiciyi baþlat
            _messageSubscription = _firebaseClient
                .Child(Constants.ConversationsCollection)
                .AsObservable<Conversation>()
                .Where(e => e.EventType == Firebase.Database.Streaming.FirebaseEventType.InsertOrUpdate &&
                             e.Object != null &&
                             (e.Object.User1Id == currentUser.UserId || e.Object.User2Id == currentUser.UserId))
                .Subscribe(async entry =>
                {
                    // Kullanýcýya ait bir konuþma güncellendiðinde, toplam okunmamýþ sayýsýný yeniden kontrol et
                    var result = await _messagingService.GetTotalUnreadMessageCountAsync(currentUser.UserId);
                    if (result.Success)
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            HasUnreadMessages = result.Data > 0;
                        });
                    }
                });
        }

        private void StopListeningForMessages()
        {
            _messageSubscription?.Dispose();
            _messageSubscription = null;
        }

        public void Dispose()
        {
            // Bellekte kalan abonelikleri temizle
            StopListeningForMessages();
            WeakReferenceMessenger.Default.UnregisterAll(this);
        }
    }

    // --- Mesaj Sýnýflarý ---
    // Bu sýnýflarýn ayrý bir dosyada olmasý daha temiz bir yapý saðlar,
    // ancak þimdilik burada kalabilirler.

    public class UnreadGeneralNotificationStatusMessage : CommunityToolkit.Mvvm.Messaging.Messages.ValueChangedMessage<bool>
    {
        public UnreadGeneralNotificationStatusMessage(bool value) : base(value) { }
    }

    public class UnreadMessageStatusMessage : CommunityToolkit.Mvvm.Messaging.Messages.ValueChangedMessage<bool>
    {
        public UnreadMessageStatusMessage(bool value) : base(value) { }
    }

    public class UserSessionChangedMessage : CommunityToolkit.Mvvm.Messaging.Messages.ValueChangedMessage<bool>
    {
        public UserSessionChangedMessage(bool isLoggedIn) : base(isLoggedIn) { }
    }
}