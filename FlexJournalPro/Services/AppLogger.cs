using FlexJournalPro.Models;

namespace FlexJournalPro.Services
{
    /// <summary>
    /// Глобальний фасад для логування подій у системі.
    /// Делегує логування реалізації <see cref="ILogService"/>.
    /// </summary>
    public static class AppLogger
    {
        #region Fields

        private static ILogService? _internalLogger;

        #endregion

        #region Initialization

        /// <summary>
        /// Ініціалізує глобальний логгер.
        /// Має бути викликано один раз при старті додатку.
        /// </summary>
        /// <param name="logService">Конкретна реалізація сервісу логування.</param>
        public static void Initialize(ILogService logService)
        {
            _internalLogger = logService;
        }

        #endregion

        #region Public methods - System logging

        /// <summary>
        /// Логує інформаційну систему подію.
        /// </summary>
        public static void LogSystemInfo(LogAction action, string message, string? details = null)
        {
            _internalLogger?.LogSystemInfo(action, message, details);
        }

        /// <summary>
        /// Логує попередження на рівні системи.
        /// </summary>
        public static void LogSystemWarning(LogAction action, string message, string? details = null)
        {
            _internalLogger?.LogSystemWarning(action, message, details);
        }

        /// <summary>
        /// Логує помилку на рівні системи.
        /// </summary>
        public static void LogSystemError(LogAction action, string message, Exception? ex = null)
        {
            _internalLogger?.LogSystemError(action, message, ex);
        }

        #endregion

        #region Public methods - Journal logging

        /// <summary>
        /// Логує дію, виконану у контексті журналу.
        /// </summary>
        public static void LogJournalAction(string journalTableName, LogAction action, string actionMessage, string? details = null)
        {
            _internalLogger?.LogJournalAction(journalTableName, action, actionMessage, details);
        }

        #endregion
    }
}
