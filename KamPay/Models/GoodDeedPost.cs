using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Models;
using Newtonsoft.Json;
// Aşağıdaki namespace'i ekleyin
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

    // Observable yaptık ki sayı değişince ekranda hemen güncellensin
    [ObservableProperty]
    private int likeCount;

    [ObservableProperty]
    private int commentCount;

    public string? ContactInfo { get; set; }

    public Dictionary<string, Comment> Comments { get; set; } = new Dictionary<string, Comment>();

    // 🔥 YENİ: Beğenen kullanıcıların listesi
    public Dictionary<string, bool> Likes { get; set; } = new Dictionary<string, bool>();

    [JsonIgnore] // <-- Bu attribute, özelliğin Firebase'e kaydedilmesini engeller.
    public bool IsOwner { get; set; }

        [ObservableProperty]
    [property: JsonIgnore]
    private bool isLiked;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisibleComments))] // Bu değişince listeyi de güncelle
    [NotifyPropertyChangedFor(nameof(ShowMoreButtonText))]
    [property: JsonIgnore]
    private bool isCommentsExpanded;

    // Eğer genişletildiyse hepsini, değilse son 2 tanesini göster
    // 🔥 GÜNCELLENMİŞ VE GÜVENLİ KOD
    public IEnumerable<Comment> VisibleComments
    {
        get
        {
            // Eğer Comments null ise boş bir liste döndür, çökmesini engelle.
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
    // Yorum listesi güncellendiğinde UI'ı tetiklemek için yardımcı metod
    public void RefreshCommentsUI()
    {
        // Comments null ise işlem yapma
        if (Comments == null) Comments = new Dictionary<string, Comment>();

        OnPropertyChanged(nameof(VisibleComments));
        OnPropertyChanged(nameof(ShowExpandButton));
        OnPropertyChanged(nameof(ShowMoreButtonText));
    }

    // 🔥 YENİ: Kullanıcının beğenip beğenmediğini kontrol et
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