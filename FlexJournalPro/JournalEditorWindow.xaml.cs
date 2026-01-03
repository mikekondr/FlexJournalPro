using System;
using System.Collections.Generic;
using System.Data;
using System.Text.Json;
using System.Windows;
using FlexJournalPro.Models;
using FlexJournalPro.Services;
using FlexJournalPro.Helpers;
using FlexJournalPro.Controls;

namespace FlexJournalPro
{
    public partial class JournalEditorWindow : Window
    {
        private JournalMetadata _journal;
        private TableTemplate _template;
        private DatabaseService _dbService;
        private Dictionary<string, object> _sessionValues = new Dictionary<string, object>();

        public JournalEditorWindow(JournalMetadata journal, DatabaseService dbService)
        {
            InitializeComponent();
            _journal = journal;
            _dbService = dbService;

            this.Title = $"Журнал: {_journal.Title}";

            SmartTable.RowSaved += SmartTable_RowSaved;

            LoadData();
        }

        private void LoadData()
        {
            // 1. Завантажуємо шаблон з БД (з кешуванням)
            SmartTable.LoadTemplateFromDatabase(_dbService, _journal.PresetId);

            // Зберігаємо посилання на шаблон для подальшого використання
            _template = SmartTable.GetCurrentTemplate();

            // 2. Відновлюємо сеансові константи
            LoadSessionConstants();

            // 3. Будуємо UI панелі констант
            BuildConstantsPanel();

            // 4. Застосовуємо константи до UserControl
            SmartTable.ApplySessionValues(_sessionValues);

            // 5. Використовуємо ВІРТУАЛЬНИЙ метод завантаження
            SmartTable.SetVirtualDataSource(_dbService, _journal.TableName);
        }

        private void LoadSessionConstants()
        {
            _sessionValues.Clear();

            if (!string.IsNullOrEmpty(_journal.SessionConstantsJson))
            {
                try
                {
                    var constants = JsonSerializer.Deserialize<Dictionary<string, object>>(_journal.SessionConstantsJson);
                    if (constants != null)
                    {
                        foreach (var kvp in constants)
                        {
                            _sessionValues[kvp.Key] = kvp.Value;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Помилка завантаження констант: {ex.Message}");
                }
            }
        }

        private void BuildConstantsPanel()
        {
            if (_template?.Constants == null || _template.Constants.Count == 0)
            {
                // Ховаємо панель, якщо немає констант
                ConstantsColumn.Width = new GridLength(0);
                ToggleConstantsButton.Visibility = Visibility.Collapsed;
                return;
            }

            // Використовуємо helper для побудови UI
            SessionConstantsHelper.BuildConstantsPanel(
                ConstantsPanel, 
                _template.Constants, 
                _sessionValues,
                OnSessionValueChanged);
        }

        private void OnSessionValueChanged()
        {
            // Застосовуємо змінені значення до UserControl
            SmartTable.ApplySessionValues(_sessionValues);
        }

        private void BtnSaveConstants_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Серіалізуємо константи
                string json = JsonSerializer.Serialize(_sessionValues);

                // Зберігаємо в БД
                _dbService.UpdateJournalConstants(_journal.Id, json);

                // Оновлюємо локальну копію
                _journal.SessionConstantsJson = json;

                MessageBox.Show("Налаштування збережено!", "Успіх", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка збереження налаштувань: {ex.Message}", 
                    "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ToggleConstantsButton_CheckedChanged(object sender, RoutedEventArgs e)
        {
            // Перевірка на null для уникнення помилки під час ініціалізації
            if (ConstantsCard == null || ConstantsColumn == null)
                return;
                
            if (ToggleConstantsButton.IsChecked == true)
            {
                ConstantsColumn.Width = new GridLength(300);
                ConstantsCard.Visibility = Visibility.Visible;
            }
            else
            {
                ConstantsColumn.Width = new GridLength(0);
                ConstantsCard.Visibility = Visibility.Collapsed;
            }
        }

        private void SmartTable_RowSaved(object sender, RowSavedEventArgs e)
        {
            try
            {
                // Зберігаємо словник у БД
                _dbService.UpsertDictionaryRow(_journal.TableName, e.RowData, _template.Columns);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка збереження: {ex.Message}");
            }
        }
    }
}