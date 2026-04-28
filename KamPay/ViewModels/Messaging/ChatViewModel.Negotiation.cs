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
        private Task SelectTransactionChipAsync(Transaction transaction)
        {
            if (transaction == null) return Task.CompletedTask;

            ActiveTransaction = transaction;
            HasActiveTransaction = true;
            selectedTransactionId = transaction.TransactionId ?? string.Empty;

            SelectedFilterTransactionId = transaction.TransactionId ?? NullTransactionFilter;
            ShowAllChipSelected = false;

            foreach (var t in ActiveTransactions)
                t.IsChipSelected = (t.TransactionId == transaction.TransactionId);

            OnPropertyChanged(nameof(FilteredMessages));
            WeakReferenceMessenger.Default.Send(new ScrollToChatMessage(null));

            return Task.CompletedTask;
        }


        // âœ… BUG-3/4 FIX: Hem alÄ±cÄ± hem satÄ±cÄ± transaction'larÄ±nÄ± yÃ¼kle
        // AynÄ± conversation'da birden fazla Ã¼rÃ¼n iÃ§in aktif pazarlÄ±k desteklenir
        private async Task LoadActiveTransactionsAsync(string currentUserId)
        {
            try
            {
                ActiveTransactions.Clear();
                var allActive = new System.Collections.Generic.List<Transaction>();

                // 1. AlÄ±cÄ± olarak transaction'lar
                var myOffersResult = await _transactionService.GetMyOffersAsync(currentUserId);
                if (myOffersResult.Success && myOffersResult.Data != null)
                {
                    allActive.AddRange(myOffersResult.Data.Where(t =>
                        t.ConversationId == ConversationId &&
                        (t.Status == TransactionStatus.Pending || t.IsNegotiating)));
                }

                // 2. SatÄ±cÄ± olarak transaction'lar (BUG-4 FIX: satÄ±cÄ± chat'te gÃ¶remiyordu)
                try
                {
                    var sellerSnapshot = await _firebaseClient
                        .Child(Constants.TransactionsCollection)
                        .OrderBy("SellerId")
                        .EqualTo(currentUserId)
                        .OnceAsync<Transaction>();

                    var sellerTransactions = sellerSnapshot
                        .Where(s => s.Object != null &&
                                    s.Object.ConversationId == ConversationId &&
                                    (s.Object.Status == TransactionStatus.Pending || s.Object.IsNegotiating))
                        .Select(s => { s.Object.TransactionId = s.Key; return s.Object; })
                        .ToList();

                    allActive.AddRange(sellerTransactions);
                }
                catch (Exception sellerEx)
                {
                    KamPay.Helpers.AppLogger.DebugLog($"âš ï¸ SatÄ±cÄ± transaction'larÄ± alÄ±namadÄ±: {sellerEx.Message}");
                }

                // Fallback: ConversationId ile bulunamazsa ProductId ile ara
                if (!allActive.Any() && Conversation != null && !string.IsNullOrEmpty(Conversation.ProductId))
                {
                    var fallback = myOffersResult?.Data?.FirstOrDefault(t =>
                        t.ProductId == Conversation.ProductId &&
                        (t.Status == TransactionStatus.Pending || t.IsNegotiating));
                    if (fallback != null) allActive.Add(fallback);
                }

                if (allActive.Any())
                {
                    // DuplikasyonlarÄ± kaldÄ±r (TransactionId bazÄ±nda)
                    var distinct = allActive
                        .GroupBy(t => t.TransactionId)
                        .Select(g => g.First())
                        .ToList();

                    ActiveTransactions.ReplaceRange(distinct);
                    HasActiveTransactions = true;

                    // KullanÄ±cÄ± seÃ§im yaptÄ±ysa onu koru, yoksa ilk transaction'Ä± seÃ§
                    var selected = !string.IsNullOrWhiteSpace(selectedTransactionId)
                        ? distinct.FirstOrDefault(t => t.TransactionId == selectedTransactionId)
                        : null;

                    ActiveTransaction = selected ?? distinct.First();
                    HasActiveTransaction = ActiveTransaction != null;
                    selectedTransactionId = ActiveTransaction?.TransactionId ?? string.Empty;

                    KamPay.Helpers.AppLogger.DebugLog($"âœ… ActiveTransactions yÃ¼klendi: {distinct.Count} transaction");
                }
                else
                {
                    HasActiveTransactions = false;
                    HasActiveTransaction = false;
                    ActiveTransaction = null;
                    selectedTransactionId = string.Empty;
                    KamPay.Helpers.AppLogger.DebugLog($"âš ï¸ Bu konuÅŸmaya baÄŸlÄ± aktif iÅŸlem bulunamadÄ±: {ConversationId}");
                }
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âš ï¸ LoadActiveTransactionsAsync hatasÄ±: {ex.Message}");
            }
        }


        [RelayCommand]
        private Task SelectActiveTransactionAsync(Transaction transaction)
        {
            if (transaction == null)
                return Task.CompletedTask;

            ActiveTransaction = transaction;
            HasActiveTransaction = true;
            selectedTransactionId = transaction.TransactionId ?? string.Empty;

            return Task.CompletedTask;
        }

        private async Task<Transaction?> ResolveTransactionAsync(string? relatedTransactionId)
        {
            Transaction? targetTransaction = null;

            if (!string.IsNullOrEmpty(relatedTransactionId))
            {
                // 1. Önce hem alıcı hem satıcı kayıtlarını barındıran yerel listede ara
                targetTransaction = ActiveTransactions.FirstOrDefault(t => t.TransactionId == relatedTransactionId);

                // 2. Bulunamazsa servisten kendi tekliflerinizi (Alıcı olarak) kontrol et
                if (targetTransaction == null && _currentUser != null)
                {
                    var myOffersResult = await _transactionService.GetMyOffersAsync(_currentUser.UserId);
                    if (myOffersResult.Success && myOffersResult.Data != null)
                        targetTransaction = myOffersResult.Data.FirstOrDefault(t => t.TransactionId == relatedTransactionId);
                }

                // 3. Hala bulunamazsa servisten gelen teklifleri (Satıcı olarak) kontrol et
                if (targetTransaction == null && _currentUser != null)
                {
                    var incomingResult = await _transactionService.GetIncomingOffersAsync(_currentUser.UserId);
                    if (incomingResult.Success && incomingResult.Data != null)
                        targetTransaction = incomingResult.Data.FirstOrDefault(t => t.TransactionId == relatedTransactionId);
                }
            }

            // 4. Hala bulunamadıysa (veya relatedTransactionId null ise) seçili/ilk aktif işlemi kullan
            if (targetTransaction == null)
                targetTransaction = ActiveTransactions.FirstOrDefault() ?? ActiveTransaction;

            if (targetTransaction == null)
            {
                await Application.Current!.MainPage!.DisplayAlert("Hata",
                    "Aktif işlem (transaction) yüklenemedi. Lütfen sayfayı yenileyin veya tekrar girin.", "Tamam");
                return null;
            }

            // ✅ FAZ 4: ProductId cross-check — teklif doğru ürüne mi ait?
            if (Conversation != null &&
                !string.IsNullOrEmpty(Conversation.ProductId) &&
                !string.IsNullOrEmpty(targetTransaction.ProductId) &&
                targetTransaction.ProductId != Conversation.ProductId)
            {
                await Application.Current!.MainPage!.DisplayAlert("Hata",
                    "Bu teklif farklı bir ürüne ait. Lütfen doğru sohbet penceresinden işlem yapın.", "Tamam");
                AppLogger.DebugLog($"⚠️ FAZ4 Cross-check başarısız: Transaction.ProductId={targetTransaction.ProductId}, Conversation.ProductId={Conversation.ProductId}");
                return null;
            }

            return targetTransaction;
        }


        [RelayCommand]
        private async Task ProposeOfferAsync(Message message)
        {
            if (message == null) return;

            var targetTransaction = await ResolveTransactionAsync(message.RelatedTransactionId);
            if (targetTransaction == null) return;

            if (_currentUser == null) return;

            try
            {
                var result = await Application.Current!.MainPage!.DisplayPromptAsync(
                    $"💰 Fiyat Teklifi ({targetTransaction.ProductTitle})",
                    "Teklif etmek istediğiniz tutarı girin (₺):",
                    Res["SendButton"] ?? "Gönder",
                    Res["Cancel"] ?? "İptal",
                    "Örn: 500",
                    keyboard: Keyboard.Numeric
                );

                if (string.IsNullOrWhiteSpace(result)) return;

                if (!decimal.TryParse(result, out var proposedPrice) || proposedPrice <= 0)
                {
                    await Application.Current.MainPage.DisplayAlert("Hata", "Geçerli bir tutar giriniz.", "Tamam");
                    return;
                }

                IsLoading = true;

                if (targetTransaction.SellerId == _currentUser.UserId)
                {
                    // Satıcı ise CounterOffer gönder
                    var response = await _transactionService.SendCounterOfferForSaleAsync(
                        targetTransaction.TransactionId, proposedPrice, _currentUser.UserId);
                    if (!response.Success)
                        await Application.Current.MainPage.DisplayAlert("Hata", response.Message, "Tamam");
                }
                else
                {
                    // Alıcı ise ProposePrice gönder
                    var response = await _transactionService.ProposePriceForSaleAsync(
                        targetTransaction.TransactionId, proposedPrice, _currentUser.UserId);
                    if (!response.Success)
                        await Application.Current.MainPage.DisplayAlert("Hata", response.Message, "Tamam");
                }

                await LoadChatAsync();
            }
            catch (Exception ex)
            {
                await Application.Current!.MainPage!.DisplayAlert("Hata", ex.Message, "Tamam");
            }
            finally
            {
                IsLoading = false;
            }
        }


        [RelayCommand]
        private async Task AcceptOfferAsync(Message message)
        {
            if (message == null) return;

            var targetTransaction = await ResolveTransactionAsync(message.RelatedTransactionId);
            if (targetTransaction == null) return;

            if (_currentUser == null) return;

            if (targetTransaction.Status != TransactionStatus.Pending)
            {
                await Application.Current!.MainPage!.DisplayAlert("Uyarı",
                    "Bu işlem artık beklemede değil (Durum: " + targetTransaction.StatusText + ").", "Tamam");
                return;
            }

            try
            {
                var confirm = await Application.Current!.MainPage!.DisplayAlert(
                    $"Onay ({targetTransaction.ProductTitle})",
                    $"{message.ProposedPrice:N2}₺ teklifi kabul etmek istediğinize emin misiniz?",
                    "Kabul Et",
                    "İptal"
                );

                if (!confirm) return;

                IsLoading = true;

                var result = await _transactionService.AcceptNegotiatedPriceAsync(
                    targetTransaction.TransactionId, _currentUser.UserId);

                if (result.Success)
                    await LoadChatAsync();
                else
                    await Application.Current.MainPage.DisplayAlert("Hata", result.Message, "Tamam");
            }
            catch (Exception ex)
            {
                await Application.Current!.MainPage!.DisplayAlert("Hata", ex.Message, "Tamam");
            }
            finally
            {
                IsLoading = false;
            }
        }

    }
}
