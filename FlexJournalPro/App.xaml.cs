using System;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using FlexJournalPro.Services;
using FlexJournalPro.Models;
using FlexJournalPro.Config;
using FlexJournalPro.Windows;
using FlexJournalPro.ViewModels;

namespace FlexJournalPro
{
    /// <summary>
    /// Основний клас додатка з управлінням потоком запуску через DI-контейнер
    /// </summary>
    public partial class App : Application
    {
        // Поточний авторизований користувач (поки залишаємо тут, 
        // згодом його можна буде перенести в окремий AuthService)
        public static AppUser? CurrentUser { get; set; }

        // Посилання на DI-контейнер
        public IServiceProvider ServiceProvider { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. Ініціалізація DI-контейнера та реєстрація залежностей
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            ServiceProvider = serviceCollection.BuildServiceProvider();

            // Установлюємо режим завершення в явний (контролюємо процес до моменту відкриття MainWindow)
            Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            try
            {
                // Перевіряємо аргументи командного рядка
                bool needsRecovery = e.Args.Contains("-recover");

                // Отримуємо базові сервіси з контейнера
                var keyManager = ServiceProvider.GetRequiredService<IKeyManagementService>();
                var dbService = ServiceProvider.GetRequiredService<IDatabaseService>();

                // Етап 1: Перевіряємо необхідність відновлення (виявляє помилки DPAPI)
                if (needsRecovery || keyManager.HasDpapiError())
                {
                    // Запитуємо вікно відновлення з DI-контейнера
                    var recoveryWindow = ServiceProvider.GetRequiredService<RecoveryWindow>();
                    if (recoveryWindow.ShowDialog() != true)
                    {
                        Shutdown();
                        return;
                    }
                }

                // Етап 2: Перевіряємо перший запуск
                if (!HandleFirstRunIfNeeded())
                {
                    Shutdown();
                    return;
                }

                // Етап 3: Показуємо вікно входу (створюється через DI з усіма залежностями)
                var loginWindow = ServiceProvider.GetRequiredService<LoginWindow>();
                if (loginWindow.ShowDialog() != true)
                {
                    Shutdown();
                    return;
                }

                // Етап 4: ІНІЦІАЛІЗАЦІЯ ПІДКЛЮЧЕННЯ ДО БД
                // Користувач успішно увійшов, DEK розшифровано в оперативній пам'яті.
                // Тепер ми можемо безпечно ініціалізувати рядок підключення!
                // ПІДКЛЮЧЕННЯ ВИКОНУЄ LoginWindow для перевірки пароля
                // dbService.Connect();

                // Етап 5: Переходимо на головне вікно
                // Повертаємо стандартний режим завершення додатку (при закритті головного вікна)
                Current.ShutdownMode = ShutdownMode.OnMainWindowClose;

                // Запитуємо MainWindow з контейнера. 
                // DI автоматично створить MainViewModel та передасть туди вже підключений DatabaseService!
                var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
                Current.MainWindow = mainWindow;
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Помилка ініціалізації додатка: {ex.Message}");
                MessageBox.Show(
                    $"Критична помилка при запуску додатка:\n\n{ex.Message}",
                    "Помилка ініціалізації",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown();
            }
        }

        /// <summary>
        /// Реєстрація всіх сервісів, ViewModels та вікон додатку
        /// </summary>
        private void ConfigureServices(IServiceCollection services)
        {
            // Реєструємо Singleton-конфігурацію
            services.AddSingleton<AppConfig>();

            // Реєструємо основні сервіси через їхні інтерфейси
            services.AddSingleton<IKeyManagementService, KeyManagementService>();
            services.AddSingleton<IDatabaseService, DatabaseService>();
            services.AddSingleton<ITemplateService, TemplateService>();
            services.AddTransient<IAuthService, AuthService>();

            // Реєструємо ViewModels (зазвичай Transient або Scoped)
            services.AddTransient<MainViewModel>();

            // Реєструємо Windows (як Transient, щоб отримувати новий екземпляр при кожному запиті)
            services.AddTransient<LoginWindow>();
            services.AddTransient<MainWindow>();
            services.AddTransient<RecoveryWindow>();
            services.AddTransient<FirstRunWindow>();
        }

        /// <summary>
        /// Обробка першого запуска додатка
        /// </summary>
        private bool HandleFirstRunIfNeeded()
        {
            var config = ServiceProvider.GetRequiredService<AppConfig>();

            string dbFilePath = config.DatabasePath;
            string configFilePath = config.ConfigPath;

            if (File.Exists(dbFilePath) && File.Exists(configFilePath))
            {
                return true;
            }

            var firstRunWindow = ServiceProvider.GetRequiredService<FirstRunWindow>();
            return firstRunWindow.ShowDialog() == true;
        }
    }
}