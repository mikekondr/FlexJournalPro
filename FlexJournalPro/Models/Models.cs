using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Windows.Data;

namespace FlexJournalPro.Models
{
    // --- ENUMS ---

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ColumnPosition
    {
        NewColumn,      // Створює нову колонку DataGrid
        NextRow,        // Розміщує поле ПІД попереднім
        SameColumn      // Розміщує поле СПРАВА від попереднього (в тому ж рядку)
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ColumnType
    {
        Text,
        Number,
        Currency,
        Boolean,
        Date, // Тільки дата (dd.MM.yyyy)
        DateTime, // Дата і час (dd.MM.yyyy HH:mm)
        Time, // Тільки час (HH:mm)
        Dropdown,
        DropdownEditable,
        SectionHeader   // Тільки для візуального оформлення заголовків
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ColumnHeaderDirection
    {
        Normal,
        Vertical
    }

    // --- CONFIGURATION CLASSES ---

    /// <summary>
    /// Головний клас шаблону (Root Object для JSON)
    /// </summary>
    public class TableTemplate
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public List<SessionConstant> Constants { get; set; } = new List<SessionConstant>();
        public List<ColumnConfig> Columns { get; set; } = new List<ColumnConfig>();
    }

    /// <summary>
    /// Опис сеансової змінної (панель праворуч)
    /// </summary>
    public class SessionConstant
    {
        public string Key { get; set; }
        public string Label { get; set; }
        public ColumnType Type { get; set; }
        public object DefaultValue { get; set; }
        public List<string> Options { get; set; }
    }

    /// <summary>
    /// Правило перерахунку значень (формула)
    /// </summary>
    public class RecalculationRule
    {
        public string TargetColumn { get; set; }
        public string Expression { get; set; }
    }

    /// <summary>
    /// Налаштування однієї колонки або поля
    /// </summary>
    public class ColumnConfig
    {
        public string FieldName { get; set; }
        public string HeaderText { get; set; }
        public ColumnHeaderConfig Header { get; set; }
        public ColumnType Type { get; set; }
        public double Width { get; set; }
        public string Format { get; set; }
        public object DefaultValue { get; set; }
        public List<string> Options { get; set; }

        // Формула для початкового розрахунку (ReadOnly в DataTable)
        public string Expression { get; set; }

        // Правила "живого" перерахунку при зміні цього поля
        public List<RecalculationRule> OnChange { get; set; }

        public string BindConstant { get; set; }
        public bool IsRequired { get; set; }
        public ColumnPosition Position { get; set; } = ColumnPosition.NewColumn;
    }

    /// <summary>
    /// Налаштування заголовку колонки
    /// </summary>
    public class ColumnHeaderConfig
    {
        public string Text { get; set; }
        public ColumnHeaderDirection Direction { get; set; }
        public double Size { get; set; }
    }

    /// <summary>
    /// Метадані журналу (запис у реєстрі)
    /// </summary>
    public class JournalMetadata
    {
        public long Id { get; set; }
        public string Title { get; set; }          // Назва журналу
        public string PresetId { get; set; }       // ID шаблону
        public string TableName { get; set; }      // Фізична назва таблиці в SQLite
        public DateTime CreatedAt { get; set; }

        // Налаштування нумерації
        public long NumberStart { get; set; }

        // Збережені константи (JSON рядок)
        public string SessionConstantsJson { get; set; }
    }

    /// <summary>
    /// Метадані шаблону (зберігається в БД)
    /// </summary>
    public class TemplateMetadata
    {
        public string Id { get; set; }             // Унікальний ID шаблону (напр. "invoice_001")
        public string Name { get; set; }           // Назва шаблону (напр. "Накладна")
        public string Description { get; set; }    // Опис шаблону
        public string JsonConfig { get; set; }     // TableTemplate у форматі JSON
        public int Version { get; set; }           // Версія шаблону (для оновлень)
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsActive { get; set; }         // Чи активний шаблон
    }

    public class BindableRow : Dictionary<string, object>, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        // Перевизначаємо індексатор, щоб додати сповіщення
        public new object this[string key]
        {
            get
            {
                return this.ContainsKey(key) ? base[key] : null;
            }
            set
            {
                // Отримуємо попереднє значення (зведене до null якщо DBNull)
                object oldValue = this.ContainsKey(key) ? base[key] : null;
                if (oldValue == DBNull.Value) oldValue = null;

                object newValue = value;
                if (newValue == DBNull.Value) newValue = null;

                // Якщо значення не змінилося — не шлемо нотифікації
                if (EqualityComparer<object>.Default.Equals(oldValue, newValue))
                {
                    // Але якщо ключ відсутній, а присвоюється null — створюємо запис (щоб під час серіалізації/збереження він був присутній)
                    if (!this.ContainsKey(key) && value != null)
                    {
                        base[key] = value;
                    }
                    return;
                }

                base[key] = value;

                // Сповіщаємо WPF про зміну індексатора та конкретної властивості
                OnPropertyChanged(Binding.IndexerName);
                OnPropertyChanged(key);
            }
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Specialized placeholder row used by virtualizing collection while loading
    public class PlaceholderRow : BindableRow
    {
        public bool IsPlaceholder { get; } = true;

        public PlaceholderRow()
        {
            // Optionally set a marker value to make inspection easier
            base["__isPlaceholder"] = true;
        }
    }
}