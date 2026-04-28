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
        private void SelectAllMessages()
        {
            SelectedFilterTransactionId = NullTransactionFilter;
            ShowAllChipSelected = true;

            foreach (var t in ActiveTransactions)
                t.IsChipSelected = false;

            OnPropertyChanged(nameof(FilteredMessages));
        }


        //  : 200ms buffer + batch processing
        private void StartListeningToMessages()
        {
            if (_isListenerActive)
            {
                KamPay.Helpers.AppLogger.DebugLog("Ã¢Å¡Â Ã¯Â¸Â Listener zaten aktif, yeniden baÃ…Å¸latÃ„Â±lmadÃ„Â±.");
                return;
            }

            KamPay.Helpers.AppLogger.DebugLog($" Real-time listener baÃ…Å¸latÃ„Â±ldÃ„Â±: {ConversationId}");

            //  : Sistem mesajlarÃ„Â±nÃ„Â± da dahil et
            _messagesSubscription = _firebaseClient
                .Child(Constants.MessagesCollection)
                .Child(ConversationId)
                .AsObservable<Message>()
                .Where(e => e.Object != null && !e.Object.IsDeleted) // Sadece silinen mesajlarÃ„Â± filtrele
                .Buffer(TimeSpan.FromMilliseconds(200))
                .Where(batch => batch.Any())
                .Subscribe(
                    events =>
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            try
                            {
                                //  DEBUG: Sistem mesajÃ„Â± kontrolÃƒÂ¼
                                foreach (var e in events)
                                {
                                    if (e.Object != null && (e.Object.Type == MessageType.System || e.Object.IsSystemMessage))
                                    {
                                        KamPay.Helpers.AppLogger.DebugLog($"ÄŸÅ¸â€œâ€¹ Realtime sistem mesajÃ„Â±: {e.Object.Content}");
                                    }
                                }
                                
                                ProcessMessageBatch(events);
                            }
                            catch (Exception ex)
                            {
                                KamPay.Helpers.AppLogger.DebugLog($"Ã¢ÂÅ’ Message batch hatasÃ„Â±: {ex.Message}");
                            }
                            finally
                            {
                                if (!_initialLoadComplete)
                                {
                                    _initialLoadComplete = true;
                                    IsLoading = false;

                                    WeakReferenceMessenger.Default.Send(new ScrollToChatMessage(null));
                                }
                            }
                        });
                    },
                    error =>
                    {
                        KamPay.Helpers.AppLogger.DebugLog($"Ã¢ÂÅ’ Firebase message listener hatasÃ„Â±: {error.Message}");
                        MainThread.BeginInvokeOnMainThread(() => IsLoading = false);
                    });

            _isListenerActive = true;
        }


        private void StartListeningToTyping()
        {
            if (Conversation == null || _currentUser == null) return;

            var otherUserId = Conversation.GetOtherUserId(_currentUser.UserId);
            if (string.IsNullOrEmpty(otherUserId)) return;

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


        //  Batch processing
        private void ProcessMessageBatch(IList<Firebase.Database.Streaming.FirebaseEvent<Message>> events)
        {
            bool shouldScroll = false;
            Message? lastNewMessage = null;

            foreach (var e in events)
            {
                var message = e.Object;
                if (message == null) continue;

                message.MessageId = e.Key;
                message.IsSentByMe = message.SenderId == _currentUser!.UserId;

                var existingMessage = Messages.FirstOrDefault(m => m.MessageId == message.MessageId);

                if (e.EventType == Firebase.Database.Streaming.FirebaseEventType.InsertOrUpdate)
                {
                    if (existingMessage != null)
                    {
                        var index = Messages.IndexOf(existingMessage);
                        Messages[index] = message;
                    }
                    else
                    {
                        InsertMessageSorted(message);

                        if (!message.IsSentByMe)
                        {
                            _ = _messagingService.MarkMessagesAsReadAsync(ConversationId, _currentUser!.UserId);
                        }

                        shouldScroll = true;
                        lastNewMessage = message;
                    }
                }
                else if (e.EventType == Firebase.Database.Streaming.FirebaseEventType.Delete)
                {
                    if (existingMessage != null)
                    {
                        Messages.Remove(existingMessage);
                    }
                }
            }

            if (shouldScroll && lastNewMessage != null && _initialLoadComplete)
            {
                WeakReferenceMessenger.Default.Send(new ScrollToChatMessage(lastNewMessage));
            }
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
