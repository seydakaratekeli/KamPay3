using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Models;
using KamPay.Helpers;

namespace KamPay.Services;
public class FirebaseGoodDeedService : IGoodDeedService
{
    // bu sayfa, Firebase Realtime Database ile etkileşim kurarak iyi niyetli ilanların oluşturulması, alınması, beğenilmesi, silinmesi ve yorum eklenmesi gibi işlevleri sağlar. kullanıcıların iyi niyetli ilanlarla etkileşimde bulunmasını mümkün kılar.
    private readonly FirebaseClient _firebaseClient;
    private const string GoodDeedPostsCollection = "good_deed_posts";

    public FirebaseGoodDeedService()
    {
        _firebaseClient = new FirebaseClient(Constants.FirebaseRealtimeDbUrl);
    }

    public async Task<ServiceResult<GoodDeedPost>> CreatePostAsync(GoodDeedPost post)
    {
        try
        {
            await _firebaseClient
                .Child(GoodDeedPostsCollection)
                .Child(post.PostId)
                .PutAsync(post);

            return ServiceResult<GoodDeedPost>.SuccessResult(post, "İlan paylaşıldı!");
        }
        catch (Exception ex)
        {
            return ServiceResult<GoodDeedPost>.FailureResult("Hata", ex.Message);
        }
    }

    public async Task<ServiceResult<List<GoodDeedPost>>> GetPostsAsync()
    {
        try
        {
            var allPosts = await _firebaseClient
                .Child(GoodDeedPostsCollection)
                .OnceAsync<GoodDeedPost>();

            var posts = allPosts
                .Select(p => p.Object)
                .Where(p => p.IsActive)
                .OrderByDescending(p => p.CreatedAt)
                .ToList();

            return ServiceResult<List<GoodDeedPost>>.SuccessResult(posts);
        }
        catch (Exception ex)
        {
            return ServiceResult<List<GoodDeedPost>>.FailureResult("Hata", ex.Message);
        }
    }

    public async Task<ServiceResult<bool>> LikePostAsync(string postId, string userId)
    {
        try
        {
            if (string.IsNullOrEmpty(postId) || string.IsNullOrEmpty(userId))
                return ServiceResult<bool>.FailureResult("Geçersiz parametreler");

            var postRef = _firebaseClient.Child(Constants.GoodDeedPostsCollection).Child(postId);
            var likesRef = postRef.Child("Likes");

            // Kullanıcının beğeni durumunu kontrol et
            var userLikeRef = likesRef.Child(userId);
            var existingLike = await userLikeRef.OnceSingleAsync<bool?>();

            var post = await postRef.OnceSingleAsync<GoodDeedPost>();
            if (post == null)
                return ServiceResult<bool>.FailureResult("Post bulunamadı");

            if (existingLike.HasValue && existingLike.Value)
            {
                // Beğeni var, kaldır
                await userLikeRef.DeleteAsync();
                post.LikeCount = Math.Max(0, post.LikeCount - 1);
            }
            else
            {
                // Beğeni yok, ekle
                await userLikeRef.PutAsync(true);
                post.LikeCount++;
            }

            // Post'un beğeni sayısını güncelle
            await postRef.Child("LikeCount").PutAsync(post.LikeCount);

            return ServiceResult<bool>.SuccessResult(true);
        }
        catch (Exception ex)
        {
            return ServiceResult<bool>.FailureResult("Beğeni işlemi başarısız", ex.Message);
        }
    }

    public async Task<ServiceResult<bool>> DeletePostAsync(string postId, string userId)
    {
        try
        {
            var post = await _firebaseClient
                .Child(GoodDeedPostsCollection)
                .Child(postId)
                .OnceSingleAsync<GoodDeedPost>();

            if (post == null || post.UserId != userId)
            {
                return ServiceResult<bool>.FailureResult("Yetkiniz yok");
            }

            await _firebaseClient
                .Child(GoodDeedPostsCollection)
                .Child(postId)
                .DeleteAsync();

            return ServiceResult<bool>.SuccessResult(true, "İlan silindi");
        }
        catch (Exception ex)
        {
            return ServiceResult<bool>.FailureResult("Hata", ex.Message);
        }
    }

