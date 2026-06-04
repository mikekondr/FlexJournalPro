using FlexJournalPro.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FlexJournalPro.Converters
{
    /// <summary>
    /// Конвертер для перевірки прав користувача на виконання певної дії.
    /// Використовується для встановлення властивості IsReadOnly або IsEnabled на основі прав користувача.
    /// </summary>
    public class ActionToBooleanConverter : IValueConverter
    {
        public bool Invert { get; set; } = false;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is string action)
            {
                var app = Application.Current as App;
                if (app?.ServiceProvider != null)
                {
                    var authService = app.ServiceProvider.GetRequiredService<IAuthService>();
                    bool hasPermission = authService.UserCan(action);

                    return Invert ? !hasPermission : hasPermission;
                }
            }

            // Якщо даних немає, за замовчуванням блокуємо (IsReadOnly = True або False залежно від Invert)
            return Invert;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}