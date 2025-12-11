using KamPay.Models;
using KamPay.Services;
using KamPay.ViewModels;

namespace KamPay.Tests.ViewModels;

/// <summary>
/// GoodDeedBoardViewModel birim testleri
/// Ýyi iþler panosu, paylaþým ve yorum testleri
/// </summary>
public class GoodDeedBoardViewModelTests
{
    private readonly Mock<IGoodDeedService> _mockGoodDeedService;
    private readonly Mock<IAuthenticationService> _mockAuthService;
    private readonly Mock<IUserProfileService> _mockUserProfileService;
    private readonly Mock<IUserStateService> _mockUserStateService;
    private readonly User _testUser;

    public GoodDeedBoardViewModelTests()
    {
        _mockGoodDeedService = new Mock<IGoodDeedService>();
        _mockAuthService = new Mock<IAuthenticationService>();
        _mockUserProfileService = new Mock<IUserProfileService>();
        _mockUserStateService = new Mock<IUserStateService>();

        _testUser = new User
        {
            UserId = "user123",
            Email = "test@bartin.edu.tr",
            FirstName = "Test",
            LastName = "User",
            ProfileImageUrl = "https://example.com/profile.jpg"
        };

        _mockAuthService
            .Setup(x => x.GetCurrentUserAsync())
            .ReturnsAsync(_testUser);

        _mockUserProfileService
            .Setup(x => x.GetUserProfileAsync(_testUser.UserId))
            .ReturnsAsync(ServiceResult<UserProfile>.SuccessResult(new UserProfile
            {
                UserId = _testUser.UserId,
                ProfileImageUrl = _testUser.ProfileImageUrl
            }));
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_InitializesCollections()
    {
        // Arrange & Act
        var viewModel = CreateViewModel();

        // Assert
        viewModel.Should().NotBeNull();
        viewModel.Posts.Should().NotBeNull();
        viewModel.PostTypes.Should().NotBeNull();
        viewModel.PostTypes.Should().HaveCountGreaterThan(0);
    }

    #endregion

    #region Form Management Tests

    [Fact]
    public void OpenPostForm_ShowsForm()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        viewModel.OpenPostFormCommand.Execute(null);

        // Assert
        viewModel.IsPostFormVisible.Should().BeTrue();
    }

    [Fact]
    public void ClosePostForm_HidesForm()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.IsPostFormVisible = true;

        // Act
        viewModel.ClosePostFormCommand.Execute(null);

