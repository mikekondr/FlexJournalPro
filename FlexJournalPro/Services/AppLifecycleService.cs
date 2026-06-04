using FlexJournalPro.Config;
using FlexJournalPro.Models;
using FlexJournalPro.Windows;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Windows;

namespace FlexJournalPro.Services
{
    /// <summary>
    /// Реалізація сервісу для управління життєвим циклом додатку.
    /// </summary>
    internal class AppLifecycleService : IAppLifecycleService
    {
        #region Fields

        private readonly IServiceProvider _serviceProvider;
        private readonly IKeyManagementService _keyManager;
        private readonly AppConfig _config;
        private readonly ILogService _logger;

        #endregion

        #region Constructor

        /// <summary>
        /// Ініціалізує новий екземпляр класу <see cref="AppLifecycleService"/>.
        /// </summary>
        public AppLifecycleService(IServiceProvider serviceProvider,
            IKeyManagementService keyManager,
            AppConfig config,
            ILogService logService)
        {
            _serviceProvider = serviceProvider;
            _keyManager = keyManager;
            _config = config;
            _logger = logService;
        }

        #endregion

        #region Public methods

        /// <summary>
        /// Запускає додаток, проходячи наступні етапи:
        /// 1. Перевіряє необхідність відновлення (DPAPI помилки або -recover аргумент);
        /// 2. Перевіряє, чи це перший запуск;
        /// 3. Показує вікно авторизації та відкриває головне вікно.
        /// </summary>
        public void Startup(string[] args)
        {
            AppLogger.LogSystemInfo(LogAction.SystemStarted, "Запуск додатку", args.Count() > 0 ? "Аргументи: " + string.Join(", ", args) : null);

            bool needsRecovery = args.Contains("-recover");

            // Етап 1: Перевіряємо необхідність відновлення
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

            // Етап 3 та 4: Запускаємо авторизацію та головне вікно
            ShowLoginFlow();
        }

        /// <summary>
        /// Виконує вихід користувача, скидає сесію та перезапускає флоу авторизації.
        /// </summary>
        public void LogoutAndRestart()
        {
            App.CurrentUser = null;
            Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Application.Current.MainWindow?.Close();

            AppLogger.LogSystemInfo(LogAction.UserLogout, "Вихід із системи");
            ShowLoginFlow();
        }

        #endregion

        #region Private helpers - Login flow

        /// <summary>
        /// Показує вікно авторизації та керує логіною користувача.
        /// При успішній авторизації: записує логи, імпортує шаблони та відкриває головне вікно.
        /// При скасуванні: завершує роботу додатку.
        /// </summary>
        private void ShowLoginFlow()
        {
            Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var loginWindow = _serviceProvider.GetRequiredService<LoginWindow>();

            if (loginWindow.ShowDialog() == true)
            {
                // База розблокована: записуємо накопичені логи
                _logger.FlushPendingLogsToDatabase();

                // Записуємо факт успішного входу
                AppLogger.LogSystemInfo(LogAction.UserLogin, "Вхід в систему");

                // Імпортуємо шаблони у фоні
                var templateService = _serviceProvider.GetRequiredService<ITemplateService>();
                Task.Run(() =>
                {
                    try
                    {
                        templateService.ImportDefaultTemplates();
                    }
                    catch (Exception ex)
                    {
                        AppLogger.LogSystemInfo(LogAction.SystemError, "Помилка імпорту шаблонів", ex.Message);
                    }
                });

                // Повертаємо стандартний режим та відкриваємо головне вікно
                Application.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;

                var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                Application.Current.MainWindow = mainWindow;
                mainWindow.Show();
            }
            else
            {
                Application.Current.Shutdown();
            }
        }

        #endregion

        #region Private helpers - First run

        /// <summary>
        /// Перевіряє, чи це перший запуск додатку.
        /// Якщо так, показує вікно початкового налаштування та обробляє результат.
        /// </summary>
        /// <returns>
        /// <c>true</c> якщо налаштування успішне або це не перший запуск;
        /// <c>false</c> якщо користувач скасував налаштування або потрібен перезапуск.
        /// </returns>
        private bool HandleFirstRunIfNeeded()
        {
            string dbFilePath = _config.DatabasePath;
            string configFilePath = _config.ConfigPath;

            // Якщо файли існують, це не перший запуск
            if (File.Exists(dbFilePath) && File.Exists(configFilePath))
            {
                return true;
            }

            // Показуємо вікно початкового налаштування
            var firstRunWindow = _serviceProvider.GetRequiredService<FirstRunWindow>();
            bool isSetupSuccessful = firstRunWindow.ShowDialog() == true;

            // Якщо налаштування успішне і потрібен перезапуск
            if (isSetupSuccessful && firstRunWindow.RequiresRestart)
            {
                StartNewInstance();
                return false;
            }

            return isSetupSuccessful;
        }

        /// <summary>
        /// Запускає новий екземпляр додатку.
        /// </summary>
        private void StartNewInstance()
        {
            string? exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath))
            {
                System.Diagnostics.Process.Start(exePath);
            }
        }

        #endregion
    }
}
