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
                    // Якщо це перший запуск адміна
                    bool isDefaultUnlocked = _keyManager.UnlockDekWithPassword(login, "");

                    if (isDefaultUnlocked && login == "admin")
                    {
                        // Відкрили базу дефолтним ключем, але користувач ввів якийсь пароль? 
                        // Це означає, що база ще не захищена і admin повинен встановити пароль.
                        // (Обробляємо цей кейс нижче)
                    }
                    else
                    {
                        // Якщо ні дефолтний, ні введений пароль не підійшли
                        ShowError("Невірний логін або пароль");
                        return;
                    }
                }
            }

            try
            {
                // На цьому етапі либо шифрування вимкнено, либо ми отримали правильний ключ
                dbService = new DatabaseService();
            }
            catch (System.Exception)
            {
                ShowError("Неможливо відкрити базу даних. Помилка шифрування.");
                return;
            }

            var authService = new AuthService(dbService);
            var user = authService.Authenticate(login, password);

            if (user != null)
            {
                // Перевірка на перший запуск адміна без пароля
                if (user.Login == "admin" && string.IsNullOrEmpty(user.PasswordHash))
                {
                    //ShowError("Потрібне встановлення пароля для адміністратора. Відкрийте вікно встановлення пароля.");
                    
                    // Тимчасово відкриємо MessageBox, але тут має відкриватися спеціальне вікно (SetPasswordWindow)
                    // TODO: Реалізувати SetPasswordWindow і розкоментувати:
                    /*
                    var setPasswordWindow = new SetPasswordWindow(user);
                    if (setPasswordWindow.ShowDialog() == true)
                    {
                        string newPassword = setPasswordWindow.NewPassword;
                        
                        if (useCipher)
                        {
                            // Перешифровуємо DEK новим паролем і зберігаємо
                            _keyManager.RotateKek(newPassword);
                            AppConfig.Instance.Database.CipherPassword = _keyManager.GetDecryptedDekString();
                        }
                        
                        // Зберігаємо змінений пароль в базі
                        authService.UpdateUserPassword(user, newPassword);
                    }
                    else
                    {
                        return; // Скасували встановлення пароля
                    }
                    */

                    // Поки що не пускаємо далі, щоб не було небезпеки
                    //return;
                }

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