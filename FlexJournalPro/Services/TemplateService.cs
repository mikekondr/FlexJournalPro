using System;
using System.Collections.Generic;
using System.Text.Json;
using FlexJournalPro.Models;
using FlexJournalPro.Views;

namespace FlexJournalPro.Services
{
    /// <summary>
    /// Сервіс для роботи з шаблонами (фасад для DatabaseService)
    /// </summary>
    public class TemplateService
    {
        private readonly DatabaseService _dbService;

        public TemplateService(DatabaseService dbService)
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

            // Очищаємо кеш для цього шаблону (на випадок оновлення)
            DynamicTableView.ClearTemplateCache(template.Id);
        }

        /// <summary>
        /// Оновити існуючий шаблон (створить нову версію)
        /// </summary>
        public void UpdateTemplate(TableTemplate template)
        {
            _dbService.SaveTemplate(template);

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

            var journal = new JournalMetadata
            {
                Title = journalTitle,
                PresetId = templateId,
                NumberStart = 1,
                SessionConstantsJson = "{}"
            };

            _dbService.CreateNewJournal(journal, template.Columns);

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
    }
}
