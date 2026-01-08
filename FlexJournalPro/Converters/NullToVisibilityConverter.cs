using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FlexJournalPro.Converters
{
    /// <summary>
    /// Конвертер для показу/приховування елементів в залежності від null значення
    /// </summary>
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter == null || (parameter is string && string.Equals((string)parameter, "false", StringComparison.OrdinalIgnoreCase)))
            {
                return value == null ? Visibility.Visible : Visibility.Collapsed;
            }
            else if (parameter is string && string.Equals((string)parameter, "true", StringComparison.OrdinalIgnoreCase))
            {
                return value == null ? Visibility.Collapsed : Visibility.Visible;
            }

            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
