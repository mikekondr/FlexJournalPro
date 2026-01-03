using FlexJournalPro.Helpers;
using FlexJournalPro.Models;
using FlexJournalPro.Services;
using MaterialDesignThemes.Wpf;
using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;

namespace FlexJournalPro.Controls
{
    /// <summary>
    /// Аргументи події збереження рядка даних.
    /// </summary>
    public class RowSavedEventArgs : EventArgs
    {
        /// <summary>
        /// Отримує або встановлює дані збереженого рядка.
        /// </summary>
        public BindableRow RowData { get; set; }
    }

    /// <summary>
    /// Користувацький контрол для динамічної таблиці з підтримкою віртуалізації та складного лейауту.
    /// </summary>
    public partial class DynamicTableView : UserControl
    {
        /// <summary>
        /// Виникає при збереженні рядка даних.
        /// </summary>
        public event EventHandler<RowSavedEventArgs> RowSaved;

        #region Private Fields

        private TableTemplate _currentTemplate;
        private readonly Dictionary<string, object> _sessionValues = new Dictionary<string, object>();

        private AsyncVirtualizingCollection _virtualData;

        private DataTable _calculationEngine;

        // Статичний кеш для DataTemplate (живе весь час роботи програми)
        private static readonly Dictionary<string, DataTemplate> _templateCache = new Dictionary<string, DataTemplate>();

        // Статичний кеш для JSON шаблонів
        private static readonly Dictionary<string, TableTemplate> _jsonTemplateCache = new Dictionary<string, TableTemplate>();

        // остання точка кліку миші (для виявлення елементу керування у складених клітинках)
        private Point? lastClickPosition;

        #endregion

        #region Constructor & Public API

        /// <summary>
        /// Ініціалізує новий екземпляр класу DynamicTableView.
        /// </summary>
        public DynamicTableView()
        {
            InitializeComponent();

            DynamicGrid.PreparingCellForEdit += DynamicGrid_PreparingCellForEdit;
            DynamicGrid.PreviewMouseLeftButtonDown += DynamicGrid_PreviewMouseLeftButtonDown;
            DynamicGrid.PreviewKeyDown += DynamicGrid_PreviewKeyDown;
            DynamicGrid.PreviewTextInput += DynamicGrid_PreviewTextInput;
            DynamicGrid.RowEditEnding += DynamicGrid_RowEditEnding;

            // ДОДАНО: Обробка Shift+Scroll для горизонтального прокручування
            DynamicGrid.PreviewMouseWheel += DynamicGrid_PreviewMouseWheel;

            // ДОДАНО: Підписка на завантаження для хука повідомлень тачпаду
            this.Loaded += DynamicTableView_Loaded;
            this.Unloaded += DynamicTableView_Unloaded;

            EventManager.RegisterClassHandler(typeof(TextBox),
                UIElement.GotFocusEvent,
                new RoutedEventHandler(TextBox_GotFocus));
        }

        /// <summary>
        /// Завантажує шаблон таблиці, будує інтерфейс та структуру даних.
        /// </summary>
        /// <param name="template">Шаблон таблиці для завантаження.</param>
        public void LoadTemplate(TableTemplate template)
        {
            if (template == null) return;

            _currentTemplate = template;

            InitializeCalculationEngine(_currentTemplate.Columns);

            // Очищення
            DynamicGrid.ItemsSource = null;
            DynamicGrid.Columns.Clear();

            // Генерація UI (тільки DataGrid, без констант)
            BuildGridStructure(_currentTemplate.Columns);
        }

        /// <summary>
        /// Завантажує шаблон з бази даних (з кешуванням JSON).
        /// </summary>
        /// <param name="dbService">Сервіс бази даних.</param>
        /// <param name="templateId">Ідентифікатор шаблону.</param>
        /// <exception cref="InvalidOperationException">Викидається, якщо шаблон не знайдено в базі даних.</exception>
        public void LoadTemplateFromDatabase(DatabaseService dbService, string templateId)
        {
            // Перевіряємо JSON кеш
            if (!_jsonTemplateCache.ContainsKey(templateId))
            {
                var template = dbService.GetTemplate(templateId);
                if (template == null)
                {
                    throw new InvalidOperationException($"Шаблон '{templateId}' не знайдено в базі даних");
                }
                _jsonTemplateCache[templateId] = template;
            }

            // Завантажуємо з кешу
            LoadTemplate(_jsonTemplateCache[templateId]);
        }

        /// <summary>
        /// Очищує кеш шаблонів (використовувати при оновленні шаблону).
        /// </summary>
        /// <param name="templateId">Ідентифікатор шаблону для очищення. Якщо null, очищає весь кеш.</param>
        public static void ClearTemplateCache(string templateId = null)
        {
            if (templateId != null)
            {
                // Очищаємо кеш для конкретного шаблону
                _jsonTemplateCache.Remove(templateId);

                // Очищаємо XAML кеш для цього шаблону
                var keysToRemove = _templateCache.Keys.Where(k => k.StartsWith(templateId + "_")).ToList();
                foreach (var key in keysToRemove)
                {
                    _templateCache.Remove(key);
                }
            }
            else
            {
                // Очищаємо весь кеш
                _jsonTemplateCache.Clear();
                _templateCache.Clear();
            }
        }

        /// <summary>
        /// Встановлює віртуальне джерело даних для таблиці.
        /// </summary>
        /// <param name="dbService">Сервіс бази даних.</param>
        /// <param name="tableName">Назва таблиці в базі даних.</param>
        public void SetVirtualDataSource(DatabaseService dbService, string tableName)
        {
            // Створюємо провайдер, який знає, як читати з БД
            var provider = new JournalDataProvider(dbService, tableName, _currentTemplate.Columns);

            // Створюємо колекцію, яка імітує список
            _virtualData = new AsyncVirtualizingCollection(provider);

            // Підписуємося на події додавання/заміни елементів для підключення обробника PropertyChanged
            _virtualData.CollectionChanged += VirtualData_CollectionChanged;

            // Прив'язуємо до DataGrid
            DynamicGrid.ItemsSource = _virtualData;
        }

