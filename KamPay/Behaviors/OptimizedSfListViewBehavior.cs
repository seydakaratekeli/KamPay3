using Syncfusion.Maui.ListView;
using Microsoft.Maui.Controls;

namespace KamPay.Behaviors
{
    /// <summary>
    /// Syncfusion SfListView için özel optimizasyonlar ve davranışlar sağlar.
    /// Bu behavior, liste aşağı kaydırıldığında klavyeyi gizler ve
    /// gerekirse kaydırma olaylarına göre UI tepkileri eklemek için bir temel teşkil eder.
    /// </summary>
    public class OptimizedSfListViewBehavior : Behavior<SfListView>
    {
        protected override void OnAttachedTo(SfListView bindable)
        {
            base.OnAttachedTo(bindable);
            bindable.ScrollStateChanged += OnScrollStateChanged;
        }

        protected override void OnDetachingFrom(SfListView bindable)
        {
            bindable.ScrollStateChanged -= OnScrollStateChanged;
            base.OnDetachingFrom(bindable);
        }

        private void OnScrollStateChanged(object? sender, ScrollStateChangedEventArgs e)
        {
            if (e.ScrollState == ListViewScrollState.Idle && Application.Current?.MainPage is Page currentPage)
            {
                HideKeyboard(currentPage);
            }
        }

        private static void HideKeyboard(Element element)
        {
            if (element is VisualElement visualElement && visualElement.IsFocused)
            {
                visualElement.Unfocus();
                return;
            }

            if (element is IVisualTreeElement layout)
            {
                foreach (var child in layout.GetVisualChildren())
                {
                    if (child is Element childElement)
                    {
                        HideKeyboard(childElement);
                    }
                }
            }
        }
    }
}
