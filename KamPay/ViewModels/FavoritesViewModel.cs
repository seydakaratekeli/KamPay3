// KamPay/ViewModels/FavoritesViewModel.cs

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

        // 🔥 Cache kontrolü - Listener sürekli açık kalacak
        private bool _isInitialized = false;
        private readonly HashSet<string> _favoriteIds = new(); // Duplicate kontrolü

        [ObservableProperty]
        private bool isLoading = true;

        [ObservableProperty]
        private string emptyMessage = "Henüz favori ürününüz yok";

        [ObservableProperty]
        private bool isRefreshing; // Bunu ekleyin (mevcut değilse)

        public ObservableCollection<Favorite> FavoriteItems { get; } = new();

        public FavoritesViewModel(IFavoriteService favoriteService, IProductService productService, IAuthenticationService authService)
        {
            _favoriteService = favoriteService;
            _authService = authService;
        }

        // 🔥 DÜZELTİLDİ: public yapıldı
        public async Task InitializeAsync()
        {
            // 🔥 Eğer zaten başlatılmışsa, sadece boş kontrol yap
            if (_isInitialized)
            {
                Console.WriteLine("✅ Favoriler cache'den gösteriliyor (Listener aktif)");
                return;
            }

            await StartListeningForFavoritesAsync();
        }

        private async Task StartListeningForFavoritesAsync()
        {
            if (_favoritesSubscription != null)
            {
                Console.WriteLine("⚠️ Listener zaten aktif");
                return;
            }

            IsLoading = true;

            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    EmptyMessage = "Favorileri görmek için giriş yapmalısınız.";
                    IsLoading = false;
                    return;
                }

                // 🔥 Real-time listener başlat (Buffer ile optimize)
                _favoritesSubscription = _firebaseClient
                    .Child(Constants.FavoritesCollection)
                    .OrderBy("UserId")
                    .EqualTo(currentUser.UserId)
                    .AsObservable<Favorite>()
                    .Buffer(TimeSpan.FromMilliseconds(200)) // 🔥 200ms batch
                    .Where(batch => batch.Any())
                    .Subscribe(
                        events =>
                        {
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                try
                                {
                                    ProcessFavoriteBatch(events);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"❌ Favorite batch hatası: {ex.Message}");
                                }
                                finally
                                {
                                    if (!_isInitialized)
                                    {
                                        _isInitialized = true;
                                        IsLoading = false;
                                        Console.WriteLine("✅ Favoriler listener başlatıldı");
                                    }
                                }
                            });
                        },
                        error =>
                        {
                            Console.WriteLine($"❌ Firebase listener hatası: {error.Message}");
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                IsLoading = false;
                                EmptyMessage = "Favoriler yüklenemedi.";
                            });
                        });

                Console.WriteLine("🔥 Favoriler real-time listener başlatıldı");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Favoriler yüklenirken hata: {ex.Message}");
                EmptyMessage = "Favoriler yüklenemedi.";
                IsLoading = false;
            }
        }

        // ProcessFavoriteBatch metodunu bulun (satır 131 civarı) ve güncelleyin:

        private void ProcessFavoriteBatch(IList<FirebaseEvent<Favorite>> events)
        {
            bool hasChanges = false;

            foreach (var e in events)
            {
                if (e.Object == null) continue;

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
                            // 🔥 Duplicate check
                            if (!_favoriteIds.Contains(favorite.FavoriteId))
                            {
                                FavoriteItems.Insert(0, favorite);
                                _favoriteIds.Add(favorite.FavoriteId);
                            }
                        }
                        hasChanges = true;
                        break;

                    case FirebaseEventType.Delete:
                        if (existing != null)
                        {
                            FavoriteItems.Remove(existing);
                            _favoriteIds.Remove(favorite.FavoriteId);
                            hasChanges = true;
                        }
                        break;
                }
            }

            // 🔥 İLK VERİ GELDİĞİNDE LOADING'İ KAPAT
            if (hasChanges && IsLoading)
            {
                IsLoading = false;
                _isInitialized = true;
            }

            EmptyMessage = FavoriteItems.Any() ? string.Empty : "Henüz favori ürününüz yok. ";
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
            if (favorite == null || string.IsNullOrEmpty(favorite.ProductId))
            {
                await Shell.Current.DisplayAlert("Hata", "Ürün bilgisi eksik.", "Tamam");
                return;
            }

            // Shell URI tabanlı navigasyon
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
                    var result = await _favoriteService.RemoveFromFavoritesAsync(currentUser.UserId, favorite.ProductId);

                    if (!result.Success)
                    {
                        await Application.Current.MainPage.DisplayAlert("Hata", result.Message, "Tamam");
                    }
                    // ✅ Real-time listener otomatik güncelleyecek
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Favori çıkarma hatası: {ex.Message}");
                await Application.Current.MainPage.DisplayAlert("Hata", "Favorilerden çıkarılamadı", "Tamam");
            }
        }

        [RelayCommand]
        private async Task RefreshFavoritesAsync()
        {
            if (isRefreshing) return;

            try
            {
                isRefreshing = true;
                // Listener zaten çalışıyor, sadece UI'ı güncellemek için kısa bir delay
                await Task.Delay(500);
            }
            finally
            {
                isRefreshing = false;
            }
        }

        public void Dispose()
        {
            // 🔥 SADECE uygulama kapanırken çağrılmalı
            Console.WriteLine("🧹 FavoritesViewModel dispose ediliyor...");
            _favoritesSubscription?.Dispose();
            _favoritesSubscription = null;
            _favoriteIds.Clear();
            _isInitialized = false;
        }
    }
}