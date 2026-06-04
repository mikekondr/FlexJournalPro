namespace FlexJournalPro.Services
{
    /// <summary>
    /// Сервіс для управління життєвим циклом додатку.
    /// Відповідає за запуск, авторизацію, відновлення та перезавантаження.
    /// </summary>
    public interface IAppLifecycleService
    {
        /// <summary>
        /// Запускає додаток, перевіряючи необхідність відновлення, першого запуску та авторизації.
        /// </summary>
        /// <param name="args">Аргументи командного рядка.</param>
        void Startup(string[] args);

        /// <summary>
        /// Виконує вихід користувача та перезапускає потік авторизації.
        /// </summary>
        void LogoutAndRestart();
    }
}
