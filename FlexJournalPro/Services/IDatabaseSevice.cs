using FlexJournalPro.Models;
using System.Data;

namespace FlexJournalPro.Services
{
    public interface IDatabaseService
    {
        // База даних
        void Connect();

        // Система логування
        void EnsureSystemLogTableExists();
        void EnsureJournalLogTableExists(string tableName, IDbConnection? connection = null, IDbTransaction? transaction = null);
        void InsertLogEntry(string tableName, LogEntry entry);
        IEnumerable<string> GetAuditTableNames();
        IEnumerable<LogEntry> GetLogsFromTable(string tableName, int limit = 1000);
        IEnumerable<LogEntry> GetAllAggregatedLogs(int limit = 1000);

        // Реєстр журналів
        List<JournalMetadata> GetAllJournals(AppUser? currentUser = null);
        void CreateNewJournal(JournalMetadata meta, List<ColumnConfig> columns);
        DataTable LoadJournalData(string tableName);
        DataTable LoadJournalPage(string tableName, int offset, int limit);
        int GetJournalCount(string tableName);
        long GetNextRegistrationNumber(string tableName, long startNumber);
        IList<BindableRow> FetchRange(string tableName, int startIndex, int count, List<ColumnConfig> columns, string orderByColumn = "Id", bool sortDescending = true);
        void UpsertDictionaryRow(string tableName, BindableRow rowData, List<ColumnConfig> columns);

        // Шаблони
        void SaveTemplate(TableTemplate template);
        TableTemplate GetTemplate(string templateId);
        string GetTemplateJson(string templateId);
        List<TemplateMetadata> GetAllTemplates();
        void DeactivateTemplate(string templateId);
        void UpdateJournalAutoFillConfig(long journalId, string autoFillConfigJson);
        void DeleteJournal(long journalId);

        // Користувачі
        List<AppUser> GetAllUsers();
        void CreateUser(AppUser user, string passwordHash);
        void UpdateUser(AppUser user, string? newPasswordHash = null);
        void DeleteUser(long userId);
        List<int> GetUserAllowedJournalIds(int userId);
        (AppUser? User, string? PasswordHash) GetUserWithHashByLogin(string login);
        public AppUser FindUserById(int id);
    }
}