        /// <summary>
        /// Обробник CollectionChanged для підключення обробника PropertyChanged до нових рядків
        /// </summary>
        private void VirtualData_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Підписуємося на PropertyChanged для нових рядків
            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems)
                {
                    if (item is BindableRow row)
                    {
                        // Відписуватись перед підпискою (на випадок повторного додавання)
                        row.PropertyChanged -= BindableRow_PropertyChanged;
                        row.PropertyChanged += BindableRow_PropertyChanged;
                    }
                }
            }
        }

        /// <summary>
        /// Обробник зміни властивостей BindableRow - викликає перерахунки
        /// </summary>
        private void BindableRow_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is not BindableRow rowData) return;
            if (_currentTemplate == null) return;

            // Ігноруємо зміни службових властивостей
            if (e.PropertyName == Binding.IndexerName) return;

            // Пропускаємо PlaceholderRow
            if (sender is PlaceholderRow) return;

            try
            {
                // 1. Виконуємо правила OnChange (якщо змінилася конкретна колонка)
                if (!string.IsNullOrEmpty(e.PropertyName))
                {
                    ExecuteChangeRules(e.PropertyName, rowData);
                }

                // 2. Виконуємо Expression обчислення для всього рядка
                PerformCalculations(rowData);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in recalculation: {ex.Message}");
            }
        }

        private void InitializeCalculationEngine(List<ColumnConfig> columns)
        {
            _calculationEngine = new DataTable();

            if (columns == null) return;

            foreach (var col in columns)
            {
                if (col == null || col.Type == ColumnType.SectionHeader) continue;

                // Перевіряємо FieldName на null
                if (string.IsNullOrEmpty(col.FieldName))
                {
                    System.Diagnostics.Debug.WriteLine($"Warning: Column with HeaderText '{col.HeaderText}' has null or empty FieldName");
                    continue;
                }

                // Визначаємо тип даних для DataTable
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

                // Створюємо колонку
                DataColumn dc = new DataColumn(col.FieldName, type);

                // Якщо є формула - прописуємо її. DataTable сама буде рахувати.
                if (!string.IsNullOrEmpty(col.Expression))
                {
                    dc.Expression = col.Expression;
                }

                _calculationEngine.Columns.Add(dc);
            }
        }

        #endregion

        #region UI Generation: Main Grid Structure

        private void BuildGridStructure(List<ColumnConfig> config)
        {
            DynamicGrid.Columns.Clear();
            ClearResources();

            // 1. Групування колонок (логіка складного лейауту)
            var groups = GroupColumns(config);

            // Налаштування сортування для всієї таблиці
            ConfigureGridSorting(config);

            // ОПТИМІЗАЦІЯ: Створюємо всі колонки спочатку, потім додаємо пакетом
            var columns = new List<DataGridColumn>(groups.Count);

            // 2. Генерація DataGridTemplateColumn для кожної групи
            foreach (var group in groups)
            {
                var templateCol = new DataGridTemplateColumn();

                // Стилізація заголовка (Header)
                templateCol.HeaderStyle = CreateHeaderStyle();

                // Стилізація клітинки (Cell)
                templateCol.CellStyle = CreateCellStyle();

                // Розрахунок ширини
                templateCol.Width = CalculateGroupWidth(group);

                // Налаштування сортування для колонки
                ConfigureSorting(templateCol, group);

                // Генерація XAML для заголовка (з кешуванням)
                try
                {
                    string fieldName = group?.MainConfig?.FieldName ?? "Unknown";
                    string headerKey = $"{_currentTemplate.Id}_Header_{fieldName}";
                    templateCol.HeaderTemplate = GetOrCreateTemplate(headerKey, () => GenerateHeaderXaml(group));
                }
                catch (Exception ex)
                {
                    templateCol.Header = group?.MainConfig?.HeaderText ?? "Column";
                    System.Diagnostics.Debug.WriteLine($"Header XAML Error: {ex.Message}");
                }

                // ОПТИМІЗАЦІЯ: Використовуємо compiled templates для простих випадків
                bool useCompiledTemplates = CanUseCompiledTemplateForGroup(group);

                if (useCompiledTemplates)
                {
                    // Compiled templates (швидше на 30-50%)
                    var item = group.Rows[0].Items[0];

                    templateCol.CellTemplate = CreateCompiledViewTemplate(item);
                    templateCol.CellEditingTemplate = CreateCompiledEditTemplate(item);
                }
                else
                {
                    // Генеруємо XAML для складних випадків
                    try
                    {
                        string fieldName = group?.MainConfig?.FieldName ?? "Unknown";
                        string viewKey = $"{_currentTemplate.Id}_View_{fieldName}";
                        templateCol.CellTemplate = GetOrCreateTemplate(viewKey, () => GenerateCellXaml(group, isEditing: false));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"View XAML Error: {ex.Message}");
                    }

                    try
                    {
                        string fieldName = group?.MainConfig?.FieldName ?? "Unknown";
                        string editKey = $"{_currentTemplate.Id}_Edit_{fieldName}";
                        templateCol.CellEditingTemplate = GetOrCreateTemplate(editKey, () => GenerateCellXaml(group, isEditing: true));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"EditControl XAML Error: {ex.Message}");
                    }
                }

                // Додаємо до колекції
                columns.Add(templateCol);
            }

            // Тимчасово вимикаємо ItemsSource для швидшого додавання колонок
            var itemsSource = DynamicGrid.ItemsSource;
            DynamicGrid.ItemsSource = null;

            // Додаємо всі колонки
            foreach (var col in columns)
            {
                DynamicGrid.Columns.Add(col);
            }

            // Відновлюємо ItemsSource
            DynamicGrid.ItemsSource = itemsSource;
        }

        /// <summary>
        /// Отримати або створити DataTemplate з кешу
        /// </summary>
        private DataTemplate GetOrCreateTemplate(string key, Func<string> xamlGenerator)
        {
            if (!_templateCache.ContainsKey(key))
            {
                string xaml = xamlGenerator();
                _templateCache[key] = (DataTemplate)XamlReader.Parse(xaml);
            }
            return _templateCache[key];
        }

        private List<VisualColumnGroup> GroupColumns(List<ColumnConfig> config)
        {
            var groups = new List<VisualColumnGroup>();
            VisualColumnGroup currentGroup = null;
            VisualRow currentRow = null;

            foreach (var col in config)
            {
                if (col.Position == ColumnPosition.NewColumn || currentGroup == null)
                {
                    currentGroup = new VisualColumnGroup { MainConfig = col };
                    groups.Add(currentGroup);

                    // SectionHeader не додається як рядок даних у клітинку
                    if (col.Type != ColumnType.SectionHeader)
                    {
                        currentRow = new VisualRow();
                        currentRow.Items.Add(col);
                        currentGroup.Rows.Add(currentRow);
                    }
                    else
                    {
                        currentRow = null;
                    }
                }
                else if (col.Position == ColumnPosition.NextRow)
                {
                    currentRow = new VisualRow();
                    currentRow.Items.Add(col);
                    currentGroup.Rows.Add(currentRow);
                }
                else if (col.Position == ColumnPosition.SameColumn)
                {
                    if (currentRow == null)
                    {
                        currentRow = new VisualRow();
                        currentGroup.Rows.Add(currentRow);
                    }
                    currentRow.Items.Add(col);
                }
            }
            return groups;
        }

        private Style CreateHeaderStyle()
        {
            // Використовуємо стиль із ресурсів XAML
            return (Style)this.Resources["DynamicTableColumnHeaderStyle"];
        }

        private Style CreateCellStyle()
        {
            // Використовуємо стиль із ресурсів XAML
            return (Style)this.Resources["DynamicTableCellStyle"];
        }

        private double CalculateGroupWidth(VisualColumnGroup group)
        {
            double calculatedMinWidth = 0;

            // Шукаємо найширший рядок у групі
            foreach (var row in group.Rows)
            {
                double rowFixedSum = row.Items.Where(i => i.Width > 0).Sum(i => i.Width);
                if (rowFixedSum > calculatedMinWidth) calculatedMinWidth = rowFixedSum;
            }

            if (group.MainConfig.Width > 0)
            {
                return Math.Max(group.MainConfig.Width, calculatedMinWidth);
            }

            return calculatedMinWidth > 0 ? calculatedMinWidth : DataGridLength.Auto.Value;
        }

        private void ConfigureSorting(DataGridTemplateColumn col, VisualColumnGroup group)
        {
            // Безпечна робота з FieldName - може бути null
            string sortField = group?.MainConfig?.FieldName;

            // Якщо це секція, беремо перше поле даних для сортування
            if (group?.MainConfig?.Type == ColumnType.SectionHeader &&
                group.Rows != null && group.Rows.Count > 0 &&
                group.Rows[0].Items != null && group.Rows[0].Items.Count > 0)
            {
                sortField = group.Rows[0].Items[0]?.FieldName;
            }

            // Якщо FieldName все ще null, вимикаємо сортування для цієї колонки
            if (string.IsNullOrEmpty(sortField))
            {
                col.CanUserSort = false;
                return;
            }

            // Сортування доступне тільки для колонки Id
            bool hasIdColumn = _currentTemplate?.Columns?.Any(c =>
                !string.IsNullOrEmpty(c.FieldName) &&
                c.FieldName.Equals("Id", StringComparison.OrdinalIgnoreCase)) ?? false;
            bool isIdColumn = sortField.Equals("Id", StringComparison.OrdinalIgnoreCase);

            if (hasIdColumn)
            {
                // Якщо є колонка Id - дозволяємо сортування тільки для неї
                col.CanUserSort = isIdColumn;
                if (isIdColumn)
                {
                    col.SortMemberPath = sortField;
                }
            }
            else
            {
                // Якщо немає колонки Id - вимикаємо сортування для всіх колонок
                col.CanUserSort = false;
            }
        }

        /// <summary>
        /// Налаштовує сортування за замовчуванням для таблиці
        /// </summary>
        private void ConfigureGridSorting(List<ColumnConfig> config)
        {
            if (config == null)
            {
                DynamicGrid.CanUserSortColumns = false;
                return;
            }

            // Перевіряємо, чи є колонка Id
            bool hasIdColumn = config.Any(c =>
                !string.IsNullOrEmpty(c?.FieldName) &&
                c.FieldName.Equals("Id", StringComparison.OrdinalIgnoreCase));

            if (hasIdColumn)
            {
                // За замовчуванням сортуємо за Id DESC (новіші зверху)
                DynamicGrid.CanUserSortColumns = true;
            }
            else
            {
                // Якщо немає Id - вимикаємо сортування взагалі
                DynamicGrid.CanUserSortColumns = false;
            }
        }

        private void ClearResources()
        {
            // Remove any resources we added to the DataGrid
            var keysToRemove = DynamicGrid.Resources.Keys.Cast<object>().ToList();
            foreach (var k in keysToRemove) DynamicGrid.Resources.Remove(k);

            // Clear session values cache
            _sessionValues.Clear();
        }

        #endregion

        #region UI Generation: Compiled Templates

        /// <summary>
        /// Перевіряє, чи можна використати compiled template для групи
        /// </summary>
        private bool CanUseCompiledTemplateForGroup(VisualColumnGroup group)
        {
            // Compiled templates підходять тільки для простих випадків:
            // - 1 рядок, 1 елемент
            // - Простий тип (Text, Number, Boolean)
            if (group.Rows.Count != 1 || group.Rows[0].Items.Count != 1)
                return false;

            var item = group.Rows[0].Items[0];
            return DataTemplateBuilder.CanUseCompiledTemplate(item);
        }

        /// <summary>
        /// Створює compiled DataTemplate для перегляду
        /// </summary>
        private DataTemplate CreateCompiledViewTemplate(ColumnConfig config)
        {
            DataTemplate innerTemplate = config.Type switch
            {
                ColumnType.Boolean => DataTemplateBuilder.CreateBooleanViewTemplate(config.FieldName),
                _ => DataTemplateBuilder.CreateTextViewTemplate(
                    config.FieldName,
                    GetDisplayFormat(config),
                    !string.IsNullOrEmpty(config.Expression))
            };

            return innerTemplate;
        }

        /// <summary>
        /// Створює compiled DataTemplate для редагування
        /// </summary>
        private DataTemplate CreateCompiledEditTemplate(ColumnConfig config)
        {
            DataTemplate innerTemplate = config.Type switch
            {
                ColumnType.Boolean => DataTemplateBuilder.CreateBooleanEditTemplate(config.FieldName),
                _ => DataTemplateBuilder.CreateTextEditTemplate(
                    config.FieldName,
                    config.Format,
                    !string.IsNullOrEmpty(config.Expression))
            };
            return innerTemplate;
        }

        /// <summary>
        /// Отримує формат для відображення
        /// </summary>
        private string GetDisplayFormat(ColumnConfig config)
        {
            if (!string.IsNullOrEmpty(config.Format))
                return config.Format;

            return config.Type switch
            {
                ColumnType.Date => "dd.MM.yyyy",
                ColumnType.DateTime => "dd.MM.yyyy HH:mm",
                ColumnType.Time => "HH\\:mm",
                ColumnType.Currency => "C2",
                _ => ""
            };
        }

        #endregion

        #region UI Generation: XAML Builders

        private string GenerateHeaderXaml(VisualColumnGroup group)
        {
            var sb = new StringBuilder();
            sb.Append(@"<DataTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"">");

            if (group.MainConfig.Type == ColumnType.SectionHeader)
            {
                // SectionHeader: два рівні (заголовок секції + підзаголовки)
                sb.Append(@"<Grid>");
                sb.Append(@"<Grid.RowDefinitions><RowDefinition Height=""Auto""/><RowDefinition Height=""*""/></Grid.RowDefinitions>");
                sb.Append(@"<Border BorderThickness=""0,0,0,1"" BorderBrush=""{DynamicResource MaterialDesignDivider}"" Focusable=""False"">");
                sb.Append($@"<TextBlock Text=""{group.MainConfig.HeaderText}"" HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>");
                sb.Append(@"</Border>");

                // Нижня частина (підзаголовки)
                sb.Append(@"<Grid Grid.Row=""1"">");
                GenerateSubHeadersGrid(sb, group.Rows);
                sb.Append(@"</Grid>");
                sb.Append(@"</Grid>");
            }
            else
            {
                // Звичайний заголовок (без секції)
                GenerateSubHeadersGrid(sb, group.Rows);
            }

            sb.Append(@"</DataTemplate>");
            return sb.ToString();
        }

        private string GenerateCellXaml(VisualColumnGroup group, bool isEditing)
        {
            var sb = new StringBuilder();
            sb.Append(@"<DataTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                              xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
                              xmlns:materialDesign=""http://materialdesigninxaml.net/winfx/xaml/themes"">");

            // Якщо один рядок і один елемент - максимально спростимо
            if (group.Rows.Count == 1 && group.Rows[0].Items.Count == 1)
            {
                var item = group.Rows[0].Items[0];

                string content = isEditing ? GenerateEditControlXaml(item) : GenerateViewControlXaml(item);

                sb.Append(content);
            }
            else
            {
                // Складна структура: Grid з рядками
                sb.Append(@"<Grid>");

                // Визначення рядків
                sb.Append(@"<Grid.RowDefinitions>");
                foreach (var _ in group.Rows) sb.Append(@"<RowDefinition Height=""*""/>");
                sb.Append(@"</Grid.RowDefinitions>");

                // Генерація вмісту
                for (int r = 0; r < group.Rows.Count; r++)
                {
                    GenerateRowContent(sb, group.Rows[r], r, isHeader: false, isEditing: isEditing);
                }

                sb.Append(@"</Grid>");
            }

            sb.Append(@"</DataTemplate>");
            return sb.ToString();
        }

        private void GenerateSubHeadersGrid(StringBuilder sb, List<VisualRow> rows)
        {
            if (rows.Count == 1)
            {
                GenerateRowContent(sb, rows[0], 0, isHeader: true);
                return;
            }

            // Кілька рядків - потрібен Grid
            sb.Append(@"<Grid>");
            sb.Append(@"<Grid.RowDefinitions>");
            foreach (var _ in rows) sb.Append(@"<RowDefinition Height=""*""/>");
            sb.Append(@"</Grid.RowDefinitions>");

            for (int r = 0; r < rows.Count; r++)
            {
                GenerateRowContent(sb, rows[r], r, isHeader: true);
            }
            sb.Append(@"</Grid>");
        }

        /// <summary>
        /// Генерація рядка: мінімум Border та Grid
        /// </summary>
        private void GenerateRowContent(StringBuilder sb, VisualRow row, int rowIndex, bool isHeader, bool isEditing = false)
        {
            // ВИПАДОК 1: Один елемент у рядку
            if (row.Items.Count == 1)
            {
                var item = row.Items[0];

                // Верхній роздільник для заголовків (крім першого рядка)
                string topBorder = (isHeader && rowIndex > 0) ? "0,1,0,0" : "0";

                // Padding тільки для заголовків
                string padding = isHeader ? "5" : "0";

                if (isHeader)
                {
                    // Потрібен Border тільки якщо є верхній роздільник або padding
                    sb.Append($@"<Border Grid.Row=""{rowIndex}"" BorderThickness=""{topBorder}"" 
                         BorderBrush=""{{DynamicResource MaterialDesignDivider}}"" Padding=""{padding}"" Focusable=""False"">");

                    sb.Append(GenerateHeaderControl(item));

                    sb.Append(@"</Border>");
                }
                else
                {
                    // Без Border взагалі - контрол безпосередньо в Grid
                    string gridRow = rowIndex > 0 ? $@" Grid.Row=""{rowIndex}""" : "";

                    if (isEditing)
                    {
                        string xaml = GenerateEditControlXaml(item);
                        sb.Append(xaml.Replace("<TextBox ", $"<TextBox{gridRow} ")
                                     .Replace("<ComboBox ", $"<ComboBox{gridRow} ")
                                     .Replace("<DatePicker ", $"<DatePicker{gridRow} ")
                                     .Replace("<CheckBox ", $"<CheckBox{gridRow} ")
                                     .Replace("<materialDesign:TimePicker ", $"<materialDesign:TimePicker{gridRow} "));
                    }
                    else
                    {
                        string xaml = GenerateViewControlXaml(item);
                        sb.Append(xaml.Replace("<TextBlock ", $"<TextBlock{gridRow} ")
                                     .Replace("<CheckBox ", $"<CheckBox{gridRow} "));
                    }
                }
            }
            // ВИПАДОК 2: Кілька елементів у рядку (горизонтальний лейаут)
            else
            {
                string topBorder = (isHeader && rowIndex > 0) ? "0,1,0,0" : "0";

                sb.Append($@"<Grid Grid.Row=""{rowIndex}"">");
                sb.Append(GenerateColumnDefinitionsXaml(row.Items));

                for (int c = 0; c < row.Items.Count; c++)
                {
                    var item = row.Items[c];

                    // Лівий роздільник (крім першої колонки)
                    string leftBorder = (c > 0) ? "1,0,0,0" : "0";
                    string padding = isHeader ? "5" : "0";

                    // Для складених клітинок встановлюємо TabIndex для правильної навігації
                    string tabIndex = isEditing ? $@" TabIndex=""{c}""" : "";

                    if (isHeader)
                    {
                        // Border для роздільника або padding
                        sb.Append($@"<Border Grid.Column=""{c}"" BorderThickness=""{leftBorder}"" 
                             BorderBrush=""{{DynamicResource MaterialDesignDivider}}"" Padding=""{padding}"" Focusable=""False"">");

                        sb.Append(GenerateHeaderControl(item));

                        sb.Append(@"</Border>");
                    }
                    else
                    {
                        // Контрол безпосередньо в Grid.Column
                        if (isEditing)
                        {
                            string xaml = GenerateEditControlXaml(item);
                            sb.Append(xaml.Replace("<TextBox ", $"<TextBox Grid.Column=\"{c}\"{tabIndex} ")
                                         .Replace("<ComboBox ", $"<ComboBox Grid.Column=\"{c}\"{tabIndex} ")
                                         .Replace("<DatePicker ", $"<DatePicker Grid.Column=\"{c}\"{tabIndex} ")
                                         .Replace("<CheckBox ", $"<CheckBox Grid.Column=\"{c}\"{tabIndex} ")
                                         .Replace("<materialDesign:TimePicker ", $"<materialDesign:TimePicker Grid.Column=\"{c}\"{tabIndex} "));
                        }
                        else
                        {
                            string xaml = GenerateViewControlXaml(item);
                            sb.Append(xaml.Replace("<TextBlock ", $"<TextBlock Grid.Column=\"{c}\" ")
                                         .Replace("<CheckBox ", $"<CheckBox Grid.Column=\"{c}\" "));
                        }
                    }
                }

                sb.Append(@"</Grid>");
            }
        }

        #endregion

        #region UI Generation: XAML Control Generators

        private string GenerateHeaderControl(ColumnConfig item)
        {
            string result = string.Empty;
            string label = "(no label)";
            
            if (item.Header != null) label = item.IsRequired ? $"{item.Header.Text} *" : item.Header.Text;
            else label = item.IsRequired ? $"{item.HeaderText} *" : item.HeaderText;

            result = $@"<TextBlock Text=""{label}"" VerticalAlignment=""Center"" HorizontalAlignment=""Center"" TextAlignment=""Center""";

            if (item.Header != null)
            {
                if (item.Header.Size > 0)
                    result += $@" FontSize=""{item.Header.Size}"">";
                else result += ">";

                if (item.Header.Direction == ColumnHeaderDirection.Vertical)
                {
                    result += $@"<TextBlock.LayoutTransform><RotateTransform Angle=""-90""/></TextBlock.LayoutTransform>";
                }
            } else result += " TextWrapping=\"Wrap\" >";

            result += @"</TextBlock>";

            return result;
        }

        private string GenerateEditControlXaml(ColumnConfig col)
        {
            return col.Type switch
            {
                ColumnType.Dropdown or ColumnType.DropdownEditable => GenerateDropdownXaml(col),
                ColumnType.Date => GenerateDatePickerXaml(col),
                ColumnType.Time => GenerateTimePickerXaml(col),
                ColumnType.DateTime => GenerateDateTimePickerXaml(col),
                ColumnType.Boolean => GenerateCheckBoxXaml(col),
                _ => GenerateTextBoxXaml(col)
            };
        }

        private string GenerateDropdownXaml(ColumnConfig col)
        {
            string resKey = $"Options_{col.FieldName}";
            if (!DynamicGrid.Resources.Contains(resKey))
                DynamicGrid.Resources.Add(resKey, col.Options ?? new List<string>());

            bool isEditable = col.Type == ColumnType.DropdownEditable;

            return $@"<ComboBox ItemsSource=""{{DynamicResource {resKey}}}""
                        Tag=""{col.FieldName}""
                        Text=""{{Binding [{col.FieldName}], Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}}""
                        IsEditable=""{isEditable}"" StaysOpenOnEdit=""True""
                        Focusable=""True"" IsTabStop=""True""
                        materialDesign:TextFieldAssist.HasClearButton=""True"" 
                        BorderThickness=""0"" Background=""Transparent""
                        Padding=""5,2"" VerticalAlignment=""Center"" HorizontalAlignment=""Stretch""/>";
        }

        private string GenerateDatePickerXaml(ColumnConfig col)
        {
            return $@"<DatePicker xmlns:helpers=""clr-namespace:FlexJournalPro.Helpers;assembly=FlexJournalPro""
                        SelectedDate=""{{Binding [{col.FieldName}], Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}}"" 
                        Tag=""{col.FieldName}""
                        Focusable=""True"" IsTabStop=""True""
                        materialDesign:TextFieldAssist.HasClearButton=""False""
                        BorderThickness=""0"" Background=""Transparent""
                        VerticalAlignment=""Center"" HorizontalAlignment=""Stretch""
                        helpers:DatePickerHelper.EnableFastInput=""True""/>";
        }

        private string GenerateTimePickerXaml(ColumnConfig col)
        {
            if (!DynamicGrid.Resources.Contains("TimeSpanToNullableDateTimeConverter"))
            {
                DynamicGrid.Resources.Add("TimeSpanToNullableDateTimeConverter",
                    new FlexJournalPro.Converters.TimeSpanToNullableDateTimeConverter());
            }

            return $@"<materialDesign:TimePicker xmlns:helpers=""clr-namespace:FlexJournalPro.Helpers;assembly=FlexJournalPro""
                          SelectedTime=""{{Binding [{col.FieldName}], Mode=TwoWay, UpdateSourceTrigger=PropertyChanged, Converter={{StaticResource TimeSpanToNullableDateTimeConverter}}}}""
                          Tag=""{col.FieldName}""
                          Focusable=""True"" IsTabStop=""True""
                          Is24Hours=""True""
                          BorderThickness=""0"" Background=""Transparent""
                          VerticalAlignment=""Center"" HorizontalAlignment=""Stretch""
                          helpers:TimePickerHelper.EnableFastInput=""True""/>";
        }

        private string GenerateDateTimePickerXaml(ColumnConfig col)
        {
            return $@"<Grid>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width=""*""/>
                            <ColumnDefinition Width=""80""/>
                        </Grid.ColumnDefinitions>
                        
                        <DatePicker xmlns:helpers=""clr-namespace:FlexJournalPro.Helpers;assembly=FlexJournalPro""
                            Grid.Column=""0"" 
                            SelectedDate=""{{Binding [{col.FieldName}], Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}}"" 
                            materialDesign:TextFieldAssist.HasClearButton=""False""
                            Tag=""{col.FieldName}""
                            Focusable=""True"" IsTabStop=""True""
                            BorderThickness=""0"" Background=""Transparent""
                            VerticalAlignment=""Center"" HorizontalAlignment=""Stretch""
                            helpers:DatePickerHelper.DisableManualEdit=""True""
                            helpers:DatePickerHelper.EnableFastInput=""True""/>

                        <materialDesign:TimePicker xmlns:helpers=""clr-namespace:FlexJournalPro.Helpers;assembly=FlexJournalPro""
                            Grid.Column=""1"" 
                            SelectedTime=""{{Binding [{col.FieldName}], Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}}"" 
                            Is24Hours=""True""
                            Margin=""5,0,0,0""
                            Tag=""{col.FieldName}""
                            Focusable=""True"" IsTabStop=""True""
                            BorderThickness=""0"" Background=""Transparent""
                            VerticalAlignment=""Center"" HorizontalAlignment=""Stretch""
                            helpers:TimePickerHelper.EnableFastInput=""True""/>
                      </Grid>";
        }

        private string GenerateCheckBoxXaml(ColumnConfig col)
        {
            string bindingDef = $"Binding [{col.FieldName}], Mode=TwoWay, UpdateSourceTrigger=PropertyChanged, Converter={{StaticResource SafeBoolConverter}}";
            return $@"<CheckBox IsChecked=""{{{bindingDef}}}""
                        Tag=""{col.FieldName}""
                        Focusable=""True"" IsTabStop=""True""
                        HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>";
        }

        private string GenerateTextBoxXaml(ColumnConfig col)
        {
            bool isCalculated = !string.IsNullOrEmpty(col.Expression);
            string mode = isCalculated ? "OneWay" : "TwoWay";
            
            string readOnlyProps = isCalculated ? @"IsReadOnly=""True"" Focusable=""False"" IsTabStop=""False"" Foreground=""Gray"" FontStyle=""Italic""" : "";

            string updateTrigger = isCalculated ? "PropertyChanged" : "LostFocus";

            string bindingDef = $"Binding [{col.FieldName}], Mode={mode}, UpdateSourceTrigger={updateTrigger}, ValidatesOnExceptions=True, TargetNullValue={{}}, FallbackValue={{}}";

            return $@"<TextBox Text=""{{{bindingDef}}}""
                       Tag=""{col.FieldName}""
                       BorderThickness=""0"" Background=""Transparent""
                       Padding=""4,2"" VerticalAlignment=""Stretch"" VerticalContentAlignment=""Center""
                       {readOnlyProps} />";
        }

        private string GenerateViewControlXaml(ColumnConfig col)
        {
            if (col.Type == ColumnType.SectionHeader) return "";

            if (col.Type == ColumnType.Boolean)
                return GenerateBooleanViewXaml(col);

            return GenerateTextBlockViewXaml(col);
        }

        private string GenerateBooleanViewXaml(ColumnConfig col)
        {
            string bindingDef = BuildViewBindingDefinition(col);

            return $@"<CheckBox IsChecked=""{{{bindingDef}}}""
                        IsEnabled=""True""
                        HorizontalAlignment=""Center"" 
                        VerticalAlignment=""Center""
                        IsHitTestVisible=""False""/>";
        }

        private string GenerateTextBlockViewXaml(ColumnConfig col)
        {
            string bindingDef = BuildViewBindingDefinition(col);
            string readOnlyProps = !string.IsNullOrEmpty(col.Expression)
                ? @"Focusable=""False"" Foreground=""Gray"" FontStyle=""Italic"""
                : "";

            return $@"<TextBlock Text=""{{{bindingDef}}}"" 
                         VerticalAlignment=""Center"" 
                         HorizontalAlignment=""Stretch""
                         Padding=""4,2""
                         TextTrimming=""CharacterEllipsis""
                         {readOnlyProps}/>";
        }

        private string BuildViewBindingDefinition(ColumnConfig col)
        {
            var binding = $"Binding [{col.FieldName}], Mode=OneWay, TargetNullValue={{}}, FallbackValue={{}}";

            if (col.Type == ColumnType.Time)
            {
                if (!DynamicGrid.Resources.Contains("TimeSpanToNullableDateTimeConverter"))
                {
                    DynamicGrid.Resources.Add("TimeSpanToNullableDateTimeConverter",
                        new FlexJournalPro.Converters.TimeSpanToNullableDateTimeConverter());
                }
                binding += ", Converter={StaticResource TimeSpanToNullableDateTimeConverter}";
            }

            if (col.Type is ColumnType.Date or ColumnType.DateTime or ColumnType.Time)
            {
                string format = GetDisplayFormat(col);
                binding += $", StringFormat={format}";
            }
            else if (col.Type == ColumnType.Boolean)
            {
                binding += ", Converter={{StaticResource SafeBoolConverter}}";
            }
            else if (!string.IsNullOrEmpty(col.Format))
            {
                binding += $", StringFormat={col.Format}";
            }

            return binding;
        }

        private string GenerateColumnDefinitionsXaml(List<ColumnConfig> items)
        {
            var sb = new StringBuilder();
            sb.Append(@"<Grid.ColumnDefinitions>");

            foreach (var item in items)
            {
                string width = item.Width > 0
                    ? item.Width.ToString(CultureInfo.InvariantCulture)
                    : "*";
                sb.Append($@"<ColumnDefinition Width=""{width}""/>");
            }

            sb.Append(@"</Grid.ColumnDefinitions>");
            return sb.ToString();
        }

        #endregion

        #region Data Logic

        private void PerformCalculations(BindableRow rowData)
        {
            if (_calculationEngine == null) return;

            try
            {
                _calculationEngine.Rows.Clear();
                DataRow row = _calculationEngine.NewRow();

                // 1. Заповнюємо вхідні дані
                foreach (var col in _currentTemplate.Columns)
                {
                    // Пропускаємо заголовки і ОБЧИСЛЮВАНІ поля (їх не можна сеттити вручну, вони ReadOnly)
                    if (col.Type == ColumnType.SectionHeader || !string.IsNullOrEmpty(col.Expression)) continue;

                    if (rowData.ContainsKey(col.FieldName) && rowData[col.FieldName] != null)
                    {
                        // Безпечне приводження типів (бо в Dictionary може бути string, а DataTable хоче decimal)
                        try
                        {
                            row[col.FieldName] = Convert.ChangeType(rowData[col.FieldName], _calculationEngine.Columns[col.FieldName].DataType);
                        }
                        catch
                        {
                            // Якщо конвертація не вдалась (напр. пустий рядок у числове поле), лишаємо DBNull
                        }
                    }
                }

                // 2. Додаємо рядок у таблицю - в цей момент спрацьовують Expressions
                _calculationEngine.Rows.Add(row);

                // 3. Забираємо результати (включаючи обчислені поля) назад у Dictionary
                foreach (DataColumn dc in _calculationEngine.Columns)
                {
                    // Нас цілком рахують в першу чергу обчислені поля, але можна оновити й інші (наприклад, якщо було форматування)
                    // Але важливо: беремо значення, тільки якщо воно не DBNull
                    if (row[dc] != DBNull.Value)
                    {
                        rowData[dc.ColumnName] = row[dc];
                    }
                    else
                    {
                        // Якщо обчислення повернуло NULL (напр. ділення на 0), записуємо null
                        rowData[dc.ColumnName] = null;
                    }
                }
            }
            catch (Exception ex)
            {
                // Логування помилки обчислення, якщо треба. 
                // Головне не впасти, щоб користувач міг хоч би зберегти введене.
                System.Diagnostics.Debug.WriteLine("Calculation error: " + ex.Message);
            }
        }

        private void ExecuteChangeRules(string sourceFieldName, BindableRow rowData)
        {
            var sourceColConfig = _currentTemplate.Columns.FirstOrDefault(c => c.FieldName == sourceFieldName);

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

                        var targetCol = _currentTemplate.Columns.FirstOrDefault(c => c.FieldName == rule.TargetColumn);
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

        private string SubstituteValuesInExpression(string expression, BindableRow rowData)
        {
            foreach (var col in _currentTemplate.Columns)
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
                                   System.Globalization.NumberStyles.Any,
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

        // Візуальні класи для групування колонок
        private class VisualColumnGroup
        {
            public ColumnConfig MainConfig { get; set; }
            public List<VisualRow> Rows { get; set; } = new List<VisualRow>();
        }

        private class VisualRow
        {
            public List<ColumnConfig> Items { get; set; } = new List<ColumnConfig>();
        }

        #endregion

        // Метод для застосування сеансних значень (для обчислень)
        public void ApplySessionValues(Dictionary<string, object> values)
        {
            if (values == null) return;

            // Оновлюємо внутрішній словник для використання в Expression/OnChange
            _sessionValues.Clear();
            foreach (var kvp in values)
            {
                _sessionValues[kvp.Key] = kvp.Value;
            }
        }

        /// <summary>
        /// Отримати поточні сеансові значення
        /// </summary>
        public Dictionary<string, object> GetSessionValues()
        {
            return new Dictionary<string, object>(_sessionValues);
        }

        /// <summary>
        /// Отримати поточний шаблон
        /// </summary>
        public TableTemplate GetCurrentTemplate()
        {
            return _currentTemplate;
        }

        #region Dynamic DataGrid Editing

        #region DataGrid Event Handlers

        private void DynamicGrid_PreparingCellForEdit(object? sender, DataGridPreparingCellForEditEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var editingElement = GetActualEditingElement(e.EditingElement);
                FocusEditingElement(editingElement);
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void DynamicGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            lastClickPosition = e.GetPosition(DynamicGrid);
        }

        private void DynamicGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                HandleEnterKey(e);
            }
        }

        private void DynamicGrid_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var currentCell = DynamicGrid.CurrentCell;

            if (currentCell.Column == null || currentCell.Item == null)
                return;

            if (IsCellInViewMode(currentCell))
            {
                StartEditingWithNewText(e.Text);
                e.Handled = true;
            }
        }

        private void DynamicGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit) return;

            var rowData = e.Row.Item as BindableRow;
            if (rowData == null) return;

            var errors = ValidateRow(rowData);

            if (errors.Any())
            {
                CancelRowEdit(e, errors);
            }
            else
            {
                SaveRow(rowData);
            }
        }

        private List<string> ValidateRow(BindableRow rowData)
        {
            var errors = new List<string>();

            foreach (var colConfig in _currentTemplate.Columns)
            {
                if (colConfig.Type == ColumnType.SectionHeader || !colConfig.IsRequired)
                    continue;

                if (IsFieldEmpty(rowData, colConfig))
                {
                    errors.Add($"Поле '{colConfig.HeaderText}' є обов'язковим!");
                }
            }

            return errors;
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

        private async void CancelRowEdit(DataGridRowEditEndingEventArgs e, List<string> errors)
        {
            e.Cancel = true;
            string message = "Неможливо зберегти рядок:\n" + string.Join("\n", errors);
            await DialogService.ShowWarningAsync(message, "Помилка валідації");
            Dispatcher.BeginInvoke(new Action(() => e.Row.Focus()));
        }

        private void SaveRow(BindableRow rowData)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                PerformCalculations(rowData);
                RowSaved?.Invoke(this, new RowSavedEventArgs { RowData = rowData });
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        #region Horizontal Scroll Support (Touchpad Fix)

        private void DynamicTableView_Loaded(object sender, RoutedEventArgs e)
        {
            // Додаємо хук до вікна для перехоплення повідомлень тачпаду
            var window = Window.GetWindow(this);
            if (window != null)
            {
                var source = PresentationSource.FromVisual(window) as HwndSource;
                source?.AddHook(WndProc);
            }
        }

        private void DynamicTableView_Unloaded(object sender, RoutedEventArgs e)
        {
            // Прибираємо хук при вивантаженні контролу
            var window = Window.GetWindow(this);
            if (window != null)
            {
                var source = PresentationSource.FromVisual(window) as HwndSource;
                source?.RemoveHook(WndProc);
            }
        }

        private void DynamicGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Підтримка Shift + Scroll (стандартна поведінка для багатьох мишок)
            if (Keyboard.Modifiers == ModifierKeys.Shift && e.Delta != 0)
            {
                var scrollViewer = FindVisualChild<ScrollViewer>(DynamicGrid);
                if (scrollViewer != null)
                {
                    if (e.Delta < 0)
                        scrollViewer.LineRight();
                    else
                        scrollViewer.LineLeft();

                    e.Handled = true;
                }
            }
        }

        private const int WM_MOUSEHWHEEL = 0x020E;

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // Перехоплюємо повідомлення горизонтального прокручування (від тачпаду)
            if (msg == WM_MOUSEHWHEEL)
            {
                // Обробляємо тільки якщо курсор над нашою таблицею
                if (DynamicGrid.IsMouseOver)
                {
                    var scrollViewer = FindVisualChild<ScrollViewer>(DynamicGrid);
                    if (scrollViewer != null)
                    {
                        // Отримуємо значення нахилу (delta)
                        int tilt = (short)((wParam.ToInt64() >> 16) & 0xFFFF);

                        // Експериментально: робимо кілька кроків для плавності, бо LineRight/Left малі
                        if (tilt > 0)
                        {
                            scrollViewer.LineRight();
                            scrollViewer.LineRight();
                            scrollViewer.LineRight();
                        }
                        else
                        {
                            scrollViewer.LineLeft();
                            scrollViewer.LineLeft();
                            scrollViewer.LineLeft();
                        }
                        handled = true;
                    }
                }
            }
            return IntPtr.Zero;
        }

        #endregion

        #endregion

        #region TextBox Event Handlers

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && IsDescendantOf(textBox, DynamicGrid))
            {
                SelectAllText(textBox);
            }
        }

        #endregion

        #region Keyboard Navigation

        private void HandleEnterKey(KeyEventArgs e)
        {
            var currentCell = DynamicGrid.CurrentCell;

            if (currentCell.Column == null || currentCell.Item == null)
                return;

            if (IsCellInEditMode(currentCell))
            {
                SimulateTabKey();
                e.Handled = true;
            }
            else
            {
                DynamicGrid.BeginEdit();
                e.Handled = true;
            }
        }

        private void SimulateTabKey()
        {
            var tabEvent = new KeyEventArgs(
                Keyboard.PrimaryDevice,
                PresentationSource.FromVisual(DynamicGrid),
                0,
                Key.Tab)
            {
                RoutedEvent = Keyboard.KeyDownEvent
            };

            InputManager.Current.ProcessInput(tabEvent);
        }

        #endregion

        #region Editing Helpers

        private FrameworkElement? GetActualEditingElement(FrameworkElement editingElement)
        {
            return editingElement is ContentPresenter contentPresenter
                ? FindVisualChild<FrameworkElement>(contentPresenter)
                : editingElement;
        }

        private void FocusEditingElement(FrameworkElement? editingElement)
        {
            switch (editingElement)
            {
                case TextBox textBox:
                    FocusAndSelectTextBox(textBox);
                    break;

                case DatePicker datePicker:
                    datePicker.Focus();
                    break;

                case TimePicker timePicker:
                    FocusTimePicker(timePicker);
                    break;

                case Grid grid:
                    FocusGridElement(grid);
                    break;

                case Control control when control.Focusable:
                    control.Focus();
                    break;
            }
        }

        private void FocusGridElement(Grid grid)
        {
            var targetControl = DetermineTargetControlInGrid(grid, lastClickPosition);

            if (targetControl != null)
            {
                FocusControl(targetControl);
                return;
            }

            // Fallback
            var focusableControl = FindVisualChild<Control>(grid);
            focusableControl?.Focus();
        }

        private void FocusControl(Control control)
        {
            control.Focus();

            switch (control)
            {
                case TextBox textBox:
                    textBox.SelectAll();
                    break;

                case ComboBox comboBox:
                    FocusComboBox(comboBox);
                    break;

                case TimePicker timePicker:
                    FocusTimePicker(timePicker);
                    break;
            }
        }

        private void FocusComboBox(ComboBox comboBox)
        {
            if (comboBox.IsEditable)
            {
                var textBox = FindVisualChild<TextBox>(comboBox);
                if (textBox != null)
                {
                    textBox.Focus();
                    textBox.SelectAll();
                }
            }
            else
            {
                comboBox.IsDropDownOpen = true;
            }
        }

        private void FocusTimePicker(TimePicker timePicker)
        {
            timePicker.Focus();
            timePicker.RaiseEvent(new RoutedEventArgs(UIElement.GotFocusEvent));
        }

        private void FocusAndSelectTextBox(TextBox textBox)
        {
            textBox.Focus();
            textBox.SelectAll();
        }

        private void StartEditingWithNewText(string text)
        {
            DynamicGrid.BeginEdit();

            Dispatcher.BeginInvoke(new Action(() =>
            {
                var textBox = GetFocusedTextBox();
                if (textBox != null)
                {
                    textBox.Clear();
                    textBox.Text = text;
                    textBox.SelectionStart = text.Length;
                }
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private TextBox? GetFocusedTextBox()
        {
            var focusedElement = Keyboard.FocusedElement;

            return focusedElement switch
            {
                TextBox textBox => textBox,
                DependencyObject dependencyObject => FindVisualChild<TextBox>(dependencyObject),
                _ => null
            };
        }

        private void SelectAllText(TextBox textBox)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                textBox.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        #endregion

        #region Cell State Checking

        private bool IsCellInEditMode(DataGridCellInfo currentCell)
        {
            var currentRow = DynamicGrid.ItemContainerGenerator.ContainerFromItem(currentCell.Item) as DataGridRow;
            if (currentRow == null) return false;

            var cell = GetCell(DynamicGrid, currentRow, currentCell.Column);
            return cell?.IsEditing == true;
        }

        private bool IsCellInViewMode(DataGridCellInfo currentCell)
        {
            return !IsCellInEditMode(currentCell);
        }

        private DataGridCell? GetCell(DataGrid dataGrid, DataGridRow? row, DataGridColumn column)
        {
            if (row == null) return null;

            int columnIndex = dataGrid.Columns.IndexOf(column);
            if (columnIndex < 0) return null;

            var presenter = FindVisualChild<DataGridCellsPresenter>(row);
            if (presenter == null) return null;

            var cell = presenter.ItemContainerGenerator.ContainerFromIndex(columnIndex) as DataGridCell;
            if (cell == null)
            {
                dataGrid.ScrollIntoView(row, column);
                cell = presenter.ItemContainerGenerator.ContainerFromIndex(columnIndex) as DataGridCell;
            }

            return cell;
        }

        #endregion

        #region Grid Column Helpers

        private Control? DetermineTargetControlInGrid(Grid grid, Point? clickPosition)
        {
            var controls = GetEditableControlsInGrid(grid);

            if (controls.Count == 0) return null;
            if (controls.Count == 1) return controls[0];
            if (clickPosition == null) return controls[0];

            return FindControlAtPosition(controls, clickPosition.Value, grid);
        }

        private List<Control> GetEditableControlsInGrid(Grid grid)
        {
            var controls = new List<Control>();

            controls.AddRange(GetVisualChildren<TextBox>(grid).Where(tb => !tb.IsReadOnly && tb.IsTabStop));
            controls.AddRange(GetVisualChildren<ComboBox>(grid).Where(cb => cb.IsTabStop));
            controls.AddRange(GetVisualChildren<DatePicker>(grid).Where(dp => dp.IsTabStop));
            controls.AddRange(GetVisualChildren<TimePicker>(grid).Where(tp => tp.IsTabStop));
            controls.AddRange(GetVisualChildren<CheckBox>(grid).Where(cb => cb.IsTabStop));

            return controls;
        }

        private Control? FindControlAtPosition(List<Control> controls, Point clickPosition, Grid grid)
        {
            // Спроба знайти точне співпадіння
            var exactMatch = controls.FirstOrDefault(control =>
                IsPointInControl(clickPosition, control));

            if (exactMatch != null) return exactMatch;

            // Визначення по горизонтальній позиції
            return FindNearestControl(controls, clickPosition, grid);
        }

        private bool IsPointInControl(Point clickPosition, Control control)
        {
            try
            {
                var relativePoint = DynamicGrid.TranslatePoint(clickPosition, control);
                var bounds = new Rect(0, 0, control.ActualWidth, control.ActualHeight);
                return bounds.Contains(relativePoint);
            }
            catch
            {
                return false;
            }
        }

        private Control? FindNearestControl(List<Control> controls, Point clickPosition, Grid grid)
        {
            var sortedControls = controls
                .Select(c => new { Control = c, Left = GetControlLeft(c, grid) })
                .OrderBy(x => x.Left)
                .ToList();

            if (sortedControls.Count == 0) return null;

            var gridPosition = DynamicGrid.TranslatePoint(clickPosition, grid);

            for (int i = 0; i < sortedControls.Count - 1; i++)
            {
                var current = sortedControls[i];
                var next = sortedControls[i + 1];
                var midPoint = (current.Left + next.Left) / 2;

                if (gridPosition.X < midPoint)
                    return current.Control;
            }

            return sortedControls[^1].Control;
        }

        private double GetControlLeft(Control control, Grid grid)
        {
            try
            {
                var position = control.TranslatePoint(new Point(0, 0), grid);
                return position.X;
            }
            catch
            {
                return 0;
            }
        }

        #endregion

        #region Visual Tree Helpers

        private T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T typedChild)
                    return typedChild;

                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }

            return null;
        }

        private void FindAllVisualChildren<T>(DependencyObject parent, List<T> children) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T typedChild)
                    children.Add(typedChild);

                FindAllVisualChildren(child, children);
            }
        }

        private IEnumerable<T> GetVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            var result = new List<T>();
            FindAllVisualChildren(parent, result);
            return result;
        }

        private bool IsDescendantOf(DependencyObject child, DependencyObject parent)
        {
            var current = child;
            while (current != null)
            {
                if (current == parent) return true;
                current = VisualTreeHelper.GetParent(current);
            }
            return false;
        }

        #endregion

        #endregion

    }
}

