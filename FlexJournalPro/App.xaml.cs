using System.IO;
using System.Windows;
using FlexJournalPro.Services;
using FlexJournalPro.Models;
using FlexJournalPro.Config;
using FlexJournalPro.Windows;

namespace FlexJournalPro
{
    /// <summary>
    /// Основной класс приложения с управлением потоком запуска
    /// </summary>
    public partial class App : Application
    {
        public static AppUser? CurrentUser { get; set; }
        
        // Глобальные сервисы
        public static KeyManagementService KeyManager { get; private set; } = null!;
        public static DatabaseService Database { get; set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Устанавливаем режим завершения в явный (контролируем сами)
            Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            try
            {
                // Проверяем аргументы командной строки
                bool needsRecovery = e.Args.Contains("-recover");

                // Этап 1: Инициализация KeyManager (выявляет ошибки DPAPI)
                KeyManager = new KeyManagementService();

                // Этап 2: Проверяем необходимость восстановления
                if (needsRecovery || KeyManager.HasDpapiError())
                {
                    if (!HandleRecoveryFlow())
                    {
                        Shutdown();
                        return;
                    }
                }

                // Этап 3: Проверяем первый запуск
                if (!HandleFirstRunIfNeeded())
                {
                    Shutdown();
                    return;
                }

                // Этап 4: Показываем окно входа
                if (!HandleLoginFlow())
                {
                    Shutdown();
                    return;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка инициализации приложения: {ex.Message}");
                MessageBox.Show(
                    $"Критическая ошибка при запуске приложения:\n\n{ex.Message}",
                    "Ошибка инициализации",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown();
            }
        }

        /// <summary>
        /// Обработка потока восстановления доступа
        /// </summary>
        private bool HandleRecoveryFlow()
        {
            // Показываем окно восстановления
            // RecoveryWindow сам будет проверять ключ через DatabaseService.VerifyRecoveryKey(base64Dek)
            var recoveryWindow = new RecoveryWindow(KeyManager);
            
            if (recoveryWindow.ShowDialog() != true)
            {
                return false; // Пользователь отменил восстановление
            }

            return true;
        }

        /// <summary>
        /// Обработка первого запуска приложения
        /// </summary>
        private bool HandleFirstRunIfNeeded()
        {
            string dbFilePath = AppConfig.DatabasePath;
            string configFilePath = AppConfig.ConfigPath;

            // Проверяем наличие конфигурации и базы данных
            if (File.Exists(dbFilePath) && File.Exists(configFilePath))
            {
                return true; // Все есть, первый запуск не требуется
            }

            // Показываем окно первого запуска
            var firstRunWindow = new FirstRunWindow();
            return firstRunWindow.ShowDialog() == true;
        }

        /// <summary>
        /// Обработка потока входа в приложение
        /// </summary>
        private bool HandleLoginFlow()
        {
            var loginWindow = new LoginWindow(KeyManager);
            
            if (loginWindow.ShowDialog() != true)
            {
                return false; // Пользователь не вошел
            }

            // Успешный вход - переходим на главное окно
            Current.ShutdownMode = ShutdownMode.OnMainWindowClose;

            var mainWindow = new MainWindow();
            Current.MainWindow = mainWindow;
            mainWindow.Show();

            return true;
        }
    }
}
