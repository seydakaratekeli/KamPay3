using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KamPay.Models;
using KamPay.Services;
using System.Collections.ObjectModel;

namespace KamPay.ViewModels
{
    /// <summary>
    /// Fiyat teklifi listesi ve yönetimi için ViewModel
    /// </summary>
    public partial class PriceQuotesViewModel : ObservableObject
    {
        private readonly IPriceQuoteService _priceQuoteService;
        private readonly IUserStateService _userStateService;
        private readonly INotificationService _notificationService;

        [ObservableProperty]
        private ObservableCollection<PriceQuote> receivedQuotes = new();

        [ObservableProperty]
        private ObservableCollection<PriceQuote> sentQuotes = new();

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private bool isRefreshing;

        [ObservableProperty]
        private int selectedTabIndex = 0; // 0: Alınan, 1: Gönderilen

        [ObservableProperty]
        private string searchText = string.Empty;

        [ObservableProperty]
        private PriceQuoteStatus? filterStatus;

        [ObservableProperty]
        private int unreadQuoteCount;

        public PriceQuotesViewModel(
            IPriceQuoteService priceQuoteService,
            IUserStateService userStateService,
            INotificationService notificationService)
        {
            _priceQuoteService = priceQuoteService;
            _userStateService = userStateService;
            _notificationService = notificationService;
        }

        [RelayCommand]
        private async Task LoadQuotesAsync()
        {
            if (IsLoading) return;

            try
            {
                IsLoading = true;
                var userId = _userStateService.CurrentUserId;

                if (string.IsNullOrEmpty(userId))
                    return;

                // Alınan teklifler (satıcı olarak)
                var received = await _priceQuoteService.GetReceivedQuotesAsync(userId);
                ReceivedQuotes = new ObservableCollection<PriceQuote>(received ?? new List<PriceQuote>());

                // Gönderilen teklifler (alıcı olarak)
                var sent = await _priceQuoteService.GetSentQuotesAsync(userId);
                SentQuotes = new ObservableCollection<PriceQuote>(sent ?? new List<PriceQuote>());

                // Okunmamış teklif sayısı
                UnreadQuoteCount = await _priceQuoteService.GetUnreadQuoteCountAsync(userId);
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", $"Teklifler yüklenirken hata: {ex.Message}", "Tamam");
            }
            finally
            {
                IsLoading = false;
                IsRefreshing = false;
            }
        }

        [RelayCommand]
        private async Task RefreshQuotesAsync()
        {
            IsRefreshing = true;
            await LoadQuotesAsync();
        }

