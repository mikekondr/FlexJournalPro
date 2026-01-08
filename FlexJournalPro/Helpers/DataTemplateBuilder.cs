using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using FlexJournalPro.Converters;
using FlexJournalPro.Models;

namespace FlexJournalPro.Helpers
{
    /// <summary>
    /// Допоміжний клас для програмного створення DataTemplate без парсингу XAML
    /// </summary>
    public static class DataTemplateBuilder
    {
        /// <summary>
        /// Створює DataTemplate для перегляду простого текстового поля
        /// </summary>
        public static DataTemplate CreateTextViewTemplate(string fieldName, string format = null, bool isCalculated = false)
        {
            var template = new DataTemplate();
            
            var factory = new FrameworkElementFactory(typeof(TextBlock));
            
            // Binding
            var binding = new Binding($"[{fieldName}]")
            {
                Mode = BindingMode.OneWay,
                TargetNullValue = "",
                FallbackValue = ""
            };
            
            if (!string.IsNullOrEmpty(format))
            {
                binding.StringFormat = format;
            }
            
            factory.SetBinding(TextBlock.TextProperty, binding);
            
            // Властивості
            factory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            factory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            factory.SetValue(TextBlock.PaddingProperty, new Thickness(4, 2, 4, 2));
            factory.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            
            if (isCalculated)
            {
                factory.SetValue(TextBlock.FocusableProperty, false);
                factory.SetValue(TextBlock.ForegroundProperty, Brushes.Gray);
                factory.SetValue(TextBlock.FontStyleProperty, FontStyles.Italic);
            }
            
            template.VisualTree = factory;
            template.Seal();
            
            return template;
        }

        /// <summary>
        /// Створює DataTemplate для редагування текстового поля
        /// </summary>
        public static DataTemplate CreateTextEditTemplate(string fieldName, string format = null, bool isCalculated = false)
        {
            var template = new DataTemplate();
            
            var factory = new FrameworkElementFactory(typeof(TextBox));
            
            // ОПТИМІЗАЦІЯ: Використовуємо LostFocus для звичайних полів, PropertyChanged для обчислюваних
            var updateTrigger = isCalculated ? UpdateSourceTrigger.PropertyChanged : UpdateSourceTrigger.LostFocus;
            
            // Binding - НЕ застосовуємо StringFormat під час редагування
            var binding = new Binding($"[{fieldName}]")
            {
                Mode = isCalculated ? BindingMode.OneWay : BindingMode.TwoWay,
                UpdateSourceTrigger = updateTrigger,
                ValidatesOnExceptions = true,
                TargetNullValue = "",
                FallbackValue = ""
            };
            
            // StringFormat НЕ додаємо - це заважає введенню для валюти та інших форматів
            
            factory.SetBinding(TextBox.TextProperty, binding);
            
            // Властивості
            factory.SetValue(TextBox.BorderThicknessProperty, new Thickness(0));
            factory.SetValue(TextBox.BackgroundProperty, Brushes.Transparent);
            factory.SetValue(TextBox.PaddingProperty, new Thickness(4, 2, 4, 2));
            factory.SetValue(TextBox.VerticalAlignmentProperty, VerticalAlignment.Stretch);
            factory.SetValue(TextBox.VerticalContentAlignmentProperty, VerticalAlignment.Center);
            
            if (isCalculated)
            {
                factory.SetValue(TextBox.IsReadOnlyProperty, true);
                factory.SetValue(TextBox.FocusableProperty, false);
                factory.SetValue(TextBox.IsTabStopProperty, false);
                factory.SetValue(TextBox.ForegroundProperty, Brushes.Gray);
                factory.SetValue(TextBox.FontStyleProperty, FontStyles.Italic);
            }
            else
            {
                // Для звичайних полів дозволяємо фокус та навігацію Tab
                factory.SetValue(TextBox.FocusableProperty, true);
                factory.SetValue(TextBox.IsTabStopProperty, true);
            }
            
            template.VisualTree = factory;
            template.Seal();
            
            return template;
        }

        /// <summary>
        /// Створює DataTemplate для перегляду Boolean (CheckBox)
        /// </summary>
        public static DataTemplate CreateBooleanViewTemplate(string fieldName)
        {
            var template = new DataTemplate();
            
            var factory = new FrameworkElementFactory(typeof(CheckBox));
            
            var binding = new Binding($"[{fieldName}]")
            {
                Mode = BindingMode.OneWay,
                Converter = new SafeBoolConverter()
            };
            
            factory.SetBinding(CheckBox.IsCheckedProperty, binding);
            factory.SetValue(CheckBox.IsEnabledProperty, true);
            factory.SetValue(CheckBox.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            factory.SetValue(CheckBox.VerticalAlignmentProperty, VerticalAlignment.Center);
            factory.SetValue(CheckBox.IsHitTestVisibleProperty, false);
            
            template.VisualTree = factory;
            template.Seal();
            
            return template;
        }

        /// <summary>
        /// Створює DataTemplate для редагування Boolean (CheckBox)
        /// </summary>
        public static DataTemplate CreateBooleanEditTemplate(string fieldName)
        {
            var template = new DataTemplate();
            
            var factory = new FrameworkElementFactory(typeof(CheckBox));
            
            var binding = new Binding($"[{fieldName}]")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                Converter = new SafeBoolConverter()
            };
            
            factory.SetBinding(CheckBox.IsCheckedProperty, binding);
            factory.SetValue(CheckBox.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            factory.SetValue(CheckBox.VerticalAlignmentProperty, VerticalAlignment.Center);
            
            // Дозволяємо фокус та навігацію Tab
            factory.SetValue(CheckBox.FocusableProperty, true);
            factory.SetValue(CheckBox.IsTabStopProperty, true);
            
            template.VisualTree = factory;
            template.Seal();
            
            return template;
        }

        /// <summary>
        /// Перевіряє, чи можна використати compiled template для даної конфігурації
        /// </summary>
        public static bool CanUseCompiledTemplate(ColumnConfig config)
        {
            // Compiled templates підходять для простих типів без складного лейауту
            return config.Type switch
            {
                ColumnType.Text => true,
                ColumnType.Number => true,
                ColumnType.Currency => true,
                ColumnType.Boolean => true,
                ColumnType.Date => string.IsNullOrEmpty(config.Format), // Тільки якщо немає кастомного форматування
                ColumnType.SectionHeader => false,
                ColumnType.Dropdown => false, // Потребує Options
                ColumnType.DropdownEditable => false,
                ColumnType.DateTime => false, // Складний (DatePicker + TimePicker)
                ColumnType.Time => false, // Потребує конвертера
                _ => false
            };
        }
    }
}
