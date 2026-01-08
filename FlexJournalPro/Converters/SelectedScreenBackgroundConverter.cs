using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace FlexJournalPro.Converters
{
    /// <summary>
    /// Конвертер для підсвічування вибраного екрану
    /// </summary>
    public class SelectedScreenBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isSelected && isSelected)
            {
                return new SolidColorBrush(Color.FromArgb(77, 0, 97, 164)); // #4D0061A4
            }
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
