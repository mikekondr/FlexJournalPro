using FlexJournalPro.Models;

namespace FlexJournalPro.Services
{
    /// <summary>
    /// Сервіс для логування системних подій та дій з журналами.
    /// Підтримує логування до файлу при закритій БД та перенесення логів після розблокування БД.
    /// </summary>
    public interface ILogService
    {
        #region System logging

        /// <summary>
        /// Логує інформаційну системну подію.
        /// </summary>
        /// <param name="action">Тип дії.</param>
        /// <param name="message">Основне повідомлення.</param>
        /// <param name="details">Додаткові деталі (опціонально).</param>
        void LogSystemInfo(LogAction action, string message, string? details = null);

        /// <summary>
        /// Логує попередження системної подія.
        /// </summary>
        /// <param name="action">Тип дії.</param>
        /// <param name="message">Основне повідомлення.</param>
        /// <param name="details">Додаткові деталі (опціонально).</param>
        void LogSystemWarning(LogAction action, string message, string? details = null);

        /// <summary>
        /// Логує помилку системної подія з винятком.
        /// </summary>
        /// <param name="action">Тип дії.</param>
        /// <param name="message">Основне повідомлення про помилку.</param>
        /// <param name="ex">Винято з деталями помилки (опціонально).</param>
        void LogSystemError(LogAction action, string message, Exception? ex = null);

        #endregion

        #region Log retrieval - System

        /// <summary>
        /// Отримує всі логи з усіх таблиць, відсортовані за спаданням часу.
        /// </summary>
        /// <param name="limit">Максимальна кількість записів для отримання (за замовчуванням 1000).</param>
        /// <returns>Колекція логів.</returns>
        IEnumerable<LogEntry> GetAllLogs(int limit = 1000);

        /// <summary>
        /// Отримує системні логи з таблиці SystemLogs.
        /// </summary>
        /// <param name="limit">Максимальна кількість записів для отримання (за замовчуванням 1000).</param>
        /// <returns>Колекція системних логів.</returns>
        IEnumerable<LogEntry> GetSystemLogs(int limit = 1000);

        #endregion

        #region Journal logging

        /// <summary>
        /// Логує дію в журналі (вставка, оновлення, видалення запису).
        /// </summary>
        /// <param name="journalTableName">Назва таблиці журналу (без суфіксу "_audit").</param>
        /// <param name="action">Тип дії.</param>
        /// <param name="actionMessage">Опис дії.</param>
        /// <param name="details">Додаткові деталі (опціонально).</param>
        void LogJournalAction(string journalTableName, LogAction action, string actionMessage, string? details = null);

        #endregion

        #region Log retrieval - Journal

        /// <summary>
        /// Отримує логи дій з журналу.
        /// </summary>
        /// <param name="journalTableName">Назва таблиці журналу (без суфіксу "_audit").</param>
        /// <param name="limit">Максимальна кількість записів для отримання (за замовчуванням 1000).</param>
        /// <returns>Колекція логів журналу.</returns>
        IEnumerable<LogEntry> GetJournalLogs(string journalTableName, int limit = 1000);

        #endregion

        #region Maintenance

        /// <summary>
        /// Перенаправляє всі накопичені логи з тимчасового файлу в БД.
        /// Викликається після успішної авторизації, коли БД розблокована.
        /// </summary>
        void FlushPendingLogsToDatabase();

        /// <summary>
        /// Видаляє старі логи, які збережені більше вказаного часу.
        /// </summary>
        /// <param name="daysToKeep">Кількість днів логів, які слід зберігати (за замовчуванням 30).</param>
        void CleanupOldLogs(int daysToKeep = 30);

        #endregion
    }
}