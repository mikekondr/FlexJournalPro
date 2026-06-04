namespace FlexJournalPro.Models
{
    public class LogEntry
    {
        public int Id { get; set; }

        public DateTime Timestamp { get; set; }

        public LogLevel Level { get; set; }

        public LogAction Action { get; set; }

        public string Message { get; set; } = string.Empty;

        public string? UserName { get; set; }

        public string? Details { get; set; }
    }

    public enum LogAction
    {
        // Системні
        SystemStarted,
        UserLoginAttempt,
        UserLoginFailed,
        SystemHalted,
        SystemError,
        SystemWarning,

        // Шаблони
        TemplateCreated,
        TemplateUpdated,
        TemplateDeleted,

        // Журнали
        JournalOpened,
        JournalClosed,
        JournalCreated,
        JournalDeleted,
        JournalRecordAdded,
        JournalRecordUpdated,
        JournalRecordDeleted,

        // База даних
        DatabaseCreated,
        DatabaseConnected,
        TableCreated,
        TableDeleted,
        DatabaseOpenError,
        DatabaseWriteError,

        // Користувачі
        UserLogin,
        UserLogout,
        UserCreated,
        UserUpdated,
        UserDeleted,
        PasswordChanged,
    }

    public enum LogLevel
    {
        Info,
        Audit,
        Warning,
        Error
    }
}
