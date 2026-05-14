using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FlexJournalPro.Services;
using Microsoft.Data.Sqlite;

namespace FlexJournalPro.Windows
{
    /// <summary>
    /// Вікно відновлення доступу до бази даних за допомогою майстер-ключа.
    /// </summary>
    public partial class RecoveryWindow : Window
    {
        private readonly KeyManagementService _keyManager;

        public RecoveryWindow(KeyManagementService keyManager)
        {
            InitializeComponent();
            _keyManager = keyManager;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void RecoverButton_Click(object sender, RoutedEventArgs e)
        {
            string masterKeyHex = MasterKeyTextBox.Text?.Trim() ?? string.Empty;
            string newPassword = NewPasswordBox.Password ?? string.Empty;
            string confirmPassword = ConfirmPasswordBox.Password ?? string.Empty;

            MasterKeyError.Visibility = Visibility.Collapsed;
            PasswordError.Visibility = Visibility.Collapsed;

            if (!ValidateInputs(masterKeyHex, newPassword, confirmPassword))
                return;

            try
            {
                // Парсимо HEX-ключ
                string cleanHex = masterKeyHex.Replace("-", "").Trim().ToUpper();

                if (cleanHex.Length != 64 || !cleanHex.All(c => "0123456789ABCDEF".Contains(c)))
                {
                    ShowError(MasterKeyError, "Майстер-ключ помилковий: він має складатись з 64 HEX-символів.");
                    return;
                }

                byte[] keyBytes = Enumerable.Range(0, cleanHex.Length / 2)
                    .Select(x => Convert.ToByte(cleanHex.Substring(x * 2, 2), 16))
                    .ToArray();

                string base64Dek = Convert.ToBase64String(keyBytes);

                // 1. Ініціалізуємо БД за допомогою введеного ключа.
                // Якщо ключ недійсний, конструктор/Open викине виключення (зазвичай SqliteException).
                App.Database = new DatabaseService(base64Dek);

                // Якщо ми дійшли сюди - база розшифрована успішно!

                // 2. Отримуємо РЕАЛЬНОГО адміністратора (перший користувач з Id=1)
                var authService = new AuthService(App.Database);
                var adminUser = authService.FindUserById(1);

                if (adminUser == null)
                {
                    ShowError(MasterKeyError, "Структура бази даних пошкоджена: не знайдено запису адміністратора (Id=1).");
                    return;
                }

                // 3. Очищуємо Keystore від старих / помилкових записів
                _keyManager.ClearKeystore();

                // 4. Обертаємо DEK новим паролем і зберігаємо під РЕАЛЬНИМ логіном
                _keyManager.RecoverWithMasterKey(base64Dek, adminUser.Login, newPassword);

                // 5. Оновлюємо PasswordHash адміністратора в базі даних
                authService.UpdateUserPassword(adminUser, newPassword);

                DialogResult = true;
                Close();
            }
            catch (SqliteException ex)
            {
                // Помилка підключення/розшифровки (код помилки часто вказує "file is not a database")
                ShowError(MasterKeyError, "Невірний майстер-ключ. Не вдалося розшифрувати базу даних.");
                System.Diagnostics.Debug.WriteLine($"Recovery Error: {ex.Message}");
            }
            catch (Exception ex)
            {
                ShowError(MasterKeyError, $"Неочікувана помилка відновлення: {ex.Message}");
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private bool ValidateInputs(string masterKeyHex, string newPassword, string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(masterKeyHex))
            {
                ShowError(MasterKeyError, "Введіть майстер-ключ.");
                return false;
            }

            if (string.IsNullOrEmpty(newPassword))
            {
                ShowError(PasswordError, "Введіть пароль.");
                return false;
            }

            if (newPassword != confirmPassword)
            {
                ShowError(PasswordError, "Паролі не збігаються.");
                return false;
            }

            if (newPassword.Length < 6)
            {
                ShowError(PasswordError, "Пароль повинен містити мінимум 6 символів.");
                return false;
            }

            return true;
        }

        private void ShowError(TextBlock errorTextBlock, string message)
        {
            errorTextBlock.Text = message;
            errorTextBlock.Visibility = Visibility.Visible;
        }
    }
}