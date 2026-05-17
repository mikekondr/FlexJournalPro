using FlexJournalPro.Config;
using FlexJournalPro.Services;
using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace FlexJournalPro.Windows
{
    public partial class FirstRunWindow : Window
    {
        // Стан: чи очікуємо ми від користувача підтвердження збереження ключа
        private bool _isWaitingForKeyBackup = false;

        // Тимчасове зберігання згенерованого ключа між натисканнями
        private string? _tempMasterKey;

        // 1. ВИКОРИСТОВУЄМО ІНТЕРФЕЙСИ ТА DI
        private readonly IKeyManagementService _keyService;
        private readonly IDatabaseService _databaseService;
        private readonly AppConfig _config;
        private readonly IAuthService _authService;

        // 2. Впроваджуємо залежності через конструктор
        public FirstRunWindow(
            IKeyManagementService keyService,
            IDatabaseService databaseService,
            IAuthService authService,
            AppConfig config)
        {
            InitializeComponent();
            _keyService = keyService;
            _databaseService = databaseService;
            _authService = authService;
            _config = config;
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

            // 0. Валідація
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

            // ЕТАП 1: Генерація ключа і зупинка для його збереження
            if (useEncryption && !_isWaitingForKeyBackup)
            {
                _tempMasterKey = GenerateRecoveryKeyFormat();
                MasterDekTextBlock.Text = _tempMasterKey;

                MasterKeyBlock.Visibility = Visibility.Visible;

                UseEncryptionSwitch.IsEnabled = false;
                LoginTextBox.IsEnabled = false;
                PasswordBox.IsEnabled = false;
                PasswordBox2.IsEnabled = false;

                var btnStage1 = (System.Windows.Controls.Button)sender;
                btnStage1.Content = "Я ЗБЕРІГ КЛЮЧ - ЗАВЕРШИТИ";

                ShowMessage("Будь ласка, збережіть ключ перед продовженням.", isError: false);

                _isWaitingForKeyBackup = true;
                return;
            }

            // ЕТАП 2: Фінальне створення системи
            try
            {
                var btnStage2 = (System.Windows.Controls.Button)sender;
                btnStage2.IsEnabled = false;
                btnStage2.Content = "НАЛАШТУВАННЯ...";

                // Виконуємо задачі асинхронно
                await Task.Run(() =>
                {
                    // 1. Оновлюємо конфігурацію 
                    CreateConfiguration(useEncryption);

                    if (useEncryption)
                    {
                        // 2. Зберігаємо ключі користувача (Шифруємо DEK паролем і пишемо в Keystore)
                        SaveEncryptionKeys(login, password);
                    }

                    // 3. СТВОРЕННЯ БАЗИ ДАНИХ (Більше жодних new DatabaseService!)
                    // Оскільки DEK вже в пам'яті (_keyService.GenerateMasterKeyInMemory()),
                    // метод Connect зчитає його і створить файл БД.
                    CreateDatabase();

                    // 4. Створюємо першого адміністратора
                    CreateAdminUser(login, password);
                });

                DialogResult = true;
            }
            catch (Exception ex)
            {
                ShowMessage($"Помилка налаштування: {ex.Message}", isError: true);
                var btnError = (System.Windows.Controls.Button)sender;
                btnError.IsEnabled = true;
                btnError.Content = _isWaitingForKeyBackup ? "Я ЗБЕРІГ КЛЮЧ - ЗАВЕРШИТИ" : "РОЗПОЧАТИ";
            }
        }

        #region Допоміжні методи UI

        private void ShowMessage(string message, bool isError)
        {
            ErrorTextBlock.Text = message;
            ErrorTextBlock.Foreground = isError ?
                System.Windows.Media.Brushes.Red :
                new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4CAF50"));
            ErrorTextBlock.Visibility = Visibility.Visible;
        }

        private void HideMessage()
        {
            ErrorTextBlock.Visibility = Visibility.Collapsed;
        }

        #endregion

        #region Бізнес-логіка (Оновлена для DI)

        private string GenerateRecoveryKeyFormat()
        {
            _keyService.GenerateMasterKeyInMemory();
            string base64Key = _keyService.ExportMasterRecoveryKey();
            byte[] keyBytes = Convert.FromBase64String(base64Key);
            string hexString = BitConverter.ToString(keyBytes).Replace("-", "");

            var formattedGroups = Enumerable.Range(0, hexString.Length / 4)
                                            .Select(i => hexString.Substring(i * 4, 4));

            return string.Join("-", formattedGroups);
        }

        private void CreateConfiguration(bool useEncryption)
        {
            _config.Database.UseCipher = useEncryption;
            _config.Save();
        }

        private void SaveEncryptionKeys(string login, string password)
        {
            _keyService.SetOrUpdateUserKey(login, password);
        }

        private void CreateDatabase()
        {
            // DI-контейнер дав нам єдиний екземпляр бази.
            // Нам потрібно лише викликати Connect(), щоб створити файл та таблиці.
            _databaseService.Connect();
        }

        private void CreateAdminUser(string login, string password)
        {
            // Припускаємо, що HashPassword - статичний метод у вашому коді
            string passwordHash = _authService.HashPassword(password);

            Models.AppUser adminUser = new Models.AppUser
            {
                Login = login,
                Role = Models.UserRole.Admin,
                FullName = "Адміністратор"
            };

            _databaseService.CreateUser(adminUser, passwordHash);
        }

        #endregion
    }
}