        // Assert
        viewModel.IsPostFormVisible.Should().BeFalse();
    }

    #endregion

    #region Create Post Tests

    [Fact]
    public async Task CreatePostAsync_WithValidData_CreatesPost()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.Title = "Test Baþlýk";
        viewModel.Description = "Test açýklama";
        viewModel.SelectedType = PostType.HelpRequest;

        _mockGoodDeedService
            .Setup(x => x.CreatePostAsync(It.IsAny<GoodDeedPost>()))
            .ReturnsAsync(ServiceResult<GoodDeedPost>.SuccessResult(new GoodDeedPost()));

        // Act
        await viewModel.CreatePostCommand.ExecuteAsync(null);

        // Assert
        _mockGoodDeedService.Verify(x => x.CreatePostAsync(It.IsAny<GoodDeedPost>()), Times.Once);
        viewModel.Title.Should().BeEmpty();
        viewModel.Description.Should().BeEmpty();
        viewModel.IsPostFormVisible.Should().BeFalse();
    }

    [Fact]
    public async Task CreatePostAsync_WithEmptyTitle_DoesNotCreate()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.Title = "";
        viewModel.Description = "Test açýklama";

        // Act
        await viewModel.CreatePostCommand.ExecuteAsync(null);

        // Assert
        _mockGoodDeedService.Verify(x => x.CreatePostAsync(It.IsAny<GoodDeedPost>()), Times.Never);
    }

    [Fact]
    public async Task CreatePostAsync_WithEmptyDescription_DoesNotCreate()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.Title = "Test Baþlýk";
        viewModel.Description = "";

        // Act
        await viewModel.CreatePostCommand.ExecuteAsync(null);

        // Assert
        _mockGoodDeedService.Verify(x => x.CreatePostAsync(It.IsAny<GoodDeedPost>()), Times.Never);
    }

    #endregion

    #region Like Post Tests

    [Fact]
    public async Task LikePostAsync_WithValidPost_LikesSuccessfully()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var post = new GoodDeedPost
        {
            PostId = "post123",
            UserId = "otheruser",
            Title = "Test Post",
            LikeCount = 5
        };

        _mockGoodDeedService
            .Setup(x => x.LikePostAsync("post123", _testUser.UserId))
            .ReturnsAsync(ServiceResult<bool>.SuccessResult(true));

        // Act
        await viewModel.LikePostCommand.ExecuteAsync(post);

        // Assert
        _mockGoodDeedService.Verify(x => x.LikePostAsync("post123", _testUser.UserId), Times.Once);
    }

    #endregion

    #region Delete Post Tests

    [Fact]
    public async Task DeletePostAsync_WithValidPost_DeletesSuccessfully()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var post = new GoodDeedPost
        {
            PostId = "post123",
            UserId = _testUser.UserId,
            Title = "Test Post"
        };

        _mockGoodDeedService
            .Setup(x => x.DeletePostAsync("post123", _testUser.UserId))
            .ReturnsAsync(ServiceResult<bool>.SuccessResult(true, "Ýlan silindi"));

        // Act
        await viewModel.DeletePostCommand.ExecuteAsync(post);

        // Assert
        _mockGoodDeedService.Verify(x => x.DeletePostAsync("post123", _testUser.UserId), Times.Once);
    }

    #endregion

    #region Add Comment Tests

    [Fact]
    public async Task AddCommentAsync_WithValidText_AddsComment()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var post = new GoodDeedPost
        {
            PostId = "post123",
            UserId = "otheruser",
            Title = "Test Post"
        };
        viewModel.NewCommentText = "Test yorum";

        _mockGoodDeedService
            .Setup(x => x.AddCommentAsync(It.IsAny<string>(), It.IsAny<Comment>()))
            .ReturnsAsync(ServiceResult<Comment>.SuccessResult(new Comment()));

        // Act
        await viewModel.AddCommentCommand.ExecuteAsync(post);

        // Assert
        _mockGoodDeedService.Verify(x => x.AddCommentAsync("post123", It.IsAny<Comment>()), Times.Once);
        viewModel.NewCommentText.Should().BeEmpty();
    }

    [Fact]
    public async Task AddCommentAsync_WithEmptyText_DoesNotAdd()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var post = new GoodDeedPost { PostId = "post123" };
        viewModel.NewCommentText = "";

        // Act
        await viewModel.AddCommentCommand.ExecuteAsync(post);

        // Assert
        _mockGoodDeedService.Verify(x => x.AddCommentAsync(It.IsAny<string>(), It.IsAny<Comment>()), Times.Never);
    }

    #endregion

    #region Toggle Comments Tests

    [Fact]
    public void ToggleComments_ExpandsAndCollapsesComments()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var post = new GoodDeedPost
        {
            PostId = "post123",
            IsCommentsExpanded = false
        };

        // Act - Expand
        viewModel.ToggleCommentsCommand.Execute(post);

        // Assert
        post.IsCommentsExpanded.Should().BeTrue();

        // Act - Collapse
        viewModel.ToggleCommentsCommand.Execute(post);

        // Assert
        post.IsCommentsExpanded.Should().BeFalse();
    }

    #endregion

    #region GoodDeedPost Model Tests

    [Fact]
    public void GoodDeedPost_HasRequiredProperties()
    {
        // Arrange & Act
        var post = new GoodDeedPost
        {
            PostId = "post123",
            UserId = "user123",
            UserName = "Test User",
            Type = PostType.HelpRequest,
            Title = "Test Baþlýk",
            Description = "Test açýklama",
            CreatedAt = DateTime.UtcNow
        };

        // Assert
        post.PostId.Should().Be("post123");
        post.Title.Should().Be("Test Baþlýk");
        post.Type.Should().Be(PostType.HelpRequest);
        post.IsActive.Should().BeTrue();
        post.LikeCount.Should().Be(0);
        post.CommentCount.Should().Be(0);
    }

    [Theory]
    [InlineData(PostType.HelpRequest)]
    [InlineData(PostType.Announcement)]
    [InlineData(PostType.ThankYou)]
    [InlineData(PostType.Volunteer)]
    public void GoodDeedPost_SupportsAllTypes(PostType type)
    {
        // Arrange & Act
        var post = new GoodDeedPost { Type = type };

        // Assert
        post.Type.Should().Be(type);
    }

    [Fact]
    public void GoodDeedPost_RefreshCommentsUI_UpdatesProperties()
    {
        // Arrange
        var post = new GoodDeedPost
        {
            PostId = "post123",
            Comments = new Dictionary<string, Comment>
            {
                { "c1", new Comment { CommentId = "c1", Text = "Test 1" } },
                { "c2", new Comment { CommentId = "c2", Text = "Test 2" } }
            }
        };

        // Act
        post.CommentCount = 2;
        post.RefreshCommentsUI();

        // Assert
        post.CommentCount.Should().Be(2);
        post.ShowExpandButton.Should().BeFalse(); // 2 or less comments
    }

    [Fact]
    public void GoodDeedPost_ShowExpandButton_WorksCorrectly()
    {
        // Arrange
        var post = new GoodDeedPost { CommentCount = 3 };

        // Act & Assert
        post.ShowExpandButton.Should().BeTrue(); // More than 2 comments

        post.CommentCount = 2;
        post.ShowExpandButton.Should().BeFalse(); // 2 or less comments
    }

    [Fact]
    public void GoodDeedPost_UpdateLikeStatus_SetsIsLikedCorrectly()
    {
        // Arrange
        var post = new GoodDeedPost
        {
            PostId = "post123",
            Likes = new Dictionary<string, bool>
            {
                { "user123", true },
                { "user456", false }
            }
        };

        // Act
        post.UpdateLikeStatus("user123");

        // Assert
        post.IsLiked.Should().BeTrue();

        // Act
        post.UpdateLikeStatus("user789"); // Not in likes

        // Assert
        post.IsLiked.Should().BeFalse();
    }

    #endregion

    #region Comment Model Tests

    [Fact]
    public void Comment_HasRequiredProperties()
    {
        // Arrange & Act
        var comment = new Comment
        {
            CommentId = "comment123",
            PostId = "post123",
            UserId = "user123",
            UserName = "Test User",
            Text = "Test yorum",
            CreatedAt = DateTime.UtcNow
        };

        // Assert
        comment.CommentId.Should().Be("comment123");
        comment.Text.Should().Be("Test yorum");
        comment.UserName.Should().Be("Test User");
    }

    #endregion

    #region Helper Methods

    private GoodDeedBoardViewModel CreateViewModel()
    {
        return new GoodDeedBoardViewModel(
            _mockGoodDeedService.Object,
            _mockAuthService.Object,
            _mockUserProfileService.Object,
            _mockUserStateService.Object
        );
    }

    #endregion
}
