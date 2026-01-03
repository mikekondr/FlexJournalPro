using System;
using System.Globalization;
using System.Windows.Data;

namespace FlexJournalPro.Converters
{
    /// <summary>
    /// Універсальний конвертер для Boolean.
    /// Приймає: bool, int (0/1), long (0/1), string ("true"/"false", "1"/"0").
    /// Повертає: bool? для CheckBox.IsChecked.
    /// </summary>
    public class SafeBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || value == DBNull.Value)
                return null;

            if (value is bool b)
                return b;

            if (value is int i)
                return i != 0;

            if (value is long l)
                return l != 0;

            if (value is string s)
            {
                if (bool.TryParse(s, out var res)) return res;
                if (int.TryParse(s, out var n)) return n != 0;
                return false;
            }

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return b;
            
            return false;
        }
    }
}