using FlexJournalPro.Models;
using FlexJournalPro.Services;
using MaterialDesignThemes.Wpf;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace FlexJournalPro.ViewModels.Screens
{
    /// <summary>
    /// Screen для керування шаблонами журналів
    /// </summary>
    public class TemplatesListScreen : ScreenBase
    {
        private readonly DatabaseService _dbService;
        private readonly MainViewModel _mainViewModel;
        private TemplateMetadata? _selectedTemplate;

        public TemplatesListScreen(DatabaseService dbService, MainViewModel mainViewModel)
        {
            _dbService = dbService;
            _mainViewModel = mainViewModel;

            Title = "Шаблони";
            Icon = PackIconKind.FileDocument;

            Templates = new ObservableCollection<TemplateMetadata>();

            // Команди
            CreateTemplateCommand = new RelayCommand(CreateTemplate);
            EditTemplateCommand = new RelayCommand(EditTemplate, () => SelectedTemplate != null);
            DeleteTemplateCommand = new RelayCommand(DeleteTemplate, () => SelectedTemplate != null);
            RefreshCommand = new RelayCommand(LoadTemplates);
            ImportTemplateCommand = new RelayCommand(ImportTemplate);

            // Завантажити шаблони
            LoadTemplates();
        }

        #region Properties

        /// <summary>
        /// Колекція шаблонів
        /// </summary>
        public ObservableCollection<TemplateMetadata> Templates { get; }

        /// <summary>
        /// Вибраний шаблон
        /// </summary>
        public TemplateMetadata? SelectedTemplate
        {
            get => _selectedTemplate;
            set
            {
                if (SetProperty(ref _selectedTemplate, value))
                {
                    (EditTemplateCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (DeleteTemplateCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public override string ScreenId => "TemplatesList";

        #endregion

        #region Commands

        public ICommand CreateTemplateCommand { get; }
        public ICommand EditTemplateCommand { get; }
        public ICommand DeleteTemplateCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ImportTemplateCommand { get; }

        #endregion

        #region Command Handlers

        private async void LoadTemplates()
        {
            try
            {
                var templates = _dbService.GetAllTemplates();
                Templates.Clear();
                foreach (var template in templates)
                {
                    Templates.Add(template);
                }
            }
            catch (System.Exception ex)
            {
                await DialogService.ShowErrorAsync(
                    $"Помилка завантаження шаблонів: {ex.Message}");
            }
        }

        private async void CreateTemplate()
        {
            await DialogService.ShowInformationAsync(
                "Створення шаблону буде додано у наступній версії",
                "У розробці");
        }

        private async void EditTemplate()
        {
            if (SelectedTemplate == null) return;

            await DialogService.ShowInformationAsync(
                $"Редагування шаблону '{SelectedTemplate.Name}' буде додано у наступній версії",
                "У розробці");
        }

        private async void DeleteTemplate()
        {
            if (SelectedTemplate == null) return;

            var result = await DialogService.ShowConfirmationAsync(
                $"Ви впевнені, що хочете видалити шаблон '{SelectedTemplate.Name}'?",
                "Підтвердження видалення");

            if (result == DialogResult.Yes)
            {
                try
                {
                    _dbService.DeactivateTemplate(SelectedTemplate.Id);
                    LoadTemplates();
                }
                catch (System.Exception ex)
                {
                    await DialogService.ShowErrorAsync(
                        $"Помилка видалення шаблону: {ex.Message}");
                }
            }
        }

        private async void ImportTemplate()
        {
            // Імпорт шаблону з JSON файлу
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                Title = "Виберіть файл шаблону"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    string jsonContent = System.IO.File.ReadAllText(dialog.FileName);
                    var template = System.Text.Json.JsonSerializer.Deserialize<TableTemplate>(jsonContent);

                    if (template != null)
                    {
                        _dbService.SaveTemplate(template);
                        LoadTemplates();
                    }
                }
                catch (System.Exception ex)
                {
                    await DialogService.ShowErrorAsync(
                        $"Помилка імпорту шаблону: {ex.Message}");
                }
            }
        }

        #endregion
    }
}
