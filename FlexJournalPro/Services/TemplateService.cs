using FlexJournalPro.Models;
using FlexJournalPro.Views;
using System.IO;
using System.Reflection;
using System.Text.Json;

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

    /// <summary>
    /// Сервіс для роботи з шаблонами (фасад для SqliteDatabaseService)
    /// </summary>
    public class TemplateService : ITemplateService
    {
        private readonly IDatabaseService _dbService;

        public TemplateService(IDatabaseService dbService)
        {
            _dbService = dbService;
        }

        /// <summary>
        /// Створити та зберегти новий шаблон
        /// </summary>
        public void CreateTemplate(TableTemplate template)
        {
            if (string.IsNullOrEmpty(template.Id))
            {
                template.Id = GenerateTemplateId(template.Title);
            }

            _dbService.SaveTemplate(template);

            AppLogger.LogSystemInfo(LogAction.TemplateCreated, $"Додано новий шаблон: {template.Title}");

            // Очищаємо кеш для цього шаблону (на випадок оновлення)
            DynamicTableView.ClearTemplateCache(template.Id);
        }

        /// <summary>
        /// Оновити існуючий шаблон (створить нову версію)
        /// </summary>
        public void UpdateTemplate(TableTemplate template)
        {
            _dbService.SaveTemplate(template);

            AppLogger.LogSystemInfo(LogAction.TemplateUpdated, $"Оновлено шаблон: {template.Title}");

            // Очищаємо кеш, щоб застосувати зміни
            DynamicTableView.ClearTemplateCache(template.Id);
        }

        /// <summary>
        /// Отримати шаблон за ID
        /// </summary>
        public TableTemplate GetTemplate(string templateId)
        {
            return _dbService.GetTemplate(templateId);
        }

        /// <summary>
        /// Отримати чистий JSON конфігурації шаблону без десеріалізації.
        /// Корисно для копіювання, експорту або створення журналів.
        /// </summary>
        public string GetTemplateJsonConfig(string templateId)
        {
            return _dbService.GetTemplateJson(templateId);
        }

        /// <summary>
        /// Отримати список усіх шаблонів
        /// </summary>
        public List<TemplateMetadata> GetAllTemplates()
        {
            return _dbService.GetAllTemplates();
        }

        /// <summary>
        /// Видалити шаблон (soft delete)
        /// </summary>
        public void DeleteTemplate(string templateId)
        {
            _dbService.DeactivateTemplate(templateId);

            AppLogger.LogSystemInfo(LogAction.TemplateDeleted, $"Деактивовано шаблон: {templateId}");

            // Очищаємо кеш
            DynamicTableView.ClearTemplateCache(templateId);
        }

        /// <summary>
        /// Створити журнал на основі шаблону
        /// </summary>
        public JournalMetadata CreateJournalFromTemplate(string templateId, string journalTitle)
        {
            var template = _dbService.GetTemplate(templateId);
            if (template == null)
            {
                throw new InvalidOperationException($"Шаблон '{templateId}' не знайдено");
            }

            // Отримуємо версію шаблону
            int version = 1;
            var allTemplates = _dbService.GetAllTemplates();
            var meta = allTemplates.FirstOrDefault(t => t.Id == templateId);
            if (meta != null)
            {
                version = meta.Version;
            }

            var journal = new JournalMetadata
            {
                Title = journalTitle,
                TemplateId = templateId,
                TemplateName = template.Title,
                TemplateVersion = version,
                NumberStart = 1,
                AutoFillConfigJson = "{}",
                TemplateConfigJson = JsonSerializer.Serialize(template)
            };

            _dbService.CreateNewJournal(journal, template.Columns);

            AppLogger.LogSystemInfo(LogAction.JournalCreated, $"Створено журнал '{journalTitle}' на основі шаблону '{template.Title}'");

            return journal;
        }

        /// <summary>
        /// Генерує унікальний ID для шаблону
        /// </summary>
        private string GenerateTemplateId(string title)
        {
            // Прибираємо спецсимволи та робимо lowercase
            string cleanTitle = System.Text.RegularExpressions.Regex.Replace(title, @"[^a-zA-Z0-9_]", "").ToLower();

            // Додаємо timestamp для унікальності
            string timestamp = DateTime.Now.Ticks.ToString().Substring(8);

            return $"{cleanTitle}_{timestamp}";
        }

        /// <summary>
        /// Сканує папку Templates у документах та імпортує нові шаблони
        /// </summary>
        public void ImportDefaultTemplates()
        {
            string presetsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Assembly.GetExecutingAssembly().GetName().Name ?? "FlexJournalPro", "Templates");

            if (!Directory.Exists(presetsPath))
            {
                Directory.CreateDirectory(presetsPath);
                return;
            }

            foreach (string filePath in Directory.GetFiles(presetsPath, "*.json"))
            {
                try
                {
                    string jsonContent = File.ReadAllText(filePath);
                    var template = JsonSerializer.Deserialize<TableTemplate>(jsonContent);
                    string key = Path.GetFileNameWithoutExtension(filePath);

                    if (template != null)
                    {
                        if (string.IsNullOrEmpty(template.Id)) template.Id = key;

                        // Перевіряємо наявність
                        if (GetTemplate(template.Id) == null)
                        {
                            CreateTemplate(template);
                            System.Diagnostics.Debug.WriteLine($"Імпортовано шаблон: {template.Id}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Помилка імпорту {Path.GetFileName(filePath)}: {ex.Message}");
                }
            }
        }
    }
}
