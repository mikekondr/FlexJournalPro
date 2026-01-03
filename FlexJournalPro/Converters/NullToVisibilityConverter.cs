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
            return value == null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
