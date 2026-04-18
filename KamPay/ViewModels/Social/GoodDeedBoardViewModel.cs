using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Firebase.Database;
using Firebase.Database.Query;
using Firebase.Database.Streaming;
using KamPay.Helpers;
using KamPay.Models;
using KamPay.Services;
using KamPay.Services.Auth;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using MauiPreserve = Microsoft.Maui.Controls.Internals.PreserveAttribute;

namespace KamPay.ViewModels
{
    [MauiPreserve(AllMembers = true)]
    public partial class GoodDeedBoardViewModel : ObservableObject, IDisposable
    {
        private readonly IGoodDeedService _goodDeedService;
        private readonly IAuthenticationService _authService;
        private readonly IUserProfileService _userProfileService;
        private readonly IUserStateService _userStateService;
        private readonly FirebaseClient _firebaseClient;

        private IDisposable? _postsSubscription;
        private readonly Dictionary<string, IDisposable> _commentSubscriptions = new();
        private readonly SemaphoreSlim _commentLock = new(1, 1);
        private readonly Dictionary<string, GoodDeedPost> _postsCache = new();

        // : Tüm ilanlar (filtrelenmeden önce)
        private List<GoodDeedPost> _allPosts = new();

        private bool _initialLoadComplete = false;
        private CancellationTokenSource? _loadingTimeoutCts;
        private const int LoadingTimeoutMs = 6000;

        [ObservableProperty]
        private bool isPostFormVisible;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private bool isSkeletonVisible;

        [ObservableProperty]
        private bool isPosting;

        [ObservableProperty]
        private bool isRefreshing;

        [ObservableProperty]
        private string title = string.Empty;

        [ObservableProperty]
        private string description = string.Empty;

        [ObservableProperty]
        private PostType selectedType;

        // : Arama ve filtreleme özellikleri
        [ObservableProperty]
        private string searchText = "";

        [ObservableProperty]
        private PostType? filterPostType = null;

        // : UI koleksiyonları
        public ObservableRangeCollection<GoodDeedPost> Posts { get; } = new();
        public ObservableRangeCollection<GoodDeedPost> FilteredPosts { get; } = new();
        public List<PostType> PostTypes { get; } = Enum.GetValues(typeof(PostType)).Cast<PostType>().ToList();

        // : Filtre için kategori listesi (null = "Hepsi" seçeneği dahil)
        public List<PostType?> FilterPostTypes { get; } =
            new List<PostType?> { null }
            .Concat(Enum.GetValues(typeof(PostType)).Cast<PostType?>())
            .ToList();

        public GoodDeedBoardViewModel(
            IGoodDeedService goodDeedService,
            IAuthenticationService authService,
            IUserProfileService userProfileService,
            IUserStateService userStateService,
            FirebaseClient firebaseClient)
        {
            _goodDeedService = goodDeedService;
            _authService = authService;
            _userProfileService = userProfileService;
            _userStateService = userStateService;

            _firebaseClient = firebaseClient;
            _userStateService.UserProfileChanged += OnUserProfileChanged;

            // Dil değiştiğinde filtreyi yeniden uygula
            LocalizationResourceManager.Instance.PropertyChanged += (sender, e) =>
            {
                OnPropertyChanged(nameof(FilterPostTypes));
                ApplyFilter();
            };
        }

