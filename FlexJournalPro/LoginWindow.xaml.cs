using System.Windows;
using FlexJournalPro.Services;

namespace FlexJournalPro
{
    public partial class LoginWindow : Window
    {
        private readonly AuthService _authService;

        public LoginWindow(AuthService authService)
        {
            InitializeComponent();
            _authService = authService;
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
                ErrorTextBlock.Text = "Введіть логін";
                ErrorTextBlock.Visibility = Visibility.Visible;
                return;
            }

            var user = _authService.Authenticate(login, password);

            if (user != null)
            {
                // TODO: Додати перевірку на перший запуск адміна без пароля тут
                // Якщо user.Login == "admin" та user.PasswordHash == "", показати вікно встановлення пароля.

                App.CurrentUser = user;
                DialogResult = true;
            }
            else
            {
                ErrorTextBlock.Text = "Невірний логін або пароль";
                ErrorTextBlock.Visibility = Visibility.Visible;
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            DragMove();
        }
    }
}