        [RelayCommand]
        private async Task AcceptQuoteAsync(PriceQuote quote)
        {
            if (quote == null) return;

            var confirm = await Shell.Current.DisplayAlert(
                "Teklifi Kabul Et",
                $"{quote.BuyerName} kullanıcısının {quote.QuotedPrice:N2} ₺ teklifini kabul etmek istiyor musunuz?",
                "Evet", "Hayır");

            if (!confirm) return;

            try
            {
                IsLoading = true;
                var userId = _userStateService.CurrentUserId;
                var result = await _priceQuoteService.AcceptQuoteAsync(userId, quote.QuoteId);

                if (result.IsValid)
                {
                    await Shell.Current.DisplayAlert("Başarılı", "Teklif kabul edildi! ✅", "Tamam");
                    await LoadQuotesAsync();
                }
                else
                {
                    await Shell.Current.DisplayAlert("Hata", string.Join("\n", result.Errors), "Tamam");
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", $"Teklif kabul edilirken hata: {ex.Message}", "Tamam");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task RejectQuoteAsync(PriceQuote quote)
        {
            if (quote == null) return;

            var reason = await Shell.Current.DisplayPromptAsync(
                "Teklifi Reddet",
                "Red nedeni (isteğe bağlı):",
                "Reddet", "İptal",
                placeholder: "Örn: Fiyat çok düşük");

            if (reason == null) return; // İptal edildi

            try
            {
                IsLoading = true;
                var userId = _userStateService.CurrentUserId;
                var result = await _priceQuoteService.RejectQuoteAsync(userId, quote.QuoteId, reason);

                if (result.IsValid)
                {
                    await Shell.Current.DisplayAlert("Başarılı", "Teklif reddedildi", "Tamam");
                    await LoadQuotesAsync();
                }
                else
                {
                    await Shell.Current.DisplayAlert("Hata", string.Join("\n", result.Errors), "Tamam");
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", $"Teklif reddedilirken hata: {ex.Message}", "Tamam");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task MakeCounterOfferAsync(PriceQuote quote)
        {
            if (quote == null) return;

            var priceStr = await Shell.Current.DisplayPromptAsync(
                "Karşı Teklif Yap",
                $"Karşı teklif fiyatınız (Orijinal: {quote.OriginalPrice:N2} ₺, Teklif: {quote.QuotedPrice:N2} ₺):",
                "Gönder", "İptal",
                placeholder: "Örn: 150",
                keyboard: Keyboard.Numeric);

            if (string.IsNullOrEmpty(priceStr)) return;

            if (!decimal.TryParse(priceStr, out var counterPrice))
            {
                await Shell.Current.DisplayAlert("Hata", "Geçerli bir fiyat girin", "Tamam");
                return;
            }

            var message = await Shell.Current.DisplayPromptAsync(
                "Mesaj",
                "Karşı teklifinizle birlikte bir mesaj ekleyin (isteğe bağlı):",
                "Gönder", "İptal",
                placeholder: "Örn: Bu fiyat benim için daha uygun");

            try
            {
                IsLoading = true;
                var userId = _userStateService.CurrentUserId;
                
                var request = new CounterOfferRequest
                {
                    QuoteId = quote.QuoteId,
                    CounterOfferPrice = counterPrice,
                    Message = message ?? string.Empty
                };

                var result = await _priceQuoteService.MakeCounterOfferAsync(userId, request);

                if (result.IsValid)
                {
                    await Shell.Current.DisplayAlert("Başarılı", "Karşı teklif gönderildi! 💬", "Tamam");
                    await LoadQuotesAsync();
                }
                else
                {
                    await Shell.Current.DisplayAlert("Hata", string.Join("\n", result.Errors), "Tamam");
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", $"Karşı teklif gönderilirken hata: {ex.Message}", "Tamam");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task AcceptCounterOfferAsync(PriceQuote quote)
        {
            if (quote == null || !quote.CounterOfferPrice.HasValue) return;

            var confirm = await Shell.Current.DisplayAlert(
                "Karşı Teklifi Kabul Et",
                $"{quote.SellerName} kullanıcısının {quote.CounterOfferPrice:N2} ₺ karşı teklifini kabul ediyor musunuz?",
                "Evet", "Hayır");

            if (!confirm) return;

            try
            {
                IsLoading = true;
                var userId = _userStateService.CurrentUserId;
                var result = await _priceQuoteService.AcceptCounterOfferAsync(userId, quote.QuoteId);

                if (result.IsValid)
                {
                    await Shell.Current.DisplayAlert("Başarılı", "Karşı teklif kabul edildi! 🎉", "Tamam");
                    await LoadQuotesAsync();
                }
                else
                {
                    await Shell.Current.DisplayAlert("Hata", string.Join("\n", result.Errors), "Tamam");
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", $"Karşı teklif kabul edilirken hata: {ex.Message}", "Tamam");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task RejectCounterOfferAsync(PriceQuote quote)
        {
            if (quote == null) return;

            var confirm = await Shell.Current.DisplayAlert(
                "Karşı Teklifi Reddet",
                $"{quote.SellerName} kullanıcısının karşı teklifini reddetmek istiyor musunuz?",
                "Evet", "Hayır");

            if (!confirm) return;

            try
            {
                IsLoading = true;
                var userId = _userStateService.CurrentUserId;
                var result = await _priceQuoteService.RejectCounterOfferAsync(userId, quote.QuoteId);

                if (result.IsValid)
                {
                    await Shell.Current.DisplayAlert("Başarılı", "Karşı teklif reddedildi", "Tamam");
                    await LoadQuotesAsync();
                }
                else
                {
                    await Shell.Current.DisplayAlert("Hata", string.Join("\n", result.Errors), "Tamam");
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", $"Karşı teklif reddedilirken hata: {ex.Message}", "Tamam");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task CancelQuoteAsync(PriceQuote quote)
        {
            if (quote == null) return;

            var confirm = await Shell.Current.DisplayAlert(
                "Teklifi İptal Et",
                "Bu teklifi iptal etmek istiyor musunuz?",
                "Evet", "Hayır");

            if (!confirm) return;

            try
            {
                IsLoading = true;
                var userId = _userStateService.CurrentUserId;
                var result = await _priceQuoteService.CancelQuoteAsync(userId, quote.QuoteId);

                if (result.IsValid)
                {
                    await Shell.Current.DisplayAlert("Başarılı", "Teklif iptal edildi", "Tamam");
                    await LoadQuotesAsync();
                }
                else
                {
                    await Shell.Current.DisplayAlert("Hata", string.Join("\n", result.Errors), "Tamam");
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", $"Teklif iptal edilirken hata: {ex.Message}", "Tamam");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task MarkAsReadAsync(PriceQuote quote)
        {
            if (quote == null || quote.IsRead) return;

            await _priceQuoteService.MarkAsReadAsync(quote.QuoteId);
            quote.IsRead = true;
            UnreadQuoteCount = await _priceQuoteService.GetUnreadQuoteCountAsync(_userStateService.CurrentUserId);
        }

        [RelayCommand]
        private async Task ViewQuoteDetailsAsync(PriceQuote quote)
        {
            if (quote == null) return;

            await MarkAsReadAsync(quote);
            
            // Teklif detaylarını göster
            var details = $"📦 {quote.ReferenceTitle}\n\n" +
                         $"💰 Orijinal Fiyat: {quote.OriginalPrice:N2} ₺\n" +
                         $"💵 Teklif: {quote.QuotedPrice:N2} ₺\n";

            if (quote.CounterOfferPrice.HasValue)
            {
                details += $"🔄 Karşı Teklif: {quote.CounterOfferPrice:N2} ₺\n";
            }

            details += $"\n📅 {quote.TimeAgoText}\n" +
                      $"📊 Durum: {quote.StatusText}";

            if (!string.IsNullOrEmpty(quote.Message))
            {
                details += $"\n\n💬 Mesaj: {quote.Message}";
            }

            if (!string.IsNullOrEmpty(quote.CounterOfferMessage))
            {
                details += $"\n\n💬 Karşı Teklif Mesajı: {quote.CounterOfferMessage}";
            }

            await Shell.Current.DisplayAlert("Teklif Detayları", details, "Tamam");
        }

        [RelayCommand]
        private void FilterByStatus(PriceQuoteStatus? status)
        {
            FilterStatus = status;
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            // TODO: Filtreleme mantığı eklenebilir
        }
    }
}
