using System.Windows;
using System.Windows.Controls;

namespace FlexJournalPro.Behaviors
{
    /// <summary>
    /// Behavior для вимкнення автоматичного скролу до вибраного елементу в ListBox
    /// </summary>
    public static class ListBoxBehavior
    {
        public static readonly DependencyProperty DisableScrollIntoViewProperty =
            DependencyProperty.RegisterAttached(
                "DisableScrollIntoView",
                typeof(bool),
                typeof(ListBoxBehavior),
                new PropertyMetadata(false, OnDisableScrollIntoViewChanged));

        public static bool GetDisableScrollIntoView(DependencyObject obj)
        {
            return (bool)obj.GetValue(DisableScrollIntoViewProperty);
        }

        public static void SetDisableScrollIntoView(DependencyObject obj, bool value)
        {
            obj.SetValue(DisableScrollIntoViewProperty, value);
        }

        private static void OnDisableScrollIntoViewChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ListBox listBox && e.NewValue is bool disable && disable)
            {
                listBox.SelectionChanged += ListBox_SelectionChanged;
            }
        }

        private static void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListBox listBox && e.AddedItems.Count > 0)
            {
                listBox.ScrollIntoView(e.AddedItems[0]);
            }
        }
    }
}
