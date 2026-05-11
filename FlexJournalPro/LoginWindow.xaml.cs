using System.Windows;
using FlexJournalPro.Services;
using FlexJournalPro.Config;
using FlexJournalPro.Models;

namespace FlexJournalPro
{
    public partial class LoginWindow : Window
    {
        private readonly KeyManagementService _keyManager;

        public LoginWindow(KeyManagementService keyManager)
        {
            InitializeComponent();
            _keyManager = keyManager;
            LoginTextBox.Focus();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            ErrorTextBlock.Visibility = Visibility.Collapsed;
            string login = LoginTextBox.Text;
            string password = PasswordBox.Password;

            if (string.IsNullOrWhiteSpace(login))
            {
                ShowError("Введіть логін");
                return;
            }

            bool useCipher = AppConfig.Instance.Database.UseCipher;
            DatabaseService? dbService = null;

            if (useCipher)
            {
                // Для шифрованої бази спочатку пробуємо розблокувати ключ шифрування
                // Заглушка обновлення виклику:
                bool isKeyUnlocked = _keyManager.UnlockDekWithPassword(login, password);

                if (!isKeyUnlocked)
                {
                    // Якщо ні дефолтний, ні введений пароль не підійшли
                    ShowError("Невірний логін або пароль");
                    return;
                }
                else
                {
                    try
                    {
                        dbService = new DatabaseService(_keyManager.GetDecryptedDekString());
                    }
                    catch (System.Exception)
                    {
                        ShowError("Неможливо відкрити базу даних. Помилка шифрування.");
                        return;
                    }

                }
            } else
            {
                try
                {
                    dbService = new DatabaseService();
                }
                catch (System.Exception)
                {
                    ShowError("Неможливо відкрити базу даних. Файл пошкоджено або зашифровано.");
                    return;
                }

            }

            var authService = new AuthService(dbService);
            var user = authService.Authenticate(login, password);

            if (user != null)
            {
                // Успішний вхід. Зберігаємо ініціалізовану БД в App, щоб не створювати знову
                App.Database = dbService;
                App.CurrentUser = user;
                
                DialogResult = true;
            }
            else
            {
                ShowError("Невірний логін або пароль");
            }
        }

        private void ShowError(string message)
        {
            ErrorTextBlock.Text = message;
            ErrorTextBlock.Visibility = Visibility.Visible;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            DragMove();
        }
    }
}