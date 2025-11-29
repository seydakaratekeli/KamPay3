using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Helpers;
using KamPay.Models;
using KamPay.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using Firebase.Database.Streaming;

namespace KamPay.ViewModels
{
    public partial class GoodDeedBoardViewModel : ObservableObject, IDisposable
    {
        private readonly IGoodDeedService _goodDeedService;
        private readonly IAuthenticationService _authService;
        private readonly IUserProfileService _userProfileService;
        private readonly IUserStateService _userStateService;
        private readonly FirebaseClient _firebaseClient;

        private IDisposable _postsSubscription;
        private readonly Dictionary<string, IDisposable> _commentSubscriptions = new();
        private readonly SemaphoreSlim _commentLock = new(1, 1);
        private readonly Dictionary<string, GoodDeedPost> _postsCache = new();
        private bool _initialLoadComplete = false;
        private CancellationTokenSource _loadingTimeoutCts;
        
        // Loading timeout süresi (milisaniye cinsinden)
        private const int LoadingTimeoutMs = 5000;

        [ObservableProperty]
        private bool isPostFormVisible;

        [ObservableProperty]
        private string newCommentText;

        // Liste yükleniyor mu?
        [ObservableProperty]
        private bool isLoading;

        // 🔥 YENİ: Paylaşım yapılıyor mu? (Butonu kontrol eder)
        [ObservableProperty]
        private bool isPosting;

        [ObservableProperty]
        private bool isRefreshing;

        [ObservableProperty]
        private string title;

        [ObservableProperty]
        private string description;

        [ObservableProperty]
        private PostType selectedType;

        public ObservableCollection<GoodDeedPost> Posts { get; } = new();
        public List<PostType> PostTypes { get; } = Enum.GetValues(typeof(PostType)).Cast<PostType>().ToList();

        public GoodDeedBoardViewModel(
            IGoodDeedService goodDeedService,
            IAuthenticationService authService,
            IUserProfileService userProfileService,
            IUserStateService userStateService)
        {
            _goodDeedService = goodDeedService;
            _authService = authService;
            _userProfileService = userProfileService;
            _userStateService = userStateService;
            _firebaseClient = new FirebaseClient(Constants.FirebaseRealtimeDbUrl);

            // Kullanıcı profil değişikliklerini dinle
            _userStateService.UserProfileChanged += OnUserProfileChanged;
        }

        private void OnUserProfileChanged(object sender, User updatedUser)
        {
            if (updatedUser == null) return;

            // 🔥 Kritik: UI'da anlık güncelleme için MainThread'de çalıştırılmalıdır.
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Kullanıcıya ait postların bilgilerini güncelle
                foreach (var post in Posts.Where(p => p.UserId == updatedUser.UserId))
                {
                    post.UserName = updatedUser.FullName;
                    post.UserProfileImageUrl = updatedUser.ProfileImageUrl;
                }

                // Cache'deki postları da güncelle
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

        [RelayCommand]
        private void ToggleComments(GoodDeedPost post)
        {
            if (post == null) return;
            post.IsCommentsExpanded = !post.IsCommentsExpanded;
            post.RefreshCommentsUI();
        }

        [RelayCommand]
        private async Task RefreshPostsAsync()
        {
            IsRefreshing = true;
            try
            {
                StopListening();
                Posts.Clear();
                StartListeningForPosts();
                await Task.Delay(500);
            }
            finally { IsRefreshing = false; }
        }

        [RelayCommand]
        private async Task CreatePostAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Title) || string.IsNullOrWhiteSpace(Description))
                {
                    await Application.Current.MainPage.DisplayAlert("Uyarı", "Başlık ve açıklama gerekli", "Tamam");
                    return;
                }

                // 🔥 DÜZELTME 1: Listeyi değil, Posting durumunu aktif et
                IsPosting = true;

                var currentUser = await _authService.GetCurrentUserAsync();

