using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Text;
using System.Text.Json;
using FlexJournalPro.Models;

namespace FlexJournalPro.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString;

        public string ConnectionString => _connectionString;

        public DatabaseService()
        {
            // БД створюється поруч з .exe файлом
            string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_data.db");
            _connectionString = $"Data Source={dbPath};Version=3;";

            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            if (!File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_data.db")))
            {
                SQLiteConnection.CreateFile("app_data.db");
            }

            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();

                // Таблиця-реєстр усіх журналів
                string sql = @"
                    CREATE TABLE IF NOT EXISTS App_Journals (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Title TEXT NOT NULL,
                        PresetId TEXT NOT NULL,
                        TableName TEXT NOT NULL UNIQUE,
                        CreatedAt TEXT NOT NULL,
                        NumberStart INTEGER DEFAULT 1,
                        SessionConstantsJson TEXT
                    )";

                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.ExecuteNonQuery();
                }

                // Таблиця шаблонів
                sql = @"
                    CREATE TABLE IF NOT EXISTS App_Templates (
                        Id TEXT PRIMARY KEY,
                        Name TEXT NOT NULL,
                        Description TEXT,
                        JsonConfig TEXT NOT NULL,
                        Version INTEGER DEFAULT 1,
                        CreatedAt TEXT NOT NULL,
                        UpdatedAt TEXT NOT NULL,
                        IsActive INTEGER DEFAULT 1
                    )";

                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // --- МЕТОДИ ДЛЯ РЕЄСТРУ ---

        public List<JournalMetadata> GetAllJournals()
        {
            var list = new List<JournalMetadata>();
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                string sql = "SELECT * FROM App_Journals ORDER BY CreatedAt DESC";
                using (var cmd = new SQLiteCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new JournalMetadata
                        {
                            Id = (long)reader["Id"],
                            Title = reader["Title"].ToString(),
                            PresetId = reader["PresetId"].ToString(),
                            TableName = reader["TableName"].ToString(),
                            CreatedAt = DateTime.Parse(reader["CreatedAt"].ToString()),
                            NumberStart = Convert.ToInt64(reader["NumberStart"]),
                            SessionConstantsJson = reader["SessionConstantsJson"].ToString()
                        });
                    }
                }
            }
            return list;
        }

        public void CreateNewJournal(JournalMetadata meta, List<ColumnConfig> columns)
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        // 1. Генеруємо унікальне ім'я таблиці (напр. journal_638301239...)
                        string tableName = $"journal_{DateTime.Now.Ticks}";
                        meta.TableName = tableName;
                        meta.CreatedAt = DateTime.Now;

                        // 2. Спочатку створюємо фізичну таблицю для даних
                        // (Якщо це не вдасться, немає сенсу додавати запис у реєстр)
                        string createTableSql = BuildCreateTableSql(tableName, columns);
                        using (var cmd = new SQLiteCommand(createTableSql, conn, transaction))
                        {
                            cmd.ExecuteNonQuery();
                        }

                        // 3. Тільки після успішного створення таблиці додаємо запис у реєстр
                        string insertSql = @"
                            INSERT INTO App_Journals (Title, PresetId, TableName, CreatedAt, NumberStart, SessionConstantsJson)
                            VALUES (@Title, @PresetId, @TableName, @CreatedAt, @NumberStart, @Json)";

                        using (var cmd = new SQLiteCommand(insertSql, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Title", meta.Title);
                            cmd.Parameters.AddWithValue("@PresetId", meta.PresetId);
                            cmd.Parameters.AddWithValue("@TableName", meta.TableName);
                            cmd.Parameters.AddWithValue("@CreatedAt", meta.CreatedAt.ToString("s"));
                            cmd.Parameters.AddWithValue("@NumberStart", meta.NumberStart);
                            cmd.Parameters.AddWithValue("@Json", meta.SessionConstantsJson ?? "{}");
                            cmd.ExecuteNonQuery();
                            
                            // Отримуємо згенерований ID для meta
                            meta.Id = conn.LastInsertRowId;
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        // --- ДИНАМІЧНИЙ SQL ---

        private string BuildCreateTableSql(string tableName, List<ColumnConfig> columns)
        {
            var sb = new StringBuilder();
            sb.Append($"CREATE TABLE [{tableName}] (");

            // Системне поле ID (завжди створюється автоматично)
            sb.Append("Id INTEGER PRIMARY KEY AUTOINCREMENT");

            foreach (var col in columns)
            {
                // Пропускаємо поле Id з шаблону (воно системне і створюється автоматично)
                if (col.FieldName?.Equals("Id", StringComparison.OrdinalIgnoreCase) == true) continue;

                // Пропускаємо заголовки секцій (вони тільки для краси)
                if (col.Type == ColumnType.SectionHeader) continue;

                sb.Append(", ");
                sb.Append($"[{col.FieldName}] {GetSqlType(col.Type)}");
            }

            sb.Append(")");
            return sb.ToString();
        }

        private string GetSqlType(ColumnType type)
        {
            switch (type)
            {
                case ColumnType.Number:
                case ColumnType.Boolean: // SQLite використовує 0/1
                    return "INTEGER";
                case ColumnType.Currency:
                    return "REAL";
                default:
                    return "TEXT"; // Всі інші типи (Date, Dropdown) зберігаємо як текст
            }
        }

        // --- РОБОТА З ДАНИМИ ---

        public DataTable LoadJournalData(string tableName)
        {
            var dt = new DataTable();
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                string sql = $"SELECT * FROM [{tableName}]";
                using (var adapter = new SQLiteDataAdapter(sql, conn))
                {
                    adapter.Fill(dt);
                }
            }
            return dt;
        }

        // Пейджинг: повертає одну сторінку даних (LIMIT/OFFSET) у вигляді DataTable
        public DataTable LoadJournalPage(string tableName, int offset, int limit)
        {
            var dt = new DataTable();
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                string sql = $"SELECT * FROM [{tableName}] ORDER BY Id DESC LIMIT @Limit OFFSET @Offset";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Limit", limit);
                    cmd.Parameters.AddWithValue("@Offset", offset);
                    using (var reader = cmd.ExecuteReader())
                    {
                        dt.Load(reader);
                    }
                }
            }
            return dt;
        }

        // Повертає загальну кількість рядків у таблиці (для віртуалізації / scrollbar)
        public int GetJournalCount(string tableName)
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand($"SELECT COUNT(*) FROM [{tableName}]", conn))
                {
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        // Повернути список BindableRow для сторінки (LIMIT/OFFSET)
        public IList<BindableRow> FetchRange(string tableName, int startIndex, int count, List<ColumnConfig> columns)
        {
            var list = new List<BindableRow>();

            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                string sql = $"SELECT * FROM [{tableName}] ORDER BY Id DESC LIMIT @Limit OFFSET @Offset";

                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Limit", count);
                    cmd.Parameters.AddWithValue("@Offset", startIndex);

                    using (var reader = cmd.ExecuteReader())
                    {
                        // Кешуємо індекси колонок ОДИН РАЗ (замість пошуку в кожному циклі)
                        var columnIndexMap = new Dictionary<string, int>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            columnIndexMap[reader.GetName(i)] = i;
                        }

                        while (reader.Read())
                        {
                            var row = new BindableRow();
                        
                            // Завжди читаємо системне поле Id (незалежно від шаблону)
                            if (columnIndexMap.TryGetValue("Id", out int idIndex))
                            {
                                row["Id"] = reader.GetInt64(idIndex);
                            }

                            // Читаємо динамічні колонки
                            foreach (var col in columns)
                            {
                                if (col.Type == ColumnType.SectionHeader) continue;
                                
                                // Пропускаємо конфігурацію поля Id з шаблону (системне поле вже додано)
                                if (col.FieldName?.Equals("Id", StringComparison.OrdinalIgnoreCase) == true) continue;

                                if (!columnIndexMap.TryGetValue(col.FieldName, out int columnIndex))
                                    continue;

                                try
                                {
                                    // Швидка перевірка на NULL
                                    if (reader.IsDBNull(columnIndex))
                                    {
                                        row[col.FieldName] = null;
                                        continue;
                                    }

                                    // Типізоване читання замість ToString() + парсингу
                                    row[col.FieldName] = ReadTypedValue(reader, columnIndex, col.Type);
                                }
                                catch (Exception ex)
                                {
                                    // Fallback на універсальний метод
                                    System.Diagnostics.Debug.WriteLine($"Помилка читання {col.FieldName}: {ex.Message}");
                                    row[col.FieldName] = reader.GetValue(columnIndex);
                                }
                            }

                            // Позначаємо рядок як збережений (без змін)
                            row.MarkAsSaved();

                            list.Add(row);
                        }
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// Читає значення з SQLiteDataReader використовуючи типізовані методи (без ToString + парсингу)
        /// </summary>
        private object ReadTypedValue(SQLiteDataReader reader, int columnIndex, ColumnType targetType)
        {
            switch (targetType)
            {
                case ColumnType.Number:
                    // Пряме читання INTEGER з SQLite
                    return reader.GetInt64(columnIndex);

                case ColumnType.Currency:
                    // Пряме читання REAL як decimal
                    return reader.GetDecimal(columnIndex);

                case ColumnType.Boolean:
                    // SQLite зберігає bool як INTEGER (0/1)
                    return reader.GetInt64(columnIndex) != 0;

                case ColumnType.Date:
                    // Читаємо як рядок (SQLite не має нативного Date)
                    var dateStr = reader.GetString(columnIndex);
                    if (DateTime.TryParseExact(dateStr, 
                        new[] { "yyyy-MM-dd", "dd.MM.yyyy" }, 
                        System.Globalization.CultureInfo.InvariantCulture, 
                        System.Globalization.DateTimeStyles.None, 
                        out var parsedDate))
                    {
                        return DateTime.SpecifyKind(parsedDate.Date, DateTimeKind.Unspecified);
                    }
                    return DateTime.TryParse(dateStr, out parsedDate) 
                        ? DateTime.SpecifyKind(parsedDate.Date, DateTimeKind.Unspecified) 
                        : null;

                case ColumnType.DateTime:
                    var dtStr = reader.GetString(columnIndex);
                    var formats = new[] { "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm", "dd.MM.yyyy HH:mm:ss", "dd.MM.yyyy HH:mm" };
                    if (DateTime.TryParseExact(dtStr, formats, 
                        System.Globalization.CultureInfo.InvariantCulture, 
                        System.Globalization.DateTimeStyles.None, 
                        out var parsedDt))
                    {
                        return DateTime.SpecifyKind(parsedDt, DateTimeKind.Unspecified);
                    }
                    return DateTime.TryParse(dtStr, out parsedDt) 
                        ? DateTime.SpecifyKind(parsedDt, DateTimeKind.Unspecified) 
                        : null;

                case ColumnType.Time:
                    var timeStr = reader.GetString(columnIndex);
                    if (TimeSpan.TryParseExact(timeStr, "c", 
                        System.Globalization.CultureInfo.InvariantCulture, 
                        out var ts))
                    {
                        return ts;
                    }
                    if (TimeSpan.TryParseExact(timeStr, @"hh\:mm\:ss", 
                        System.Globalization.CultureInfo.InvariantCulture, 
                        out ts))
                    {
                        return ts;
                    }
                    // Fallback: парсинг як DateTime
                    if (DateTime.TryParse(timeStr, out var dtForTime))
                    {
                        return dtForTime.TimeOfDay;
                    }
                    return null;

                case ColumnType.Text:
                case ColumnType.Dropdown:
                case ColumnType.DropdownEditable:
                default:
                    // Пряме читання рядка (без ToString())
                    return reader.GetString(columnIndex);
            }
        }

        public void UpsertDictionaryRow(string tableName, BindableRow rowData, List<ColumnConfig> columns)
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();

                long id = -1;
                if (rowData.ContainsKey("Id") && rowData["Id"] != null)
                {
                    long.TryParse(rowData["Id"].ToString(), out id);
                }

                if (id > 0)
                {
                    ExecuteUpdate(conn, tableName, rowData, columns, id);
                }
                else
                {
                    ExecuteInsert(conn, tableName, rowData, columns);
                }

                // Позначаємо рядок як збережений після успішного запису в БД
                rowData.MarkAsSaved();
            }
        }

        /// <summary>
        /// Виконує UPDATE з оптимізованою конвертацією типів
        /// </summary>
        private void ExecuteUpdate(SQLiteConnection conn, string tableName, BindableRow rowData, List<ColumnConfig> columns, long id)
        {
            var sb = new StringBuilder();
            sb.Append($"UPDATE [{tableName}] SET ");

            var parameters = new List<SQLiteParameter>();
            bool first = true;

            foreach (var col in columns)
            {
                if (col.Type == ColumnType.SectionHeader) continue;
                
                // Пропускаємо системне поле Id (його не можна змінювати)
                if (col.FieldName?.Equals("Id", StringComparison.OrdinalIgnoreCase) == true) continue;

                if (!first) sb.Append(", ");
                sb.Append($"[{col.FieldName}] = @{col.FieldName}");

                object value = rowData.ContainsKey(col.FieldName) ? rowData[col.FieldName] : DBNull.Value;
                
                // Типізована конвертація
                parameters.Add(CreateTypedParameter($"@{col.FieldName}", value, col.Type));
                first = false;
            }

            sb.Append(" WHERE Id = @Id");
            parameters.Add(new SQLiteParameter("@Id", id));

            using (var cmd = new SQLiteCommand(sb.ToString(), conn))
            {
                cmd.Parameters.AddRange(parameters.ToArray());
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Виконує INSERT з оптимізованою конвертацією типів
        /// </summary>
        private void ExecuteInsert(SQLiteConnection conn, string tableName, BindableRow rowData, List<ColumnConfig> columns)
        {
            var sb = new StringBuilder();
            sb.Append($"INSERT INTO [{tableName}] (");
            var colNames = new List<string>();
            var paramNames = new List<string>();
            var parameters = new List<SQLiteParameter>();

            foreach (var col in columns)
            {
                if (col.Type == ColumnType.SectionHeader) continue;
                
                // Пропускаємо системне поле Id (воно генерується автоматично)
                if (col.FieldName?.Equals("Id", StringComparison.OrdinalIgnoreCase) == true) continue;

                colNames.Add($"[{col.FieldName}]");
                paramNames.Add($"@{col.FieldName}");

                object value = rowData.ContainsKey(col.FieldName) ? rowData[col.FieldName] : DBNull.Value;
                
                // Типізована конвертація
                parameters.Add(CreateTypedParameter($"@{col.FieldName}", value, col.Type));
            }

            sb.Append(string.Join(", ", colNames));
            sb.Append(") VALUES (");
            sb.Append(string.Join(", ", paramNames));
            sb.Append("); SELECT last_insert_rowid();");

            using (var cmd = new SQLiteCommand(sb.ToString(), conn))
            {
                cmd.Parameters.AddRange(parameters.ToArray());
                object newId = cmd.ExecuteScalar();
                rowData["Id"] = Convert.ToInt64(newId);
            }
        }

        /// <summary>
        /// Створює типізований SQLiteParameter без ToString() конвертації
        /// </summary>
        private SQLiteParameter CreateTypedParameter(string paramName, object value, ColumnType columnType)
        {
            if (value == null || value == DBNull.Value)
            {
                return new SQLiteParameter(paramName, DBNull.Value);
            }

            switch (columnType)
            {
                case ColumnType.Number:
                    return new SQLiteParameter(paramName, Convert.ToInt64(value));

                case ColumnType.Currency:
                    return new SQLiteParameter(paramName, Convert.ToDecimal(value));

                case ColumnType.Boolean:
                    bool boolVal = value is bool b ? b : Convert.ToBoolean(value);
                    return new SQLiteParameter(paramName, boolVal ? 1 : 0);

                case ColumnType.Date:
                    if (value is DateTime dateVal)
                    {
                        return new SQLiteParameter(paramName, dateVal.ToString("yyyy-MM-dd"));
                    }
                    return new SQLiteParameter(paramName, value.ToString());

                case ColumnType.DateTime:
                    if (value is DateTime dtVal)
                    {
                        return new SQLiteParameter(paramName, dtVal.ToString("yyyy-MM-dd HH:mm:ss"));
                    }
                    return new SQLiteParameter(paramName, value.ToString());

                case ColumnType.Time:
                    if (value is TimeSpan tsVal)
                    {
                        return new SQLiteParameter(paramName, tsVal.ToString(@"hh\:mm\:ss"));
                    }
                    if (value is DateTime dtTimeVal)
                    {
                        return new SQLiteParameter(paramName, dtTimeVal.ToString("HH:mm:ss"));
                    }
                    return new SQLiteParameter(paramName, value.ToString());

                default:
                    return new SQLiteParameter(paramName, value.ToString());
            }
        }

        // --- МЕТОДИ ДЛЯ ШАБЛОНІВ ---

        /// <summary>
        /// Зберегти або оновити шаблон у БД
        /// </summary>
        public void SaveTemplate(TableTemplate template)
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();

                var meta = new TemplateMetadata
                {
                    Id = template.Id,
                    Name = template.Title,
                    JsonConfig = JsonSerializer.Serialize(template),
                    UpdatedAt = DateTime.Now
                };

                // Перевіряємо, чи існує шаблон
                using (var checkCmd = new SQLiteCommand("SELECT Version FROM App_Templates WHERE Id = @Id", conn))
                {
                    checkCmd.Parameters.AddWithValue("@Id", meta.Id);
                    var existingVersion = checkCmd.ExecuteScalar();

                    if (existingVersion != null)
                    {
                        // Оновлення існуючого шаблону (інкремент версії)
                        meta.Version = Convert.ToInt32(existingVersion) + 1;

                        string updateSql = @"
                            UPDATE App_Templates 
                            SET Name = @Name, 
                                Description = @Description, 
                                JsonConfig = @JsonConfig, 
                                Version = @Version, 
                                UpdatedAt = @UpdatedAt
                            WHERE Id = @Id";

                        using (var cmd = new SQLiteCommand(updateSql, conn))
                        {
                            cmd.Parameters.AddWithValue("@Id", meta.Id);
                            cmd.Parameters.AddWithValue("@Name", meta.Name);
                            cmd.Parameters.AddWithValue("@Description", meta.Description ?? "");
                            cmd.Parameters.AddWithValue("@JsonConfig", meta.JsonConfig);
                            cmd.Parameters.AddWithValue("@Version", meta.Version);
                            cmd.Parameters.AddWithValue("@UpdatedAt", meta.UpdatedAt.ToString("s"));
                            cmd.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        // Новий шаблон
                        meta.Version = 1;
                        meta.CreatedAt = DateTime.Now;
                        meta.IsActive = true;

                        string insertSql = @"
                            INSERT INTO App_Templates (Id, Name, Description, JsonConfig, Version, CreatedAt, UpdatedAt, IsActive)
                            VALUES (@Id, @Name, @Description, @JsonConfig, @Version, @CreatedAt, @UpdatedAt, @IsActive)";

                        using (var cmd = new SQLiteCommand(insertSql, conn))
                        {
                            cmd.Parameters.AddWithValue("@Id", meta.Id);
                            cmd.Parameters.AddWithValue("@Name", meta.Name);
                            cmd.Parameters.AddWithValue("@Description", meta.Description ?? "");
                            cmd.Parameters.AddWithValue("@JsonConfig", meta.JsonConfig);
                            cmd.Parameters.AddWithValue("@Version", meta.Version);
                            cmd.Parameters.AddWithValue("@CreatedAt", meta.CreatedAt.ToString("s"));
                            cmd.Parameters.AddWithValue("@UpdatedAt", meta.UpdatedAt.ToString("s"));
                            cmd.Parameters.AddWithValue("@IsActive", meta.IsActive ? 1 : 0);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Завантажити шаблон з БД
        /// </summary>
        public TableTemplate GetTemplate(string templateId)
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                string sql = "SELECT JsonConfig FROM App_Templates WHERE Id = @Id AND IsActive = 1";

                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", templateId);
                    var json = cmd.ExecuteScalar() as string;

                    if (json != null)
                    {
                        return JsonSerializer.Deserialize<TableTemplate>(json);
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Отримати список усіх активних шаблонів
        /// </summary>
        public List<TemplateMetadata> GetAllTemplates()
        {
            var list = new List<TemplateMetadata>();
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                string sql = "SELECT * FROM App_Templates WHERE IsActive = 1 ORDER BY Name";

                using (var cmd = new SQLiteCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new TemplateMetadata
                        {
                            Id = reader["Id"].ToString(),
                            Name = reader["Name"].ToString(),
                            Description = reader["Description"].ToString(),
                            JsonConfig = reader["JsonConfig"].ToString(),
                            Version = Convert.ToInt32(reader["Version"]),
                            CreatedAt = DateTime.Parse(reader["CreatedAt"].ToString()),
                            UpdatedAt = DateTime.Parse(reader["UpdatedAt"].ToString()),
                            IsActive = Convert.ToInt32(reader["IsActive"]) == 1
                        });
                    }
                }
            }
            return list;
        }

        /// <summary>
        /// Деактивувати шаблон (soft delete)
        /// </summary>
        public void DeactivateTemplate(string templateId)
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                string sql = "UPDATE App_Templates SET IsActive = 0, UpdatedAt = @UpdatedAt WHERE Id = @Id";

                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", templateId);
                    cmd.Parameters.AddWithValue("@UpdatedAt", DateTime.Now.ToString("s"));
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Оновлює сеансові константи для журналу
        /// </summary>
        public void UpdateJournalConstants(long journalId, string sessionConstantsJson)
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                string sql = "UPDATE App_Journals SET SessionConstantsJson = @Json WHERE Id = @Id";

                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", journalId);
                    cmd.Parameters.AddWithValue("@Json", sessionConstantsJson ?? "{}");
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Видаляє журнал з реєстру та його фізичну таблицю з бази даних
        /// </summary>
        /// <param name="journalId">ID журналу для видалення</param>
        public void DeleteJournal(long journalId)
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        // 1. Отримуємо назву таблиці журналу
                        string tableName = null;
                        using (var cmd = new SQLiteCommand("SELECT TableName FROM App_Journals WHERE Id = @Id", conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Id", journalId);
                            var result = cmd.ExecuteScalar();
                            if (result != null)
                            {
                                tableName = result.ToString();
                            }
                        }

                        if (string.IsNullOrEmpty(tableName))
                        {
                            throw new InvalidOperationException($"Журнал з ID {journalId} не знайдено");
                        }

                        // 2. Видаляємо запис з реєстру журналів
                        // (Робимо це спочатку, щоб уникнути ситуації, коли таблиця видалена, а запис залишився)
                        using (var cmd = new SQLiteCommand("DELETE FROM App_Journals WHERE Id = @Id", conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Id", journalId);
                            int rowsAffected = cmd.ExecuteNonQuery();
                            
                            if (rowsAffected == 0)
                            {
                                throw new InvalidOperationException($"Не вдалося видалити запис журналу з ID {journalId}");
                            }
                        }

                        // 3. Видаляємо фізичну таблицю з даними
                        using (var cmd = new SQLiteCommand($"DROP TABLE IF EXISTS [{tableName}]", conn, transaction))
                        {
                            cmd.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }
    }

    // Интерфейс провайдера елементів для віртуалізації
    public interface IItemsProvider
    {
        int FetchCount();
        IList<BindableRow> FetchRange(int startIndex, int count);
    }

    public class JournalDataProvider : IItemsProvider
    {
        private readonly DatabaseService _dbService;
        private readonly string _tableName;
        private readonly List<ColumnConfig> _columns;

        public JournalDataProvider(DatabaseService dbService, string tableName, List<ColumnConfig> columns)
        {
            _dbService = dbService;
            _tableName = tableName;
            _columns = columns;
        }

        public int FetchCount()
        {
            return _dbService.GetJournalCount(_tableName);
        }

        public IList<BindableRow> FetchRange(int startIndex, int count)
        {
            return _dbService.FetchRange(_tableName, startIndex, count, _columns);
        }
    }
}