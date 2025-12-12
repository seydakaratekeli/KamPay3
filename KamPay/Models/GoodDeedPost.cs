using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Models;
using Newtonsoft.Json;
using Microsoft.Maui.Controls.Internals;

namespace KamPay.Models;

[Preserve(AllMembers = true)]
public partial class GoodDeedPost : ObservableObject
{
    public string PostId { get; set; }
    public string UserId { get; set; }
    public string UserName { get; set; }
    public string UserProfileImageUrl { get; set; }

    public PostType Type { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }

    [ObservableProperty]
    private int likeCount;

    [ObservableProperty]
    private int commentCount;

    public string? ContactInfo { get; set; }

    public Dictionary<string, Comment> Comments { get; set; } = new Dictionary<string, Comment>();

    public Dictionary<string, bool> Likes { get; set; } = new Dictionary<string, bool>();

    [JsonIgnore]
    public bool IsOwner { get; set; }

    [ObservableProperty]
    [property: JsonIgnore]
    private bool isLiked;

    // 🔥 YENİ EKLENDİ 1: Yorum yazılan metin (Her ilanınki kendine özel olsun diye)
    [ObservableProperty]
    [property: JsonIgnore] // Firebase'e kaydedilmesin, sadece ekranda tutulsun
    private string draftComment;

    // 🔥 YENİ EKLENDİ 2: Yorum kutusunun görünürlüğü
    [ObservableProperty]
    [property: JsonIgnore]
    private bool isCommentBoxVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisibleComments))]
    [NotifyPropertyChangedFor(nameof(ShowMoreButtonText))]
    [property: JsonIgnore]
    private bool isCommentsExpanded;

    public IEnumerable<Comment> VisibleComments
    {
        get
        {
            if (Comments == null) return Enumerable.Empty<Comment>();

            return IsCommentsExpanded
                ? Comments.Values.OrderBy(c => c.CreatedAt)
                : Comments.Values.OrderBy(c => c.CreatedAt).Take(2);
        }
    }

    public string ShowMoreButtonText => IsCommentsExpanded
        ? "Yorumları Gizle"
        : $"Tüm Yorumları Gör ({CommentCount})";

    public bool ShowExpandButton => CommentCount > 2;

    public GoodDeedPost()
    {
        PostId = Guid.NewGuid().ToString();
        CreatedAt = DateTime.UtcNow;
        IsActive = true;
        LikeCount = 0;
        CommentCount = 0;
        UserProfileImageUrl = "default_avatar.png";
        Likes = new Dictionary<string, bool>();
    }

    public void RefreshCommentsUI()
    {
        if (Comments == null) Comments = new Dictionary<string, Comment>();

        OnPropertyChanged(nameof(VisibleComments));
        OnPropertyChanged(nameof(ShowExpandButton));
        OnPropertyChanged(nameof(ShowMoreButtonText));
    }

    public void UpdateLikeStatus(string userId)
    {
        if (Likes != null && Likes.ContainsKey(userId))
        {
            IsLiked = Likes[userId];
        }
        else
        {
            IsLiked = false;
        }
    }
}

public enum PostType
{
    HelpRequest = 0,   // Yardım talebi
    Announcement = 1,  // Duyuru
    ThankYou = 2,      // Teşekkür
    Volunteer = 3      // Gönüllü arıyorum
}