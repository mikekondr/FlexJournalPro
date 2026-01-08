using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace FlexJournalPro.Helpers
{
    public static class DatePickerHelper
    {
        public static readonly DependencyProperty EnableFastInputProperty =
            DependencyProperty.RegisterAttached(
                "EnableFastInput",
                typeof(bool),
                typeof(DatePickerHelper),
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
            if (d is DatePicker dp)
            {
                dp.Loaded -= DatePicker_Loaded;
                if ((bool)e.NewValue)
                {
                    dp.Loaded += DatePicker_Loaded;
                }
            }
        }

        private static void DatePicker_Loaded(object sender, RoutedEventArgs e)
        {
            var dp = sender as DatePicker;
            if (dp == null) return;

            // Знаходимо внутрішній TextBox шаблону DatePicker
            var tb = FindVisualChild<DatePickerTextBox>(dp);
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
            // Дозволяємо вводити лише цифри
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

                // Витягуємо лише цифри з поточного тексту
                string digits = new string(originalText.Where(char.IsDigit).ToArray());

                // Обмежуємо довжину (ddMMyyyy = 8 цифр)
                if (digits.Length > 8) digits = digits.Substring(0, 8);

                string newText = "";

                if (digits.Length > 0)
                {
                    // День (перші 2 цифри)
                    newText += digits.Substring(0, Math.Min(2, digits.Length));
                }

                if (digits.Length > 2)
                {
                    // Додаємо крапку після дня
                    newText += ".";
                    // Місяць (наступні 2 цифри)
                    newText += digits.Substring(2, Math.Min(2, digits.Length - 2));
                }

                if (digits.Length > 4)
                {
                    // Додаємо крапку після місяця
                    newText += ".";
                    // Рік (решта цифр)
                    newText += digits.Substring(4, digits.Length - 4);
                }

                // Оновлюємо текст тільки якщо він змінився (щоб уникнути зациклення)
                if (newText != originalText)
                {
                    tb.Text = newText;
                    // Переміщуємо курсор в кінець для зручного послідовного вводу
                    tb.CaretIndex = newText.Length;
                }
            }
            finally
            {
                _isUpdating = false;
            }
        }
    }
}