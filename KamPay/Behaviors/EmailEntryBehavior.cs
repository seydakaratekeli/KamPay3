using Microsoft.Maui.Controls;

namespace KamPay.Behaviors
{
    /// <summary>
    /// E-posta girişini otomatik küçük harfe çevirir ve baştaki/sondaki boşlukları kaldırır.
    /// Kullanım: &lt;Entry&gt;&lt;Entry.Behaviors&gt;&lt;behaviors:EmailEntryBehavior/&gt;&lt;/Entry.Behaviors&gt;&lt;/Entry&gt;
    /// </summary>
    public class EmailEntryBehavior : Behavior<Entry>
    {
        protected override void OnAttachedTo(Entry entry)
        {
            base.OnAttachedTo(entry);
            entry.TextChanged += OnTextChanged;
            entry.Unfocused += OnUnfocused;
        }

        protected override void OnDetachingFrom(Entry entry)
        {
            base.OnDetachingFrom(entry);
            entry.TextChanged -= OnTextChanged;
            entry.Unfocused -= OnUnfocused;
        }

        private void OnTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (sender is not Entry entry || e.NewTextValue == null) return;

            // Yazarken boşluk girişini engelle
            if (e.NewTextValue.Contains(' '))
            {
                entry.Text = e.NewTextValue.Replace(" ", "");
            }
        }

        private void OnUnfocused(object? sender, FocusEventArgs e)
        {
            if (sender is not Entry entry || entry.Text == null) return;
            // Odaktan çıkınca küçük harf + trim uygula
            var cleaned = entry.Text.Trim().ToLowerInvariant();
            if (entry.Text != cleaned)
                entry.Text = cleaned;
        }
    }
}