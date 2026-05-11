using FlexJournalPro.Config;
using FlexJournalPro.Services;
using Microsoft.Win32;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace FlexJournalPro
{
    public partial class FirstRunWindow : Window
    {
        // Стан: чи очікуємо ми від користувача підтвердження збереження ключа
        private bool _isWaitingForKeyBackup = false;

        // Тимчасове зберігання згенерованого ключа між натисканнями
        private string _tempMasterKey;

        private KeyManagementService _keyService;
        private AppConfig _config;
        private DatabaseService _databaseService;
        private AuthService _auth;

        public FirstRunWindow()
        {
            InitializeComponent();
        }

        // Перетягування вікна
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        // Закриття програми
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        // Копіювання ключа
        private void CopyKeyButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(MasterDekTextBlock.Text))
            {
                Clipboard.SetText(MasterDekTextBlock.Text);
                ShowMessage("Ключ скопійовано в буфер обміну!", isError: false);
            }
        }

        // Збереження ключа
        private void SaveKeyButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_tempMasterKey)) return;

            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|Key files (*.key)|*.key",
                DefaultExt = ".txt",
                FileName = "JournalPro_RecoveryKey.txt",
                Title = "Збереження Майстер-ключа"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    // Формуємо гарний файл з інструкціями для користувача
                    string fileContent = $"ЖурналПро - Майстер-ключ відновлення\r\n" +
                                         $"Створено: {DateTime.Now}\r\n" +
                                         $"Логін адміністратора: {LoginTextBox.Text.Trim()}\r\n" +
                                         $"----------------------------------------\r\n" +
                                         $"{_tempMasterKey}\r\n" +
                                         $"----------------------------------------\r\n" +
                                         $"УВАГА: Цей файл є єдиним способом відновити доступ до ваших даних\r\n" +
                                         $"у разі втрати пароля або перенесення бази на інший комп'ютер.";

                    File.WriteAllText(saveFileDialog.FileName, fileContent);
                    ShowMessage("Ключ успішно збережено у файл!", isError: false);
                }
                catch (Exception ex)
                {
                    ShowMessage($"Помилка збереження файлу: {ex.Message}", isError: true);
                }
            }
        }

        // Головна логіка кнопки
        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            HideMessage();

            string login = LoginTextBox.Text.Trim();
            string password = PasswordBox.Password;
            string passwordConfirm = PasswordBox2.Password;
            bool useEncryption = UseEncryptionSwitch.IsChecked == true;

            // 0. Базова валідація
            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
            {
                ShowMessage("Будь ласка, заповніть всі поля.", isError: true);
                return;
            }

            if (password != passwordConfirm)
            {
                ShowMessage("Паролі не співпадають!", isError: true);
                return;
            }

            // ЕТАП 1: Генерація ключа і зупинка для його збереження (тільки якщо є шифрування)
            if (useEncryption && !_isWaitingForKeyBackup)
            {
                // Генеруємо візуальне представлення ключа (Recovery Key / KEK)
                _tempMasterKey = GenerateRecoveryKeyFormat();
                MasterDekTextBlock.Text = _tempMasterKey;

                // Показуємо блок з ключем
                MasterKeyBlock.Visibility = Visibility.Visible;

                // Змінюємо UI, щоб користувач не міг змінити дані на цьому етапі
                UseEncryptionSwitch.IsEnabled = false;
                LoginTextBox.IsEnabled = false;
                PasswordBox.IsEnabled = false;
                PasswordBox2.IsEnabled = false;

                // Змінюємо кнопку
                var btn = (System.Windows.Controls.Button)sender;
                btn.Content = "Я ЗБЕРІГ КЛЮЧ - ЗАВЕРШИТИ";

                ShowMessage("Будь ласка, збережіть ключ перед продовженням.", isError: false);

                _isWaitingForKeyBackup = true;
                return; // Зупиняємо виконання, чекаємо другого натискання
            }

            // ЕТАП 2: Фінальне створення системи (Або Етап 1, якщо шифрування вимкнено)
            try
            {
                // Деактивуємо кнопку, щоб уникнути подвійного кліку під час завантаження
                var btn = (System.Windows.Controls.Button)sender;
                btn.IsEnabled = false;
                btn.Content = "НАЛАШТУВАННЯ...";

                // Виконуємо важкі задачі асинхронно, щоб не блокувати інтерфейс
                await Task.Run(() =>
                {
                    // 1. Створює файл конфігурації
                    CreateConfiguration(useEncryption);

                    if (useEncryption)
                    {
                        // 2. Генерує ключ DEK з використанням ключа KEK та пароля
                        SaveEncryptionKeys(login, password);
                    }

                    // 4. Створює базу даних
                    CreateDatabase(useEncryption);

                    // 5. Створює користувача
                    CreateAdminUser(login, password, useEncryption);
                });
                DialogResult = true;
            }
            catch (Exception ex)
            {
                ShowMessage($"Помилка налаштування: {ex.Message}", isError: true);
                var btn = (System.Windows.Controls.Button)sender;
                btn.IsEnabled = true;
                btn.Content = _isWaitingForKeyBackup ? "Я ЗБЕРІГ КЛЮЧ - ЗАВЕРШИТИ" : "РОЗПОЧАТИ";
            }
        }

        #region Допоміжні методи UI

        private void ShowMessage(string message, bool isError)
        {
            ErrorTextBlock.Text = message;
            ErrorTextBlock.Foreground = isError ?
                System.Windows.Media.Brushes.Red :
                new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4CAF50")); // Зелений
            ErrorTextBlock.Visibility = Visibility.Visible;
        }

        private void HideMessage()
        {
            ErrorTextBlock.Visibility = Visibility.Collapsed;
        }

        #endregion

        #region Заглушки для вашої бізнес-логіки (Ваші 5 пунктів)

        private string GenerateRecoveryKeyFormat()
        {
            _keyService = new KeyManagementService();
            _keyService.EnsureKeyStoreInitialized();

            // Отримуємо сирий Base64
            string base64Key = _keyService.ExportMasterRecoveryKey();

            // Конвертуємо Base64 назад у байти
            byte[] keyBytes = Convert.FromBase64String(base64Key);

            // Конвертуємо байти у суцільний Hex-рядок (напр. 4A3B8C9D...)
            string hexString = BitConverter.ToString(keyBytes).Replace("-", "");

            // Розбиваємо дефісами кожні 4 символи для зручності читання
            var formattedGroups = Enumerable.Range(0, hexString.Length / 6)
                                            .Select(i => hexString.Substring(i * 6, 6));

            return string.Join("-", formattedGroups);
        }

        private void CreateConfiguration(bool useEncryption)
        {
            _config = AppConfig.Instance;
            _config.Database.UseCipher = useEncryption;
            _config.Save();
        }

        private void SaveEncryptionKeys(string login, string password)
        {
            _keyService.RemoveUserKey("admin");
            _keyService.SetOrUpdateUserKey(login, password);
        }

        private void CreateDatabase(bool useEncryption)
        {
            _databaseService = new DatabaseService(_keyService.GetDecryptedDekString());
        }

        private void CreateAdminUser(string login, string password, bool isEncryptedDb)
        {
            _auth = new AuthService(_databaseService);
            string passwordHash = _auth.HashPassword(password);

            Models.AppUser adminUser = new Models.AppUser
            {
                Login = login,
                PasswordHash = passwordHash,
                Role = Models.UserRole.Admin,
                FullName = "Адміністратор"
            };

            _databaseService.CreateUser(adminUser, passwordHash);
        }

        #endregion
    }
}