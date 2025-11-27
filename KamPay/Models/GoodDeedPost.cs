
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Models;
using Newtonsoft.Json;

namespace KamPay.Models;


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

    [JsonIgnore] // <-- Bu attribute, özelliğin Firebase'e kaydedilmesini engeller.
    public bool IsOwner { get; set; }

    //🔥 YENİ: Beğeni Durumu(UI İçin)
        [ObservableProperty]
    [property: JsonIgnore]
    private bool isLiked;

    // 🔥 YENİ: Yorumlar genişletildi mi?
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisibleComments))] // Bu değişince listeyi de güncelle
    [NotifyPropertyChangedFor(nameof(ShowMoreButtonText))]
    [property: JsonIgnore]
    private bool isCommentsExpanded;

    // 🔥 YENİ: Ekranda Görünen Yorumlar
    // Eğer genişletildiyse hepsini, değilse son 2 tanesini göster
    public IEnumerable<Comment> VisibleComments =>
        IsCommentsExpanded
            ? Comments.Values.OrderBy(c => c.CreatedAt)
            : Comments.Values.OrderBy(c => c.CreatedAt).Take(2);

    // 🔥 YENİ: Buton Metni
    public string ShowMoreButtonText => IsCommentsExpanded
        ? "Yorumları Gizle"
        : $"Tüm Yorumları Gör ({CommentCount})";

    // 🔥 YENİ: "Daha Fazla Göster" butonu görünsün mü?
    public bool ShowExpandButton => CommentCount > 2;

    public GoodDeedPost()
    {
        PostId = Guid.NewGuid().ToString();
        CreatedAt = DateTime.UtcNow;
        IsActive = true;
        LikeCount = 0;
        CommentCount = 0;
        UserProfileImageUrl = "default_avatar.png";
    }
    // Yorum listesi güncellendiğinde UI'ı tetiklemek için yardımcı metod
    public void RefreshCommentsUI()
    {
        OnPropertyChanged(nameof(VisibleComments));
        OnPropertyChanged(nameof(ShowExpandButton));
        OnPropertyChanged(nameof(ShowMoreButtonText));
    }
}

public enum PostType
{
    HelpRequest = 0,   // Yardım talebi
    Announcement = 1,  // Duyuru
    ThankYou = 2,      // Teşekkür
    Volunteer = 3      // Gönüllü arıyorum
}