// KamPay/ViewModels/AppShellViewModel.cs

using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Helpers;
using KamPay.Models;
using KamPay.Services;
using KamPay.Services.Auth;
using KamPay.Services.Messaging;

namespace KamPay.ViewModels
{
    public partial class AppShellViewModel : ObservableObject, IDisposable
    {
        // Constants for resource initialization retry logic
        private const int MaxResourceInitRetries = 5;
        private const int InitialRetryDelayMs = 100;

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

        // FirebaseClient'Ä± her seferinde yeniden oluÅŸturmak yerine bir kere oluÅŸturup kullanmak daha verimlidir.
        private readonly FirebaseClient _firebaseClient = new(Constants.FirebaseRealtimeDbUrl);


        public AppShellViewModel(IAuthenticationService authService, IMessagingService messagingService)
        {
            _authService = authService;
            _messagingService = messagingService;

            // Initialize with fallback values first to prevent crashes
            HomeTitle = "Ana Sayfa";
            ServicesTitle = "Hizmetler";
            GoodDeedBoardTitle = "Ä°yilik Panosu";
            MessagesTitle = "Mesajlar";
            ProfileTitle = "Profil";
            FavoritesTitle = "Favoriler";

            // Defer resource initialization to avoid constructor exceptions
            // This will be called after the UI is fully loaded
            _ = InitializeTabTitlesAsync();

            // Genel bildirimleri dinle
            WeakReferenceMessenger.Default.Register<UnreadGeneralNotificationStatusMessage>(this, (r, m) =>
            {
                HasUnreadNotifications = m.Value;
            });

            // Mesaj bildirimlerini dinle (Bu mesaj ÅŸu anki kodda kullanÄ±lmÄ±yor, ancak gelecekte kullanÄ±labilir)
            WeakReferenceMessenger.Default.Register<UnreadMessageStatusMessage>(this, (r, m) =>
            {
                HasUnreadMessages = m.Value;
            });

            // Language change message listener
            WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this, (r, m) =>
            {
                // UI thread'de gÃ¼ncelleme yap
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    UpdateTabTitles();
                });
            });

            // KullanÄ±cÄ± giriÅŸ / Ã§Ä±kÄ±ÅŸ yaptÄ±ÄŸÄ±nda asenkron olarak tepki ver
            WeakReferenceMessenger.Default.Register<UserSessionChangedMessage>(this, (r, m) =>
            {
                if (m.Value) // GiriÅŸ yapÄ±ldÄ±
                {
                    // Async iÅŸlemi baÅŸlat ama constructor'Ä± bloklamadan
                    _ = Task.Run(async () => await StartListeningForMessagesAsync());
                }
                else // Ã‡Ä±kÄ±ÅŸ yapÄ±ldÄ±
                {
                    StopListeningForMessages();
                    HasUnreadMessages = false;
                }
            });
        }

        /// <summary>
        /// Asynchronously initializes tab titles with retry logic to wait for LocalizationResourceManager.
        /// Uses exponential backoff to avoid busy-waiting.
        /// </summary>
        private async Task InitializeTabTitlesAsync()
        {
            try
            {
                // Wait for resources to be initialized with multiple attempts
                for (int attempt = 0; attempt < MaxResourceInitRetries; attempt++)
                {
                    await Task.Delay(InitialRetryDelayMs * (attempt + 1)); // Exponential backoff
                    
                    if (LocalizationResourceManager.Instance?.IsInitialized == true)
                    {
                        // Update on UI thread
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            try
                            {
                                UpdateTabTitles();
                                KamPay.Helpers.AppLogger.DebugLog("âœ“ Tab titles updated successfully");
                            }
                            catch (Exception ex)
                            {
                                KamPay.Helpers.AppLogger.DebugLog($"âš ï¸ UpdateTabTitles deferred error: {ex.Message}");
                            }
                        });
                        return;
                    }
                }
                
                // If resources still not initialized after all attempts, keep fallback values
                KamPay.Helpers.AppLogger.DebugLog("âš ï¸ Resources not initialized after max attempts, using fallback values");
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âš ï¸ InitializeTabTitlesAsync error: {ex.Message}");
            }
        }

        private void UpdateTabTitles()
        {
            try
            {
                var res = LocalizationResourceManager.Instance;
                if (res == null)
                {
                    KamPay.Helpers.AppLogger.DebugLog("LocalizationResourceManager.Instance is null");
                    return;
                }

                // Try to get localized strings with fallback
                HomeTitle = GetLocalizedString(res, "Home", "Ana Sayfa");
                ServicesTitle = GetLocalizedString(res, "Services", "Hizmetler");
                GoodDeedBoardTitle = GetLocalizedString(res, "GoodDeedBoard", "Ä°yilik Panosu");
                MessagesTitle = GetLocalizedString(res, "Messages", "Mesajlar");
                ProfileTitle = GetLocalizedString(res, "Profile", "Profil");
                FavoritesTitle = GetLocalizedString(res, "Favorites", "Favoriler");
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"UpdateTabTitles error: {ex.Message}");
                // Keep fallback values that were set in constructor
            }
        }

        private string GetLocalizedString(LocalizationResourceManager res, string key, string fallback)
        {
            try
            {
                var value = res[key];
                // If the key is returned as-is, it means translation not found
                if (string.IsNullOrEmpty(value) || value == key)
                {
                    return fallback;
                }
                return value;
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"GetLocalizedString error for key '{key}': {ex.Message}");
                return fallback;
            }
        }

        private async Task StartListeningForMessagesAsync()
        {
            try
            {
                StopListeningForMessages(); // Ã–nceki dinleyiciyi durdur

                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null) return;

                // Uygulama aÃ§Ä±ldÄ±ÄŸÄ±nda ilk kontrol yap
                var initialCheckResult = await _messagingService.GetTotalUnreadMessageCountAsync(currentUser.UserId);
                if (initialCheckResult.Success)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        HasUnreadMessages = initialCheckResult.Data > 0;
                    });
                }

                // GerÃ§ek zamanlÄ± dinleyiciyi baÅŸlat
                _messageSubscription = _firebaseClient
                    .Child(Constants.ConversationsCollection)
                    .AsObservable<Conversation>()
                    .Where(e => e.EventType == Firebase.Database.Streaming.FirebaseEventType.InsertOrUpdate &&
                                 e.Object != null &&
                                 (e.Object.User1Id == currentUser.UserId || e.Object.User2Id == currentUser.UserId))
                    .Subscribe(async entry =>
                    {
                        // KullanÄ±cÄ±ya ait bir konuÅŸma gÃ¼ncellendiÄŸinde, toplam okunmamÄ±ÅŸ sayÄ±sÄ±nÄ± yeniden kontrol et
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
                KamPay.Helpers.AppLogger.DebugLog($"Error in StartListeningForMessagesAsync: {ex.Message}");
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

    // --- Mesaj SÄ±nÄ±flarÄ± ---
    
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

