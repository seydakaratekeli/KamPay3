using System.Collections;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Helpers;
using KamPay.Models;
using KamPay.Services;
using KamPay.Services.Auth;
using KamPay.Services.Messaging;
using KamPay.Services.Transactions;

namespace KamPay.ViewModels
{
    [QueryProperty(nameof(ConversationId), "conversationId")]
    [QueryProperty(nameof(OtherUserPhoto), "otherUserPhoto")]
    [QueryProperty(nameof(OtherUserName), "otherUserName")]
    public partial class NegotiationChatViewModel : ObservableObject, IDisposable
    {
        private const string NullTransactionFilter = "__ALL__";

        private readonly ChatViewModel _chat;
        private readonly IAuthenticationService _authService;
        private readonly ITransactionService _transactionService;
        private readonly FirebaseClient _firebaseClient;
        private readonly INegotiationChatService _negotiationChatService;
        private User? _currentUser;
        private string selectedTransactionId = string.Empty;
        private bool _disposed;

        public NegotiationChatViewModel(
            ChatViewModel chat,
            IAuthenticationService authService,
            ITransactionService transactionService,
            FirebaseClient firebaseClient,
            INegotiationChatService negotiationChatService)
        {
            _chat = chat;
            _authService = authService;
            _transactionService = transactionService;
            _firebaseClient = firebaseClient;
            _negotiationChatService = negotiationChatService;

            _chat.PropertyChanged += (_, e) =>
            {
                OnPropertyChanged(e.PropertyName);
                if (e.PropertyName == nameof(IsLoading))
                {
                    OnPropertyChanged(nameof(IsLoading));
                }
            };

            _chat.Messages.CollectionChanged += OnMessagesChanged;
        }

        public ObservableRangeCollection<Message> Messages => _chat.Messages;
        public Conversation? Conversation => _chat.Conversation;
        public ObservableRangeCollection<Transaction> ActiveTransactions { get; } = new();

        public IAsyncRelayCommand LoadOlderMessagesCommand => _chat.LoadOlderMessagesCommand;
        public IAsyncRelayCommand RefreshMessagesCommand => _chat.RefreshMessagesCommand;
        public IAsyncRelayCommand SendMessageCommand => _chat.SendMessageCommand;
        public IAsyncRelayCommand PickImageCommand => _chat.PickImageCommand;
        public IAsyncRelayCommand TakePhotoCommand => _chat.TakePhotoCommand;
        public IAsyncRelayCommand<string> ViewImageCommand => _chat.ViewImageCommand;
        public IAsyncRelayCommand GoBackCommand => _chat.GoBackCommand;

        public string ConversationId
        {
            get => _chat.ConversationId;
            set
            {
                if (_chat.ConversationId == value)
                    return;

                _chat.ConversationId = value;
                OnPropertyChanged();
                _ = LoadNegotiationContextAsync();
            }
        }

        public string MessageText
        {
            get => _chat.MessageText;
            set
            {
                if (_chat.MessageText == value)
                    return;

                _chat.MessageText = value;
                OnPropertyChanged();
            }
        }

        public string OtherUserPhoto
        {
            get => _chat.OtherUserPhoto;
            set
            {
                if (_chat.OtherUserPhoto == value)
                    return;

                _chat.OtherUserPhoto = value;
                OnPropertyChanged();
            }
        }

        public string OtherUserName
        {
            get => _chat.OtherUserName;
            set
            {
                if (_chat.OtherUserName == value)
                    return;

                _chat.OtherUserName = value;
                OnPropertyChanged();
            }
        }

        public bool IsLoading
        {
            get => _chat.IsLoading || IsNegotiationLoading;
            set => _chat.IsLoading = value;
        }

