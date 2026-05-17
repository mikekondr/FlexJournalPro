using FlexJournalPro.Config;
using FlexJournalPro.Windows;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Windows;

namespace FlexJournalPro.Services
{
    public interface IAppLifecycleService
    {
        void Startup(string[] args);
        void LogoutAndRestart();
    }

    internal class AppLifecycleService : IAppLifecycleService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IKeyManagementService _keyManager;
        private readonly AppConfig _config;

        public AppLifecycleService(IServiceProvider serviceProvider,
            IKeyManagementService keyManager,
            AppConfig config)
        {
            _serviceProvider = serviceProvider;
            _keyManager = keyManager;
            _config = config;
        }

        public void Startup(string[] args)
        {
            bool needsRecovery = args.Contains("-recover");

            // Етап 1: Перевіряємо необхідність відновлення (виявляє помилки DPAPI)
            if (needsRecovery || _keyManager.HasDpapiError())
            {
                var recoveryWindow = _serviceProvider.GetRequiredService<RecoveryWindow>();
                if (recoveryWindow.ShowDialog() != true)
                {
                    Application.Current.Shutdown();
                    return;
                }
            }

            // Етап 2: Перевіряємо перший запуск
            if (!HandleFirstRunIfNeeded())
            {
                Application.Current.Shutdown();
                return;
            }

            // Етап 3 та 4: Запускаємо флоу авторизації та відкриття головного вікна
            ShowLoginFlow();
        }

        public void LogoutAndRestart()
        {
            // Скидаємо користувача
            App.CurrentUser = null;

            Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Закриваємо головне вікно, якщо воно відкрите
            Application.Current.MainWindow?.Close();

            // Перезапускаємо процес авторизації
            ShowLoginFlow();
        }

        private void ShowLoginFlow()
        {
            // Змінюємо режим зупинки, щоб додаток не закрився між вікнами
            Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var loginWindow = _serviceProvider.GetRequiredService<LoginWindow>();

            if (loginWindow.ShowDialog() == true)
            {
                // Повертаємо стандартний режим після успішного входу
                Application.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;

                var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                Application.Current.MainWindow = mainWindow;
                mainWindow.Show();
            }
            else
            {
                // Якщо закрили вікно логіну - завершуємо роботу
                Application.Current.Shutdown();
            }
        }

        private bool HandleFirstRunIfNeeded()
        {
            string dbFilePath = _config.DatabasePath;
            string configFilePath = _config.ConfigPath;

            // Якщо файли вже існують - перший запуск не потрібен
            if (File.Exists(dbFilePath) && File.Exists(configFilePath))
            {
                return true;
            }

            // Якщо файлів немає - показуємо вікно початкового налаштування
            var firstRunWindow = _serviceProvider.GetRequiredService<FirstRunWindow>();
            return firstRunWindow.ShowDialog() == true;
        }
    }
}
