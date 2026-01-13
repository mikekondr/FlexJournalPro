using FlexJournalPro.Models;
using FlexJournalPro.Services;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Windows.Input;

namespace FlexJournalPro.ViewModels.Screens
{
    /// <summary>
    /// Screen для створення нового журналу
    /// </summary>
    public class NewJournalScreen : ScreenBase
    {
        private readonly DatabaseService _dbService;
        private readonly MainViewModel _mainViewModel;
        private TemplateMetadata? _selectedTemplate;
        private string _journalTitle = string.Empty;
        private long _startNumber = 1;

        public NewJournalScreen(DatabaseService dbService, MainViewModel mainViewModel)
        {
            _dbService = dbService;
            _mainViewModel = mainViewModel;

            Title = "Новий журнал";
            Icon = PackIconKind.BookPlus;

            Templates = new ObservableCollection<TemplateMetadata>();
            AutoFillFields = new ObservableCollection<AutoFillFieldViewModel>();
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
        /// Поля параметрів заповнення для відображення
        /// </summary>
        public ObservableCollection<AutoFillFieldViewModel> AutoFillFields { get; }

        /// <summary>
        /// Значення параметрів заповнення
        /// </summary>
        public Dictionary<string, object> AutoFillValues { get; }

        /// <summary>
        /// Чи є параметри заповнення для відображення
        /// </summary>
        public bool HasAutoFillParams => AutoFillFields.Any();

        public override string ScreenId => "NewJournal";

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
                AutoFillFields.Clear();
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

                    // Будуємо форму параметрів заповнення
                    BuildAutoFillForm(template.AutoFillConfig);
                }
            }
            catch (Exception ex)
            {
                _ = DialogService.ShowErrorAsync($"Помилка завантаження шаблону: {ex.Message}");
            }
        }

        private void BuildAutoFillForm(List<AutoFillParameter>? parameters)
        {
            AutoFillFields.Clear();
            AutoFillValues.Clear();

            if (parameters == null || parameters.Count == 0)
            {
                OnPropertyChanged(nameof(HasAutoFillParams));
                return;
            }

            foreach (var parameter in parameters)
            {
                var fieldVm = new AutoFillFieldViewModel
                {
                    Key = parameter.Key,
                    Label = parameter.Label,
                    Value = parameter.DefaultValue?.ToString() ?? string.Empty
                };

                if (parameter.DefaultValue != null)
                {
                    AutoFillValues[parameter.Key] = parameter.DefaultValue.ToString() ?? string.Empty;
                }

                fieldVm.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(AutoFillFieldViewModel.Value))
                    {
                        AutoFillValues[fieldVm.Key] = fieldVm.Value;
                    }
                };

                AutoFillFields.Add(fieldVm);
            }

            OnPropertyChanged(nameof(HasAutoFillParams));
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

        private void Cancel()
        {
            _mainViewModel.CloseScreenCommand.Execute(this);
        }

        #endregion
    }

    /// <summary>
    /// ViewModel для поля параметрів заповнення
    /// </summary>
    public class AutoFillFieldViewModel : ViewModelBase
    {
        private string _key = string.Empty;
        private string _label = string.Empty;
        private string _value = string.Empty;

        public string Key
        {
            get => _key;
            set => SetProperty(ref _key, value);
        }

        public string Label
        {
            get => _label;
            set => SetProperty(ref _label, value);
        }

        public string Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }
    }
}
