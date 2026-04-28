using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KamPay.Models;
using KamPay.Services;
using KamPay.Services.Auth;
using System.Diagnostics;

namespace KamPay.ViewModels
{
    [QueryProperty(nameof(Post), "Post")]
    public partial class GoodDeedPostDetailViewModel : ObservableObject
    {
        private readonly IGoodDeedService _goodDeedService;
        private readonly IAuthenticationService _authService;
        private readonly IUserProfileService _userProfileService;
        private readonly INotificationCoordinator _notificationCoordinator;

        [ObservableProperty]
        private GoodDeedPost post;

        [ObservableProperty]
        private bool isOwner;

        public GoodDeedPostDetailViewModel(
            IGoodDeedService goodDeedService,
            IAuthenticationService authService,
            IUserProfileService userProfileService,
            INotificationCoordinator notificationCoordinator)
        {
            _goodDeedService = goodDeedService;
            _authService = authService;
            _userProfileService = userProfileService;
            _notificationCoordinator = notificationCoordinator;
        }

        async partial void OnPostChanged(GoodDeedPost value)
        {
            if (value != null)
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser != null)
                {
                    IsOwner = value.UserId == currentUser.UserId;
                    value.UpdateLikeStatus(currentUser.UserId);
                }
            }
        }

        [RelayCommand]
        private async Task LikePostAsync()
        {
            if (Post == null) return;
            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser == null) return;

            bool isLikedNewState = !Post.IsLiked;
            Post.IsLiked = isLikedNewState;

            if (isLikedNewState)
            {
                Post.LikeCount++;
                if (!Post.Likes.ContainsKey(currentUser.UserId))
                    Post.Likes[currentUser.UserId] = true;
            }
            else
            {
                Post.LikeCount = Math.Max(0, Post.LikeCount - 1);
                if (Post.Likes.ContainsKey(currentUser.UserId))
                    Post.Likes.Remove(currentUser.UserId);
            }

            try
            {
                var result = await _goodDeedService.LikePostAsync(Post.PostId, currentUser.UserId);
                if (!result.Success)
                {
                    // Revert
                    Post.IsLiked = !isLikedNewState;
                    if (isLikedNewState)
                    {
                        Post.LikeCount = Math.Max(0, Post.LikeCount - 1);
                        Post.Likes.Remove(currentUser.UserId);
                    }
                    else
                    {
                        Post.LikeCount++;
                        Post.Likes[currentUser.UserId] = true;
                    }
                }
                else
                {
                    if (isLikedNewState && Post.UserId != currentUser.UserId)
                    {
                        await _notificationCoordinator.SendPriorityNotificationAsync(
                            Post.UserId,
                            "İyilik Panosu",
                            $"{currentUser.FullName} ilanınızı beğendi: {Post.Title}",
                            NotificationType.SystemNotice);
                    }
                }
            }
            catch
            {
                // Revert
                Post.IsLiked = !isLikedNewState;
                if (isLikedNewState) Post.LikeCount = Math.Max(0, Post.LikeCount - 1);
                else Post.LikeCount++;
            }
        }

        [RelayCommand]
        private async Task AddCommentAsync()
        {
            if (Post == null || string.IsNullOrWhiteSpace(Post.DraftComment)) return;

            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser == null) return;

            var userProfile = await _userProfileService.GetUserProfileAsync(currentUser.UserId);

            var comment = new Comment
            {
                PostId = Post.PostId,
                UserId = currentUser.UserId,
                UserName = userProfile?.Data?.FullName ?? currentUser.FullName,
                UserProfileImageUrl = userProfile?.Data?.ProfileImageUrl ?? "default_avatar.png",
                Text = Post.DraftComment.Trim(),
                CommentId = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow
            };

            Post.DraftComment = string.Empty;

            Post.Comments ??= new Dictionary<string, Comment>();
            Post.Comments[comment.CommentId] = comment;
            Post.CommentCount++;
            Post.RefreshCommentsUI();

            var result = await _goodDeedService.AddCommentAsync(Post.PostId, comment);
            if (!result.Success)
            {
                Post.Comments.Remove(comment.CommentId);
                Post.CommentCount--;
                Post.RefreshCommentsUI();
            }
            else
            {
                if (Post.UserId != currentUser.UserId)
                {
                    await _notificationCoordinator.SendPriorityNotificationAsync(
                        Post.UserId,
                        "Yeni Yorum",
                        $"{currentUser.FullName} ilanınıza yorum yaptı: {Post.Title}",
                        NotificationType.SystemNotice);
                }
            }
        }

        [RelayCommand]
        private async Task SendMessageAsync()
        {
            if (Post == null || Post.UserId == null) return;
            
            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser == null || currentUser.UserId == Post.UserId) return;

            // ChatPage navigate
            await Shell.Current.GoToAsync("ChatPage", new Dictionary<string, object>
            {
                { "TargetUserId", Post.UserId },
                { "TargetUserName", Post.UserName }
            });
        }

        [RelayCommand]
        private async Task EditPostAsync()
        {
            if (Post == null) return;
            await Shell.Current.GoToAsync("EditGoodDeedPostPage", new Dictionary<string, object>
            {
                { "Post", Post }
            });
        }

        [RelayCommand]
        private async Task MarkAsCompletedAsync()
        {
            if (Post == null) return;
            bool answer = await Application.Current.MainPage.DisplayAlert("Onay", "Bu ilanı 'Tamamlandı' olarak işaretlemek istediğinize emin misiniz?", "Evet", "Hayır");
            if (!answer) return;

            Post.Status = PostStatus.Completed;
            Post.IsActive = false;
            var result = await _goodDeedService.UpdatePostAsync(Post);
            if (result.Success)
            {
                await _userProfileService.AddPointsAsync(Post.UserId, 15, "İyilik yapıldı (İlan tamamlandı)");
                await Application.Current.MainPage.DisplayAlert("Başarılı", "İlan tamamlandı olarak işaretlendi.", "Tamam");
                await Shell.Current.GoToAsync("..");
            }
        }

        [RelayCommand]
        private async Task ClosePostAsync()
        {
            if (Post == null) return;
            bool answer = await Application.Current.MainPage.DisplayAlert("Onay", "Bu ilanı kapatmak (gizlemek) istediğinize emin misiniz?", "Evet", "Hayır");
            if (!answer) return;

            Post.Status = PostStatus.Closed;
            Post.IsActive = false;
            var result = await _goodDeedService.UpdatePostAsync(Post);
            if (result.Success)
            {
                await Application.Current.MainPage.DisplayAlert("Başarılı", "İlan kapatıldı.", "Tamam");
                await Shell.Current.GoToAsync("..");
            }
        }
    }
}
