using System;
using System.Globalization;
using System.Windows.Data;

namespace FlexJournalPro.Converters
{
    public class TimeSpanToNullableDateTimeConverter : IValueConverter
    {
        // Convert TimeSpan -> DateTime? (for SelectedTime)
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // TimeSpan (з БД) -> DateTime (для TimePicker)
            if (value is TimeSpan ts)
            {
                return DateTime.Today.Add(ts);
            }
            
            return null;
        }

        // ConvertBack DateTime? -> TimeSpan
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
