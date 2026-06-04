using FlexJournalPro.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FlexJournalPro.Converters
{
    /// <summary>
    /// Конвертер для перевірки прав користувача на виконання певної дії.
    /// Якщо користувач має право, елемент буде Visible, інакше - Collapsed.
    /// </summary>
    public class ActionToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is string action)
            {
                var app = Application.Current as App;
                if (app?.ServiceProvider != null)
                {
                    // Отримуємо DI-контейнер з нашого додатку
                    var authService = app.ServiceProvider.GetRequiredService<IAuthService>();
                    bool hasPermission = authService.UserCan(action);

                    return hasPermission ? Visibility.Visible : Visibility.Collapsed;
                }
            }

            // Якщо параметр не є рядком або виникла помилка, приховуємо елемент
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}