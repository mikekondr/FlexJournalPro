using FlexJournalPro.Models;

namespace FlexJournalPro.Services
{
    public interface ITemplateService
    {
        void CreateTemplate(TableTemplate template);
        void UpdateTemplate(TableTemplate template);
        TableTemplate GetTemplate(string templateId);
        string GetTemplateJsonConfig(string templateId);
        List<TemplateMetadata> GetAllTemplates();
        void DeleteTemplate(string templateId);
        JournalMetadata CreateJournalFromTemplate(string templateId, string journalTitle);
        void ImportDefaultTemplates();
    }
}
