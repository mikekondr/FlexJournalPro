using FlexJournalPro.Models;
using System.IO;
using System.Text.Json;

namespace FlexJournalPro.Services
{
    /// <summary>
    /// Реалізація сервісу для логування системних подій та дій з журналами.
    /// Підтримує буферизацію логів у тимчасовий файл, поки БД не розблокована,
    /// та автоматичне перенесення логів після авторизації.
    /// </summary>
    public class LogService : ILogService
    {
        #region Fields

        private readonly IDatabaseService _dbService;
        private readonly string _pendingLogsFilePath;
        private readonly object _lockObj = new object();

        /// <summary>
        /// Прапорець, який вказує, чи можна писати в БД (стає true після авторизації).
        /// </summary>
        private bool _isDatabaseReady = false;

        /// <summary>
        /// Прапорець для оптимізації: таблиця SystemLogs вже створена.
        /// </summary>
        private bool _isSystemTableCreated = false;

        #endregion

        #region Constructor

        /// <summary>
        /// Ініціалізує новий екземпляр класу <see cref="LogService"/>.
        /// </summary>
        /// <param name="dbService">Сервіс для роботи з базою даних.</param>
        public LogService(IDatabaseService dbService)
        {
            _dbService = dbService;

            // Статичне ім'я файлу, щоб зберігати логи між перезапусками програми
            _pendingLogsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_audit.tmp");
        }

        #endregion

        #region Public methods - System logging

        /// <summary>
        /// Логує інформаційну системну подію.
        /// </summary>
        /// <param name="action">Тип дії.</param>
        /// <param name="message">Основне повідомлення.</param>
        /// <param name="details">Додаткові деталі (опціонально).</param>
        public void LogSystemInfo(LogAction action, string message, string? details = null) =>
            ProcessLog("SystemLogs", LogLevel.Info, action, message, details);

        /// <summary>
        /// Логує попередження системної подія.
        /// </summary>
        /// <param name="action">Тип дії.</param>
        /// <param name="message">Основне повідомлення.</param>
        /// <param name="details">Додаткові деталі (опціонально).</param>
        public void LogSystemWarning(LogAction action, string message, string? details = null) =>
            ProcessLog("SystemLogs", LogLevel.Warning, action, message, details);

        /// <summary>
        /// Логує помилку системної подія з винятком.
        /// </summary>
        /// <param name="action">Тип дії.</param>
        /// <param name="message">Основне повідомлення про помилку.</param>
        /// <param name="ex">Виняток з деталями помилки (опціонально).</param>
        public void LogSystemError(LogAction action, string message, Exception? ex = null) =>
            ProcessLog("SystemLogs", LogLevel.Error, action, message, ex?.ToString());

        #endregion

        #region Public methods - Journal logging

        /// <summary>
        /// Логує дію в журналі (вставка, оновлення, видалення запису).
        /// </summary>
        /// <param name="journalTableName">Назва таблиці журналу (без суфіксу "_audit").</param>
        /// <param name="action">Тип дії.</param>
        /// <param name="actionMessage">Опис дії.</param>
        /// <param name="details">Додаткові деталі (опціонально).</param>
        public void LogJournalAction(string journalTableName, LogAction action, string actionMessage, string? details = null) =>
            ProcessLog($"{journalTableName}_audit", LogLevel.Audit, action, actionMessage, details);

        #endregion

        #region Public methods - Log retrieval

        /// <summary>
        /// Отримує всі логи з усіх таблиць, відсортовані за спаданням часу.
        /// </summary>
        /// <param name="limit">Максимальна кількість записів для отримання (за замовчуванням 1000).</param>
        /// <returns>Колекція логів, або порожній список, якщо БД ще не розблокована.</returns>
        public IEnumerable<LogEntry> GetAllLogs(int limit = 1000)
        {
            if (!_isDatabaseReady)
                return new List<LogEntry>();

            try
            {
                return _dbService.GetAllAggregatedLogs(limit);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Помилка агрегації логів: {ex.Message}");
                return new List<LogEntry>();
            }
        }

        /// <summary>
        /// Отримує системні логи з таблиці SystemLogs.
        /// </summary>
        /// <param name="limit">Максимальна кількість записів для отримання (за замовчуванням 1000).</param>
        /// <returns>Колекція системних логів, або порожній список, якщо БД ще не розблокована.</returns>
        public IEnumerable<LogEntry> GetSystemLogs(int limit = 1000)
        {
            if (!_isDatabaseReady)
                return new List<LogEntry>();

            return _dbService.GetLogsFromTable("SystemLogs", limit);
        }

