using System.Windows;
using FlexJournalPro.Services;
using FlexJournalPro.Models;

namespace FlexJournalPro
{
    public partial class App : Application
    {
        public static AppUser? CurrentUser { get; set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Тимчасово змінюємо режим закриття, щоб програма не завершувалась при закритті LoginWindow
            Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var dbService = new DatabaseService();
            var authService = new AuthService(dbService);

            var loginWindow = new LoginWindow(authService);
            if (loginWindow.ShowDialog() == true)
            {
                // Після успішного входу міняємо режим закриття на OnMainWindowClose (закриття при закритті головного вікна)
                Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
                
                var mainWindow = new MainWindow();
                Current.MainWindow = mainWindow; // Вказуємо головне вікно
                mainWindow.Show();
            }
            else
            {
                // Якщо користувач закрив вікно входу кнопкою "Х" або скасував
                Shutdown();
            }
        }
    }
}
