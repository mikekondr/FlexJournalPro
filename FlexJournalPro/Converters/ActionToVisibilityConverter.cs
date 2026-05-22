using FlexJournalPro.Services;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Microsoft.Extensions.DependencyInjection;

namespace FlexJournalPro.Converters
{
    public class ActionToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is string actionKey)
            {
                // Отримуємо DI-контейнер з нашого додатку
                var app = Application.Current as App;
                var authService = app?.ServiceProvider.GetService<IAuthService>();

                if (authService != null && authService.UserCan(actionKey))
                {
                    return Visibility.Visible;
                }
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}