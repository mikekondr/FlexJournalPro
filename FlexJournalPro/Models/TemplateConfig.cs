namespace FlexJournalPro.Models
{
    /// <summary>
    /// Головний клас шаблону (Root Object для JSON)
    /// </summary>
    public class TableTemplate
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }

        public RegistrationParams RegistrationParams { get; set; } = new RegistrationParams();

        public List<AutoFillParameter> AutoFillConfig { get; set; } = new List<AutoFillParameter>();
        public List<ColumnConfig> Columns { get; set; } = new List<ColumnConfig>();
    }

    /// <summary>
    /// Параметри реєстрації документа
    /// </summary>
    public class RegistrationParams
    {
        public bool UseRegistration { get; set; }
        public bool UseStrictNumbering { get; set; }
        public bool UseCustomStartNumber { get; set; }
        public bool UseLocking { get; set; }
        public bool UseNumberPrefix { get; set; }
        public bool UseNumberSuffix { get; set; }
    }

    /// <summary>
    /// Опис параметра заповнення (панель праворуч)
    /// </summary>
    public class AutoFillParameter
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

        public string BindAutoFillParam { get; set; }
        public bool IsRequired { get; set; }
        public bool IsReadOnly { get; set; } // New property
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
}