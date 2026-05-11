using System.IO;
using System.Windows;
using FlexJournalPro.Services;
using FlexJournalPro.Models;
using FlexJournalPro.Config;
using FlexJournalPro.Views;

namespace FlexJournalPro
{
    public partial class App : Application
    {
        public static AppUser? CurrentUser { get; set; }
        
        // Робимо сервіси доступними глобально
        public static KeyManagementService KeyManager { get; private set; } = null!;
        public static DatabaseService Database { get; set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Тимчасово змінюємо режим закриття
            Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Перевіряємо, чи це перший запуск (немає конфігу або БД)
            string dbFilePath = AppConfig.DatabasePath;
            string configFilePath = AppConfig.ConfigPath;

            if (!File.Exists(dbFilePath) || !File.Exists(configFilePath))
                if (new FirstRunWindow().ShowDialog() != true)
                {
                    // Якщо користувач закрив вікно налаштувань або скасував, завершуємо роботу
                    Shutdown();
                    return;
                }
            
            // 1. Ініціалізуємо менеджер ключів (тепер це безпечно робити, налаштування завершено)
            KeyManager = new KeyManagementService();
            // Якщо шифрування було вимкнено майстром, цей метод має перевіряти налаштування і не створювати Keystore
            KeyManager.EnsureKeyStoreInitialized();

            // Передаємо його у вікно входу
            var loginWindow = new LoginWindow(KeyManager); 
            
            if (loginWindow.ShowDialog() == true)
            {
                // Database вже створена та присвоєна (App.Database = dbService) всередині LoginWindow
                Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
                
                var mainWindow = new MainWindow();
                Current.MainWindow = mainWindow; 
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
