using System;

namespace FlexJournalPro.Models
{
    /// <summary>
    /// Метадані журналу (запис у реєстрі)
    /// </summary>
    public class JournalMetadata
    {
        public long Id { get; set; }
        public string Title { get; set; }          // Назва журналу
        public string TemplateId { get; set; }     // ID шаблону
        public string TemplateName { get; set; }   // Назва шаблону
        public int TemplateVersion { get; set; }   // Версія шаблону
        public string TableName { get; set; }      // Фізична назва таблиці в SQLite
        public DateTime CreatedAt { get; set; }

        public string TemplateConfigJson { get; set; } // Зліпок конфігурації шаблону

        // Налаштування нумерації
        public long NumberStart { get; set; }

        // Збережені константи (JSON рядок)
        public string AutoFillConfigJson { get; set; }
    }

    /// <summary>
    /// Метадані шаблону (зберігається в БД)
    /// </summary>
    public class TemplateMetadata
    {
        public string Id { get; set; }             // Унікальний ID шаблону
        public string Name { get; set; }           // Назва шаблону
        public string Description { get; set; }    // Опис шаблону
        
        // Поле JsonConfig видалено для оптимізації. 
        // Використовуйте TemplateService.GetTemplateJsonConfig(id)

        public int Version { get; set; }           // Версія шаблону
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsActive { get; set; }         // Чи активний шаблон
    }
}