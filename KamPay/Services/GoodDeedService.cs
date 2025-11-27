/*gereksizse sil
 * 
 * 
 * using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KamPay.Services
{
    public class GoodDeedService : IGoodDeedService
    {
        private readonly FirebaseClient _firebaseClient;
        private const string GoodDeedPostsCollection = "good_deed_posts";

        public GoodDeedService(string firebaseUrl)
        {
            _firebaseClient = new FirebaseClient(firebaseUrl);
        }

        public async Task<ServiceResult<List<GoodDeedPost>>> GetPostsAsync()
        {
            try
            {
                var posts = await _firebaseClient
                    .Child(GoodDeedPostsCollection)
                    .OnceAsync<GoodDeedPost>();

                var list = posts.Select(p =>
                {
                    var item = p.Object;
                    item.PostId = p.Key;
                    return item;
                }).OrderByDescending(p => p.CreatedAt).ToList();

                return ServiceResult<List<GoodDeedPost>>.SuccessResult(list);
            }
            catch (Exception ex)
            {
                return ServiceResult<List<GoodDeedPost>>.FailureResult("Veri alýnamadý", ex.Message);
            }
        }

        public async Task<ServiceResult<GoodDeedPost>> CreatePostAsync(GoodDeedPost post)
        {
            try
            {
                post.CreatedAt = DateTime.UtcNow;

                var result = await _firebaseClient
                    .Child(GoodDeedPostsCollection)
                    .PostAsync(post);

                post.PostId = result.Key;

                return ServiceResult<GoodDeedPost>.SuccessResult(post, "Paylaþým yapýldý");
            }
            catch (Exception ex)
            {
                return ServiceResult<GoodDeedPost>.FailureResult("Hata", ex.Message);
            }
        }

        public async Task<ServiceResult<bool>> LikePostAsync(string postId, string userId)
        {
            try
            {
                var post = await _firebaseClient
                    .Child(GoodDeedPostsCollection)
                    .Child(postId)
                    .OnceSingleAsync<GoodDeedPost>();

                if (post == null)
                    return ServiceResult<bool>.FailureResult("Post bulunamadý");

                post.LikeCount++;

                await _firebaseClient
                    .Child(GoodDeedPostsCollection)
                    .Child(postId)
                    .PutAsync(post);

                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Hata", ex.Message);
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
                    return ServiceResult<bool>.FailureResult("Yetkiniz yok");

                await _firebaseClient
                    .Child(GoodDeedPostsCollection)
                    .Child(postId)
                    .DeleteAsync();

                return ServiceResult<bool>.SuccessResult(true, "Ýlan silindi");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Hata", ex.Message);
            }
        }
    }
}
*/