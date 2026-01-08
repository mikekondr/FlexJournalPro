using MaterialDesignThemes.Wpf;
using System;
using System.Globalization;
using System.Windows.Data;

namespace FlexJournalPro.Converters
{
    /// <summary>
    /// Конвертер для зміни іконки sidebar (Menu/MenuOpen)
    /// </summary>
    public class SidebarIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isExpanded)
            {
                return isExpanded ? PackIconKind.MenuOpen : PackIconKind.MenuClose;
            }
            return PackIconKind.Menu;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
