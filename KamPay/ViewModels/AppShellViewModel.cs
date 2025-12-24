// KamPay/ViewModels/AppShellViewModel.cs

using System;
using System.Linq;
using System.Reactive.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Helpers;
using KamPay.Models;
using KamPay.Services;

namespace KamPay.ViewModels
{
    public partial class AppShellViewModel : ObservableObject, IDisposable
    {
        [ObservableProperty]
        private bool hasUnreadNotifications;

        [ObservableProperty]
        private bool hasUnreadMessages;

        
        [ObservableProperty]
        private string homeTitle = string.Empty;

        [ObservableProperty]
        private string servicesTitle = string.Empty;

        [ObservableProperty]
        private string goodDeedBoardTitle = string.Empty;

        [ObservableProperty]
        private string messagesTitle = string.Empty;

        [ObservableProperty]
        private string profileTitle = string.Empty;

        [ObservableProperty]
        private string favoritesTitle = string.Empty;

        private readonly IAuthenticationService _authService;
        private readonly IMessagingService _messagingService;
        private IDisposable? _messageSubscription;

        // FirebaseClient'ı her seferinde yeniden oluşturmak yerine bir kere oluşturup kullanmak daha verimlidir.
        private readonly FirebaseClient _firebaseClient = new(Constants.FirebaseRealtimeDbUrl);


        public AppShellViewModel(IAuthenticationService authService, IMessagingService messagingService)
        {
            _authService = authService;
            _messagingService = messagingService;

            // Initialize tab titles with current language
            UpdateTabTitles();

            // Genel bildirimleri dinle
            WeakReferenceMessenger.Default.Register<UnreadGeneralNotificationStatusMessage>(this, (r, m) =>
            {
                HasUnreadNotifications = m.Value;
            });

            // Mesaj bildirimlerini dinle (Bu mesaj şu anki kodda kullanılmıyor, ancak gelecekte kullanılabilir)
            WeakReferenceMessenger.Default.Register<UnreadMessageStatusMessage>(this, (r, m) =>
            {
                HasUnreadMessages = m.Value;
            });

            // Language change message listener
            WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this, (r, m) =>
            {
                // UI thread'de güncelleme yap
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    UpdateTabTitles();
                });
            });

            // Kullanıcı giriş / çıkış yaptığında asenkron olarak tepki ver
            WeakReferenceMessenger.Default.Register<UserSessionChangedMessage>(this, (r, m) =>
            {
                if (m.Value) // Giriş yapıldı
                {
                    // Async işlemi başlat ama constructor'ı bloklamadan
                    _ = Task.Run(async () => await StartListeningForMessagesAsync());
                }
                else // Çıkış yapıldı
                {
                    StopListeningForMessages();
                    HasUnreadMessages = false;
                }
            });
        }

        private void UpdateTabTitles()
        {
            var res = LocalizationResourceManager.Instance;
            HomeTitle = res["Home"];
            ServicesTitle = res["Services"];
            GoodDeedBoardTitle = res["GoodDeedBoard"];
            MessagesTitle = res["Messages"];
            ProfileTitle = res["Profile"];
            FavoritesTitle = res["Favorites"];
        }

        private async Task StartListeningForMessagesAsync()
        {
            try
            {
                StopListeningForMessages(); // Önceki dinleyiciyi durdur

                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null) return;

                // Uygulama açıldığında ilk kontrol yap
                var initialCheckResult = await _messagingService.GetTotalUnreadMessageCountAsync(currentUser.UserId);
                if (initialCheckResult.Success)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        HasUnreadMessages = initialCheckResult.Data > 0;
                    });
                }

                // Gerçek zamanlı dinleyiciyi başlat
                _messageSubscription = _firebaseClient
                    .Child(Constants.ConversationsCollection)
                    .AsObservable<Conversation>()
                    .Where(e => e.EventType == Firebase.Database.Streaming.FirebaseEventType.InsertOrUpdate &&
                                 e.Object != null &&
                                 (e.Object.User1Id == currentUser.UserId || e.Object.User2Id == currentUser.UserId))
                    .Subscribe(async entry =>
                    {
                        // Kullanıcıya ait bir konuşma güncellendiğinde, toplam okunmamış sayısını yeniden kontrol et
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
            catch (Exception ex)
            {
                // Hata logla veya sessizce yut
                System.Diagnostics.Debug.WriteLine($"Error in StartListeningForMessagesAsync: {ex.Message}");
            }
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

    // --- Mesaj Sınıfları ---
    
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
