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
                // 2. Отримуємо сервіс життєвого циклу та запускаємо початковий ланцюжок вікон
                var lifecycleService = ServiceProvider.GetRequiredService<IAppLifecycleService>();
                lifecycleService.Startup(e.Args);
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

            // Реєструємо сервіси через їхні інтерфейси
            services.AddSingleton<IAppLifecycleService, AppLifecycleService>();
            services.AddSingleton<IKeyManagementService, KeyManagementService>();
            services.AddSingleton<IDatabaseService, DatabaseService>();
            services.AddSingleton<ITemplateService, TemplateService>();
            services.AddTransient<IAuthService, AuthService>();

            // Реєструємо фабрику для створення вікон
            services.AddSingleton<IScreenFactory, ScreenFactory>();

            // Реєструємо ViewModels (зазвичай Transient або Scoped)
            services.AddTransient<MainViewModel>();

            // Реєструємо Windows (як Transient, щоб отримувати новий екземпляр при кожному запиті)
            services.AddTransient<LoginWindow>();
            services.AddTransient<MainWindow>();
            services.AddTransient<RecoveryWindow>();
            services.AddTransient<FirstRunWindow>();
        }
    }
}