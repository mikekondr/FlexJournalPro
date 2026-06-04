using System.Windows;
using System.Windows.Controls;

namespace FlexJournalPro.Helpers
{
    /// <summary>
    /// Допоміжний клас для прив'язки PasswordBox до властивості Password.
    /// Оскільки у WPF PasswordBox не підтримує двосторонню прив'язку до властивості моделі, 
    /// цей клас використовує прикріплені властивості для синхронізації значення пароля між PasswordBox 
    /// та властивістю в ViewModel.
    /// </summary>
    public static class PasswordHelper
    {
        #region Attached properties

        public static readonly DependencyProperty PasswordProperty =
            DependencyProperty.RegisterAttached(
                "Password",
                typeof(string),
                typeof(PasswordHelper),
                new FrameworkPropertyMetadata(string.Empty, OnPasswordPropertyChanged));

        public static readonly DependencyProperty AttachProperty =
            DependencyProperty.RegisterAttached(
                "Attach",
                typeof(bool),
                typeof(PasswordHelper),
                new PropertyMetadata(false, Attach));

        public static void SetAttach(DependencyObject dp, bool value) => dp.SetValue(AttachProperty, value);
        public static bool GetAttach(DependencyObject dp) => (bool)dp.GetValue(AttachProperty);

        public static string GetPassword(DependencyObject dp) => (string)dp.GetValue(PasswordProperty);
        public static void SetPassword(DependencyObject dp, string value) => dp.SetValue(PasswordProperty, value);

        #endregion

        #region Event handlers

        private static void Attach(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                if ((bool)e.OldValue)
                {
                    passwordBox.PasswordChanged -= PasswordChanged;
                }

                if ((bool)e.NewValue)
                {
                    passwordBox.PasswordChanged += PasswordChanged;
                }
            }
        }

        private static void PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                SetPassword(passwordBox, passwordBox.Password);
            }
        }

        private static void OnPasswordPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                passwordBox.PasswordChanged -= PasswordChanged;

                string newPassword = e.NewValue as string ?? string.Empty;
                if (passwordBox.Password != newPassword)
                {
                    passwordBox.Password = newPassword;
                }

                passwordBox.PasswordChanged += PasswordChanged;
            }
        }

        #endregion
    }
}
