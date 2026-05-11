using System.Data;
using System.IO;
using System.Text;
using System.Text.Json;
using FlexJournalPro.Config;
using FlexJournalPro.Models;
using Microsoft.Data.Sqlite;

namespace FlexJournalPro.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString;
        private readonly AppConfig _config = AppConfig.Instance;
        private readonly bool _useCipher;

        public string ConnectionString => _connectionString;

        public DatabaseService(string? decryptedDek = null)
        {
            // Завантажимо конфігурацію
            _useCipher = _config.Database.UseCipher;

            // БД створюється поряд з .exe файлом
            string dbPath = AppConfig.DatabasePath;

            if (_useCipher)
            {
                // SQLite-Cipher
                if (string.IsNullOrWhiteSpace(decryptedDek))
                {
                    throw new InvalidOperationException("DEK ключ не передано для шифрованої бази даних.");
                }
                _connectionString = $"Data Source={dbPath};Password={decryptedDek};";
            }
            else
            {
                // Звичайний SQLite
                _connectionString = $"Data Source={dbPath};";
            }

            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            // Створюємо файл БД якщо його немає
            if (!File.Exists(AppConfig.DatabasePath))
            {
                if (_useCipher)
                {
                    // Для SQLCipher створюємо через відкриття з'єднання
                    using (var conn = new SqliteConnection(_connectionString))
                    {
                        conn.Open();
                    }
                }
                else
                {
                    // SqliteConnection буде автоматично створювати файл, якщо його немає
                    using (var conn = new SqliteConnection(_connectionString))
                    {
                        conn.Open();
                    }
                }
            }

            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();

                // Таблиця-реєстр усіх журналів
                string sql = @"
                    CREATE TABLE IF NOT EXISTS App_Journals (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Title TEXT NOT NULL,
                        TemplateId TEXT NOT NULL,
                        TemplateName TEXT,
                        TemplateVersion INTEGER DEFAULT 1,
                        TableName TEXT NOT NULL UNIQUE,
                        CreatedAt TEXT NOT NULL,
                        NumberStart INTEGER DEFAULT 1,
                        AutoFillConfigJson TEXT,
                        TemplateConfigJson TEXT
                    )";

                using (var cmd = new SqliteCommand(sql, conn))
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

                using (var cmd = new SqliteCommand(sql, conn))
                {
                    cmd.ExecuteNonQuery();
                }

                // Таблиця користувачів
                sql = @"
                    CREATE TABLE IF NOT EXISTS App_Users (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Login TEXT NOT NULL UNIQUE,
                        PasswordHash TEXT NOT NULL,
                        FullName TEXT NOT NULL,
                        Role INTEGER DEFAULT 0
                    )";

                using (var cmd = new SqliteCommand(sql, conn))
                {
                    cmd.ExecuteNonQuery();
                }

                // Таблиця доступу користувачів до журналів
                sql = @"
                    CREATE TABLE IF NOT EXISTS App_UserJournalAccess (
                        UserId INTEGER NOT NULL,
                        JournalId INTEGER NOT NULL,
                        PRIMARY KEY (UserId, JournalId),
                        FOREIGN KEY (UserId) REFERENCES App_Users(Id) ON DELETE CASCADE,
                        FOREIGN KEY (JournalId) REFERENCES App_Journals(Id) ON DELETE CASCADE
                    )";
                using (var cmd = new SqliteCommand(sql, conn))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // --- МЕТОДИ ДЛЯ РЕЄСТРУ ---

        public List<JournalMetadata> GetAllJournals(AppUser? currentUser = null)
        {
            // Якщо currentUser не передано, беремо глобального (App.CurrentUser)
            currentUser ??= App.CurrentUser;

            var list = new List<JournalMetadata>();
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                
                string sql;
                bool filterByUser = currentUser != null && currentUser.Role != UserRole.Admin;
                
                if (filterByUser)
                {
                    if (currentUser.AllowedJournalIds.Count == 0)
                        return list; // Немає доступу до жодного журналу

                    string ids = string.Join(",", currentUser.AllowedJournalIds);
                    sql = $"SELECT * FROM App_Journals WHERE Id IN ({ids}) ORDER BY CreatedAt DESC";
                }
                else
                {
                    // Адмін бачить все
                    sql = "SELECT * FROM App_Journals ORDER BY CreatedAt DESC";
                }

                using (var cmd = new SqliteCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var journal = new JournalMetadata
                        {
                            Id = (long)reader["Id"],
                            Title = reader["Title"]?.ToString() ?? string.Empty,
                            TemplateId = reader["TemplateId"]?.ToString() ?? string.Empty,
                            TableName = reader["TableName"]?.ToString() ?? string.Empty,
                            CreatedAt = DateTime.Parse(reader["CreatedAt"]?.ToString() ?? DateTime.MinValue.ToString()),
                            NumberStart = Convert.ToInt64(reader["NumberStart"]),
                            AutoFillConfigJson = reader["AutoFillConfigJson"]?.ToString()
                        };

                        if (reader["TemplateName"] != DBNull.Value)
                            journal.TemplateName = reader["TemplateName"]?.ToString();

                        if (reader["TemplateVersion"] != DBNull.Value)
                            journal.TemplateVersion = Convert.ToInt32(reader["TemplateVersion"]);

                        if (reader["TemplateConfigJson"] != DBNull.Value)
                        {
                            journal.TemplateConfigJson = reader["TemplateConfigJson"]?.ToString();
                        }

                        list.Add(journal);
                    }
                }
            }
            return list;
        }

        public void CreateNewJournal(JournalMetadata meta, List<ColumnConfig> columns)
        {
            using (var conn = new SqliteConnection(_connectionString))
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
                        using (var cmd = new SqliteCommand(createTableSql, conn, transaction))
                        {
                            cmd.ExecuteNonQuery();
                        }

                        // 3. Тільки після успішного створення таблиці додаємо запис у реєстр
                        string insertSql = @"
                            INSERT INTO App_Journals (Title, TemplateId, TemplateName, TemplateVersion, TableName, CreatedAt, NumberStart, AutoFillConfigJson, TemplateConfigJson)
                            VALUES (@Title, @TemplateId, @TemplateName, @TemplateVersion, @TableName, @CreatedAt, @NumberStart, @Json, @TemplateJson)";

                        using (var cmd = new SqliteCommand(insertSql, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Title", meta.Title);
                            cmd.Parameters.AddWithValue("@TemplateId", meta.TemplateId);
                            cmd.Parameters.AddWithValue("@TemplateName", meta.TemplateName ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@TemplateVersion", meta.TemplateVersion);
                            cmd.Parameters.AddWithValue("@TableName", meta.TableName);
                            cmd.Parameters.AddWithValue("@CreatedAt", meta.CreatedAt.ToString("s"));
                            cmd.Parameters.AddWithValue("@NumberStart", meta.NumberStart);
                            cmd.Parameters.AddWithValue("@Json", meta.AutoFillConfigJson ?? "{}");
                            cmd.Parameters.AddWithValue("@TemplateJson", meta.TemplateConfigJson ?? "{}");
                            cmd.ExecuteNonQuery();
                            
                            // Отримуємо згенерований ID для meta
                            using (var cmdId = new SqliteCommand("SELECT last_insert_rowid()", conn, transaction))
                            {
                                var scalarResult = cmdId.ExecuteScalar();
                                meta.Id = scalarResult != null ? Convert.ToInt64(scalarResult) : 0;
                            }
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
                case ColumnType.RegNumber: // Реєстраційний номер - ціле число
                case ColumnType.Boolean: // SQLite використовує 0/1
                case ColumnType.Lock:    // Блокування - boolean
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
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                string sql = $"SELECT * FROM [{tableName}]";
                using (var cmd = new SqliteCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    dt.Load(reader);
                }
            }
            return dt;
        }

        // Пейджинг: повертає одну сторінку даних (LIMIT/OFFSET) у вигляді DataTable
        public DataTable LoadJournalPage(string tableName, int offset, int limit)
        {
            var dt = new DataTable();
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                string sql = $"SELECT * FROM [{tableName}] ORDER BY Id DESC LIMIT @Limit OFFSET @Offset";
                using (var cmd = new SqliteCommand(sql, conn))
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
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqliteCommand($"SELECT COUNT(*) FROM [{tableName}]", conn))
                {
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        public long GetNextRegistrationNumber(string tableName, long startNumber)
        {
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                string sql = $"SELECT MAX(RegNumber) FROM [{tableName}]";
                using (var cmd = new SqliteCommand(sql, conn))
                {
                    var result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                    {
                        return Convert.ToInt64(result) + 1;
                    }
                }
            }
            // Якщо записів немає або MAX вернув NULL - починаємо зі стартового номера
            return startNumber;
        }

        // Повернути список BindableRow для сторінки (LIMIT/OFFSET)
        public IList<BindableRow> FetchRange(string tableName, int startIndex, int count, List<ColumnConfig> columns)
        {
            var list = new List<BindableRow>();

            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                string sql = $"SELECT * FROM [{tableName}] ORDER BY Id DESC LIMIT @Limit OFFSET @Offset";

                using (var cmd = new SqliteCommand(sql, conn))
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

                                if (col.FieldName == null || !columnIndexMap.TryGetValue(col.FieldName, out int columnIndex))
                                    continue;

                                try
                                {
                                    // Швидка перевірка на NULL
                                    if (reader.IsDBNull(columnIndex))
                                    {
                                        row[col.FieldName] = string.Empty; // замість null для уникнення помилок
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
        /// Читає значення з SqliteDataReader використовуючи типізовані методи (без ToString + парсингу)
        /// </summary>
        private object ReadTypedValue(SqliteDataReader reader, int columnIndex, ColumnType targetType)
        {
            switch (targetType)
            {
                case ColumnType.Number:
                case ColumnType.RegNumber:
                    // Пряме читання INTEGER з SQLite
                    return reader.GetInt64(columnIndex);

                case ColumnType.Currency:
                    // Пряме читання REAL як decimal
                    return reader.GetDecimal(columnIndex);

                case ColumnType.Boolean:
                case ColumnType.Lock:
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
                        ? (object)DateTime.SpecifyKind(parsedDate.Date, DateTimeKind.Unspecified) 
                        : DBNull.Value;

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
                        ? (object)DateTime.SpecifyKind(parsedDt, DateTimeKind.Unspecified) 
                        : DBNull.Value;

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
                    return DBNull.Value;

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
            using (var conn = new SqliteConnection(_connectionString))
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
        private void ExecuteUpdate(SqliteConnection conn, string tableName, BindableRow rowData, List<ColumnConfig> columns, long id)
        {
            var sb = new StringBuilder();
            sb.Append($"UPDATE [{tableName}] SET ");

            var parameters = new List<SqliteParameter>();
            bool first = true;

            foreach (var col in columns)
            {
                if (col.Type == ColumnType.SectionHeader) continue;
                
                // Пропускаємо системне поле Id (його не можна змінювати)
                if (col.FieldName?.Equals("Id", StringComparison.OrdinalIgnoreCase) == true) continue;

                if (!first) sb.Append(", ");
                sb.Append($"[{col.FieldName}] = @{col.FieldName}");

                object value = col.FieldName != null && rowData.ContainsKey(col.FieldName) ? rowData[col.FieldName] : DBNull.Value;
                
                // Типізована конвертація
                parameters.Add(CreateTypedParameter($"@{col.FieldName}", value, col.Type));
                first = false;
            }

            sb.Append(" WHERE Id = @Id");
            parameters.Add(new SqliteParameter("@Id", id));

            using (var cmd = new SqliteCommand(sb.ToString(), conn))
            {
                cmd.Parameters.AddRange(parameters.ToArray());
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Виконує INSERT з оптимізованою конвертацією типів
        /// </summary>
        private void ExecuteInsert(SqliteConnection conn, string tableName, BindableRow rowData, List<ColumnConfig> columns)
        {
            var sb = new StringBuilder();
            sb.Append($"INSERT INTO [{tableName}] (");
            var colNames = new List<string>();
            var paramNames = new List<string>();
            var parameters = new List<SqliteParameter>();

            foreach (var col in columns)
            {
                if (col.Type == ColumnType.SectionHeader) continue;
                
                // Пропускаємо системне поле Id (воно генерується автоматично)
                if (col.FieldName?.Equals("Id", StringComparison.OrdinalIgnoreCase) == true) continue;

                colNames.Add($"[{col.FieldName}]");
                paramNames.Add($"@{col.FieldName}");

                object value = col.FieldName != null && rowData.ContainsKey(col.FieldName) ? rowData[col.FieldName] : DBNull.Value;
                
                // Типізована конвертація
                parameters.Add(CreateTypedParameter($"@{col.FieldName}", value, col.Type));
            }

            sb.Append(string.Join(", ", colNames));
            sb.Append(") VALUES (");
            sb.Append(string.Join(", ", paramNames));
            sb.Append("); SELECT last_insert_rowid();");

            using (var cmd = new SqliteCommand(sb.ToString(), conn))
            {
                cmd.Parameters.AddRange(parameters.ToArray());
                object? newId = cmd.ExecuteScalar();
                rowData["Id"] = newId != null ? Convert.ToInt64(newId) : 0;
            }
        }

        /// <summary>
        /// Створює типізований SqliteParameter без ToString() конвертації
        /// </summary>
        private SqliteParameter CreateTypedParameter(string paramName, object value, ColumnType columnType)
        {
            if (value == null || value == DBNull.Value)
            {
                return new SqliteParameter(paramName, DBNull.Value);
            }

            switch (columnType)
            {
                case ColumnType.Number:
                case ColumnType.RegNumber:
                    return new SqliteParameter(paramName, Convert.ToInt64(value));

                case ColumnType.Currency:
                    return new SqliteParameter(paramName, Convert.ToDecimal(value));

                case ColumnType.Boolean:
                case ColumnType.Lock:
                    bool boolVal = value is bool b ? b : Convert.ToBoolean(value);
                    return new SqliteParameter(paramName, boolVal ? 1 : 0);

                case ColumnType.Date:
                    if (value is DateTime dateVal)
                    {
                        return new SqliteParameter(paramName, dateVal.ToString("yyyy-MM-dd"));
                    }
                    return new SqliteParameter(paramName, value.ToString());

                case ColumnType.DateTime:
                    if (value is DateTime dtVal)
                    {
                        return new SqliteParameter(paramName, dtVal.ToString("yyyy-MM-dd HH:mm:ss"));
                    }
                    return new SqliteParameter(paramName, value.ToString());

                case ColumnType.Time:
                    if (value is TimeSpan tsVal)
                    {
                        return new SqliteParameter(paramName, tsVal.ToString(@"hh\:mm\:ss"));
                    }
                    if (value is DateTime dtTimeVal)
                    {
                        return new SqliteParameter(paramName, dtTimeVal.ToString("HH:mm:ss"));
                    }
                    return new SqliteParameter(paramName, value.ToString());

                default:
                    return new SqliteParameter(paramName, value.ToString());
            }
        }

        // --- МЕТОДИ ДЛЯ ШАБЛОНІВ ---

        /// <summary>
        /// Зберегти або оновити шаблон у БД
        /// </summary>
        public void SaveTemplate(TableTemplate template)
        {
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();

                // Генеруємо JSON локально
                string jsonConfig = JsonSerializer.Serialize(template);

                var meta = new TemplateMetadata
                {
                    Id = template.Id,
                    Name = template.Title,
                    Description = template.Description,
                    UpdatedAt = DateTime.Now
                };

                // Перевіряємо, чи існує шаблон
                using (var checkCmd = new SqliteCommand("SELECT Version FROM App_Templates WHERE Id = @Id", conn))
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

                        using (var cmd = new SqliteCommand(updateSql, conn))
                        {
                            cmd.Parameters.AddWithValue("@Id", meta.Id);
                            cmd.Parameters.AddWithValue("@Name", meta.Name);
                            cmd.Parameters.AddWithValue("@Description", meta.Description);
                            cmd.Parameters.AddWithValue("@JsonConfig", jsonConfig);
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

                        using (var cmd = new SqliteCommand(insertSql, conn))
                        {
                            cmd.Parameters.AddWithValue("@Id", meta.Id);
                            cmd.Parameters.AddWithValue("@Name", meta.Name);
                            cmd.Parameters.AddWithValue("@Description", meta.Description);
                            cmd.Parameters.AddWithValue("@JsonConfig", jsonConfig);
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
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                string sql = "SELECT JsonConfig FROM App_Templates WHERE Id = @Id AND IsActive = 1";

                using (var cmd = new SqliteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", templateId);
                    var json = cmd.ExecuteScalar() as string;

                    if (json != null)
                    {
                        return JsonSerializer.Deserialize<TableTemplate>(json) ?? new TableTemplate();
                    }
                }
            }
            return new TableTemplate();
        }

        /// <summary>
        /// Отримати сирий JSON конфіг шаблону (без десеріалізації)
        /// </summary>
        public string GetTemplateJson(string templateId)
        {
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                string sql = "SELECT JsonConfig FROM App_Templates WHERE Id = @Id";
                using (var cmd = new SqliteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", templateId);
                    return cmd.ExecuteScalar() as string ?? string.Empty;
                }
            }
        }

        /// <summary>
        /// Отримати список усіх активних шаблонів
        /// ОПТИМІЗОВАНО: Не читає важке поле JsonConfig
        /// </summary>
        public List<TemplateMetadata> GetAllTemplates()
        {
            var list = new List<TemplateMetadata>();
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                string sql = "SELECT Id, Name, Description, Version, CreatedAt, UpdatedAt, IsActive FROM App_Templates WHERE IsActive = 1 ORDER BY Name";

                using (var cmd = new SqliteCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new TemplateMetadata
                        {
                            Id = reader["Id"]?.ToString() ?? string.Empty,
                            Name = reader["Name"]?.ToString() ?? string.Empty,
                            Description = reader["Description"]?.ToString() ?? string.Empty,
                            Version = Convert.ToInt32(reader["Version"]),
                            CreatedAt = DateTime.Parse(reader["CreatedAt"]?.ToString() ?? DateTime.MinValue.ToString()),
                            UpdatedAt = DateTime.Parse(reader["UpdatedAt"]?.ToString() ?? DateTime.MinValue.ToString()),
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
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                string sql = "UPDATE App_Templates SET IsActive = 0, UpdatedAt = @UpdatedAt WHERE Id = @Id";

                using (var cmd = new SqliteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", templateId);
                    cmd.Parameters.AddWithValue("@UpdatedAt", DateTime.Now.ToString("s"));
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Оновлює параметри заповнення для журналу
        /// </summary>
        public void UpdateJournalAutoFillConfig(long journalId, string autoFillConfigJson)
        {
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                string sql = "UPDATE App_Journals SET AutoFillConfigJson = @Json WHERE Id = @Id";

                using (var cmd = new SqliteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", journalId);
                    cmd.Parameters.AddWithValue("@Json", autoFillConfigJson ?? "{}");
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
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        // 1. Отримуємо назву таблиці журналу
                        string tableName = string.Empty;
                        using (var cmd = new SqliteCommand("SELECT TableName FROM App_Journals WHERE Id = @Id", conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Id", journalId);
                            var result = cmd.ExecuteScalar();
                            if (result != null)
                            {
                                tableName = result.ToString() ?? string.Empty;
                            }
                        }

                        if (string.IsNullOrEmpty(tableName))
                        {
                            throw new InvalidOperationException($"Журнал з ID {journalId} не знайдено");
                        }

                        // 2. Видаляємо запис з реєстру журналів
                        // (Робимо це спочатку, щоб уникнути ситуації, коли таблиця видалена, а запис залишився)
                        using (var cmd = new SqliteCommand("DELETE FROM App_Journals WHERE Id = @Id", conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Id", journalId);
                            int rowsAffected = cmd.ExecuteNonQuery();
                            
                            if (rowsAffected == 0)
                            {
                                throw new InvalidOperationException($"Не вдалося видалити запис журналу з ID {journalId}");
                            }
                        }

                        // 3. Видаляємо фізичну таблицю з даними
                        using (var cmd = new SqliteCommand($"DROP TABLE IF EXISTS [{tableName}]", conn, transaction))
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

        // --- КЕРУВАННЯ КОРИСТУВАЧАМИ ---

        /// <summary>
        /// Отримує список усіх користувачів разом з їхніми правами доступу до журналів.
        /// </summary>
        public List<AppUser> GetAllUsers()
        {
            var users = new Dictionary<int, AppUser>();

            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();

                // 1. Завантажуємо основні дані користувачів
                string sqlUsers = "SELECT Id, Login, FullName, Role FROM App_Users ORDER BY FullName";
                using (var cmd = new SqliteCommand(sqlUsers, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var user = new AppUser
                        {
                            Id = (int)reader["Id"],
                            Login = reader["Login"]?.ToString() ?? string.Empty,
                            FullName = reader["FullName"]?.ToString() ?? string.Empty,
                            Role = (UserRole)Convert.ToInt32(reader["Role"]),
                            AllowedJournalIds = new List<long>()
                        };
                        users.Add(user.Id, user);
                    }
                }

                // 2. Завантажуємо права доступу до журналів (щоб уникнути проблеми N+1 запитів)
                string sqlAccess = "SELECT UserId, JournalId FROM App_UserJournalAccess";
                using (var cmd = new SqliteCommand(sqlAccess, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int userId = (int)reader["UserId"];
                        int journalId = (int)reader["JournalId"];

                        if (users.ContainsKey(userId))
                        {
                            users[userId].AllowedJournalIds.Add(journalId);
                        }
                    }
                }
            }

            return users.Values.ToList();
        }

        /// <summary>
        /// Створює нового користувача та призначає йому права доступу до журналів.
        /// </summary>
        public void CreateUser(AppUser user, string passwordHash)
        {
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();

                // Перевірка на унікальність логіна
                using (var checkCmd = new SqliteCommand("SELECT COUNT(1) FROM App_Users WHERE Login = @Login", conn))
                {
                    checkCmd.Parameters.AddWithValue("@Login", user.Login);
                    if (Convert.ToInt64(checkCmd.ExecuteScalar()) > 0)
                    {
                        throw new InvalidOperationException($"Користувач з логіном '{user.Login}' вже існує.");
                    }
                }

                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        // 1. Додаємо користувача
                        string insertUserSql = @"
                            INSERT INTO App_Users (Login, PasswordHash, FullName, Role) 
                            VALUES (@Login, @PasswordHash, @FullName, @Role);
                            SELECT last_insert_rowid();";

                        using (var cmd = new SqliteCommand(insertUserSql, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Login", user.Login);
                            cmd.Parameters.AddWithValue("@PasswordHash", passwordHash);
                            cmd.Parameters.AddWithValue("@FullName", user.FullName);
                            cmd.Parameters.AddWithValue("@Role", (int)user.Role);

                            user.Id = Convert.ToInt32(cmd.ExecuteScalar());
                        }

                        // 2. Додаємо доступи до журналів
                        if (user.AllowedJournalIds != null && user.AllowedJournalIds.Any())
                        {
                            string insertAccessSql = "INSERT INTO App_UserJournalAccess (UserId, JournalId) VALUES (@UserId, @JournalId)";
                            using (var cmd = new SqliteCommand(insertAccessSql, conn, transaction))
                            {
                                // Оптимізація: використовуємо один об'єкт команди і міняємо лише параметри
                                var paramUserId = cmd.Parameters.Add("@UserId", SqliteType.Integer);
                                var paramJournalId = cmd.Parameters.Add("@JournalId", SqliteType.Integer);

                                foreach (long journalId in user.AllowedJournalIds)
                                {
                                    paramUserId.Value = user.Id;
                                    paramJournalId.Value = journalId;
                                    cmd.ExecuteNonQuery();
                                }
                            }
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

        /// <summary>
        /// Оновлює дані існуючого користувача. 
        /// Якщо передано newPasswordHash (не null), пароль також буде оновлено.
        /// </summary>
        public void UpdateUser(AppUser user, string? newPasswordHash = null)
        {
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();

                // Перевірка на унікальність логіна (щоб не зайняти логін іншого користувача)
                using (var checkCmd = new SqliteCommand("SELECT COUNT(1) FROM App_Users WHERE Login = @Login AND Id != @Id", conn))
                {
                    checkCmd.Parameters.AddWithValue("@Login", user.Login);
                    checkCmd.Parameters.AddWithValue("@Id", user.Id);
                    if (Convert.ToInt64(checkCmd.ExecuteScalar()) > 0)
                    {
                        throw new InvalidOperationException($"Користувач з логіном '{user.Login}' вже існує.");
                    }
                }

                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        // 1. Оновлюємо дані користувача
                        string updateUserSql = newPasswordHash != null
                            ? "UPDATE App_Users SET Login = @Login, FullName = @FullName, Role = @Role, PasswordHash = @PasswordHash WHERE Id = @Id"
                            : "UPDATE App_Users SET Login = @Login, FullName = @FullName, Role = @Role WHERE Id = @Id";

                        using (var cmd = new SqliteCommand(updateUserSql, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Id", user.Id);
                            cmd.Parameters.AddWithValue("@Login", user.Login);
                            cmd.Parameters.AddWithValue("@FullName", user.FullName);
                            cmd.Parameters.AddWithValue("@Role", (int)user.Role);

                            if (newPasswordHash != null)
                            {
                                cmd.Parameters.AddWithValue("@PasswordHash", newPasswordHash);
                            }

                            cmd.ExecuteNonQuery();
                        }

                        // 2. Оновлюємо доступи до журналів
                        // Найбезпечніший спосіб: видалити старі доступи і записати нові
                        using (var cmd = new SqliteCommand("DELETE FROM App_UserJournalAccess WHERE UserId = @UserId", conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@UserId", user.Id);
                            cmd.ExecuteNonQuery();
                        }

                        if (user.AllowedJournalIds != null && user.AllowedJournalIds.Any())
                        {
                            string insertAccessSql = "INSERT INTO App_UserJournalAccess (UserId, JournalId) VALUES (@UserId, @JournalId)";
                            using (var cmd = new SqliteCommand(insertAccessSql, conn, transaction))
                            {
                                var paramUserId = cmd.Parameters.Add("@UserId", SqliteType.Integer);
                                var paramJournalId = cmd.Parameters.Add("@JournalId", SqliteType.Integer);

                                foreach (long journalId in user.AllowedJournalIds)
                                {
                                    paramUserId.Value = user.Id;
                                    paramJournalId.Value = journalId;
                                    cmd.ExecuteNonQuery();
                                }
                            }
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

        /// <summary>
        /// Видаляє користувача та всі пов'язані з ним права доступу.
        /// </summary>
        public void DeleteUser(long userId)
        {
            // Захист від видалення єдиного/головного адміністратора (опціонально, але рекомендовано)
            if (userId == 1)
            {
                throw new InvalidOperationException("Неможливо видалити головного адміністратора системи (Id = 1).");
            }

            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        // 1. Спочатку видаляємо права доступу (хоча ON DELETE CASCADE є в схемі, 
                        // в SQLite зовнішні ключі за замовчуванням вимкнені для кожного з'єднання, 
                        // тому безпечніше видалити вручну або увімкнути PRAGMA foreign_keys = ON).
                        using (var cmd = new SqliteCommand("DELETE FROM App_UserJournalAccess WHERE UserId = @Id", conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Id", userId);
                            cmd.ExecuteNonQuery();
                        }

                        // 2. Видаляємо самого користувача
                        using (var cmd = new SqliteCommand("DELETE FROM App_Users WHERE Id = @Id", conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Id", userId);
                            int rowsAffected = cmd.ExecuteNonQuery();

                            if (rowsAffected == 0)
                            {
                                throw new InvalidOperationException($"Користувача з ID {userId} не знайдено.");
                            }
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

        /// <summary>
        /// Перевіряє, чи підходить вказаний DEK (у форматі Base64) для розшифрування існуючої бази даних.
        /// Цей метод використовується при аварійному відновленні (Recovery).
        /// </summary>
        /// <param name="base64Dek">Спробуваний майстер-ключ відновлення.</param>
        /// <returns>True, якщо база успішно відкрилась, інакше False.</returns>
        public static bool VerifyRecoveryKey(string base64Dek)
        {
            string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_data.db");
            
            // Якщо бази не існує, ключу нічого відкривати.
            // Можна вважати ключ "умовно правильним", якщо бази ще немає, 
            // але в сценарії відновлення ми відновлюємо саме ДОСТУП ДО ДАНИХ.
            if (!File.Exists(dbPath))
            {
                return false; 
            }

            string testConnectionString = $"Data Source={dbPath};Password={base64Dek};";

            try
            {
                using (var conn = new SqliteConnection(testConnectionString))
                {
                    conn.Open();
                    
                    // Щоб гарантовано перевірити, чи ключ дійсно правильний для SQLCipher,
                    // потрібно виконати хоча б один запит на читання будь-якої таблиці.
                    // Користуємося системною таблицею sqlite_master.
                    using (var cmd = new SqliteCommand("SELECT count(*) FROM sqlite_master", conn))
                    {
                        cmd.ExecuteScalar();
                    }
                }
                
                return true; // Спроба читання успішна
            }
            catch (SqliteException ex)
            {
                // Помилка шифрування зазвичай видає "file is not a database"
                System.Diagnostics.Debug.WriteLine($"Recovery Check Failed: {ex.Message}");
                return false;
            }
            catch (Exception)
            {
                return false;
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