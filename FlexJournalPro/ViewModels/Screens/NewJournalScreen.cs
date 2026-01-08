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
            ConstantFields = new ObservableCollection<ConstantFieldViewModel>();
            SessionValues = new Dictionary<string, object>();

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
        /// Поля констант для відображення
        /// </summary>
        public ObservableCollection<ConstantFieldViewModel> ConstantFields { get; }

        /// <summary>
        /// Значення сеансових констант
        /// </summary>
        public Dictionary<string, object> SessionValues { get; }

        /// <summary>
        /// Чи є константи для відображення
        /// </summary>
        public bool HasConstants => ConstantFields.Any();

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
                ConstantFields.Clear();
                SessionValues.Clear();
                OnPropertyChanged(nameof(HasConstants));
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

                    // Будуємо форму констант
                    BuildConstantsForm(template.Constants);
                }
            }
            catch (Exception ex)
            {
                _ = DialogService.ShowErrorAsync($"Помилка завантаження шаблону: {ex.Message}");
            }
        }

        private void BuildConstantsForm(List<SessionConstant>? constants)
        {
            ConstantFields.Clear();
            SessionValues.Clear();

            if (constants == null || constants.Count == 0)
            {
                OnPropertyChanged(nameof(HasConstants));
                return;
            }

            foreach (var constant in constants)
            {
                var fieldVm = new ConstantFieldViewModel
                {
                    Key = constant.Key,
                    Label = constant.Label,
                    Value = constant.DefaultValue?.ToString() ?? string.Empty
                };

                if (constant.DefaultValue != null)
                {
                    SessionValues[constant.Key] = constant.DefaultValue.ToString() ?? string.Empty;
                }

                fieldVm.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(ConstantFieldViewModel.Value))
                    {
                        SessionValues[fieldVm.Key] = fieldVm.Value;
                    }
                };

                ConstantFields.Add(fieldVm);
            }

            OnPropertyChanged(nameof(HasConstants));
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
                    PresetId = template.Id,
                    NumberStart = StartNumber > 0 ? StartNumber : 1,
                    SessionConstantsJson = SessionValues.Count > 0
                        ? JsonSerializer.Serialize(SessionValues)
                        : "{}"
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
    /// ViewModel для поля константи
    /// </summary>
    public class ConstantFieldViewModel : ViewModelBase
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
