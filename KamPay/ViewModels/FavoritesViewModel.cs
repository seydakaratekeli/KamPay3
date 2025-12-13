using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KamPay.Models;
using KamPay.Services;
using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Helpers;
using System.Reactive.Linq;
using KamPay.Views;
using Firebase.Database.Streaming;

namespace KamPay.ViewModels
{
    public partial class FavoritesViewModel : ObservableObject, IDisposable
    {
        private readonly IFavoriteService _favoriteService;
        private readonly IAuthenticationService _authService;
        private IDisposable _favoritesSubscription;
        private readonly FirebaseClient _firebaseClient = new(Constants.FirebaseRealtimeDbUrl);

        private bool _isInitialized = false;
        private readonly HashSet<string> _favoriteIds = new();

        [ObservableProperty]
        private bool isLoading = true;

        [ObservableProperty]
        private string emptyMessage = "Henüz favori ürününüz yok";

        [ObservableProperty]
        private bool isRefreshing;

        public ObservableCollection<Favorite> FavoriteItems { get; } = new();

        public FavoritesViewModel(IFavoriteService favoriteService, IProductService productService, IAuthenticationService authService)
        {
            _favoriteService = favoriteService;
            _authService = authService;
        }

        public async Task InitializeAsync()
        {
            if (_isInitialized)
            {
                Console.WriteLine("✅ Favoriler cache'den gösteriliyor");
                return;
            }

            await StartListeningForFavoritesAsync();
        }

        private async Task StartListeningForFavoritesAsync()
        {
            if (_favoritesSubscription != null) return;

            try
            {
                IsLoading = true;

                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    EmptyMessage = "Favorileri görmek için giriş yapmalısınız.";
                    IsLoading = false;
                    return;
                }

                // 1. ADIM: Önce mevcut veriyi "Bir Kez" (Snapshot) çek
                // Bu sayede liste boşsa bile loading'i kapatabiliriz.
                var initialSnapshot = await _firebaseClient
                    .Child(Constants.FavoritesCollection)
                    .OrderBy("UserId")
                    .EqualTo(currentUser.UserId)
                    .OnceAsync<Favorite>();

                // Listeyi temizle ve doldur
                FavoriteItems.Clear();
                _favoriteIds.Clear();

                foreach (var item in initialSnapshot)
                {
                    var fav = item.Object;
                    fav.FavoriteId = item.Key;

                    if (!_favoriteIds.Contains(fav.FavoriteId))
                    {
                        FavoriteItems.Add(fav);
                        _favoriteIds.Add(fav.FavoriteId);
                    }
                }

                //  KRİTİK: Veri olsun ya da olmasın yüklemeyi bitir.
                IsLoading = false;
                _isInitialized = true;

                // Boş mesajını güncelle (Gerçi XAML'da EmptyView var ama yine de duralım)
                EmptyMessage = FavoriteItems.Any() ? string.Empty : "Henüz favori ürününüz yok.";

                // 2. ADIM: Canlı Dinlemeyi (Stream) Başlat
                // Buffer kullanımını kaldırdık veya basitleştirdik ki olayları kaçırmayalım
                _favoritesSubscription = _firebaseClient
                    .Child(Constants.FavoritesCollection)
                    .OrderBy("UserId")
                    .EqualTo(currentUser.UserId)
                    .AsObservable<Favorite>()
                    .Where(e => e.Object != null) // Buffer olmadan doğrudan akış
                    .Subscribe(
                        e =>
                        {
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                try
                                {
                                    HandleSingleFirebaseEvent(e);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"❌ Favorite event hatası: {ex.Message}");
                                }
                            });
                        },
                        error =>
                        {
                            Console.WriteLine($"❌ Firebase listener hatası: {error.Message}");
                        });

                Console.WriteLine(" Favoriler real-time listener başlatıldı");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Favoriler yüklenirken hata: {ex.Message}");
                EmptyMessage = "Favoriler yüklenemedi.";
                IsLoading = false;
            }
        }

        // Tekil olay işleyici (Buffer yerine bunu kullanıyoruz, daha güvenli)
        private void HandleSingleFirebaseEvent(FirebaseEvent<Favorite> e)
        {
            var favorite = e.Object;
            favorite.FavoriteId = e.Key;

            var existing = FavoriteItems.FirstOrDefault(f => f.FavoriteId == favorite.FavoriteId);

            switch (e.EventType)
            {
                case FirebaseEventType.InsertOrUpdate:
                    if (existing != null)
                    {
                        // Güncelleme
                        var index = FavoriteItems.IndexOf(existing);
                        FavoriteItems[index] = favorite;
                    }
                    else
                    {
                        // Ekleme (Duplicate check - Snapshot ile çakışmayı önle)
                        if (!_favoriteIds.Contains(favorite.FavoriteId))
                        {
                            FavoriteItems.Insert(0, favorite);
                            _favoriteIds.Add(favorite.FavoriteId);
                        }
                    }
                    break;

                case FirebaseEventType.Delete:
                    if (existing != null)
                    {
                        FavoriteItems.Remove(existing);
                        _favoriteIds.Remove(favorite.FavoriteId);
                    }
                    break;
            }

            // Liste her değiştiğinde boş mesajını kontrol et
            EmptyMessage = FavoriteItems.Any() ? string.Empty : "Henüz favori ürününüz yok.";
        }

        [RelayCommand]
        private async Task ProductTappedAsync(Favorite favorite)
        {
            if (favorite == null) return;
            await Shell.Current.GoToAsync($"{nameof(ProductDetailPage)}?productId={favorite.ProductId}");
        }

        [RelayCommand]
        private async Task GoToProductDetailAsync(Favorite favorite)
        {
            if (favorite == null || string.IsNullOrEmpty(favorite.ProductId)) return;
            await Shell.Current.GoToAsync($"{nameof(ProductDetailPage)}?ProductId={favorite.ProductId}");
        }

        [RelayCommand]
        private async Task RemoveFavoriteAsync(Favorite favorite)
        {
            if (favorite == null) return;

            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser != null)
                {
                    // Optimistik silme (Hemen arayüzden sil)
                    if (FavoriteItems.Contains(favorite))
                    {
                        FavoriteItems.Remove(favorite);
                        _favoriteIds.Remove(favorite.FavoriteId);
                    }

                    var result = await _favoriteService.RemoveFromFavoritesAsync(currentUser.UserId, favorite.ProductId);
                    if (!result.Success)
                    {
                        // Hata olursa geri yükle (İsteğe bağlı)
                        await Application.Current.MainPage.DisplayAlert("Hata", result.Message, "Tamam");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Favori çıkarma hatası: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task RefreshFavoritesAsync()
        {
            if (isRefreshing) return;
            try
            {
                isRefreshing = true;
                _isInitialized = false; // Cache'i geçersiz kıl
                await StartListeningForFavoritesAsync(); // Yeniden yükle
            }
            finally
            {
                isRefreshing = false;
            }
        }

        public void Dispose()
        {
            Console.WriteLine("🧹 FavoritesViewModel dispose ediliyor...");
            _favoritesSubscription?.Dispose();
            _favoritesSubscription = null;
            _favoriteIds.Clear();
            _isInitialized = false;
        }
    }
}