        private void OnUserProfileChanged(object sender, User updatedUser)
        {
            if (updatedUser == null) return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                // _allPosts listesindeki kullanıcı bilgilerini güncelle
                foreach (var post in _allPosts.Where(p => p.UserId == updatedUser.UserId))
                {
                    post.UserName = updatedUser.FullName;
                    post.UserProfileImageUrl = updatedUser.ProfileImageUrl;
                }

                // Posts ve FilteredPosts koleksiyonlarını da güncelle
                foreach (var post in Posts.Where(p => p.UserId == updatedUser.UserId))
                {
                    post.UserName = updatedUser.FullName;
                    post.UserProfileImageUrl = updatedUser.ProfileImageUrl;
                }

                foreach (var post in FilteredPosts.Where(p => p.UserId == updatedUser.UserId))
                {
                    post.UserName = updatedUser.FullName;
                    post.UserProfileImageUrl = updatedUser.ProfileImageUrl;
                }

                foreach (var kvp in _postsCache.Where(p => p.Value.UserId == updatedUser.UserId))
                {
                    kvp.Value.UserName = updatedUser.FullName;
                    kvp.Value.UserProfileImageUrl = updatedUser.ProfileImageUrl;
                }
            });
        }

        [RelayCommand]
        private void OpenPostForm() => IsPostFormVisible = true;

        [RelayCommand]
        private void ClosePostForm() => IsPostFormVisible = false;

        //  : Yorum yapma kutusunu açıp kapatır
        [RelayCommand]
        private void ToggleCommentBox(GoodDeedPost post)
        {
            if (post == null) return;
            post.IsCommentBoxVisible = !post.IsCommentBoxVisible;
        }

        // Mevcut yorumları genişletip daraltır
        [RelayCommand]
        private void ToggleComments(GoodDeedPost post)
        {
            if (post == null) return;
            post.IsCommentsExpanded = !post.IsCommentsExpanded;
            post.RefreshCommentsUI();
        }

        // : Filtre temizleme komutu
        [RelayCommand]
        private void ClearCategoryFilter()
        {
            FilterPostType = null;
            ApplyFilter();
        }

        [RelayCommand]
        private async Task RefreshPostsAsync()
        {
            IsRefreshing = true;
            try
            {
                StopListening();
                Posts.Clear();
                FilteredPosts.Clear();
                _allPosts.Clear();
                _postsCache.Clear();
                _initialLoadComplete = false;

                StartListeningForPosts();
                await Task.Delay(400);
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        // : Filtreleme metodları
        partial void OnSearchTextChanged(string value) => ApplyFilter();
        partial void OnFilterPostTypeChanged(PostType? value) => ApplyFilter();

        /// <summary>
        /// Arama ve kategori filtreleme işlemini uygular
        /// </summary>
        [RelayCommand]
        private void ApplyFilter()
        {
            FilterPosts();
        }

        private void FilterPosts()
        {
            var query = _allPosts.AsEnumerable();

            // Arama filtresi
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var searchLower = SearchText.ToLower();
                query = query.Where(p =>
                    (p.Title ?? "").Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                    (p.Description ?? "").Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                    (p.UserName ?? "").Contains(searchLower, StringComparison.OrdinalIgnoreCase)
                );
            }

            // Kategori filtresi
            if (FilterPostType != null)
            {
                query = query.Where(p => p.Type == FilterPostType.Value);
            }

            // Tarihe göre sırala (en yeni önce)
            query = query.OrderByDescending(p => p.CreatedAt);

            // Filtrelenmiş listeyi performanslı şekilde güncelle
            FilteredPosts.ReplaceRange(query);
        }

        [RelayCommand]
        private async Task CreatePostAsync()
        {
            try
            {
                var loc = LocalizationResourceManager.Instance;
                
                if (string.IsNullOrWhiteSpace(Title) || string.IsNullOrWhiteSpace(Description))
                {
                    await Application.Current.MainPage.DisplayAlert(
                        loc["Warning"], 
                        loc["TitleAndDescriptionRequired"], 
                        loc["Ok"]);
                    return;
                }

                IsPosting = true;

                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    await Application.Current.MainPage.DisplayAlert(
                        loc["Error"], 
                        loc["SessionNotFound"], 
                        loc["Ok"]);
                    return;
                }

                var userProfile = await _userProfileService.GetUserProfileAsync(currentUser.UserId);
                string userImage = userProfile?.Data?.ProfileImageUrl ?? "default_avatar.png";

                var post = new GoodDeedPost
                {
                    UserId = currentUser.UserId,
                    UserName = currentUser.FullName,
                    UserProfileImageUrl = userImage,
                    Type = SelectedType,
                    Title = Title,
                    Description = Description,
                    CreatedAt = DateTime.UtcNow
                };

                var result = await _goodDeedService.CreatePostAsync(post);

                if (result.Success)
                {
                    Title = string.Empty;
                    Description = string.Empty;
                    IsPostFormVisible = false;
                    await Application.Current.MainPage.DisplayAlert(
                        loc["Success"], 
                        loc["PostCreatedSuccess"], 
                        loc["Ok"]);
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert(
                    LocalizationResourceManager.Instance["Error"], 
                    ex.Message, 
                    LocalizationResourceManager.Instance["Ok"]);
            }
            finally
            {
                IsPosting = false;
            }
        }

        [RelayCommand]
        private async Task LikePostAsync(GoodDeedPost post)
        {
            if (post == null) return;

            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser == null) return;

            bool isLikedNewState = !post.IsLiked;

            post.IsLiked = isLikedNewState;

            if (isLikedNewState)
            {
                post.LikeCount++;
                if (!post.Likes.ContainsKey(currentUser.UserId))
                    post.Likes[currentUser.UserId] = true;
            }
            else
            {
                post.LikeCount = Math.Max(0, post.LikeCount - 1);
                if (post.Likes.ContainsKey(currentUser.UserId))
                    post.Likes.Remove(currentUser.UserId);
            }

            try
            {
                var result = await _goodDeedService.LikePostAsync(post.PostId, currentUser.UserId);

                if (!result.Success)
                {
                    post.IsLiked = !isLikedNewState;

                    if (isLikedNewState)
                    {
                        post.LikeCount = Math.Max(0, post.LikeCount - 1);
                        post.Likes.Remove(currentUser.UserId);
                    }
                    else
                    {
                        post.LikeCount++;
                        post.Likes[currentUser.UserId] = true;
                    }

                    await Application.Current.MainPage.DisplayAlert(
                        LocalizationResourceManager.Instance["Error"], 
                        LocalizationResourceManager.Instance["OperationFailedTryAgain"], 
                        LocalizationResourceManager.Instance["Ok"]);
                }
            }
            catch (Exception ex)
            {
                post.IsLiked = !isLikedNewState;
                if (isLikedNewState)
                    post.LikeCount = Math.Max(0, post.LikeCount - 1);
                else
                    post.LikeCount++;

                KamPay.Helpers.AppLogger.DebugLog($"❌ Beğeni hatası: {ex.Message}");
            }
        }
        
        [RelayCommand]
        private async Task DeletePostAsync(GoodDeedPost post)
        {
            if (post == null) return;

            try
            {
                var loc = LocalizationResourceManager.Instance;
                var confirm = await Application.Current.MainPage.DisplayAlert(
                    loc["Delete"], 
                    loc["ConfirmDeletePost"], 
                    loc["Yes"], 
                    loc["No"]);
                if (!confirm) return;

                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null) return;

                var result = await _goodDeedService.DeletePostAsync(post.PostId, currentUser.UserId);

                if (result.Success && _commentSubscriptions.ContainsKey(post.PostId))
                {
                    _commentSubscriptions[post.PostId].Dispose();
                    _commentSubscriptions.Remove(post.PostId);
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert(
                    LocalizationResourceManager.Instance["Error"], 
                    ex.Message, 
                    LocalizationResourceManager.Instance["Ok"]);
            }
        }

        [RelayCommand]
        private async Task AddCommentAsync(GoodDeedPost post)
        {
            if (post == null || string.IsNullOrWhiteSpace(post.DraftComment)) return;

            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser == null) return;

            var userProfile = await _userProfileService.GetUserProfileAsync(currentUser.UserId);

            var comment = new Comment
            {
                PostId = post.PostId,
                UserId = currentUser.UserId,
                // ✅ FIX: FullName kullan
                UserName = userProfile?.Data?.FullName ?? currentUser.FullName,
                UserProfileImageUrl = userProfile?.Data?.ProfileImageUrl ?? "default_avatar.png",
                Text = post.DraftComment.Trim(),
                CommentId = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow
            };

            post.DraftComment = string.Empty;

            post.Comments ??= new Dictionary<string, Comment>();
            post.Comments[comment.CommentId] = comment;
            post.CommentCount++;
            post.RefreshCommentsUI();

            var result = await _goodDeedService.AddCommentAsync(post.PostId, comment);

            if (!result.Success)
            {
                post.Comments.Remove(comment.CommentId);
                post.CommentCount--;
                post.RefreshCommentsUI();
                await Shell.Current.DisplayAlert(
                    LocalizationResourceManager.Instance["Error"], 
                    result.Message, 
                    LocalizationResourceManager.Instance["Ok"]);
            }
        }

        private bool ContainsRealPost(IList<FirebaseEvent<GoodDeedPost>> events)
        {
            return events.Any(e =>
                e.Object != null &&
                !string.IsNullOrWhiteSpace(e.Key) &&
                !string.IsNullOrWhiteSpace(e.Object?.Title)
            );
        }

        public void StartListeningForPosts()
        {
            try
            {
                if (_postsSubscription != null) return;

                if (!IsRefreshing && !Posts.Any())
                {
                    IsLoading = true;
                    IsSkeletonVisible = true;
                }

                _loadingTimeoutCts?.Cancel();
                _loadingTimeoutCts?.Dispose();
                _loadingTimeoutCts = new CancellationTokenSource();
                var timeoutToken = _loadingTimeoutCts.Token;

                _ = LoadInitialSnapshotAsync(timeoutToken);

                Task.Delay(LoadingTimeoutMs, timeoutToken).ContinueWith(t =>
                {
                    if (t.IsCanceled) return;

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (!_initialLoadComplete)
                        {
                            Debug.WriteLine("⏳ Loading timeout - veri gelmedi.");
                            IsLoading = false;
                            IsSkeletonVisible = false;
                        }
                    });
                }, TaskContinuationOptions.OnlyOnRanToCompletion);

                _postsSubscription = _firebaseClient
                    .Child("good_deed_posts")
                    .AsObservable<GoodDeedPost>()
                    .Where(e => e.Object != null)
                    .Buffer(TimeSpan.FromMilliseconds(400))
                    .Where(batch => batch.Any())
                    .Subscribe(
                        onNext: async events =>
                        {
                            try
                            {
                                _loadingTimeoutCts?.Cancel();

                                var currentUser = await _authService.GetCurrentUserAsync();
                                if (currentUser == null)
                                {
                                    Debug.WriteLine("⚠️ CurrentUser null - postlar salt-okunur modda.");
                                }

                                await MainThread.InvokeOnMainThreadAsync(() =>
                                {
                                    try
                                    {
                                        ProcessPostBatch(events, currentUser);
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.WriteLine($"❌ Post batch hatası: {ex.Message}");
                                    }

                                    if (!_initialLoadComplete && ContainsRealPost(events))
                                    {
                                        _initialLoadComplete = true;
                                        IsLoading = false;
                                        IsSkeletonVisible = false;
                                        Debug.WriteLine("✅ İlk gerçek realtime post geldi — loading kapatıldı.");
                                    }
                                });
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"❌ Firebase event işleme hatası: {ex.Message}");
                                MainThread.BeginInvokeOnMainThread(() =>
                                {
                                    IsLoading = false;
                                    IsSkeletonVisible = false;
                                });
                            }
                        },
                        onError: ex =>
                        {
                            Debug.WriteLine($"❌ Firebase bağlantı hatası: {ex.Message}");
                            Debug.WriteLine($"❌ Stack trace: {ex.StackTrace}");
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                IsLoading = false;
                                IsSkeletonVisible = false;
                            });
                        }
                    );
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ StartListeningForPosts hatası: {ex.Message}");
                Debug.WriteLine($"❌ Exception type: {ex.GetType().Name}");
                Debug.WriteLine($"❌ Stack trace: {ex.StackTrace}");
                IsLoading = false;
                IsSkeletonVisible = false;
            }
        }

        private async Task LoadInitialSnapshotAsync(CancellationToken token)
        {
            try
            {
                if (token.IsCancellationRequested) return;

                var snapshot = await _firebaseClient
                    .Child("good_deed_posts")
                    .OnceAsync<GoodDeedPost>();

                if (token.IsCancellationRequested) return;

                var posts = snapshot
                    .Where(s => s.Object != null && !string.IsNullOrWhiteSpace(s.Object.Title))
                    .Select(s =>
                    {
                        var p = s.Object;
                        p.PostId = s.Key;
                        return p;
                    })
                    .OrderByDescending(p => p.CreatedAt)
                    .ToList();

                if (!posts.Any()) return;

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    foreach (var post in posts)
                    {
                        var existing = _allPosts.FirstOrDefault(p => p.PostId == post.PostId);
                        if (existing == null)
                        {
                            _allPosts.Add(post);
                            InsertPostSorted(post);
                            _postsCache[post.PostId] = post;
                            StartListeningForComments(post);
                        }
                    }

                    // : İlk yükleme sonrası filtreyi uygula
                    ApplyFilter();

                    if (!_initialLoadComplete)
                    {
                        _initialLoadComplete = true;
                        IsLoading = false;
                        IsSkeletonVisible = false;
                        Debug.WriteLine("✅ Snapshot yüklendi — loading kapatıldı.");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ Snapshot yüklenirken hata: {ex.Message}");
            }
        }

        private void ProcessPostBatch(IList<FirebaseEvent<GoodDeedPost>> events, User currentUser)
        {
            if (events == null || !events.Any())
            {
                Debug.WriteLine("⚠️ Boş event batch alındı");
                return;
            }

            var validEvents = events.Where(e =>
                e.Object != null &&
                !string.IsNullOrWhiteSpace(e.Key) &&
                !string.IsNullOrWhiteSpace(e.Object?.Title)
            ).ToList();

            if (!validEvents.Any())
            {
                Debug.WriteLine("⚠️ Henüz gerçek veri gelmedi — loading devam ediyor.");
                return;
            }

            bool hasChanges = false;

            foreach (var e in validEvents)
            {
                try
                {
                    var post = e.Object;
                    post.PostId = e.Key;

                    if (currentUser != null)
                    {
                        post.IsOwner = post.UserId == currentUser.UserId;
                        post.UpdateLikeStatus(currentUser.UserId);
                    }

                    var existingPost = Posts.FirstOrDefault(p => p.PostId == post.PostId);
                    var existingInAllPosts = _allPosts.FirstOrDefault(p => p.PostId == post.PostId);

                    if (e.EventType == FirebaseEventType.InsertOrUpdate)
                    {
                        if (existingPost != null)
                        {
                            var index = Posts.IndexOf(existingPost);

                            // UI state koru
                            post.IsCommentsExpanded = existingPost.IsCommentsExpanded;
                            post.IsCommentBoxVisible = existingPost.IsCommentBoxVisible;
                            post.DraftComment = existingPost.DraftComment;

                            if (existingPost.Comments != null && post.Comments == null)
                            {
                                post.Comments = existingPost.Comments;
                                post.CommentCount = existingPost.CommentCount;
                            }

                            post.RefreshCommentsUI();
                            Posts[index] = post;
                            _postsCache[post.PostId] = post;
                        }
                        else
                        {
                            InsertPostSorted(post);
                            _postsCache[post.PostId] = post;
                            StartListeningForComments(post);
                        }

                        // : _allPosts listesini de güncelle
                        if (existingInAllPosts != null)
                        {
                            var indexInAll = _allPosts.IndexOf(existingInAllPosts);
                            _allPosts[indexInAll] = post;
                        }
                        else
                        {
                            _allPosts.Add(post);
                        }

                        hasChanges = true;
                    }
                    else if (e.EventType == FirebaseEventType.Delete && existingPost != null)
                    {
                        Posts.Remove(existingPost);
                        _allPosts.Remove(existingInAllPosts);
                        _postsCache.Remove(post.PostId);

                        if (_commentSubscriptions.ContainsKey(post.PostId))
                        {
                            _commentSubscriptions[post.PostId].Dispose();
                            _commentSubscriptions.Remove(post.PostId);
                        }
                        hasChanges = true;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"❌ Post işleme hatası: {ex}");
                    continue;
                }
            }

            if (hasChanges)
            {
                try
                {
                    SortPostsInPlace();
                    // : Değişiklik olduysa filtreyi yeniden uygula
                    ApplyFilter();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"❌ Sort/Filter hatası: {ex.Message}");
                }
            }
        }

        private void InsertPostSorted(GoodDeedPost post)
        {
            if (Posts.Count == 0)
            {
                Posts.Add(post);
                return;
            }

            if (Posts[0].CreatedAt <= post.CreatedAt)
            {
                Posts.Insert(0, post);
                return;
            }

            for (int i = 0; i < Posts.Count; i++)
            {
                if (Posts[i].CreatedAt < post.CreatedAt)
                {
                    Posts.Insert(i, post);
                    return;
                }
            }

            Posts.Add(post);
        }

        private void SortPostsInPlace()
        {
            var sorted = Posts.OrderByDescending(p => p.CreatedAt).ToList();

            for (int i = 0; i < sorted.Count; i++)
            {
                var currentIndex = Posts.IndexOf(sorted[i]);
                if (currentIndex != i)
                    Posts.Move(currentIndex, i);
            }
        }

        public void StartListeningForComments(GoodDeedPost post)
        {
            if (_commentSubscriptions.ContainsKey(post.PostId)) return;

            var subscription = _firebaseClient
                .Child("good_deed_posts")
                .Child(post.PostId)
                .Child("Comments")
                .AsObservable<Comment>()
                .Where(e => e.Object != null)
                .Buffer(TimeSpan.FromMilliseconds(300))
                .Where(batch => batch.Any())
                .Subscribe(async events =>
                {
                    await _commentLock.WaitAsync();
                    try
                    {
                        await MainThread.InvokeOnMainThreadAsync(() =>
                            ProcessCommentBatch(post, events)
                        );
                    }
                    finally
                    {
                        _commentLock.Release();
                    }
                });

            _commentSubscriptions[post.PostId] = subscription;
        }

        private void ProcessCommentBatch(GoodDeedPost post, IList<FirebaseEvent<Comment>> events)
        {
            post.Comments ??= new Dictionary<string, Comment>();
            bool hasChanges = false;

            foreach (var e in events)
            {
                if (e.EventType == FirebaseEventType.InsertOrUpdate)
                {
                    post.Comments[e.Key] = e.Object;
                    hasChanges = true;
                }
                else if (e.EventType == FirebaseEventType.Delete && post.Comments.ContainsKey(e.Key))
                {
                    post.Comments.Remove(e.Key);
                    hasChanges = true;
                }
            }

            if (hasChanges)
            {
                post.CommentCount = post.Comments.Count;
                post.RefreshCommentsUI();
            }
        }

        public void StopListening()
        {
            try
            {
                Debug.WriteLine("🛑 Listener durduruluyor...");

                _loadingTimeoutCts?.Cancel();
                _postsSubscription?.Dispose();
                _postsSubscription = null;

                foreach (var sub in _commentSubscriptions.Values)
                    sub?.Dispose();

                _commentSubscriptions.Clear();

                Debug.WriteLine("✅ Listener başarıyla durduruldu");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ StopListening hatası: {ex.Message}");
            }
        }

        public void Dispose()
        {
            try
            {
                Debug.WriteLine("🧹 GoodDeedBoardViewModel dispose ediliyor...");

                _userStateService.UserProfileChanged -= OnUserProfileChanged;

                _loadingTimeoutCts?.Cancel();
                _loadingTimeoutCts?.Dispose();

                _postsSubscription?.Dispose();
                _postsSubscription = null;

                foreach (var sub in _commentSubscriptions.Values)
                    sub?.Dispose();

                _commentSubscriptions.Clear();
                _postsCache.Clear();
                _allPosts.Clear();
                _initialLoadComplete = false;

                Debug.WriteLine("✅ Dispose tamamlandı");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Dispose hatası: {ex.Message}");
            }
        }
    }
}
