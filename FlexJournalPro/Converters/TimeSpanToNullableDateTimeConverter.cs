using System.Globalization;
using System.Windows.Data;

namespace FlexJournalPro.Converters
{
    /// <summary>
    /// Конвертер для перетворення TimeSpan (з БД) в DateTime? (для TimePicker) та навпаки.
    /// </summary>
    public class TimeSpanToNullableDateTimeConverter : IValueConverter
    {
        // Конвертація TimeSpan -> DateTime? (для вибраного часу)
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // TimeSpan (з БД) -> DateTime (для TimePicker)
            if (value is TimeSpan ts)
            {
                return DateTime.Today.Add(ts);
            }

            return null;
        }

        // Конвертація DateTime? -> TimeSpan
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // DateTime (з TimePicker) -> TimeSpan (у БД)
            if (value is DateTime dt)
            {
                return dt.TimeOfDay;
            }
            return null;
        }
    }
}
