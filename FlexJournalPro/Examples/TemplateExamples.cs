using System;
using System.Collections.Generic;
using FlexJournalPro.Models;
using FlexJournalPro.Services;

namespace FlexJournalPro.Examples
{
    /// <summary>
    /// Приклади використання системи шаблонів
    /// </summary>
    public static class TemplateExamples
    {
        /// <summary>
        /// Приклад 1: Створення простого шаблону накладної
        /// </summary>
        public static TableTemplate CreateInvoiceTemplate()
        {
            return new TableTemplate
            {
                Id = "invoice_simple",
                Title = "Проста накладна",
                Constants = new List<SessionConstant>
                {
                    new SessionConstant
                    {
                        Key = "CompanyName",
                        Label = "Назва компанії",
                        Type = ColumnType.Text,
                        DefaultValue = "ТОВ Приклад"
                    },
                    new SessionConstant
                    {
                        Key = "InvoiceDate",
                        Label = "Дата накладної",
                        Type = ColumnType.Date,
                        DefaultValue = "NOW"
                    }
                },
                Columns = new List<ColumnConfig>
                {
                    new ColumnConfig
                    {
                        FieldName = "Number",
                        HeaderText = "№",
                        Type = ColumnType.Number,
                        Width = 50,
                        IsRequired = true,
                        Position = ColumnPosition.NewColumn
                    },
                    new ColumnConfig
                    {
                        FieldName = "ProductName",
                        HeaderText = "Найменування товару",
                        Type = ColumnType.Text,
                        Width = 250,
                        IsRequired = true,
                        Position = ColumnPosition.NewColumn
                    },
                    new ColumnConfig
                    {
                        FieldName = "Quantity",
                        HeaderText = "Кількість",
                        Type = ColumnType.Number,
                        Width = 80,
                        DefaultValue = 1,
                        Position = ColumnPosition.NewColumn
                    },
                    new ColumnConfig
                    {
                        FieldName = "Price",
                        HeaderText = "Ціна",
                        Type = ColumnType.Currency,
                        Width = 100,
                        Format = "{0:C2}",
                        Position = ColumnPosition.NewColumn
                    },
                    new ColumnConfig
                    {
                        FieldName = "Total",
                        HeaderText = "Сума",
                        Type = ColumnType.Currency,
                        Width = 120,
                        Format = "{0:C2}",
                        Expression = "Quantity * Price",  // Автоматичний розрахунок
                        Position = ColumnPosition.NewColumn
                    }
                }
            };
        }

        /// <summary>
        /// Приклад 2: Шаблон з групованими колонками
        /// </summary>
        public static TableTemplate CreateGroupedTemplate()
        {
            return new TableTemplate
            {
                Id = "invoice_grouped",
                Title = "Накладна з групуванням",
                Columns = new List<ColumnConfig>
                {
                    // Секція "Інформація про товар"
                    new ColumnConfig
                    {
                        FieldName = "ProductInfo",
                        HeaderText = "Інформація про товар",
                        Type = ColumnType.SectionHeader,
                        Position = ColumnPosition.NewColumn
                    },
                    new ColumnConfig
                    {
                        FieldName = "ProductCode",
                        HeaderText = "Код",
                        Type = ColumnType.Text,
                        Width = 80,
                        Position = ColumnPosition.NextRow
                    },
                    new ColumnConfig
                    {
                        FieldName = "ProductName",
                        HeaderText = "Назва",
                        Type = ColumnType.Text,
                        Width = 150,
                        Position = ColumnPosition.SameColumn
                    },

                    // Секція "Кількість та ціна"
                    new ColumnConfig
                    {
                        FieldName = "PriceInfo",
                        HeaderText = "Кількість та ціна",
                        Type = ColumnType.SectionHeader,
                        Position = ColumnPosition.NewColumn
                    },
                    new ColumnConfig
                    {
                        FieldName = "Quantity",
                        HeaderText = "Кількість",
                        Type = ColumnType.Number,
                        Width = 80,
                        Position = ColumnPosition.NextRow
                    },
                    new ColumnConfig
                    {
                        FieldName = "Price",
                        HeaderText = "Ціна",
                        Type = ColumnType.Currency,
                        Width = 100,
                        Format = "{0:C2}",
                        Position = ColumnPosition.SameColumn
                    },

                    // Підсумок
                    new ColumnConfig
                    {
                        FieldName = "Total",
                        HeaderText = "Разом",
                        Type = ColumnType.Currency,
                        Width = 120,
                        Format = "{0:C2}",
                        Expression = "Quantity * Price",
                        Position = ColumnPosition.NewColumn
                    }
                }
            };
        }