    public async Task<ServiceResult<Comment>> AddCommentAsync(string postId, Comment comment)
    {
        try
        {
            // ID Güvenliği: Eğer ID yoksa oluştur
            if (string.IsNullOrWhiteSpace(comment.CommentId))
                comment.CommentId = Guid.NewGuid().ToString();

            if (comment.CreatedAt == default)
                comment.CreatedAt = DateTime.UtcNow;

            var commentsNode = _firebaseClient
                .Child(GoodDeedPostsCollection)
                .Child(postId)
                .Child("Comments");

            //  DÜZELTME  PostAsync YERİNE PutAsync KULLANIYORUZ
            // PostAsync: Firebase rastgele bir ID üretir -> Ekranda Çift Kayıt Yapar!
            // PutAsync: Bizim verdiğimiz (comment.CommentId) ID'yi kullanır -> Sorunu Çözer.
            await commentsNode
                .Child(comment.CommentId) // ID'yi biz veriyoruz
                .PutAsync(comment);       // O ID'nin altına kaydediyoruz

            // Yorum Sayısını Güncelle
            var allComments = await commentsNode.OnceAsync<Comment>();
            var commentCount = allComments?.Count ?? 0;

            await _firebaseClient
                .Child(GoodDeedPostsCollection)
                .Child(postId)
                .Child("CommentCount")
                .PutAsync(commentCount);

            return ServiceResult<Comment>.SuccessResult(comment, "Yorum eklendi.");
        }
        catch (Exception ex)
        {
            return ServiceResult<Comment>.FailureResult("Yorum eklenirken bir hata oluştu.", ex.Message);
        }
    }
    public async Task<ServiceResult<List<Comment>>> GetCommentsAsync(string postId)
    {
        try
        {
            var post = await _firebaseClient
                .Child(GoodDeedPostsCollection)
                .Child(postId)
                .OnceSingleAsync<GoodDeedPost>();

            var comments = post?.Comments?.Values
                .OrderBy(c => c.CreatedAt)
                .ToList() ?? new List<Comment>();

            return ServiceResult<List<Comment>>.SuccessResult(comments);
        }
        catch (Exception ex)
        {
            return ServiceResult<List<Comment>>.FailureResult("Yorumlar alınamadı.", ex.Message);
        }
    }

    
    /// <summary>
    /// Kullanıcının tüm panolarındaki isim ve profil fotoğrafı bilgilerini günceller
    /// ✅ OPTIMIZE: Firebase multi-path atomic update ile tek istekle güncelleme
    /// </summary>
    public async Task<ServiceResult<bool>> UpdateUserInfoInPostsAsync(string userId, string newName, string newPhotoUrl)
    {
        try
        {
            // 1️⃣ Kullanıcının gönderilerini bul
            var allPosts = await _firebaseClient
                .Child(GoodDeedPostsCollection)
                .OrderBy("UserId")
                .EqualTo(userId)
                .OnceAsync<GoodDeedPost>();

            if (!allPosts.Any())
            {
                return ServiceResult<bool>.SuccessResult(true, "Güncellenecek pano yok");
            }

            // 2️⃣ ✅ FIX: Multi-path atomic update için tüm yolları topla
            var updates = new Dictionary<string, object>();

            foreach (var postEntry in allPosts)
            {
                var postPath = $"{GoodDeedPostsCollection}/{postEntry.Key}";
                
                if (!string.IsNullOrWhiteSpace(newName))
                {
                    updates[$"{postPath}/UserName"] = newName;
                }
                
                if (!string.IsNullOrWhiteSpace(newPhotoUrl))
                {
                    updates[$"{postPath}/UserProfileImageUrl"] = newPhotoUrl;
                }
            }

            // 3️⃣ ✅ TEK BİR İSTEKLE TÜM YOLLARİ GÜNCELLE
            if (updates.Any())
            {
                await _firebaseClient.UpdateAsync(updates);
                Console.WriteLine($"✅ {allPosts.Count()} pano atomic update ile güncellendi");
            }

            return ServiceResult<bool>.SuccessResult(true, $"{allPosts.Count()} pano güncellendi");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ UpdateUserInfoInPosts hatası: {ex.Message}");
            return ServiceResult<bool>.FailureResult("Panolar güncellenemedi", ex.Message);
        }
    }
}