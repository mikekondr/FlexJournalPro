using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using FlexJournalPro.Models;
using FlexJournalPro.Services;

namespace FlexJournalPro
{
    public partial class NewJournalDialog : Window
    {
        public JournalMetadata ResultMetadata { get; private set; }
        public TableTemplate SelectedTemplate { get; private set; }
        public Dictionary<string, object> SessionValues { get; private set; } = new Dictionary<string, object>();

        private List<TemplateMetadata> _templates;
        private DatabaseService _dbService;

        public NewJournalDialog(DatabaseService dbService)
        {
            InitializeComponent();
            _dbService = dbService;
            LoadTemplatesFromDatabase();
        }

        private void LoadTemplatesFromDatabase()
        {
            try
            {
                _templates = _dbService.GetAllTemplates();
                
                if (_templates.Count == 0)
                {
                    MessageBox.Show("В базі даних немає доступних шаблонів.\nСпочатку створіть або імпортуйте шаблони.", 
                        "Немає шаблонів", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                // Прив'язуємо до ComboBox
                ComboPresets.ItemsSource = _templates;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка завантаження шаблонів: {ex.Message}", 
                    "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ComboPresets_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComboPresets.SelectedItem is TemplateMetadata templateMeta)
            {
                try
                {
                    // Завантажуємо повний шаблон з БД
                    SelectedTemplate = _dbService.GetTemplate(templateMeta.Id);
                    
                    if (SelectedTemplate != null)
                    {
                        TxtTitle.Text = SelectedTemplate.Title + " " + DateTime.Now.ToString("yyyy-MM");
                        BuildConstantsForm(SelectedTemplate.Constants);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Помилка завантаження шаблону: {ex.Message}", 
                        "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Спрощена генерація UI для констант
        private void BuildConstantsForm(List<SessionConstant> constants)
        {
            PanelConstants.Children.Clear();
            SessionValues.Clear();

            if (constants == null || constants.Count == 0)
            {
                return;
            }

            foreach (var constant in constants)
            {
                var tb = new TextBox { Margin = new Thickness(0, 5, 0, 10) };
                MaterialDesignThemes.Wpf.HintAssist.SetHint(tb, constant.Label);

                if (constant.DefaultValue != null)
                {
                    tb.Text = constant.DefaultValue.ToString();
                    SessionValues[constant.Key] = tb.Text;
                }

                tb.TextChanged += (s, ev) => SessionValues[constant.Key] = tb.Text;
                PanelConstants.Children.Add(tb);
            }
        }

        private void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedTemplate == null || string.IsNullOrWhiteSpace(TxtTitle.Text))
            {
                MessageBox.Show("Заповніть всі поля!", "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            long.TryParse(TxtStartNumber.Text, out long startNum);
            if (startNum <= 0) startNum = 1;

            ResultMetadata = new JournalMetadata
            {
                Title = TxtTitle.Text.Trim(),
                PresetId = SelectedTemplate.Id,  // ID шаблону з БД
                NumberStart = startNum,
                SessionConstantsJson = SessionValues.Count > 0 
                    ? JsonSerializer.Serialize(SessionValues) 
                    : "{}"
            };

            DialogResult = true;
        }
    }
}