        /// <summary>
        /// Приклад 3: Тестування системи шаблонів
        /// </summary>
        public static void TestTemplateSystem()
        {
            var dbService = new DatabaseService();
            var templateService = new TemplateService(dbService);

            // 1. Створюємо шаблон
            var template = CreateInvoiceTemplate();
            templateService.CreateTemplate(template);
            Console.WriteLine($"? Створено шаблон: {template.Id} (Version 1)");

            // 2. Оновлюємо шаблон
            template.Columns.Add(new ColumnConfig
            {
                FieldName = "Notes",
                HeaderText = "Примітки",
                Type = ColumnType.Text,
                Width = 200,
                Position = ColumnPosition.NewColumn
            });
            templateService.UpdateTemplate(template);
            Console.WriteLine($"? Оновлено шаблон: {template.Id} (Version 2)");

            // 3. Створюємо журнал
            var journal = templateService.CreateJournalFromTemplate(
                template.Id,
                "Накладні за грудень 2024"
            );
            Console.WriteLine($"? Створено журнал: {journal.Title} (ID: {journal.Id})");

            // 4. Перевіряємо кеш
            Console.WriteLine("\n?? Тестування кешування:");
            
            // Перше завантаження
            var start1 = DateTime.Now;
            var loaded1 = dbService.GetTemplate(template.Id);
            var time1 = (DateTime.Now - start1).TotalMilliseconds;
            Console.WriteLine($"Перше завантаження: {time1:F2} мс");

            // Друге завантаження (має бути швидше через кеш)
            var start2 = DateTime.Now;
            var loaded2 = dbService.GetTemplate(template.Id);
            var time2 = (DateTime.Now - start2).TotalMilliseconds;
            Console.WriteLine($"Друге завантаження: {time2:F2} мс");

            Console.WriteLine($"\n? Покращення: {(time1 / time2):F0}x швидше");

            // 5. Перелік шаблонів
            var allTemplates = templateService.GetAllTemplates();
            Console.WriteLine($"\n?? Всього шаблонів у БД: {allTemplates.Count}");
            foreach (var t in allTemplates)
            {
                Console.WriteLine($"  - {t.Name} (ID: {t.Id}, Version: {t.Version})");
            }
        }

        /// <summary>
        /// Приклад 4: Міграція шаблонів з JSON файлів
        /// </summary>
        public static void MigrateJsonTemplates(string folderPath)
        {
            var dbService = new DatabaseService();
            var templateService = new TemplateService(dbService);

            int imported = 0;
            var files = System.IO.Directory.GetFiles(folderPath, "*.json");

            foreach (var file in files)
            {
                try
                {
                    var json = System.IO.File.ReadAllText(file);
                    var template = System.Text.Json.JsonSerializer.Deserialize<TableTemplate>(json);

                    if (template != null)
                    {
                        templateService.CreateTemplate(template);
                        imported++;
                        Console.WriteLine($"? Імпортовано: {template.Title} ({template.Id})");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"? Помилка імпорту {System.IO.Path.GetFileName(file)}: {ex.Message}");
                }
            }

            Console.WriteLine($"\n?? Загалом імпортовано: {imported} шаблонів");
        }
    }
}
