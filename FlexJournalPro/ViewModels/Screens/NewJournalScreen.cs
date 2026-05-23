using FlexJournalPro.Models;
using FlexJournalPro.Services;
using MaterialDesignThemes.Wpf;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows.Input;

namespace FlexJournalPro.ViewModels.Screens
{
    /// <summary>
    /// Screen для створення нового журналу
    /// </summary>
    public class NewJournalScreen : ScreenBase
    {
        private readonly IDatabaseService _dbService;
        private readonly MainViewModel _mainViewModel;
        private TemplateMetadata? _selectedTemplate;
        private string _journalTitle = string.Empty;
        private long _startNumber = 1;
        private bool _isStartNumberEnabled = true;

        private TableTemplate? _currentConfiguredTemplate;

        public NewJournalScreen(IDatabaseService dbService, MainViewModel mainViewModel)
        {
            _dbService = dbService;
            _mainViewModel = mainViewModel;

            Title = "Новий журнал";
            Icon = PackIconKind.BookPlus;

            Templates = new ObservableCollection<TemplateMetadata>();
            AutoFillValues = new Dictionary<string, object>();

            // Команди
            CreateJournalCommand = new RelayCommand(CreateJournal, CanCreateJournal);
            CancelCommand = new RelayCommand(Cancel);

            // Завантажуємо шаблони
            LoadTemplates();
        }

        #region Properties

        /// <summary>
        /// Доступні шаблони
        /// </summary>
        public ObservableCollection<TemplateMetadata> Templates { get; }

