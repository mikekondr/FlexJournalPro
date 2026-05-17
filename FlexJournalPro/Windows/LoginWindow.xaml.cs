using FlexJournalPro.Config;
using FlexJournalPro.Services;
using System.Windows;

namespace FlexJournalPro.Windows
{
    public partial class LoginWindow : Window
    {
        private readonly IKeyManagementService _keyManager;
        private readonly IDatabaseService _dbService;
        private readonly IAuthService _authService;
        private readonly AppConfig _config;

        // Впроваджуємо залежності через конструктор
        public LoginWindow(
            IKeyManagementService keyManager,
            IDatabaseService dbService,
            IAuthService authService,
            AppConfig config)
        {
            InitializeComponent();
            _keyManager = keyManager;
            _dbService = dbService;
            _authService = authService;
            _config = config;

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
            if (string.IsNullOrWhiteSpace(password))
            {
                ShowError("Введіть пароль");
                return;
            }

            bool useCipher = _config.Database.UseCipher;

            if (useCipher)
            {
                // Для шифрованої бази спочатку пробуємо розблокувати ключ шифрування
                bool isKeyUnlocked = _keyManager.UnlockDekWithPassword(login, password);

                if (!isKeyUnlocked)
                {
                    ShowError("Невірний логін або пароль");
                    return;
                }
            }

            try
            {
                // Ключ успішно розблоковано в пам'яті (або шифрування вимкнено).
                // ТЕПЕР ініціалізуємо підключення єдиного екземпляра БД!
                _dbService.Connect();
            }
            catch (Exception)
            {
                ShowError("Неможливо відкрити базу даних. Помилка шифрування або файл пошкоджено.");
                return;
            }

            // Перевіряємо користувача в БД
            var user = _authService.Authenticate(login, password);

            if (user != null)
            {
                // Успішний вхід. Зберігаємо поточного користувача.
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