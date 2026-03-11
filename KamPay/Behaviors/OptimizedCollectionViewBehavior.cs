using Microsoft.Maui.Controls;

#if ANDROID
using AndroidX.RecyclerView.Widget;
using Microsoft.Maui.Handlers;
#endif

namespace KamPay.Behaviors
{
    /// <summary>
    /// CollectionView için RecyclerView optimizasyonlarýný uygular
    /// KULLANIM: XAML'de CollectionView.Behaviors altýna ekleyin
    /// </summary>
    public class OptimizedCollectionViewBehavior : Behavior<CollectionView>
    {
        // ? XAML'den yapýlandýrýlabilir özellikler
        public static readonly BindableProperty ItemCacheSizeProperty =
            BindableProperty.Create(
                nameof(ItemCacheSize),
                typeof(int),
                typeof(OptimizedCollectionViewBehavior),
                20);

        public static readonly BindableProperty PrefetchItemCountProperty =
            BindableProperty.Create(
                nameof(PrefetchItemCount),
                typeof(int),
                typeof(OptimizedCollectionViewBehavior),
                4);

        public int ItemCacheSize
        {
            get => (int)GetValue(ItemCacheSizeProperty);
            set => SetValue(ItemCacheSizeProperty, value);
        }

        public int PrefetchItemCount
        {
            get => (int)GetValue(PrefetchItemCountProperty);
            set => SetValue(PrefetchItemCountProperty, value);
        }

        protected override void OnAttachedTo(CollectionView bindable)
        {
            base.OnAttachedTo(bindable);

            bindable.HandlerChanged += OnHandlerChanged;

            if (bindable.Handler != null)
            {
                ApplyOptimizations(bindable);
            }

            System.Diagnostics.Debug.WriteLine($"? OptimizedCollectionViewBehavior attached (ItemCache: {ItemCacheSize}, Prefetch: {PrefetchItemCount})");
        }

        protected override void OnDetachingFrom(CollectionView bindable)
        {
            base.OnDetachingFrom(bindable);
            bindable.HandlerChanged -= OnHandlerChanged;
            System.Diagnostics.Debug.WriteLine("?? OptimizedCollectionViewBehavior detached");
        }

        private void OnHandlerChanged(object? sender, EventArgs e)
        {
            if (sender is CollectionView collectionView)
            {
                ApplyOptimizations(collectionView);
            }
        }

        private void ApplyOptimizations(CollectionView collectionView)
        {
#if ANDROID
            try
            {
                // Platform-specific View'a eriþim için reflection veya Handler API kullan
                if (collectionView.Handler?.PlatformView is RecyclerView recyclerView)
                {
                    // ? 1. Item View Cache Size
                    recyclerView.SetItemViewCacheSize(ItemCacheSize);
                    System.Diagnostics.Debug.WriteLine($"  ? Item cache: {ItemCacheSize}");

                    // ? 2. Nested Scrolling
                    recyclerView.NestedScrollingEnabled = false;
                    System.Diagnostics.Debug.WriteLine("  ? Nested scrolling: false");

                    // ? 3. RecycledViewPool
                    var viewPool = new RecyclerView.RecycledViewPool();
                    viewPool.SetMaxRecycledViews(0, ItemCacheSize + 10);
                    recyclerView.SetRecycledViewPool(viewPool);
                    System.Diagnostics.Debug.WriteLine($"  ? View pool: {ItemCacheSize + 10} items");

                    // ? 4. Layout Manager Optimizasyonlarý
                    if (recyclerView.GetLayoutManager() is LinearLayoutManager layoutManager)
                    {
                        layoutManager.InitialPrefetchItemCount = PrefetchItemCount;
                        System.Diagnostics.Debug.WriteLine($"  ? Prefetch count: {PrefetchItemCount}");
                    }

                    // ? 5. Item Animator (hýzlý animasyonlar)
                    if (recyclerView.GetItemAnimator() is DefaultItemAnimator animator)
                    {
                        animator.AddDuration = 150;
                        animator.RemoveDuration = 150;
                        animator.MoveDuration = 150;
                        animator.ChangeDuration = 150;
                        System.Diagnostics.Debug.WriteLine("  ? Animation duration: 150ms");
                    }

                    // ? 6. Over Scroll Mode
                    recyclerView.OverScrollMode = Android.Views.OverScrollMode.Never;

                    System.Diagnostics.Debug.WriteLine($"?? RecyclerView optimizasyonlarý uygulandý");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"?? RecyclerView optimizasyon hatasý: {ex.Message}");
            }
#else
            System.Diagnostics.Debug.WriteLine("?? OptimizedCollectionViewBehavior sadece Android'de çalýþýr");
#endif
        }
    }
}
