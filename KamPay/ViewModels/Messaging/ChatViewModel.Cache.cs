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
        //  : Cache kaydetme (LRU pattern)
        private void SaveToCache(string conversationId)
        {
            if (string.IsNullOrEmpty(conversationId)) return;

            //  LRU: Maksimum cache sayÃ„Â±sÃ„Â±nÃ„Â± kontrol et
            if (_conversationCache.Count >= MaxCachedConversations)
            {
                var oldestKey = _conversationCache
                    .OrderBy(kvp => kvp.Value.LastAccessedAt)
                    .First().Key;

                _conversationCache.Remove(oldestKey);
                KamPay.Helpers.AppLogger.DebugLog($"ÄŸÅ¸â€”â€˜Ã¯Â¸Â LRU: En eski cache temizlendi: {oldestKey}");
            }

            var state = new ConversationState
            {
                Messages = Messages.ToList(),
                Conversation = Conversation,
                OtherUserName = OtherUserName,
                OtherUserPhoto = OtherUserPhoto,
                CachedAt = DateTime.UtcNow,
                LastAccessedAt = DateTime.UtcNow
            };

            _conversationCache[conversationId] = state;
            _chatCacheService.Set(conversationId, new ChatConversationState
            {
                Messages = state.Messages,
                Conversation = state.Conversation,
                OtherUserName = state.OtherUserName,
                OtherUserPhoto = state.OtherUserPhoto,
                CachedAt = state.CachedAt,
                LastAccessedAt = state.LastAccessedAt
            });
            KamPay.Helpers.AppLogger.DebugLog($"ÄŸÅ¸â€™Â¾ Cache'e kaydedildi: {conversationId} ({state.Messages.Count} mesaj)");
        }


        //  Cache'den geri yÃƒÂ¼kleme
        private void RestoreFromCache(ConversationState state)
        {
            // Cache yaÃ…Å¸Ã„Â±nÃ„Â± kontrol et
            if ((DateTime.UtcNow - state.CachedAt).TotalMinutes > MaxCacheAgeMinutes)
            {
                KamPay.Helpers.AppLogger.DebugLog("Ã¢Å¡Â Ã¯Â¸Â Cache eski, yeniden yÃƒÂ¼kleniyor...");
                _conversationCache.Remove(ConversationId);
                _initialLoadComplete = false;
                _ = Task.Run(() => LoadChatAsync());
                return;
            }

            // Last accessed time gÃƒÂ¼ncelle (LRU iÃƒÂ§in)
            state.LastAccessedAt = DateTime.UtcNow;

            Messages.Clear();
            _knownMessageIds.Clear();
            _messageLookup.Clear();
            foreach (var msg in state.Messages)
            {
                Messages.Add(msg);
                TrackKnownMessage(msg);
            }

            Conversation = state.Conversation;
            OtherUserName = state.OtherUserName;
            OtherUserPhoto = state.OtherUserPhoto;
// Listener'Ã„Â± yeniden baÃ…Å¸lat
            StartListeningToMessages();
            StartListeningToTyping();

            KamPay.Helpers.AppLogger.DebugLog($"Ã¢Å“â€¦ Cache'den geri yÃƒÂ¼klendi: {Messages.Count} mesaj");
        }


        //  Mevcut konuÃ…Å¸mayÃ„Â± temizle
        private void CleanupCurrentConversation()
        {
            _chatRealtimeService.StopListening();
            _isListenerActive = false;
            _initialLoadComplete = false;
            _knownMessageIds.Clear();
            _messageLookup.Clear();

            _typingSubscription?.Dispose();
            _typingSubscription = null;
        }


        //  Otomatik cache temizleme
        private static void CleanupOldCache()
        {
            var now = DateTime.UtcNow;
            var oldKeys = _conversationCache
                .Where(kvp => (now - kvp.Value.CachedAt).TotalMinutes > MaxCacheAgeMinutes)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in oldKeys)
            {
                _conversationCache.Remove(key);
            }

            if (oldKeys.Any())
            {
                KamPay.Helpers.AppLogger.DebugLog($"ÄŸÅ¸â€”â€˜Ã¯Â¸Â {oldKeys.Count} eski cache otomatik temizlendi");
            }
        }


        // Public helper metodlar
        public static void ClearCache()
        {
            _conversationCache.Clear();
            KamPay.Helpers.AppLogger.DebugLog("Ã¢Å“â€¦ TÃƒÂ¼m chat cache temizlendi");
        }


        public static void ClearOldCache(int maxAgeMinutes = 30)
        {
            var now = DateTime.UtcNow;
            var oldKeys = _conversationCache
                .Where(kvp => (now - kvp.Value.CachedAt).TotalMinutes > maxAgeMinutes)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in oldKeys)
            {
                _conversationCache.Remove(key);
            }

            // Ã¢Å“â€¦ EKLEME: Boyut limiti kontrolÃƒÂ¼
            int removedOldestCount = 0;
            if (_conversationCache.Count > MaxCachedConversations)
            {
                var oldestItems = _conversationCache
                    .OrderBy(kvp => kvp.Value.CachedAt)
                    .Take(_conversationCache.Count - MaxCachedConversations)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in oldestItems)
                {
                    _conversationCache.Remove(key);
                }

                removedOldestCount = oldestItems.Count;
            }

            if (oldKeys.Any() || removedOldestCount > 0)
            {
                KamPay.Helpers.AppLogger.DebugLog($"Ã¢Å“â€¦ Cache temizlendi: {oldKeys.Count} eski, {removedOldestCount} fazla ÃƒÂ¶Ã„Å¸e silindi");
            }
        }


    //  : Cache state modeli
    public class ConversationState
    {
        public required List<Message> Messages { get; set; }
        public Conversation? Conversation { get; set; }
        public required string OtherUserName { get; set; }
        public required string OtherUserPhoto { get; set; }
        public DateTime CachedAt { get; set; }
        public DateTime LastAccessedAt { get; set; } //  LRU iÃƒÂ§in
    }

    }
}
