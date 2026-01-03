using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FlexJournalPro.Helpers
{
    public static class TimePickerHelper
    {
        public static readonly DependencyProperty EnableFastInputProperty =
            DependencyProperty.RegisterAttached(
                "EnableFastInput",
                typeof(bool),
                typeof(TimePickerHelper),
                new PropertyMetadata(false, OnEnableFastInputChanged));

        public static bool GetEnableFastInput(DependencyObject obj)
        {
            return (bool)obj.GetValue(EnableFastInputProperty);
        }

        public static void SetEnableFastInput(DependencyObject obj, bool value)
        {
            obj.SetValue(EnableFastInputProperty, value);
        }

        private static void OnEnableFastInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // Використовуємо FrameworkElement, щоб підтримати різні реалізації TimePicker (наприклад, MaterialDesign)
            if (d is FrameworkElement element)
            {
                element.Loaded -= Element_Loaded;
                if ((bool)e.NewValue)
                {
                    element.Loaded += Element_Loaded;
                }
            }
        }

        private static void Element_Loaded(object sender, RoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element == null) return;

            // Шукаємо TextBox всередині контролу TimePicker
            var tb = FindVisualChild<TextBox>(element);
            if (tb != null)
            {
                tb.PreviewTextInput -= TextBox_PreviewTextInput;
                tb.PreviewTextInput += TextBox_PreviewTextInput;

                tb.TextChanged -= TextBox_TextChanged;
                tb.TextChanged += TextBox_TextChanged;

                tb.PreviewKeyDown -= TextBox_PreviewKeyDown;
                tb.PreviewKeyDown += TextBox_PreviewKeyDown;
            }
        }

        private static T FindVisualChild<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T t) return t;

                var childItem = FindVisualChild<T>(child);
                if (childItem != null) return childItem;
            }
            return null;
        }

        private static void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Дозволяємо лише цифри
            if (!e.Text.All(char.IsDigit))
            {
                e.Handled = true;
            }
        }

        private static void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Блокуємо пробіл
            if (e.Key == Key.Space)
            {
                e.Handled = true;
            }
        }

        private static bool _isUpdating;

        private static void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating) return;

            var tb = sender as TextBox;
            if (tb == null) return;

            _isUpdating = true;
            try
            {
                string originalText = tb.Text;
                
                // Залишаємо тільки цифри
                string digits = new string(originalText.Where(char.IsDigit).ToArray());

                // Обмежуємо довжину (HHmm = 4 цифри)
                if (digits.Length > 4) digits = digits.Substring(0, 4);

                string newText = "";

                if (digits.Length > 0)
                {
                    // Години (перші 2 цифри)
                    newText += digits.Substring(0, Math.Min(2, digits.Length));
                }

                if (digits.Length > 2)
                {
                    // Додаємо двокрапку
                    newText += ":";
                    // Хвилини (наступні 2 цифри)
                    newText += digits.Substring(2, Math.Min(2, digits.Length - 2));
                }

                // Оновлюємо текст тільки якщо він змінився
                if (newText != originalText)
                {
                    tb.Text = newText;
                    tb.CaretIndex = newText.Length; // Курсор в кінець
                }
            }
            finally
            {
                _isUpdating = false;
            }
        }
    }
}