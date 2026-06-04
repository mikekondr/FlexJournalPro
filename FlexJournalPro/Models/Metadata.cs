namespace FlexJournalPro.Models
{
    /// <summary>
    /// Метадані журналу (запис у реєстрі журналів)
    /// </summary>
    public class JournalMetadata
    {
        public long Id { get; set; }
        public string Title { get; set; }
        public string TemplateId { get; set; }
        public string TemplateName { get; set; }
        public int TemplateVersion { get; set; }
        public string TableName { get; set; }
        public DateTime CreatedAt { get; set; }
        public string TemplateConfigJson { get; set; }
        public long NumberStart { get; set; }
        public string AutoFillConfigJson { get; set; }
    }

    /// <summary>
    /// Метадані шаблону
    /// </summary>
    public class TemplateMetadata
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int Version { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsActive { get; set; }
    }
}