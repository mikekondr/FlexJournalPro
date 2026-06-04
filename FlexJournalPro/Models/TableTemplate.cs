using System.Text.Json.Serialization;

namespace FlexJournalPro.Models
{
    public class TableTemplate
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }

        public RegistrationParams RegistrationParams { get; set; } = new RegistrationParams();

        public List<AutoFillParameter> AutoFillConfig { get; set; } = new List<AutoFillParameter>();
        public List<ColumnConfig> Columns { get; set; } = new List<ColumnConfig>();
    }

    public class RegistrationParams
    {
        public bool UseRegistration { get; set; }
        public bool UseStrictNumbering { get; set; }
        public bool UseCustomStartNumber { get; set; }
        public bool UseLocking { get; set; }
        public bool UseNumberPrefix { get; set; }
        public bool UseNumberSuffix { get; set; }
    }

    public class AutoFillParameter
    {
        public string Key { get; set; }
        public string Label { get; set; }
        public ColumnType Type { get; set; }
        public object DefaultValue { get; set; }
        public List<string> Options { get; set; }
    }

    public class RecalculationRule
    {
        public string TargetColumn { get; set; }
        public string Expression { get; set; }
    }

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

        public string Expression { get; set; }

        public List<RecalculationRule> OnChange { get; set; }

        public string BindAutoFillParam { get; set; }
        public bool IsRequired { get; set; }
        public bool IsReadOnly { get; set; }
        public ColumnPosition Position { get; set; } = ColumnPosition.NewColumn;
    }

    public class ColumnHeaderConfig
    {
        public string Text { get; set; }
        public ColumnHeaderDirection Direction { get; set; }
        public double Size { get; set; }
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ColumnPosition
    {
        NewColumn,
        NextRow,
        SameColumn
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ColumnType
    {
        Text,
        Number,
        Currency,
        Boolean,
        Date,
        DateTime,
        Time,
        Dropdown,
        DropdownEditable,
        SectionHeader,
        RegNumber,
        Lock
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ColumnHeaderDirection
    {
        Normal,
        Vertical
    }

}