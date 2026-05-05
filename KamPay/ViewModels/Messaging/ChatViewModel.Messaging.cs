using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using KamPay.Services;
using KamPay.Models;
using KamPay.Views;
using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Helpers;
using System.Reactive.Linq;
using KamPay.Services.Auth;
using KamPay.Services.Messaging;
using KamPay.Services.Transactions;

namespace KamPay.ViewModels
{
    public partial class ChatViewModel
    {
        [RelayCommand]
        private async Task LoadOlderMessagesAsync()
        {
            if (IsLoading || _currentUser == null || string.IsNullOrEmpty(ConversationId) || Messages.Count == 0)
                return;

            try
            {
                IsLoading = true;
                var beforeMessageId = Messages.First().MessageId;
                var olderMessages = await _chatRealtimeService.LoadOlderMessagesAsync(
                    ConversationId,
                    beforeMessageId,
                    _currentUser.UserId);

                foreach (var message in olderMessages.OrderByDescending(m => m.SentAt))
                {
                    if (_knownMessageIds.Contains(message.MessageId))
                        continue;

                    Messages.Insert(0, message);
                    TrackKnownMessage(message);
                }
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"Eski mesajlar yuklenemedi: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }


        //  : 200ms buffer + batch processing
        private async void StartListeningToMessages()
        {
            if (_isListenerActive)
            {
                KamPay.Helpers.AppLogger.DebugLog("Ã¢Å¡Â Ã¯Â¸Â Listener zaten aktif, yeniden baÃ…Å¸latÃ„Â±lmadÃ„Â±.");
                return;
            }

            KamPay.Helpers.AppLogger.DebugLog($" Real-time listener baÃ…Å¸latÃ„Â±ldÃ„Â±: {ConversationId}");

            //  : Sistem mesajlarÃ„Â±nÃ„Â± da dahil et
            _isListenerActive = true;

            try
            {
                if (_currentUser == null || string.IsNullOrEmpty(ConversationId))
                    return;

                var latestMessages = await _chatRealtimeService.LoadAndListenAsync(
                    ConversationId,
                    _currentUser.UserId,
                    message =>
                    {
                        InsertMessageSorted(message);
                        if (!message.IsSentByMe)
                        {
                            _ = _messagingService.MarkMessagesAsReadAsync(ConversationId, _currentUser.UserId);
                        }

                        if (_initialLoadComplete)
                        {
                            WeakReferenceMessenger.Default.Send(new ScrollToChatMessage(message));
                        }
                    },
                    message =>
                    {
                        _messageLookup.TryGetValue(message.MessageId, out var existing);
                        if (existing != null)
                        {
                            var index = Messages.IndexOf(existing);
                            Messages[index] = message;
                            TrackKnownMessage(message);
                        }
                    },
                    messageId =>
                    {
                        _messageLookup.TryGetValue(messageId, out var existing);
                        if (existing != null)
                        {
                            Messages.Remove(existing);
                        }

                        _knownMessageIds.Remove(messageId);
                        _messageLookup.Remove(messageId);
                    });

                Messages.Clear();
                _knownMessageIds.Clear();
                _messageLookup.Clear();
                foreach (var message in latestMessages)
                {
                    Messages.Add(message);
                    TrackKnownMessage(message);
                }

                _initialLoadComplete = true;
                IsLoading = false;
                WeakReferenceMessenger.Default.Send(new ScrollToChatMessage(null));
                return;
            }
            catch (Exception ex)
            {
                _isListenerActive = false;
                IsLoading = false;
                KamPay.Helpers.AppLogger.DebugLog($"Chat realtime service hatasi: {ex.Message}");
                return;
            }
        }


        private void StartListeningToTyping()
        {
            if (Conversation == null || _currentUser == null) return;

            var otherUserId = Conversation.GetOtherUserId(_currentUser.UserId);
            if (string.IsNullOrEmpty(otherUserId)) return;

            _typingSubscription?.Dispose();
            _typingSubscription = _firebaseClient
                .Child("typing")
                .Child(ConversationId)
                .Child(otherUserId)
                .AsObservable<bool>()
                .Subscribe(e =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        IsOtherUserTyping = e.Object;
                        if (IsOtherUserTyping)
                            WeakReferenceMessenger.Default.Send(new ScrollToChatMessage(null));
                    });
                });
        }


