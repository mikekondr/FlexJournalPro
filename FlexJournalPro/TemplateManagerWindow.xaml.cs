using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using FlexJournalPro.Models;
using FlexJournalPro.Services;
using FlexJournalPro.Controls;

namespace FlexJournalPro
{
    /// <summary>
    /// Вікно управління шаблонами
    /// </summary>
    public partial class TemplateManagerWindow : Window
    {
        private DatabaseService _dbService;
        private List<TemplateMetadata> _templates;

        public TemplateManagerWindow(DatabaseService dbService)
        {
            InitializeComponent();
            _dbService = dbService;
            LoadTemplates();
        }

        private void LoadTemplates()
        {
            _templates = _dbService.GetAllTemplates();
            GridTemplates.ItemsSource = _templates;
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadTemplates();
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (GridTemplates.SelectedItem is TemplateMetadata template)
            {
                var result = MessageBox.Show(
                    $"Видалити шаблон '{template.Name}' (версія {template.Version})?\n\nУвага: існуючі журнали можуть перестати працювати!",
                    "Підтвердження видалення",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        _dbService.DeactivateTemplate(template.Id);
                        DynamicTableView.ClearTemplateCache(template.Id);
                        LoadTemplates();
                        MessageBox.Show("Шаблон деактивовано", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Помилка: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void BtnViewDetails_Click(object sender, RoutedEventArgs e)
        {
            if (GridTemplates.SelectedItem is TemplateMetadata meta)
            {
                var template = _dbService.GetTemplate(meta.Id);
                if (template != null)
                {
                    string details = $"ID: {meta.Id}\n" +
                                   $"Назва: {meta.Name}\n" +
                                   $"Версія: {meta.Version}\n" +
                                   $"Створено: {meta.CreatedAt:dd.MM.yyyy HH:mm}\n" +
                                   $"Оновлено: {meta.UpdatedAt:dd.MM.yyyy HH:mm}\n" +
                                   $"Колонок: {template.Columns.Count}\n" +
                                   $"Констант: {template.Constants?.Count ?? 0}";

                    MessageBox.Show(details, "Деталі шаблону", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void BtnImportJson_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON файли (*.json)|*.json",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                int imported = 0;
                foreach (var file in dialog.FileNames)
                {
                    try
                    {
                        var json = System.IO.File.ReadAllText(file);
                        var template = System.Text.Json.JsonSerializer.Deserialize<TableTemplate>(json);

                        if (template != null)
                        {
                            if (string.IsNullOrEmpty(template.Id))
                            {
                                template.Id = System.IO.Path.GetFileNameWithoutExtension(file);
                            }

                            _dbService.SaveTemplate(template);
                            imported++;
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Помилка імпорту {System.IO.Path.GetFileName(file)}: {ex.Message}",
                            "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }

                LoadTemplates();
                MessageBox.Show($"Імпортовано шаблонів: {imported}", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnExportJson_Click(object sender, RoutedEventArgs e)
        {
            if (GridTemplates.SelectedItem is TemplateMetadata meta)
            {
                var template = _dbService.GetTemplate(meta.Id);
                if (template != null)
                {
                    var dialog = new Microsoft.Win32.SaveFileDialog
                    {
                        Filter = "JSON файли (*.json)|*.json",
                        FileName = $"{meta.Id}.json"
                    };

                    if (dialog.ShowDialog() == true)
                    {
                        try
                        {
                            var json = System.Text.Json.JsonSerializer.Serialize(template, new System.Text.Json.JsonSerializerOptions
                            {
                                WriteIndented = true
                            });
                            System.IO.File.WriteAllText(dialog.FileName, json);
                            MessageBox.Show("Шаблон експортовано", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Помилка експорту: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
