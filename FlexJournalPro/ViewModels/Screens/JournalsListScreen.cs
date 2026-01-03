using FlexJournalPro.Models;
using FlexJournalPro.Services;
using MaterialDesignThemes.Wpf;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace FlexJournalPro.ViewModels.Screens
{
    /// <summary>
    /// Screen для відображення списку журналів
    /// </summary>
    public class JournalsListScreen : ScreenBase
    {
        private readonly DatabaseService _dbService;
        private readonly MainViewModel _mainViewModel;
        private JournalMetadata? _selectedJournal;

        public JournalsListScreen(DatabaseService dbService, MainViewModel mainViewModel)
        {
            _dbService = dbService;
            _mainViewModel = mainViewModel;

            Title = "Журнали";
            Icon = PackIconKind.Book;

            Journals = new ObservableCollection<JournalMetadata>();

            // Команди
            CreateNewJournalCommand = new RelayCommand(CreateNewJournal);
            OpenJournalCommand = new RelayCommand(OpenJournal, () => SelectedJournal != null);
            DeleteJournalCommand = new RelayCommand(DeleteJournal, () => SelectedJournal != null);
            RefreshCommand = new RelayCommand(LoadJournals);

            // Завантажити журнали
            LoadJournals();
        }

        #region Properties

        /// <summary>
        /// Колекція журналів
        /// </summary>
        public ObservableCollection<JournalMetadata> Journals { get; }

        /// <summary>
        /// Вибраний журнал
        /// </summary>
        public JournalMetadata? SelectedJournal
        {
            get => _selectedJournal;
            set
            {
                if (SetProperty(ref _selectedJournal, value))
                {
                    (OpenJournalCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (DeleteJournalCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public override string ScreenId => "JournalsList";

        #endregion

        #region Commands

        public ICommand CreateNewJournalCommand { get; }
        public ICommand OpenJournalCommand { get; }
        public ICommand DeleteJournalCommand { get; }
        public ICommand RefreshCommand { get; }

        #endregion

        #region Command Handlers

        private async void LoadJournals()
        {
            try
            {
                var journals = _dbService.GetAllJournals();
                Journals.Clear();
                foreach (var journal in journals)
                {
                    Journals.Add(journal);
                }
            }
            catch (System.Exception ex)
            {
                await DialogService.ShowErrorAsync(
                    $"Помилка завантаження журналів: {ex.Message}");
            }
        }

        private void CreateNewJournal()
        {
            // Відкриваємо екран створення нового журналу
            var newJournalScreen = new NewJournalScreen(_dbService, _mainViewModel);
            
            _mainViewModel.OpenScreens.Add(newJournalScreen);
            _mainViewModel.SelectedScreen = newJournalScreen;
        }

        private void OpenJournal()
        {
            if (SelectedJournal == null) return;

            // Відкриваємо екран редагування журналу
            var editorScreen = new JournalEditorScreen(SelectedJournal, _dbService, _mainViewModel);
            
            // Перевіряємо, чи не відкритий вже цей журнал
            var existingScreen = _mainViewModel.OpenScreens
                .OfType<JournalEditorScreen>()
                .FirstOrDefault(s => s.ScreenId == editorScreen.ScreenId);

            if (existingScreen != null)
            {
                _mainViewModel.SelectedScreen = existingScreen;
            }
            else
            {
                _mainViewModel.OpenScreens.Add(editorScreen);
                _mainViewModel.SelectedScreen = editorScreen;
            }
        }

        private async void DeleteJournal()
        {
            if (SelectedJournal == null) return;

            var result = await DialogService.ShowConfirmationAsync(
                $"Ви впевнені, що хочете видалити журнал '{SelectedJournal.Title}'?\n\nВСІ ДАНІ БУДУТЬ ВТРАЧЕНІ!",
                "Підтвердження видалення");

            if (result == DialogResult.Yes)
            {
                try
                {
                    // TODO: Додати метод видалення журналу у DatabaseService
                    await DialogService.ShowInformationAsync(
                        "Функція видалення журналу буде додана у наступній версії",
                        "У розробці");
                }
                catch (System.Exception ex)
                {
                    await DialogService.ShowErrorAsync(
                        $"Помилка видалення журналу: {ex.Message}");
                }
            }
        }

        #endregion
    }
}