        partial void OnMessageTextChanged(string value)
        {
            if (_currentUser == null || string.IsNullOrEmpty(ConversationId)) return;

            _ = _firebaseClient
                .Child("typing")
                .Child(ConversationId)
                .Child(_currentUser.UserId)
                .PutAsync(true);

            _typingDebounceTimer?.Stop();
            _typingDebounceTimer = new System.Timers.Timer(3000) { AutoReset = false };
            _typingDebounceTimer.Elapsed += async (_, _) =>
            {
                await _firebaseClient
                    .Child("typing")
                    .Child(ConversationId)
                    .Child(_currentUser.UserId)
                    .PutAsync(false);
            };
            _typingDebounceTimer.Start();
        }
        [RelayCommand]
        private async Task SendMessageAsync()
        {
            if (IsSending || string.IsNullOrWhiteSpace(MessageText) || _currentUser == null || Conversation == null)
            {
                return;
            }

            // GÃƒÅ“VENLÃ„Â°K: Mesaj iÃƒÂ§eriÃ„Å¸ini XSS saldÃ„Â±rÃ„Â±larÃ„Â±na karÃ…Å¸Ã„Â± temizle
            var messageContent = InputSanitizer.SanitizeText(MessageText.Trim());

            // EÃ„Å¸er temizleme sonrasÃ„Â± mesaj tamamen boÃ…Å¸aldÃ„Â±ysa iÃ…Å¸lemi iptal et
            if (string.IsNullOrEmpty(messageContent)) return;

            var tempMessage = new Message
            {
                MessageId = $"temp_{Guid.NewGuid()}",
                ConversationId = ConversationId,
                SenderId = _currentUser.UserId,
                SenderName = _currentUser.FullName,
                SenderPhotoUrl = _currentUser.ProfileImageUrl,
                ReceiverId = Conversation.GetOtherUserId(_currentUser.UserId) ?? string.Empty,
                ReceiverName = Conversation.GetOtherUserName(_currentUser.UserId),
                ReceiverPhotoUrl = Conversation.GetOtherUserPhotoUrl(_currentUser.UserId),
                Content = messageContent,
                Type = MessageType.Text,
                ProductId = Conversation.ProductId,
                ProductTitle = Conversation.ProductTitle,
                ProductThumbnail = Conversation.ProductThumbnail,
                IsSentByMe = true,
                SentAt = DateTime.UtcNow,
                IsDelivered = false,
                IsRead = false
            };

            InsertMessageSorted(tempMessage);
            WeakReferenceMessenger.Default.Send(new ScrollToChatMessage(tempMessage));

            try
            {
                IsSending = true;

                var receiverId = Conversation.GetOtherUserId(_currentUser.UserId);
                if (string.IsNullOrEmpty(receiverId))
                {
                    Messages.Remove(tempMessage);
                    await Application.Current!.MainPage!.DisplayAlert("Hata", "AlÃ„Â±cÃ„Â± bilgisi bulunamadÃ„Â±.", "Tamam");
                    return;
                }

                var request = new SendMessageRequest
                {
                    ReceiverId = receiverId,
                    Content = messageContent,
                    Type = MessageType.Text,
                    ProductId = Conversation.ProductId
                };

                var result = await _messagingService.SendMessageAsync(request, _currentUser);

                if (result.Success)
                {
                    // Ã¢Å“â€¦ BaÃ…Å¸arÃ„Â±lÃ„Â± gÃƒÂ¶nderim sonrasÃ„Â± input temizle
                    MessageText = string.Empty;
                }
                else
                {
                    Messages.Remove(tempMessage);
                    await Application.Current!.MainPage!.DisplayAlert("Hata", result.Message ?? "Mesaj gÃƒÂ¶nderilemedi", "Tamam");
                }
            }
            catch (Exception ex)
            {
                Messages.Remove(tempMessage);
                await Application.Current!.MainPage!.DisplayAlert("Hata", ex.Message, "Tamam");
                KamPay.Helpers.AppLogger.DebugLog($"Ã¢ÂÅ’ SendMessage hatasÃ„Â±: {ex.Message}");
            }
            finally
            {
                IsSending = false;
            }
        }

    }
}