        /// <summary>
        /// Отримує логи дій з журналу.
        /// </summary>
        /// <param name="journalTableName">Назва таблиці журналу (без суфіксу "_audit").</param>
        /// <param name="limit">Максимальна кількість записів для отримання (за замовчуванням 1000).</param>
        /// <returns>Колекція логів журналу, або порожній список, якщо БД ще не розблокована.</returns>
        public IEnumerable<LogEntry> GetJournalLogs(string journalTableName, int limit = 1000)
        {
            if (!_isDatabaseReady)
                return new List<LogEntry>();

            return _dbService.GetLogsFromTable($"{journalTableName}_audit", limit);
        }

        #endregion

        #region Public methods - Maintenance

        /// <summary>
        /// Перенаправляє всі накопичені логи з тимчасового файлу в БД.
        /// Викликається після успішної авторизації, коли БД розблокована.
        /// </summary>
        public void FlushPendingLogsToDatabase()
        {
            lock (_lockObj)
            {
                _isDatabaseReady = true;

                if (!File.Exists(_pendingLogsFilePath))
                    return;

                try
                {
                    // Читаємо всі рядки з тимчасового файлу
                    string[] lines = File.ReadAllLines(_pendingLogsFilePath);

                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        try
                        {
                            using var doc = JsonDocument.Parse(line);
                            string tableName = doc.RootElement.GetProperty("Table").GetString()!;
                            var logElement = doc.RootElement.GetProperty("Log");

                            var entry = JsonSerializer.Deserialize<LogEntry>(logElement.GetRawText());

                            if (entry != null)
                            {
                                WriteToDatabase(tableName, entry);
                            }
                        }
                        catch
                        {
                            // Ігноруємо пошкоджені рядки, продовжуємо з наступним
                        }
                    }

                    // Після успішного перенесення видаляємо файл
                    File.Delete(_pendingLogsFilePath);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Помилка перенесення тимчасових логів: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Видаляє старі логи, які збережені більше вказаного часу.
        /// </summary>
        /// <param name="daysToKeep">Кількість днів логів, які слід зберігати (за замовчуванням 30).</param>
        public void CleanupOldLogs(int daysToKeep = 30)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Private helpers - Core logging

        /// <summary>
        /// Основний метод обробки логування.
        /// Записує лог до БД (якщо відкрита) або до тимчасового файлу (якщо закрита).
        /// </summary>
        private void ProcessLog(string tableName, LogLevel level, LogAction action, string message, string? details)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = level,
                Action = action,
                Message = message,
                UserName = App.CurrentUser?.FullName ?? "System",
                Details = details
            };

            lock (_lockObj)
            {
                if (_isDatabaseReady)
                {
                    // База відкрита - пишемо напряму
                    WriteToDatabase(tableName, entry);
                }
                else
                {
                    // База ще закрита - складаємо у тимчасовий текстовий файл
                    WriteToTempFile(entry, tableName);
                }
            }
        }

        /// <summary>
        /// Записує лог до тимчасового файлу (JSON рядок на кожну лінію).
        /// </summary>
        private void WriteToTempFile(LogEntry entry, string targetTable)
        {
            try
            {
                // Створюємо анонімний об'єкт, щоб зберегти і сам лог, і таблицю призначення
                var wrapper = new { Table = targetTable, Log = entry };
                string json = JsonSerializer.Serialize(wrapper);

                // Дописуємо один рядок у файл
                File.AppendAllText(_pendingLogsFilePath, json + Environment.NewLine);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Не вдалося записати в тимчасовий лог: {ex.Message}");
            }
        }

        /// <summary>
        /// Записує лог до БД з ленивим створенням таблиць, якщо необхідно.
        /// </summary>
        private void WriteToDatabase(string tableName, LogEntry entry)
        {
            try
            {
                // Ліниве створення таблиць
                if (tableName == "SystemLogs" && !_isSystemTableCreated)
                {
                    CreateSystemTableIfNotExists();
                    _isSystemTableCreated = true;
                }
                else if (tableName.EndsWith("_audit"))
                {
                    CreateJournalAuditTableIfNotExists(tableName);
                }

                // Викликаємо універсальний метод вставки
                _dbService.InsertLogEntry(tableName, entry);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Помилка запису логу в БД: {ex.Message}");
            }
        }

        #endregion

        #region Private helpers - Table management

        /// <summary>
        /// Створює таблицю SystemLogs, якщо вона не існує.
        /// </summary>
        private void CreateSystemTableIfNotExists()
        {
            _dbService.EnsureSystemLogTableExists();
        }

        /// <summary>
        /// Створює таблицю журналу аудиту, якщо вона не існує.
        /// </summary>
        private void CreateJournalAuditTableIfNotExists(string tableName)
        {
            _dbService.EnsureJournalLogTableExists(tableName);
        }

        #endregion
    }
}