        /// <summary>
        /// Обраний шаблон
        /// </summary>
        public TemplateMetadata? SelectedTemplate
        {
            get => _selectedTemplate;
            set
            {
                if (SetProperty(ref _selectedTemplate, value))
                {
                    OnTemplateSelected();
                    (CreateJournalCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// Назва журналу
        /// </summary>
        public string JournalTitle
        {
            get => _journalTitle;
            set
            {
                if (SetProperty(ref _journalTitle, value))
                {
                    (CreateJournalCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// Початковий номер
        /// </summary>
        public long StartNumber
        {
            get => _startNumber;
            set => SetProperty(ref _startNumber, value);
        }

        /// <summary>
        /// Чи можна змінювати початковий номер
        /// </summary>
        public bool IsStartNumberEnabled
        {
            get => _isStartNumberEnabled;
            set => SetProperty(ref _isStartNumberEnabled, value);
        }

        /// <summary>
        /// Значення параметрів заповнення
        /// </summary>
        public Dictionary<string, object> AutoFillValues { get; }

        /// <summary>
        /// Чи є параметри заповнення для відображення
        /// </summary>
        public bool HasAutoFillParams => CurrentConfiguredTemplate?.AutoFillConfig?.Any() == true;

        public override string ScreenId => "NewJournal";

        public TableTemplate? CurrentConfiguredTemplate
        {
            get => _currentConfiguredTemplate;
            set
            {
                if (SetProperty(ref _currentConfiguredTemplate, value))
                {
                    OnPropertyChanged(nameof(HasAutoFillParams));
                }
            }
        }

        #endregion

        #region Commands

        public ICommand CreateJournalCommand { get; }
        public ICommand CancelCommand { get; }

        #endregion

        #region Command Handlers

        private void LoadTemplates()
        {
            try
            {
                var templates = _dbService.GetAllTemplates();
                Templates.Clear();
                foreach (var template in templates)
                {
                    Templates.Add(template);
                }

                if (templates.Count == 0)
                {
                    _ = DialogService.ShowInformationAsync(
                        "В базі даних немає доступних шаблонів.\nСпочатку створіть або імпортуйте шаблони.",
                        "Немає шаблонів");
                }
            }
            catch (Exception ex)
            {
                _ = DialogService.ShowErrorAsync($"Помилка завантаження шаблонів: {ex.Message}");
            }
        }

        private void OnTemplateSelected()
        {
            if (SelectedTemplate == null)
            {
                AutoFillValues.Clear();
                OnPropertyChanged(nameof(HasAutoFillParams));
                return;
            }

            try
            {
                // Завантажуємо повний шаблон з БД
                var template = _dbService.GetTemplate(SelectedTemplate.Id);

                if (template != null)
                {
                    // Генеруємо назву за замовчуванням
                    JournalTitle = template.Title + " " + DateTime.Now.ToString("yyyy-MM");

                    // Налаштування реєстраційних параметрів
                    ApplyRegistrationParams(template.RegistrationParams);

                    // Будуємо форму параметрів заповнення
                    BuildAutoFillForm(template);
                }
            }
            catch (Exception ex)
            {
                _ = DialogService.ShowErrorAsync($"Помилка завантаження шаблонів: {ex.Message}");
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

        private void ApplyRegistrationParams(RegistrationParams regParams)
        {
            if (regParams != null)
            {
                IsStartNumberEnabled = regParams.UseCustomStartNumber || !regParams.UseRegistration;
                // Якщо не використовуємо реєстрацію або кастомний старт - скидаємо на 1
                if (!IsStartNumberEnabled)
                {
                    StartNumber = 1;
                }
            }
            else
            {
                IsStartNumberEnabled = true;
            }
        }

        private void BuildAutoFillForm(TableTemplate template)
        {
            // Inject dynamic params before building
            InjectSystemAutoFillParameters(template);

            AutoFillValues.Clear();

            CurrentConfiguredTemplate = template;

        }

        private bool CanCreateJournal()
        {
            return SelectedTemplate != null && !string.IsNullOrWhiteSpace(JournalTitle);
        }

        private async void CreateJournal()
        {
            if (SelectedTemplate == null || string.IsNullOrWhiteSpace(JournalTitle))
            {
                await DialogService.ShowWarningAsync("Заповніть всі поля!", "Помилка");
                return;
            }

            try
            {
                // Завантажуємо повний шаблон
                var template = _dbService.GetTemplate(SelectedTemplate.Id);
                if (template == null)
                {
                    await DialogService.ShowErrorAsync("Не вдалося завантажити шаблон!");
                    return;
                }

                // Inject dynamic AutoFill params (RegPrefix, RegSuffix) before serialization
                InjectSystemAutoFillParameters(template);

                // Ін'єкція системних колонок
                InjectSystemColumns(template);

                // Створюємо метадані журналу
                var metadata = new JournalMetadata
                {
                    Title = JournalTitle.Trim(),
                    TemplateId = template.Id,
                    TemplateName = template.Title,
                    TemplateVersion = SelectedTemplate.Version, // Використовуємо версію з метаданих
                    NumberStart = StartNumber > 0 ? StartNumber : 1,
                    AutoFillConfigJson = AutoFillValues.Count > 0
                        ? JsonSerializer.Serialize(AutoFillValues)
                        : "{}",
                    TemplateConfigJson = JsonSerializer.Serialize(template)
                };

                // Зберігаємо в БД
                _dbService.CreateNewJournal(metadata, template.Columns);

                // Оновлюємо список журналів, якщо він відкритий
                var journalsListScreen = _mainViewModel.OpenScreens
                    .OfType<JournalsListScreen>()
                    .FirstOrDefault();

                if (journalsListScreen != null)
                {
                    journalsListScreen.RefreshCommand.Execute(null);
                }

                // Закриваємо цей екран
                _mainViewModel.CloseScreenCommand.Execute(this);
            }
            catch (Exception ex)
            {
                await DialogService.ShowErrorAsync($"Помилка створення журналу: {ex.Message}");
            }
        }

        private void InjectSystemColumns(TableTemplate template)
        {
            var r = template.RegistrationParams;
            if (r == null) return;

            // 1. RegPrefix
            if (r.UseRegistration && r.UseNumberPrefix)
            {
                if (!template.Columns.Any(c => c.FieldName == "RegPrefix"))
                {
                    // Вставляємо перед RegNumber або на початок
                    int index = 0;
                    var regNumCol = template.Columns.FirstOrDefault(c => c.Type == ColumnType.RegNumber || c.FieldName == "RegNumber");
                    if (regNumCol != null) index = template.Columns.IndexOf(regNumCol);

                    template.Columns.Insert(index, new ColumnConfig
                    {
                        FieldName = "RegPrefix",
                        HeaderText = "Префікс",
                        Type = ColumnType.Text,
                        Width = 60,
                        BindAutoFillParam = "RegPrefix",
                        Position = ColumnPosition.NewColumn
                    });
                }
            }

            // 2. RegNumber
            if (r.UseRegistration)
            {
                if (!template.Columns.Any(c => c.Type == ColumnType.RegNumber || c.FieldName == "RegNumber"))
                {
                    // Якщо немає, додаємо на першу позицію (після префікса, якщо він був доданий щойно)
                    int index = 0;
                    if (r.UseNumberPrefix && template.Columns.Count > 0 && template.Columns[0].FieldName == "RegPrefix")
                        index = 1;

                    template.Columns.Insert(index, new ColumnConfig
                    {
                        FieldName = "RegNumber",
                        HeaderText = "№ з/п",
                        Type = ColumnType.RegNumber,
                        Width = 60,
                        Position = index == 1 ? ColumnPosition.SameColumn : ColumnPosition.NewColumn
                    });
                }
                else
                {
                    // Ensure the existing column has correct type if it is named RegNumber
                    var col = template.Columns.FirstOrDefault(c => c.FieldName == "RegNumber");
                    if (col != null && col.Type != ColumnType.RegNumber)
                        col.Type = ColumnType.RegNumber;
                }
            }

            // 3. RegSuffix
            if (r.UseRegistration && r.UseNumberSuffix)
            {
                if (!template.Columns.Any(c => c.FieldName == "RegSuffix"))
                {
                    int index = template.Columns.Count;
                    var regNumCol = template.Columns.FirstOrDefault(c => c.Type == ColumnType.RegNumber || c.FieldName == "RegNumber");
                    if (regNumCol != null)
                        index = template.Columns.IndexOf(regNumCol) + 1;

                    template.Columns.Insert(index, new ColumnConfig
                    {
                        FieldName = "RegSuffix",
                        HeaderText = "Суфікс",
                        Type = ColumnType.Text,
                        Width = 60,
                        BindAutoFillParam = "RegSuffix",
                        Position = ColumnPosition.SameColumn
                    });
                }
            }

            // 4. Lock Column
            if (r.UseLocking)
            {
                if (!template.Columns.Any(c => c.Type == ColumnType.Lock || c.FieldName == "IsLocked"))
                {
                    template.Columns.Add(new ColumnConfig
                    {
                        FieldName = "IsLocked",
                        HeaderText = "Блок",
                        Type = ColumnType.Lock,
                        Width = 50,
                        Position = ColumnPosition.NewColumn
                    });
                }
            }
        }

        private void Cancel()
        {
            _mainViewModel.CloseScreenCommand.Execute(this);
        }

        #endregion
    }

}
