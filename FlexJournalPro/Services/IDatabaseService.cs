using FlexJournalPro.Models;
using System.Data;

namespace FlexJournalPro.Services
{
    /// <summary>
    /// Сервіс для управління базою даних журналів.
    /// Підтримує операції з користувачами, журналами, шаблонами та логуванням.
    /// </summary>
    public interface IDatabaseService
    {
        #region Database connection

        /// <summary>
        /// Встановлює з'єднання з базою даних.
        /// </summary>
        void Connect();

        #endregion

        #region System logging

        /// <summary>
        /// Забезпечує існування таблиці SystemLogs для логування системних подій.
        /// </summary>
        void EnsureSystemLogTableExists();

        /// <summary>
        /// Забезпечує існування таблиці журналу аудиту для журналу.
        /// </summary>
        /// <param name="tableName">Назва таблиці журналу аудиту (наприклад, "journal_XXX_audit").</param>
        /// <param name="connection">Опціональне з'єднання з БД (якщо null, використовується поточне).</param>
        /// <param name="transaction">Опціональна транзакція.</param>
        void EnsureJournalLogTableExists(string tableName, IDbConnection? connection = null, IDbTransaction? transaction = null);

        /// <summary>
        /// Вставляє запис логу у вказану таблицю.
        /// </summary>
        /// <param name="tableName">Назва таблиці для логу.</param>
        /// <param name="entry">Запис логу для вставки.</param>
        void InsertLogEntry(string tableName, LogEntry entry);

        /// <summary>
        /// Отримує список усіх імен таблиць аудиту в БД.
        /// </summary>
        /// <returns>Колекція імен таблиць аудиту.</returns>
        IEnumerable<string> GetAuditTableNames();

        /// <summary>
        /// Отримує логи з вказаної таблиці з обмеженням кількості записів.
        /// </summary>
        /// <param name="tableName">Назва таблиці для отримання логів.</param>
        /// <param name="limit">Максимальна кількість логів для отримання (за замовчуванням 1000).</param>
        /// <returns>Колекція логів з таблиці.</returns>
        IEnumerable<LogEntry> GetLogsFromTable(string tableName, int limit = 1000);

        /// <summary>
        /// Отримує агреговані логи з усіх таблиць (SystemLogs + всі таблиці журналів).
        /// </summary>
        /// <param name="limit">Максимальна кількість логів для отримання (за замовчуванням 1000).</param>
        /// <returns>Колекція агрегованих логів, відсортована за часом.</returns>
        IEnumerable<LogEntry> GetAllAggregatedLogs(int limit = 1000);

        #endregion

        #region Journal registry

        /// <summary>
        /// Отримує список усіх журналів, доступних поточному користувачу.
        /// </summary>
        /// <param name="currentUser">Поточний користувач (якщо null, повертаються всі журнали).</param>
        /// <returns>Список метаданих журналів.</returns>
        List<JournalMetadata> GetAllJournals(AppUser? currentUser = null);

        /// <summary>
        /// Створює новий журнал з вказаними метаданими та конфігурацією колон.
        /// </summary>
        /// <param name="meta">Метаді журналу (назва, описання тощо).</param>
        /// <param name="columns">Список конфігурацій колон для журналу.</param>
        void CreateNewJournal(JournalMetadata meta, List<ColumnConfig> columns);

        /// <summary>
        /// Завантажує всі дані журналу в DataTable.
        /// </summary>
        /// <param name="tableName">Назва таблиці журналу.</param>
        /// <returns>DataTable з усіма записами журналу.</returns>
        DataTable LoadJournalData(string tableName);

        /// <summary>
        /// Завантажує сторінку даних журналу з пагінацією.
        /// </summary>
        /// <param name="tableName">Назва таблиці журналу.</param>
        /// <param name="offset">Кількість записів для пропуску.</param>
        /// <param name="limit">Кількість записів для завантаження.</param>
        /// <returns>DataTable із записами сторінки.</returns>
        DataTable LoadJournalPage(string tableName, int offset, int limit);

        /// <summary>
        /// Отримує загальну кількість записів у журналі.
        /// </summary>
        /// <param name="tableName">Назва таблиці журналу.</param>
        /// <returns>Кількість записів у журналі.</returns>
        int GetJournalCount(string tableName);

        /// <summary>
        /// Отримує наступний номер реєстрації для журналу.
        /// </summary>
        /// <param name="tableName">Назва таблиці журналу.</param>
        /// <param name="startNumber">Початкове число для нумерації.</param>
        /// <returns>Наступний доступний номер реєстрації.</returns>
        long GetNextRegistrationNumber(string tableName, long startNumber);

