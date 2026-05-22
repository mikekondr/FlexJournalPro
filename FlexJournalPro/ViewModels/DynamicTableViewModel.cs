using FlexJournalPro.Models;
using FlexJournalPro.Services;
using FlexJournalPro.Views;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Data;

namespace FlexJournalPro.ViewModels
{
    /// <summary>
    /// ViewModel для DynamicTableView - управляє бізнес-логікою та станом таблиці
    /// </summary>
    public class DynamicTableViewModel : INotifyPropertyChanged
    {
        #region Fields

        private readonly Dictionary<string, object> _autoFillValues = new Dictionary<string, object>();
        private TableTemplate _currentTemplate;
        private AsyncVirtualizingCollection _virtualData;
        private DataTable _calculationEngine;
        private IDatabaseService _dbService; // Store reference
        private string _tableName;          // Store reference
        private long _initialStartNumber = 1; // Default

        // Статичні кеші
        private static readonly Dictionary<string, TableTemplate> _jsonTemplateCache = new Dictionary<string, TableTemplate>();

        #endregion

        #region Properties

        /// <summary>
        /// Поточний шаблон таблиці
        /// </summary>
        public TableTemplate CurrentTemplate
        {
            get => _currentTemplate;
            private set
            {
                if (_currentTemplate != value)
                {
                    _currentTemplate = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Віртуальна колекція даних
        /// </summary>
        public AsyncVirtualizingCollection VirtualData
        {
            get => _virtualData;
            private set
            {
                if (_virtualData != value)
                {
                    // Відписуємось від попередньої колекції
                    if (_virtualData != null)
                    {
                        _virtualData.CollectionChanged -= VirtualData_CollectionChanged;
                    }

                    _virtualData = value;

                    // Підписуємось на нову колекцію
                    if (_virtualData != null)
                    {
                        _virtualData.CollectionChanged += VirtualData_CollectionChanged;
                    }

                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Сеансові значення для обчислень
        /// </summary>
        public IReadOnlyDictionary<string, object> AutoFillValues => _autoFillValues;

        #endregion

        #region Events

        /// <summary>
        /// Виникає при збереженні рядка даних
        /// </summary>
        public event EventHandler<RowSavedEventArgs> RowSaved;

        /// <summary>
        /// Виникає при помилці валідації (наприклад, при спробі заблокувати некоректний рядок)
        /// </summary>
        public event EventHandler<string> ValidationFailed;

        /// <summary>
        /// Виникає при зміні властивості
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region Public Methods

        /// <summary>
        /// Завантажує шаблон таблиці
        /// </summary>
        public void LoadTemplate(TableTemplate template)
        {
            if (template == null) return;

            CurrentTemplate = template;

            // Apply strict numbering read-only logic
            if (CurrentTemplate.RegistrationParams != null && CurrentTemplate.RegistrationParams.UseStrictNumbering)
            {
                foreach (var col in CurrentTemplate.Columns)
                {
                    if (col.Type == ColumnType.RegNumber)
                    {
                        col.IsReadOnly = true;
                    }
                }
            }

            InitializeCalculationEngine(CurrentTemplate.Columns);
        }

        /// <summary>
        /// Завантажує шаблон з бази даних (з кешуванням)
        /// </summary>
        public void LoadTemplateFromDatabase(IDatabaseService dbService, string templateId)
        {
            if (!_jsonTemplateCache.ContainsKey(templateId))
            {
                var template = dbService.GetTemplate(templateId);
                if (template == null)
                {
                    throw new InvalidOperationException($"Шаблон '{templateId}' не знайдено в базі даних");
                }
                _jsonTemplateCache[templateId] = template;
            }

            LoadTemplate(_jsonTemplateCache[templateId]);
        }

        /// <summary>
        /// Очищує кеш шаблонів
        /// </summary>
        public static void ClearTemplateCache(string templateId = null)
        {
            if (templateId != null)
            {
                _jsonTemplateCache.Remove(templateId);
            }
            else
            {
                _jsonTemplateCache.Clear();
            }
        }

        /// <summary>
        /// Встановлює віртуальне джерело даних
        /// </summary>
        public void SetVirtualDataSource(IDatabaseService dbService, string tableName, long startNumber = 1, bool isReadOnly = false)
        {
            if (CurrentTemplate == null)
            {
                throw new InvalidOperationException("Спочатку завантажте шаблон");
            }

            _dbService = dbService;
            _tableName = tableName;
            _initialStartNumber = startNumber;

            var provider = new JournalDataProvider(dbService, tableName, CurrentTemplate.Columns);
            VirtualData = new AsyncVirtualizingCollection(provider, isReadOnly);

            // Ініціалізуємо рядок-заглушку
            var placeholder = VirtualData.GetPlaceholder();
            InitializeNewRow(placeholder);
            SubscribeToRowEvents(placeholder);
        }

        /// <summary>
        /// Застосовує значення параметрів заповнення
        /// </summary>
        public void ApplyAutoFillValues(Dictionary<string, object> values)
        {
            if (values == null) return;

            _autoFillValues.Clear();
            foreach (var kvp in values)
            {
                _autoFillValues[kvp.Key] = kvp.Value;
            }
            OnPropertyChanged(nameof(AutoFillValues));
        }

        /// <summary>
        /// Отримує поточні значення параметрів заповнення
        /// </summary>
        public Dictionary<string, object> GetAutoFillValues()
        {
            return new Dictionary<string, object>(_autoFillValues);
        }

        /// <summary>
        /// Створює та ініціалізує новий рядок
        /// </summary>
        public BindableRow CreateNewRow()
        {
            if (CurrentTemplate == null)
            {
                throw new InvalidOperationException("Шаблон не завантажено");
            }

            var newRow = new BindableRow();
            InitializeNewRow(newRow);
            return newRow;
        }

        /// <summary>
        /// Додає новий рядок до віртуальної колекції
        /// </summary>
        public void AddNewRow(BindableRow newRow)
        {
            if (VirtualData == null)
            {
                throw new InvalidOperationException("Віртуальні дані не встановлені");
            }

            VirtualData.Add(newRow);

            // Підписуємося на PropertyChanged
            newRow.PropertyChanged -= BindableRow_PropertyChanged;
            newRow.PropertyChanged += BindableRow_PropertyChanged;
        }

        /// <summary>
        /// Валідує рядок даних
        /// </summary>
        public List<string> ValidateRow(BindableRow rowData)
        {
            var errors = new List<string>();

            if (CurrentTemplate == null || rowData == null)
                return errors;

            // Пропускаємо валідацію для рядка-заглушки, якщо він порожній
            if (rowData is NewRowPlaceholder && IsRowEmpty(rowData))
            {
                return errors; // Порожній placeholder - це нормально
            }

            foreach (var colConfig in CurrentTemplate.Columns)
            {
                if (colConfig.Type == ColumnType.SectionHeader || !colConfig.IsRequired)
                    continue;

                if (colConfig.FieldName?.Equals("Id", StringComparison.OrdinalIgnoreCase) == true)
                    continue;

                if (IsFieldEmpty(rowData, colConfig))
                {
                    errors.Add($"Поле '{colConfig.HeaderText}' є обов'язковим!");
                }
            }

            return errors;
        }

        /// <summary>
        /// Перевіряє, чи рядок повністю порожній
        /// </summary>
        public bool IsRowEmpty(BindableRow rowData)
        {
            if (CurrentTemplate == null) return true;

            foreach (var col in CurrentTemplate.Columns)
            {
                if (col.Type == ColumnType.SectionHeader) continue;
                if (col.FieldName?.Equals("Id", StringComparison.OrdinalIgnoreCase) == true) continue;
                if (string.IsNullOrEmpty(col.FieldName)) continue;

                if (!IsFieldEmpty(rowData, col))
                {
                    return false; // Знайдено заповнене поле
                }
            }

            return true; // Всі поля порожні
        }

        /// <summary>
        /// Виконує обчислення для рядка
        /// </summary>
        public void PerformCalculations(BindableRow rowData)
        {
            if (_calculationEngine == null || rowData == null) return;

            try
            {
                _calculationEngine.Rows.Clear();
                DataRow row = _calculationEngine.NewRow();

                // 1. Заповнюємо вхідні дані
                foreach (var col in CurrentTemplate.Columns)
                {
                    if (col.Type == ColumnType.SectionHeader) continue;
                    if (col.FieldName?.Equals("Id", StringComparison.OrdinalIgnoreCase) == true) continue;
                    if (!string.IsNullOrEmpty(col.Expression)) continue;

                    if (rowData.ContainsKey(col.FieldName) && rowData[col.FieldName] != null)
                    {
                        try
                        {
                            row[col.FieldName] = Convert.ChangeType(rowData[col.FieldName],
                                _calculationEngine.Columns[col.FieldName].DataType);
                        }
                        catch
                        {
                            // Якщо конвертація не вдалась, лишаємо DBNull
                        }
                    }
                }

                // 2. Додаємо рядок - спрацюють Expressions
                _calculationEngine.Rows.Add(row);

                // 3. Забираємо результати
                foreach (DataColumn dc in _calculationEngine.Columns)
                {
                    if (row[dc] != DBNull.Value)
                    {
                        rowData[dc.ColumnName] = row[dc];
                    }
                    else
                    {
                        rowData[dc.ColumnName] = null;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Calculation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Виконує правила OnChange
        /// </summary>
        public void ExecuteChangeRules(string sourceFieldName, BindableRow rowData)
        {
            if (CurrentTemplate == null) return;

            var sourceColConfig = CurrentTemplate.Columns.FirstOrDefault(c => c.FieldName == sourceFieldName);

            if (sourceColConfig == null || sourceColConfig.OnChange == null || sourceColConfig.OnChange.Count == 0)
                return;

            using (var calcEngine = new DataTable())
            {
                foreach (var rule in sourceColConfig.OnChange)
                {
                    try
                    {
                        string expression = SubstituteValuesInExpression(rule.Expression, rowData);
                        object result = calcEngine.Compute(expression, string.Empty);

                        var targetCol = CurrentTemplate.Columns.FirstOrDefault(c => c.FieldName == rule.TargetColumn);
                        if (targetCol != null)
                        {
                            object typedResult = ConvertToTargetType(result, targetCol.Type);
                            rowData[rule.TargetColumn] = typedResult;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error calculating {rule.TargetColumn}: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Обробляє збереження рядка
        /// </summary>
        public void SaveRow(BindableRow rowData)
        {
            if (rowData == null) return;

            // Перевірка на блокування
            if (IsRowLocked(rowData))
            {
                // Якщо рядок заблокований, дозволяємо тільки зміну статусу блокування (розблокування, якщо дозволено, або логування)
                // Але якщо UseStrictNumbering and UseLocking -> заблокований рядок редагувати не можна.
                // Перевіримо, чи це спроба змінити інші поля. 
                // Для спрощення: якщо IsLocked=true, ми просто зберігаємо (раптом це сама операція блокування).
                // Валідація UI повинна заборонити редагування полів.
                // Тут ми можемо зробити додаткову перевірку, якщо потрібно.
            }

            // Якщо це рядок-заглушка і він порожній - не зберігаємо
            if (rowData is NewRowPlaceholder && IsRowEmpty(rowData))
            {
                return; // Просто виходимо, не зберігаючи порожній рядок
            }

            var rowToSave = rowData;

            // Якщо це рядок-заглушка з даними - конвертуємо його в звичайний рядок
            if (rowData is NewRowPlaceholder placeholder)
            {
                var converted = VirtualData.ConvertPlaceholderToNewRow(placeholder);

                if (converted != null)
                {
                    rowToSave = converted;
                }

                // Після конвертації отримуємо новий placeholder і ініціалізуємо його
                var newPlaceholder = VirtualData.GetPlaceholder();
                InitializeNewRow(newPlaceholder);
                SubscribeToRowEvents(newPlaceholder);
            }

            bool isNewRow = !rowToSave.ContainsKey("Id") ||
                            rowToSave["Id"] == null ||
                            Convert.ToInt64(rowToSave["Id"]) <= 0;

            // Для суворої нумерації актуалізуємо номер перед остаточним збереженням (тільки для нових рядків)
            if (isNewRow && CurrentTemplate.RegistrationParams != null && CurrentTemplate.RegistrationParams.UseStrictNumbering)
            {
                rowToSave["RegNumber"] = GetNextRegistrationNumber();
            }

            PerformCalculations(rowToSave);

            // Скидаємо прапорець "змінено" після збереження
            rowToSave.MarkAsSaved();

            OnRowSaved(new RowSavedEventArgs { RowData = rowToSave });

            // Оновлюємо віртуальну колекцію після збереження
            if (VirtualData != null)
            {
                if (isNewRow)
                {
                    VirtualData.RefreshAfterSave();

                    // Ініціалізуємо новий рядок-заглушку після оновлення
                    var newPlaceholder = VirtualData.GetPlaceholder();
                    InitializeNewRow(newPlaceholder);
                    SubscribeToRowEvents(newPlaceholder);
                }
            }
        }

        #endregion

        #region Private Methods

        private void SubscribeToRowEvents(BindableRow row)
        {
            if (row == null) return;
            row.PropertyChanged -= BindableRow_PropertyChanged;
            row.PropertyChanged += BindableRow_PropertyChanged;
        }

        private void InitializeCalculationEngine(List<ColumnConfig> columns)
        {
            _calculationEngine = new DataTable();

            if (columns == null) return;

            foreach (var col in columns)
            {
                if (col == null || col.Type == ColumnType.SectionHeader) continue;

                if (string.IsNullOrEmpty(col.FieldName))
                {
                    System.Diagnostics.Debug.WriteLine($"Warning: Column with HeaderText '{col.HeaderText}' has null or empty FieldName");
                    continue;
                }

                if (col.FieldName.Equals("Id", StringComparison.OrdinalIgnoreCase))
                {
                    System.Diagnostics.Debug.WriteLine($"Warning: Template contains system field 'Id' configuration - ignoring");
                    continue;
                }

                Type type = typeof(string);
                switch (col.Type)
                {
                    case ColumnType.Number: type = typeof(int); break;
                    case ColumnType.Currency: type = typeof(decimal); break;
                    case ColumnType.Boolean: type = typeof(bool); break;
                    case ColumnType.Date:
                    case ColumnType.DateTime:
                        type = typeof(DateTime); break;
                    case ColumnType.Time:
                        type = typeof(TimeSpan); break;
                }

                DataColumn dc = new DataColumn(col.FieldName, type);

                if (!string.IsNullOrEmpty(col.Expression))
                {
                    dc.Expression = col.Expression;
                }

                _calculationEngine.Columns.Add(dc);
            }
        }

        private void VirtualData_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems)
                {
                    if (item is BindableRow row)
                    {
                        row.PropertyChanged -= BindableRow_PropertyChanged;
                        row.PropertyChanged += BindableRow_PropertyChanged;
                    }
                }
            }
        }

        private void BindableRow_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is not BindableRow rowData) return;
            if (CurrentTemplate == null) return;

            if (e.PropertyName == Binding.IndexerName) return;
            if (sender is PlaceholderRow) return; // Пропускаємо PlaceholderRow для завантаження

            // Allow NewRowPlaceholder to trigger logic
            // if (sender is NewRowPlaceholder) ... it falls through here, which is what we want now

            try
            {
                if (!string.IsNullOrEmpty(e.PropertyName))
                {
                    ExecuteChangeRules(e.PropertyName, rowData);
                }

                PerformCalculations(rowData);

                // Автоматичне збереження при зміні статусу блокування
                if (!string.IsNullOrEmpty(e.PropertyName))
                {
                    var col = CurrentTemplate.Columns.FirstOrDefault(c => c.FieldName == e.PropertyName);
                    if (col != null && col.Type == ColumnType.Lock)
                    {
                        // Перевіряємо, чи намагається користувач заблокувати рядок
                        bool isLocking = false;
                        var val = rowData[e.PropertyName];
                        if (val is bool b && b) isLocking = true;
                        else if (val is long l && l == 1) isLocking = true;
                        else if (val?.ToString().ToLower() == "true") isLocking = true;

                        if (isLocking)
                        {
                            // Валідуємо рядок перед блокуванням
                            var errors = ValidateRow(rowData);
                            if (errors.Any())
                            {
                                // Скасовуємо блокування
                                rowData[e.PropertyName] = false;
                                ValidationFailed?.Invoke(this, "Неможливо заблокувати рядок з помилками:\n" + string.Join("\n", errors));
                                return;
                            }
                        }

                        // Якщо це новий рядок (заглушка) і він ще не ініціалізований - ініціалізуємо значеннями за замовчуванням
                        if (rowData is NewRowPlaceholder placeholder && !placeholder.IsInitialized)
                        {
                            InitializeRowDefaults(placeholder);
                        }

                        SaveRow(rowData);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in recalculation: {ex.Message}");
            }
        }

        private void InitializeNewRow(BindableRow newRow)
        {
            if (CurrentTemplate == null || newRow == null) return;

            // If it is a placeholder, do not initialize values yet, keep it empty
            if (newRow is NewRowPlaceholder) return;

            InitializeRowDefaults(newRow);
        }

        public void InitializeRowDefaults(BindableRow row)
        {
            if (CurrentTemplate == null || row == null) return;

            // 1. Спочатку заповнюємо стандартні поля
            foreach (var col in CurrentTemplate.Columns)
            {
                if (col == null || col.Type == ColumnType.SectionHeader) continue;
                if (string.IsNullOrEmpty(col.FieldName)) continue;
                if (col.FieldName.Equals("Id", StringComparison.OrdinalIgnoreCase)) continue;

                // Skip if already has value (e.g. partial edit)
                if (row.ContainsKey(col.FieldName) && row[col.FieldName] != null) continue;

                if (col.DefaultValue != null)
                {
                    row[col.FieldName] = col.DefaultValue;
                }
                else if (!string.IsNullOrEmpty(col.BindAutoFillParam) && _autoFillValues.ContainsKey(col.BindAutoFillParam))
                {
                    row[col.FieldName] = _autoFillValues[col.BindAutoFillParam];
                }
                else
                {
                    row[col.FieldName] = GetDefaultValueForType(col.Type);
                }
            }

            // 2. Спеціальна логіка для RegistrationParams
            if (CurrentTemplate.RegistrationParams != null && CurrentTemplate.RegistrationParams.UseRegistration)
            {
                // Префікс
                if (CurrentTemplate.RegistrationParams.UseNumberPrefix && _autoFillValues.ContainsKey("RegPrefix"))
                {
                    if (!row.ContainsKey("RegPrefix") || row["RegPrefix"] == null)
                        row["RegPrefix"] = _autoFillValues["RegPrefix"];
                }

                // Суфікс
                if (CurrentTemplate.RegistrationParams.UseNumberSuffix && _autoFillValues.ContainsKey("RegSuffix"))
                {
                    if (!row.ContainsKey("RegSuffix") || row["RegSuffix"] == null)
                        row["RegSuffix"] = _autoFillValues["RegSuffix"];
                }

                // Номер
                // Якщо сувора нумерація - обчислюємо наступний номер
                // Або якщо це поле RegNumber ще не заповнене
                if (CurrentTemplate.RegistrationParams.UseStrictNumbering || !row.ContainsKey("RegNumber") || row["RegNumber"] == null)
                {
                    long nextNumber = GetNextRegistrationNumber();
                    row["RegNumber"] = nextNumber;
                }
            }

            PerformCalculations(row);

            // Mark as initialized so UI updates
            row.IsInitialized = true;

            // Скидаємо статус "IsDirty" після ініціалізації (оскільки це дефолтні значення)
            row.MarkAsSaved();
        }

        private long GetNextRegistrationNumber()
        {
            if (_dbService != null && !string.IsNullOrEmpty(_tableName))
            {
                // Отримуємо наступний номер з БД, враховуючи початковий номер
                return _dbService.GetNextRegistrationNumber(_tableName, _initialStartNumber);
            }

            // Якщо немає доступу до БД (наприклад, ще не створено), повертаємо StartNumber
            return _initialStartNumber;
        }

        public bool IsRowLocked(BindableRow row)
        {
            if (CurrentTemplate?.RegistrationParams?.UseLocking != true) return false;

            // Find lock column
            var lockCol = CurrentTemplate.Columns.FirstOrDefault(c => c.Type == ColumnType.Lock);
            string fieldName = lockCol?.FieldName ?? "IsLocked";

            if (row.ContainsKey(fieldName))
            {
                var val = row[fieldName];
                if (val is bool b) return b;
                if (val is long l) return l == 1;
                if (val is int i) return i == 1;
                if (val?.ToString().ToLower() == "true") return true;
                if (val?.ToString() == "1") return true;
            }
            return false;
        }

        private object GetDefaultValueForType(ColumnType type)
        {
            return type switch
            {
                ColumnType.Number => null,
                ColumnType.Currency => null,
                ColumnType.Boolean => false,
                ColumnType.Lock => false, // Default unlocked
                ColumnType.Date => null,
                ColumnType.DateTime => null,
                ColumnType.Time => null,
                _ => null
            };
        }

        private bool IsFieldEmpty(BindableRow rowData, ColumnConfig colConfig)
        {
            object value = rowData[colConfig.FieldName];

            if (value == null || value == DBNull.Value)
                return true;

            if (colConfig.Type is ColumnType.Text or ColumnType.Dropdown or ColumnType.DropdownEditable)
            {
                return string.IsNullOrWhiteSpace(value.ToString());
            }

            return false;
        }

        private string SubstituteValuesInExpression(string expression, BindableRow rowData)
        {
            foreach (var col in CurrentTemplate.Columns)
            {
                if (col.Type == ColumnType.SectionHeader) continue;

                string pattern = $@"\b{col.FieldName}\b";

                if (Regex.IsMatch(expression, pattern, RegexOptions.IgnoreCase))
                {
                    object val = rowData.ContainsKey(col.FieldName) && rowData[col.FieldName] != null
                        ? rowData[col.FieldName]
                        : 0;

                    string valStr = FormatValueForExpression(val);
                    expression = Regex.Replace(expression, pattern, valStr, RegexOptions.IgnoreCase);
                }
            }

            return expression;
        }

        private string FormatValueForExpression(object val)
        {
            if (IsNumeric(val))
                return string.Format(CultureInfo.InvariantCulture, "{0}", val);

            if (val is bool bVal)
                return bVal ? "1" : "0";

            return $"'{val}'";
        }

        private bool IsNumeric(object expression)
        {
            if (expression == null) return false;
            return double.TryParse(Convert.ToString(expression, CultureInfo.InvariantCulture),
                                   NumberStyles.Any,
                                   NumberFormatInfo.InvariantInfo, out _);
        }

        private object ConvertToTargetType(object val, ColumnType type)
        {
            if (val == null || val == DBNull.Value) return null;
            try
            {
                switch (type)
                {
                    case ColumnType.Number: return Convert.ToInt32(val);
                    case ColumnType.Currency: return Convert.ToDecimal(val);
                    case ColumnType.Boolean: return Convert.ToBoolean(val);
                    default: return val.ToString();
                }
            }
            catch { return val; }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual void OnRowSaved(RowSavedEventArgs e)
        {
            RowSaved?.Invoke(this, e);
        }

        #endregion
    }
}
