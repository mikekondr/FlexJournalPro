using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FlexJournalPro.Helpers
{
    /// <summary>
    /// Допоміжний клас для швидкого введення часу у TimePicker.
    /// </summary>
    public static class TimePickerHelper
    {
        #region Fields

        private static bool _isUpdating;

        #endregion

        #region Attached property

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

        #endregion

        #region Event wiring

        private static void OnEnableFastInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
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
            if (sender is not FrameworkElement element)
            {
                return;
            }

            // Шукаємо TextBox всередині контролу TimePicker.
            var textBox = FindVisualChild<TextBox>(element);
            if (textBox != null)
            {
                textBox.PreviewTextInput -= TextBox_PreviewTextInput;
                textBox.PreviewTextInput += TextBox_PreviewTextInput;

                textBox.TextChanged -= TextBox_TextChanged;
                textBox.TextChanged += TextBox_TextChanged;

                textBox.PreviewKeyDown -= TextBox_PreviewKeyDown;
                textBox.PreviewKeyDown += TextBox_PreviewKeyDown;
            }
        }

        #endregion

        #region Visual tree helper

        private static T? FindVisualChild<T>(DependencyObject depObj) where T : DependencyObject
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

        #endregion

        #region Input handlers

        private static void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Дозволяємо лише цифри.
            if (!e.Text.All(char.IsDigit))
            {
                e.Handled = true;
            }
        }

        private static void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Блокуємо пробіл.
            if (e.Key == Key.Space)
            {
                e.Handled = true;
            }
        }

        private static void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating) return;
            if (sender is not TextBox tb) return;

            _isUpdating = true;
            try
            {
                string originalText = tb.Text;

                // Залишаємо тільки цифри.
                string digits = new string(originalText.Where(char.IsDigit).ToArray());

                // Обмежуємо довжину формату HHmm.
                if (digits.Length > 4)
                {
                    digits = digits.Substring(0, 4);
                }

                string newText = string.Empty;

                if (digits.Length > 0)
                {
                    newText += digits.Substring(0, Math.Min(2, digits.Length));
                }

                if (digits.Length > 2)
                {
                    newText += ":";
                    newText += digits.Substring(2, Math.Min(2, digits.Length - 2));
                }

                // Оновлюємо текст тільки якщо він змінився.
                if (newText != originalText)
                {
                    tb.Text = newText;
                    tb.CaretIndex = newText.Length;
                }
            }
            finally
            {
                _isUpdating = false;
            }
        }

        #endregion
    }
}