        /// <summary>
        /// Отримує діапазон рядків журналу з сортуванням.
        /// </summary>
        /// <param name="tableName">Назва таблиці журналу.</param>
        /// <param name="startIndex">Індекс стартового рядка.</param>
        /// <param name="count">Кількість рядків для отримання.</param>
        /// <param name="columns">Список конфігурацій колон для парсингу.</param>
        /// <param name="orderByColumn">Колона для сортування (за замовчуванням "Id").</param>
        /// <param name="sortDescending">Чи сортувати за спаданням (за замовчуванням true).</param>
        /// <returns>Список привязаних рядків.</returns>
        IList<BindableRow> FetchRange(string tableName, int startIndex, int count, List<ColumnConfig> columns, string orderByColumn = "Id", bool sortDescending = true);

        /// <summary>
        /// Вставляє або оновлює рядок у журналі.
        /// </summary>
        /// <param name="tableName">Назва таблиці журналу.</param>
        /// <param name="rowData">Дані рядка для вставки або оновлення.</param>
        /// <param name="columns">Список конфігурацій колон.</param>
        void UpsertDictionaryRow(string tableName, BindableRow rowData, List<ColumnConfig> columns);

        #endregion

        #region Templates

        /// <summary>
        /// Зберігає шаблон таблиці в БД.
        /// </summary>
        /// <param name="template">Шаблон для збереження.</param>
        void SaveTemplate(TableTemplate template);

        /// <summary>
        /// Отримує шаблон за його ID.
        /// </summary>
        /// <param name="templateId">ID шаблону.</param>
        /// <returns>Об'єкт шаблону.</returns>
        TableTemplate GetTemplate(string templateId);

        /// <summary>
        /// Отримує JSON представлення шаблону за його ID.
        /// </summary>
        /// <param name="templateId">ID шаблону.</param>
        /// <returns>JSON строка шаблону.</returns>
        string GetTemplateJson(string templateId);

        /// <summary>
        /// Отримує метаді всіх доступних шаблонів.
        /// </summary>
        /// <returns>Список метаданих шаблонів.</returns>
        List<TemplateMetadata> GetAllTemplates();

        /// <summary>
        /// Деактивує шаблон, припиняючи його використання.
        /// </summary>
        /// <param name="templateId">ID шаблону для деактивації.</param>
        void DeactivateTemplate(string templateId);

        /// <summary>
        /// Оновлює конфігурацію автозаповнення журналу.
        /// </summary>
        /// <param name="journalId">ID журналу.</param>
        /// <param name="autoFillConfigJson">JSON строка конфігурації автозаповнення.</param>
        void UpdateJournalAutoFillConfig(long journalId, string autoFillConfigJson);

        /// <summary>
        /// Видаляє журнал з БД.
        /// </summary>
        /// <param name="journalId">ID журналу для видалення.</param>
        void DeleteJournal(long journalId);

        #endregion

        #region Users

        /// <summary>
        /// Отримує список всіх користувачів.
        /// </summary>
        /// <returns>Список користувачів.</returns>
        List<AppUser> GetAllUsers();

        /// <summary>
        /// Створює нового користувача з хешем пароля.
        /// </summary>
        /// <param name="user">Дані користувача.</param>
        /// <param name="passwordHash">Хеш пароля користувача (PBKDF2).</param>
        void CreateUser(AppUser user, string passwordHash);

        /// <summary>
        /// Оновлює дані користувача та опціонально пароль.
        /// </summary>
        /// <param name="user">Дані користувача для оновлення.</param>
        /// <param name="newPasswordHash">Новий хеш пароля (якщо null, пароль не змінюється).</param>
        void UpdateUser(AppUser user, string? newPasswordHash = null);

        /// <summary>
        /// Видаляє користувача з БД.
        /// </summary>
        /// <param name="userId">ID користувача для видалення.</param>
        void DeleteUser(long userId);

        /// <summary>
        /// Отримує список ID журналів, до яких має доступ користувач.
        /// </summary>
        /// <param name="userId">ID користувача.</param>
        /// <returns>Список ID журналів.</returns>
        List<int> GetUserAllowedJournalIds(int userId);

        /// <summary>
        /// Отримує користувача та його хеш пароля за логіном.
        /// </summary>
        /// <param name="login">Логін користувача.</param>
        /// <returns>Кортеж (User, PasswordHash) або (null, null) якщо користувача не знайдено.</returns>
        (AppUser? User, string? PasswordHash) GetUserWithHashByLogin(string login);

        /// <summary>
        /// Знаходить користувача за його ID.
        /// </summary>
        /// <param name="id">ID користувача.</param>
        /// <returns>Об'єкт користувача, або null якщо не знайдено.</returns>
        AppUser FindUserById(int id);

        #endregion
    }
}