                // 🔥 DÜZELTME 2: Null kontrolü (Veritabanı silinince oturum düşebilir)
                if (currentUser == null)
                {
                    await Application.Current.MainPage.DisplayAlert("Hata", "Oturum açılmamış.", "Tamam");
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
                    await Application.Current.MainPage.DisplayAlert("Başarılı", "İlan paylaşıldı!", "Tamam");
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Hata", ex.Message, "Tamam");
            }
            finally
            {
                IsPosting = false; // İşlem bitince butonu tekrar aç
            }
        }

        [RelayCommand]
        private async Task LikePostAsync(GoodDeedPost post)
        {
            if (post == null) return;
            bool previousState = post.IsLiked;
            post.IsLiked = !post.IsLiked;
            post.LikeCount = post.IsLiked ? post.LikeCount + 1 : Math.Max(0, post.LikeCount - 1);

            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                await _goodDeedService.LikePostAsync(post.PostId, currentUser.UserId);
            }
            catch
            {
                post.IsLiked = previousState;
                post.LikeCount = post.IsLiked ? post.LikeCount + 1 : Math.Max(0, post.LikeCount - 1);
            }
        }

        [RelayCommand]
        private async Task DeletePostAsync(GoodDeedPost post)
        {
            try
            {
                var confirm = await Application.Current.MainPage.DisplayAlert("Sil", "Emin misiniz?", "Evet", "Hayır");
                if (!confirm) return;

                var currentUser = await _authService.GetCurrentUserAsync();
                var result = await _goodDeedService.DeletePostAsync(post.PostId, currentUser.UserId);

                if (result.Success && _commentSubscriptions.ContainsKey(post.PostId))
                {
                    _commentSubscriptions[post.PostId].Dispose();
                    _commentSubscriptions.Remove(post.PostId);
                }
            }
            catch (Exception ex) { await Application.Current.MainPage.DisplayAlert("Hata", ex.Message, "Tamam"); }
        }

        [RelayCommand]
        private async Task AddCommentAsync(GoodDeedPost post)
        {
            if (post == null || string.IsNullOrWhiteSpace(NewCommentText)) return;

            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser == null) return;

            var userProfile = await _userProfileService.GetUserProfileAsync(currentUser.UserId);

            var comment = new Comment
            {
                PostId = post.PostId,
                UserId = currentUser.UserId,
                UserName = userProfile?.Data?.Username ?? currentUser.FullName,
                UserProfileImageUrl = userProfile?.Data?.ProfileImageUrl ?? "default_avatar.png",
                Text = NewCommentText.Trim(),
                CommentId = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow
            };

            NewCommentText = string.Empty;
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
                await Shell.Current.DisplayAlert("Hata", result.Message, "Tamam");
            }
        }

        public void StartListeningForPosts()
        {
            try
            {
                if (_postsSubscription != null) return;
                if (!IsRefreshing) IsLoading = !Posts.Any();

                // 🔥 Önceki timeout task'ı iptal et
                _loadingTimeoutCts?.Cancel();
                _loadingTimeoutCts = new CancellationTokenSource();
                var timeoutToken = _loadingTimeoutCts.Token;

                // 🔥 Loading timeout mekanizması - belirlenen süre içinde veri gelmezse loading'i kapat
                _ = Task.Delay(LoadingTimeoutMs, timeoutToken).ContinueWith(_ =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (IsLoading && !_initialLoadComplete)
                        {
                            IsLoading = false;
                            Debug.WriteLine($"⚠️ Loading timeout - veri yüklenemedi ({LoadingTimeoutMs}ms)");
                        }
                    });
                }, TaskContinuationOptions.NotOnCanceled);

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
                                // 🔥 Veri geldiğinde timeout task'ı iptal et
                                _loadingTimeoutCts?.Cancel();

                                var currentUser = await _authService.GetCurrentUserAsync();
                                // 🔥 Null kontrolü: currentUser null olabilir, ancak post listesi yine de gösterilebilir
                                if (currentUser == null)
                                {
                                    Debug.WriteLine("⚠️ CurrentUser null - kullanıcı oturumu yok, postlar salt-okunur modda gösterilecek");
                                }

                                await MainThread.InvokeOnMainThreadAsync(() =>
                                {
                                    try { ProcessPostBatch(events, currentUser); }
                                    catch (Exception ex) { Debug.WriteLine($"❌ Post batch hatası: {ex.Message}"); }
                                    finally { if (!_initialLoadComplete) { _initialLoadComplete = true; IsLoading = false; } }
                                });
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"❌ Firebase event işleme hatası: {ex.Message}");
                                MainThread.BeginInvokeOnMainThread(() => IsLoading = false);
                            }
                        },
                        onError: ex =>
                        {
                            // 🔥 Firebase subscription error handler
                            Debug.WriteLine($"❌ Firebase bağlantı hatası: {ex.Message}");
                            Debug.WriteLine($"❌ Stack trace: {ex.StackTrace}");
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                IsLoading = false;
                                // Not: _initialLoadComplete değerini değiştirmiyoruz çünkü bu 
                                // hata sonrası tekrar deneme davranışını bozabilir
                            });
                        }
                    );
            }
            catch (Exception ex)
            {
                // 🔥 Kapsamlı hata yönetimi - network hataları dahil
                Debug.WriteLine($"❌ StartListeningForPosts hatası: {ex.Message}");
                Debug.WriteLine($"❌ Exception type: {ex.GetType().Name}");
                Debug.WriteLine($"❌ Stack trace: {ex.StackTrace}");
                IsLoading = false;
            }
        }

        private void ProcessPostBatch(IList<FirebaseEvent<GoodDeedPost>> events, User currentUser)
        {
            bool hasChanges = false;
            foreach (var e in events)
            {
                var post = e.Object;
                post.PostId = e.Key;
                if (currentUser != null) post.IsOwner = post.UserId == currentUser.UserId;

                var existingPost = Posts.FirstOrDefault(p => p.PostId == post.PostId);

                if (e.EventType == FirebaseEventType.InsertOrUpdate)
                {
                    if (existingPost != null)
                    {
                        var index = Posts.IndexOf(existingPost);
                        post.IsCommentsExpanded = existingPost.IsCommentsExpanded;
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
                    hasChanges = true;
                }
                else if (e.EventType == FirebaseEventType.Delete && existingPost != null)
                {
                    Posts.Remove(existingPost);
                    _postsCache.Remove(post.PostId);
                    if (_commentSubscriptions.ContainsKey(post.PostId))
                    {
                        _commentSubscriptions[post.PostId].Dispose();
                        _commentSubscriptions.Remove(post.PostId);
                    }
                    hasChanges = true;
                }
            }
            if (hasChanges) SortPostsInPlace();
        }

        private void InsertPostSorted(GoodDeedPost post)
        {
            if (Posts.Count == 0) { Posts.Add(post); return; }
            if (Posts[0].CreatedAt <= post.CreatedAt) { Posts.Insert(0, post); return; }
            for (int i = 0; i < Posts.Count; i++) { if (Posts[i].CreatedAt < post.CreatedAt) { Posts.Insert(i, post); return; } }
            Posts.Add(post);
        }

        private void SortPostsInPlace()
        {
            var sorted = Posts.OrderByDescending(p => p.CreatedAt).ToList();
            for (int i = 0; i < sorted.Count; i++)
            {
                var currentIndex = Posts.IndexOf(sorted[i]);
                if (currentIndex != i) Posts.Move(currentIndex, i);
            }
        }

        public void StartListeningForComments(GoodDeedPost post)
        {
            if (_commentSubscriptions.ContainsKey(post.PostId)) return;
            var subscription = _firebaseClient.Child("good_deed_posts").Child(post.PostId).Child("Comments")
                .AsObservable<Comment>().Where(e => e.Object != null).Buffer(TimeSpan.FromMilliseconds(300)).Where(batch => batch.Any())
                .Subscribe(async events =>
                {
                    await _commentLock.WaitAsync();
                    try { await MainThread.InvokeOnMainThreadAsync(() => ProcessCommentBatch(post, events)); }
                    finally { _commentLock.Release(); }
                });
            _commentSubscriptions[post.PostId] = subscription;
        }

        private void ProcessCommentBatch(GoodDeedPost post, IList<FirebaseEvent<Comment>> events)
        {
            post.Comments ??= new Dictionary<string, Comment>();
            bool hasChanges = false;
            foreach (var e in events)
            {
                if (e.EventType == FirebaseEventType.InsertOrUpdate) { post.Comments[e.Key] = e.Object; hasChanges = true; }
                else if (e.EventType == FirebaseEventType.Delete && post.Comments.ContainsKey(e.Key)) { post.Comments.Remove(e.Key); hasChanges = true; }
            }
            if (hasChanges)
            {
                post.CommentCount = post.Comments.Count;
                post.RefreshCommentsUI();
                var existingPost = Posts.FirstOrDefault(p => p.PostId == post.PostId);
                if (existingPost != null) Posts[Posts.IndexOf(existingPost)] = post;
            }
        }

        public void StopListening()
        {
            _loadingTimeoutCts?.Cancel();
            _postsSubscription?.Dispose();
            _postsSubscription = null;
            foreach (var sub in _commentSubscriptions.Values) sub.Dispose();
            _commentSubscriptions.Clear();
            _postsCache.Clear();
            _initialLoadComplete = false;
        }
        public void Dispose()
        {
            _userStateService.UserProfileChanged -= OnUserProfileChanged;
            _loadingTimeoutCts?.Cancel();
            _loadingTimeoutCts?.Dispose();
            StopListening();
        }
    }
}