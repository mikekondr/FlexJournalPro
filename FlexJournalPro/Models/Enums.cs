using System.Text.Json.Serialization;

namespace FlexJournalPro.Models
{
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
        Date,           // Тільки дата (dd.MM.yyyy)
        DateTime,       // Дата і час (dd.MM.yyyy HH:mm)
        Time,           // Тільки час (HH:mm)
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
}