using FlexJournalPro.Models;
using FlexJournalPro.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FlexJournalPro
{
    public partial class MainWindow : Window
    {
        private DatabaseService _dbService;

        public MainWindow()
        {
            InitializeComponent();
            _dbService = new DatabaseService();
            
            // Імпортуємо шаблони з JSON файлів у БД (якщо є)
            ImportTemplatesFromJsonFiles();
            
            RefreshJournals();
        }

        /// <summary>
        /// Імпортує шаблони з JSON файлів у БД (виконується один раз при першому запуску)
        /// </summary>
        private void ImportTemplatesFromJsonFiles()
        {
            string presetsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Presets");

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
                        // Встановлюємо ID якщо не вказано
                        if (string.IsNullOrEmpty(template.Id))
                        {
                            template.Id = key;
                        }

                        // Перевіряємо, чи вже є такий шаблон у БД
                        var existing = _dbService.GetTemplate(template.Id);
                        if (existing == null)
                        {
                            _dbService.SaveTemplate(template);
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

        private void RefreshJournals()
        {
            var journals = _dbService.GetAllJournals();
            GridJournals.ItemsSource = journals;
        }

        private void BtnCreateNew_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new NewJournalDialog(_dbService);
            if (dialog.ShowDialog() == true)
            {
                var meta = dialog.ResultMetadata;
                var template = dialog.SelectedTemplate;

                try
                {
                    // Створюємо в БД
                    _dbService.CreateNewJournal(meta, template.Columns);
                    RefreshJournals();
                    
                    MessageBox.Show($"Журнал '{meta.Title}' успішно створено!", 
                        "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Помилка створення журналу: {ex.Message}", 
                        "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void GridJournals_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (GridJournals.SelectedItem is JournalMetadata journal)
            {
                OpenJournal(journal);
            }
        }

        private void OpenJournal(JournalMetadata journal)
        {
            // Відкриваємо вікно редагування з автоматичним завантаженням шаблону з БД
            try
            {
                var editor = new JournalEditorWindow(journal, _dbService);
                editor.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка відкриття журналу: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnManageTemplates_Click(object sender, RoutedEventArgs e)
        {
            var window = new TemplateManagerWindow(_dbService);
            window.ShowDialog();
            
            // Оновлюємо список журналів після закриття менеджера шаблонів
            RefreshJournals();
        }
    }
}