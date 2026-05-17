using FlexJournalPro.Models;
using FlexJournalPro.Services;
using MaterialDesignThemes.Wpf;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;

namespace FlexJournalPro.ViewModels.Screens
{
    /// <summary>
    /// Screen для редагування журналу
    /// </summary>
    public class JournalEditorScreen : ScreenBase
    {
        private readonly JournalMetadata _journal;
        private readonly IDatabaseService _dbService;
        private readonly MainViewModel _mainViewModel;
        private TableTemplate? _template;
        private Dictionary<string, object> _autoFillValues = new Dictionary<string, object>();
        public IDatabaseService DatabaseService => _dbService;

        public ICommand SaveConstantsCommand { get; }
        public ICommand SaveRowCommand { get; }

        public JournalEditorScreen(JournalMetadata journal, IDatabaseService dbService, MainViewModel mainViewModel)
        {
            _journal = journal;
            _dbService = dbService;
            _mainViewModel = mainViewModel;

            Title = journal.Title;
            Icon = PackIconKind.BookEdit;

            SaveConstantsCommand = new RelayCommand(SaveConstants);
            SaveRowCommand = new RelayCommand<BindableRow>(SaveRow);

            // Завантажуємо шаблон та дані
            LoadTemplate();
        }

        #region Properties

        /// <summary>
        /// Метадані журналу
        /// </summary>
        public JournalMetadata Journal => _journal;

        /// <summary>
        /// Шаблон журналу
        /// </summary>
        public TableTemplate? Template
        {
            get => _template;
            private set => SetProperty(ref _template, value);
        }

        /// <summary>
        /// Значення параметрів заповнення
        /// </summary>
        public Dictionary<string, object> AutoFillValues => _autoFillValues;

        public override string ScreenId => $"JournalEditor_{_journal.Id}";

        #endregion

        #region Methods

        private void LoadTemplate()
        {
            // Спроба завантажити конфігурацію зі зліпка (snapshot)
            if (!string.IsNullOrEmpty(_journal.TemplateConfigJson))
            {
                try
                {
                    Template = System.Text.Json.JsonSerializer.Deserialize<TableTemplate>(_journal.TemplateConfigJson);
                    
                    if (Template != null)
                    {
                        // Ensure system dynamic parameters are present (fixes older journals)
                        InjectSystemAutoFillParameters(Template);
                    }
                }
                catch (System.Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Помилка десеріалізації зліпка шаблону: {ex.Message}");
                }
            }

            // Відновлюємо параметри заповнення
            if (!string.IsNullOrEmpty(_journal.AutoFillConfigJson))
            {
                try
                {
                    var autoFillParams = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(_journal.AutoFillConfigJson);
                    if (autoFillParams != null)
                    {
                        _autoFillValues = new Dictionary<string, object>(autoFillParams);
                    }
                }
                catch (System.Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Помилка завантаження параметрів заповнення: {ex.Message}");
                }
            }
        }

        private void InjectSystemAutoFillParameters(TableTemplate template)
        {
            if (template == null) return;
            if (template.AutoFillConfig == null) template.AutoFillConfig = new List<AutoFillParameter>();

            var r = template.RegistrationParams;
            if (r == null) return;

            var parameters = template.AutoFillConfig;

            if (r.UseRegistration && r.UseNumberPrefix)
            {
                if (!parameters.Any(p => p.Key == "RegPrefix"))
                {
                    parameters.Insert(0, new AutoFillParameter
                    {
                        Key = "RegPrefix",
                        Label = "Префікс номера",
                        DefaultValue = "",
                        Type = ColumnType.Text
                    });
                }
            }
            if (r.UseRegistration && r.UseNumberSuffix)
            {
                if (!parameters.Any(p => p.Key == "RegSuffix"))
                {
                    parameters.Add(new AutoFillParameter
                    {
                        Key = "RegSuffix",
                        Label = "Суфікс номера",
                        DefaultValue = "",
                        Type = ColumnType.Text
                    });
                }
            }
        }

        public void SaveConstants()
        {
            try
            {
                string json = JsonSerializer.Serialize(AutoFillValues);
                _dbService.UpdateJournalAutoFillConfig(Journal.Id, json);

                // В ідеалі тут має бути _dialogService.ShowMessage(...), але поки залишимо MessageBox
                MessageBox.Show("Параметри збережено!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Помилка збереження параметрів: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void SaveRow(BindableRow rowData)
        {
            if (rowData == null || Template == null) return;

            try
            {
                _dbService.UpsertDictionaryRow(Journal.TableName, rowData, Template.Columns);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Помилка збереження: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public override void OnClosing()
        {
            // Можна додати логіку збереження перед закриттям
            base.OnClosing();
        }

        #endregion
    }
}