        public bool IsRefreshing => _chat.IsRefreshing;
        public bool IsSending => _chat.IsSending;
        public bool IsUploadingImage => _chat.IsUploadingImage;
        public string? SelectedImagePath => _chat.SelectedImagePath;
        public bool IsOtherUserTyping => _chat.IsOtherUserTyping;
        public bool IsOtherUserOnline => _chat.IsOtherUserOnline;
        public string OnlineStatusText => _chat.OnlineStatusText;
        public bool IsNegotiationChat => true;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsLoading))]
        private bool isNegotiationLoading;

        [ObservableProperty]
        private Transaction? activeTransaction;

        [ObservableProperty]
        private bool hasActiveTransaction;

        [ObservableProperty]
        private bool hasActiveTransactions;

        private string _selectedFilterTransactionId = NullTransactionFilter;
        public string SelectedFilterTransactionId
        {
            get => _selectedFilterTransactionId;
            set
            {
                if (SetProperty(ref _selectedFilterTransactionId, value))
                {
                    OnPropertyChanged(nameof(FilteredMessages));
                }
            }
        }

        private bool _showAllChipSelected = true;
        public bool ShowAllChipSelected
        {
            get => _showAllChipSelected;
            set => SetProperty(ref _showAllChipSelected, value);
        }

        public IEnumerable FilteredMessages =>
            SelectedFilterTransactionId == NullTransactionFilter
                ? Messages
                : Messages.Where(m =>
                    m.RelatedTransactionId == SelectedFilterTransactionId ||
                    m.Type == MessageType.System ||
                    m.Type == MessageType.Negotiation);

        public void PauseRealtimeListeners() => _chat.PauseRealtimeListeners();

        public void ResumeRealtimeListeners()
        {
            _chat.ResumeRealtimeListeners();
            _ = LoadNegotiationContextAsync();
        }

        [RelayCommand]
        private void SelectAllMessages()
        {
            SelectedFilterTransactionId = NullTransactionFilter;
            ShowAllChipSelected = true;

            foreach (var transaction in ActiveTransactions)
            {
                transaction.IsChipSelected = false;
            }

            OnPropertyChanged(nameof(FilteredMessages));
        }

        [RelayCommand]
        private Task SelectTransactionChipAsync(Transaction transaction)
        {
            if (transaction == null)
                return Task.CompletedTask;

            ActiveTransaction = transaction;
            HasActiveTransaction = true;
            selectedTransactionId = transaction.TransactionId ?? string.Empty;
            SelectedFilterTransactionId = transaction.TransactionId ?? NullTransactionFilter;
            ShowAllChipSelected = false;

            foreach (var item in ActiveTransactions)
            {
                item.IsChipSelected = item.TransactionId == transaction.TransactionId;
            }

            OnPropertyChanged(nameof(FilteredMessages));
            return Task.CompletedTask;
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

        [RelayCommand]
        private async Task ProposeOfferAsync(Message? message)
        {
            var targetTransaction = await ResolveTransactionAsync(message?.RelatedTransactionId);
            if (targetTransaction == null)
                return;

            _currentUser ??= await _authService.GetCurrentUserAsync();
            if (_currentUser == null)
                return;

            try
            {
                var result = await Application.Current!.MainPage!.DisplayPromptAsync(
                    $"Fiyat Teklifi ({targetTransaction.ProductTitle})",
                    "Teklif etmek istediginiz tutari girin:",
                    "Gonder",
                    "Iptal",
                    "Orn: 500",
                    keyboard: Keyboard.Numeric);

                if (string.IsNullOrWhiteSpace(result))
                    return;

                if (!decimal.TryParse(result, out var proposedPrice) || proposedPrice <= 0)
                {
                    await Application.Current.MainPage.DisplayAlert("Hata", "Gecerli bir tutar giriniz.", "Tamam");
                    return;
                }

                IsNegotiationLoading = true;
                var response = await _negotiationChatService.ProposeOfferAsync(
                    targetTransaction.TransactionId,
                    proposedPrice,
                    _currentUser.UserId);

                if (!response.Success)
                {
                    await Application.Current.MainPage.DisplayAlert("Hata", response.Message, "Tamam");
                    return;
                }

                await RefreshAfterNegotiationActionAsync();
            }
            catch (Exception ex)
            {
                await Application.Current!.MainPage!.DisplayAlert("Hata", ex.Message, "Tamam");
            }
            finally
            {
                IsNegotiationLoading = false;
            }
        }

        [RelayCommand]
        private async Task AcceptOfferAsync(Message message)
        {
            if (message == null)
                return;

            var targetTransaction = await ResolveTransactionAsync(message.RelatedTransactionId);
            if (targetTransaction == null)
                return;

            _currentUser ??= await _authService.GetCurrentUserAsync();
            if (_currentUser == null)
                return;

            if (!message.IsActiveOffer)
            {
                await Application.Current!.MainPage!.DisplayAlert("Bilgi", "Bu teklif artik gecerli degil.", "Tamam");
                return;
            }

            if (message.SenderId == _currentUser.UserId || targetTransaction.LastActionBy == _currentUser.UserId)
            {
                await Application.Current!.MainPage!.DisplayAlert("Bilgi", "Kendi teklifinizi kabul edemezsiniz.", "Tamam");
                return;
            }

            if (targetTransaction.Status != TransactionStatus.Pending &&
                targetTransaction.Status != TransactionStatus.Negotiating)
            {
                await Application.Current!.MainPage!.DisplayAlert("Uyari", "Bu islem artik beklemede degil.", "Tamam");
                return;
            }

            var confirm = await Application.Current!.MainPage!.DisplayAlert(
                $"Onay ({targetTransaction.ProductTitle})",
                $"{message.ProposedPrice:N2} teklifini kabul etmek istediginize emin misiniz?",
                "Kabul Et",
                "Iptal");

            if (!confirm)
                return;

            try
            {
                IsNegotiationLoading = true;
                var result = await _negotiationChatService.AcceptOfferAsync(
                    targetTransaction.TransactionId,
                    _currentUser.UserId);

                if (!result.Success)
                {
                    await Application.Current.MainPage.DisplayAlert("Hata", result.Message, "Tamam");
                    return;
                }

                await RefreshAfterNegotiationActionAsync();
            }
            finally
            {
                IsNegotiationLoading = false;
            }
        }

        [RelayCommand]
        private async Task RejectOfferAsync(Message message)
        {
            if (message == null)
                return;

            var targetTransaction = await ResolveTransactionAsync(message.RelatedTransactionId);
            if (targetTransaction == null)
                return;

            _currentUser ??= await _authService.GetCurrentUserAsync();
            if (_currentUser == null)
                return;

            try
            {
                IsNegotiationLoading = true;
                var result = await _negotiationChatService.RejectOfferAsync(
                    targetTransaction.TransactionId,
                    _currentUser.UserId);

                if (!result.Success)
                {
                    await Application.Current!.MainPage!.DisplayAlert("Hata", result.Message, "Tamam");
                    return;
                }

                await RefreshAfterNegotiationActionAsync();
            }
            finally
            {
                IsNegotiationLoading = false;
            }
        }

        private async Task LoadNegotiationContextAsync()
        {
            if (string.IsNullOrWhiteSpace(ConversationId))
                return;

            try
            {
                _currentUser ??= await _authService.GetCurrentUserAsync();
                if (_currentUser == null)
                    return;

                await LoadActiveTransactionsAsync(_currentUser.UserId);
            }
            catch (Exception ex)
            {
                AppLogger.DebugLog($"Negotiation context yuklenemedi: {ex.Message}");
            }
        }

        private async Task LoadActiveTransactionsAsync(string currentUserId)
        {
            ActiveTransactions.Clear();
            var allActive = new List<Transaction>();

            var myOffersResult = await _transactionService.GetMyOffersAsync(currentUserId);
            if (myOffersResult.Success && myOffersResult.Data != null)
            {
                allActive.AddRange(myOffersResult.Data.Where(t =>
                    t.ConversationId == ConversationId &&
                    (t.Status == TransactionStatus.Pending || t.IsNegotiating)));
            }

            var sellerSnapshot = await _firebaseClient
                .Child(Constants.TransactionsCollection)
                .OrderBy("SellerId")
                .EqualTo(currentUserId)
                .OnceAsync<Transaction>();

            allActive.AddRange(sellerSnapshot
                .Where(s => s.Object != null &&
                            s.Object.ConversationId == ConversationId &&
                            (s.Object.Status == TransactionStatus.Pending || s.Object.IsNegotiating))
                .Select(s =>
                {
                    s.Object.TransactionId = s.Key;
                    return s.Object;
                }));

            var distinct = allActive
                .Where(t => !string.IsNullOrWhiteSpace(t.TransactionId))
                .GroupBy(t => t.TransactionId)
                .Select(g => g.First())
                .ToList();

            if (distinct.Count == 0)
            {
                HasActiveTransactions = false;
                HasActiveTransaction = false;
                ActiveTransaction = null;
                selectedTransactionId = string.Empty;
                OnPropertyChanged(nameof(FilteredMessages));
                return;
            }

            ActiveTransactions.ReplaceRange(distinct);
            HasActiveTransactions = true;

            ActiveTransaction = !string.IsNullOrWhiteSpace(selectedTransactionId)
                ? distinct.FirstOrDefault(t => t.TransactionId == selectedTransactionId) ?? distinct.First()
                : distinct.First();

            HasActiveTransaction = ActiveTransaction != null;
            selectedTransactionId = ActiveTransaction?.TransactionId ?? string.Empty;
            OnPropertyChanged(nameof(FilteredMessages));
        }

        private async Task<Transaction?> ResolveTransactionAsync(string? relatedTransactionId)
        {
            if (ActiveTransactions.Count == 0)
            {
                await LoadNegotiationContextAsync();
            }

            var targetTransaction = !string.IsNullOrWhiteSpace(relatedTransactionId)
                ? ActiveTransactions.FirstOrDefault(t => t.TransactionId == relatedTransactionId)
                : null;

            targetTransaction ??= ActiveTransaction ?? ActiveTransactions.FirstOrDefault();
            if (targetTransaction != null)
                return targetTransaction;

            await Application.Current!.MainPage!.DisplayAlert(
                "Hata",
                "Aktif islem yuklenemedi. Lutfen sayfayi yenileyin.",
                "Tamam");
            return null;
        }

        private async Task RefreshAfterNegotiationActionAsync()
        {
            await _chat.LoadChatCommand.ExecuteAsync(null);
            await LoadNegotiationContextAsync();
            OnPropertyChanged(nameof(FilteredMessages));
        }

        private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(FilteredMessages));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _chat.Messages.CollectionChanged -= OnMessagesChanged;
            _chat.Dispose();
        }
    }
}
