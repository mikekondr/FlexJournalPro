using FlexJournalPro.Models;
using FlexJournalPro.Services;
using MaterialDesignThemes.Wpf;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace FlexJournalPro.ViewModels.Screens
{
    /// <summary>
    /// Screen для відображення списку журналів
    /// </summary>
    public class JournalsListScreen : ScreenBase
    {
        private readonly IDatabaseService _dbService;
        private readonly MainViewModel _mainViewModel;
        private readonly IScreenFactory _screenFactory;
        private readonly IAuthService _authService;
        private JournalMetadata? _selectedJournal;

        public JournalsListScreen(IDatabaseService dbService, 
                                  MainViewModel mainViewModel,
                                  IScreenFactory screenFactory,
                                  IAuthService authService)
        {
            _dbService = dbService;
            _mainViewModel = mainViewModel;
            _screenFactory = screenFactory;
            _authService = authService;

            Title = "Всі журнали";
            Icon = PackIconKind.BookOpen;

            Journals = new ObservableCollection<JournalMetadata>();

            // Команди
            CreateNewJournalCommand = new RelayCommand(CreateNewJournal, () => _authService.UserCan("ManageJournals"));
            OpenJournalCommand = new RelayCommand(OpenJournal, () => SelectedJournal != null);
            DeleteJournalCommand = new RelayCommand(DeleteJournal, () => SelectedJournal != null && _authService.UserCan("ManageJournals"));
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
            var newJournalScreen = _screenFactory.CreateNewJournalScreen(_mainViewModel);

            _mainViewModel.OpenScreens.Add(newJournalScreen);
            _mainViewModel.SelectedScreen = newJournalScreen;
        }

        private void OpenJournal()
        {
            if (SelectedJournal == null) return;

            // Відкриваємо екран редагування журналу
            var editorScreen = _screenFactory.CreateJournalEditorScreen(SelectedJournal, _mainViewModel);
            editorScreen.IsReadOnly = !_authService.UserCan("EditJournals");

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
                AppLogger.LogJournalAction(SelectedJournal.TableName, LogAction.JournalOpened, $"Журнал '{SelectedJournal.Title}' відкрито для редагування.");
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
                    // Закриваємо редактор журналу, якщо він відкритий
                    var openEditorScreen = _mainViewModel.OpenScreens
                        .OfType<JournalEditorScreen>()
                        .FirstOrDefault(s => s.Journal?.Id == SelectedJournal.Id);

                    if (openEditorScreen != null)
                    {
                        _mainViewModel.OpenScreens.Remove(openEditorScreen);
                    }

                    // Видаляємо журнал з бази даних
                    _dbService.DeleteJournal(SelectedJournal.Id);
                    
                    AppLogger.LogSystemInfo(LogAction.JournalDeleted, $"Журнал '{SelectedJournal.Title}' (ID: {SelectedJournal.Id}) видалено.");

                    // Видаляємо з локального списку
                    Journals.Remove(SelectedJournal);

                    // Очищаємо вибір
                    SelectedJournal = null;

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
