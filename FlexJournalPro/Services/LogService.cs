using FlexJournalPro.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Text.Json;

namespace FlexJournalPro.Services
{
    public static class AppLogger
    {
        private static ILogService? _internalLogger;

        /// <summary>
        /// Ініціалізація глобального логгера (викликається один раз при старті)
        /// </summary>
        public static void Initialize(ILogService logService)
        {
            _internalLogger = logService;
        }

        public static void LogSystemInfo(LogAction action, string message, string? details = null)
        {
            _internalLogger?.LogSystemInfo(action, message, details);
        }

        public static void LogSystemWarning(LogAction action, string message, string? details = null)
        {
            _internalLogger?.LogSystemWarning(action, message, details);
        }

        public static void LogSystemError(LogAction action, string message, Exception? ex = null)
        {
            _internalLogger?.LogSystemError(action, message, ex);
        }

        public static void LogJournalAction(string journalTableName, LogAction action, string actionMessage, string? details = null)
        {
            _internalLogger?.LogJournalAction(journalTableName, action, actionMessage, details);
        }
    }

    public interface ILogService
    {
        // ==========================================
        // 1. СИСТЕМНІ ПОДІЇ (Таблиця SystemLogs)
        // ==========================================

        void LogSystemInfo(LogAction action, string message, string? details = null);
        void LogSystemWarning(LogAction action, string message, string? details = null);
        void LogSystemError(LogAction action, string message, Exception? ex = null);

        IEnumerable<LogEntry> GetAllLogs(int limit = 1000);
        IEnumerable<LogEntry> GetSystemLogs(int limit = 1000);

        // ==========================================
        // 2. ПОДІЇ ЖУРНАЛІВ (Таблиці Journal_XXX_Audit)
        // ==========================================

        void LogJournalAction(string journalTableName, LogAction action, string actionMessage, string? details = null);

        IEnumerable<LogEntry> GetJournalLogs(string journalTableName, int limit = 1000);

        // ==========================================
        // 3. ОБСЛУГОВУВАННЯ
        // ==========================================

        void FlushPendingLogsToDatabase();

        void CleanupOldLogs(int daysToKeep = 30);
    }

    public class LogService : ILogService
    {
        private readonly IDatabaseService _dbService;
        private readonly string _pendingLogsFilePath;
        private readonly object _lockObj = new object();

        // Прапорець, який вказує, чи можна вже писати прямо в БД
        private bool _isDatabaseReady = false;
        private bool _isSystemTableCreated = false;

        public LogService(IDatabaseService dbService)
        {
            _dbService = dbService;

            // Статичне ім'я файлу, щоб зберігати логи між перезапусками програми
            _pendingLogsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_audit.tmp");
        }

        // ==========================================
        // МЕТОДИ ЛОГУВАННЯ
        // ==========================================

        public void LogSystemInfo(LogAction action, string message, string? details = null) =>
            ProcessLog("SystemLogs", LogLevel.Info, action, message, details);

        public void LogSystemWarning(LogAction action, string message, string? details = null) =>
            ProcessLog("SystemLogs", LogLevel.Warning, action, message, details);

        public void LogSystemError(LogAction action, string message, Exception? ex = null) =>
            ProcessLog("SystemLogs", LogLevel.Error, action, message, ex?.ToString());

        public void LogJournalAction(string journalTableName, LogAction action, string actionMessage, string? details = null) =>
            ProcessLog($"{journalTableName}_audit", LogLevel.Audit, action, actionMessage, details);

        // ==========================================
        // ЯДРО ОБРОБКИ (ФАЙЛ АБО БД)
        // ==========================================

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
                else if (tableName.StartsWith("journal_"))
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

        // ==========================================
        // ПЕРЕНЕСЕННЯ ЛОГІВ ПІСЛЯ АВТОРИЗАЦІЇ
        // ==========================================

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
                        if (string.IsNullOrWhiteSpace(line)) continue;

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

        private void CreateSystemTableIfNotExists()
        {
            _dbService.EnsureSystemLogTableExists();
        }

        private void CreateJournalAuditTableIfNotExists(string tableName)
        {
            _dbService.EnsureJournalLogTableExists(tableName);
        }

        public IEnumerable<LogEntry> GetAllLogs(int limit = 1000)
        {
            // Захист: якщо до БД ще немає доступу, повертаємо порожній список (або лог "Додаток стартує")
            if (!_isDatabaseReady) return new List<LogEntry>();

            try
            {
                // 2. Отримуємо імена всіх таблиць аудиту
                return _dbService.GetAllAggregatedLogs(limit);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Помилка агрегації логів: {ex.Message}");
            }
            
            return new List<LogEntry>();
        }

        public IEnumerable<LogEntry> GetSystemLogs(int limit = 1000)
        {
            if (!_isDatabaseReady) return new List<LogEntry>();
            return _dbService.GetLogsFromTable("SystemLogs", limit);
        }

        public IEnumerable<LogEntry> GetJournalLogs(string journalTableName, int limit = 1000)
        {
            if (!_isDatabaseReady) return new List<LogEntry>();
            return _dbService.GetLogsFromTable($"{journalTableName}_audit", limit);
        }

        public void CleanupOldLogs(int daysToKeep = 30)
        {
            throw new NotImplementedException();
        }
    }
}