using FlexJournalPro.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Input;

namespace FlexJournalPro.ViewModels
{
    /// <summary>
    /// ViewModel головного вікна з sidebar та керуванням екранами
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        private readonly DatabaseService _dbService;
        private bool _isSidebarExpanded = true;
        private ScreenBase? _currentScreen;
        private ScreenBase? _selectedScreen;
        private bool _canScrollLeft;
        private bool _canScrollRight;
        private ScrollViewer? _screensPanelScrollViewer;

        public MainViewModel()
        {
            _dbService = new DatabaseService();
            OpenScreens = new ObservableCollection<ScreenBase>();

            // Імпортуємо шаблони з JSON файлів
            ImportTemplatesFromJsonFiles();

            // Команди
            ToggleSidebarCommand = new RelayCommand(ToggleSidebar);
            OpenJournalsListCommand = new RelayCommand(OpenJournalsList);
            OpenTemplatesListCommand = new RelayCommand(OpenTemplatesList);
            OpenUsersListCommand = new RelayCommand(OpenUsersList);
            CloseScreenCommand = new RelayCommand(CloseScreen);
            ScrollLeftCommand = new RelayCommand(ScrollLeft, () => CanScrollLeft);
            ScrollRightCommand = new RelayCommand(ScrollRight, () => CanScrollRight);

            // Підписка на зміну колекції екранів
            OpenScreens.CollectionChanged += (s, e) => UpdateScrollButtons();
        }

        #region Properties

        /// <summary>
        /// Чи розгорнуто sidebar
        /// </summary>
        public bool IsSidebarExpanded
        {
            get => _isSidebarExpanded;
            set => SetProperty(ref _isSidebarExpanded, value);
        }

        /// <summary>
        /// Поточний відображуваний екран
        /// </summary>
        public ScreenBase? CurrentScreen
        {
            get => _currentScreen;
            set => SetProperty(ref _currentScreen, value);
        }

        /// <summary>
        /// Вибраний екран у панелі знизу
        /// </summary>
        public ScreenBase? SelectedScreen
        {
            get => _selectedScreen;
            set
            {
                if (SetProperty(ref _selectedScreen, value) && value != null)
                {
                    CurrentScreen = value;
                }
            }
        }

        /// <summary>
        /// Колекція відкритих екранів
        /// </summary>
        public ObservableCollection<ScreenBase> OpenScreens { get; }

        /// <summary>
        /// Чи можна прокручувати вліво
        /// </summary>
        public bool CanScrollLeft
        {
            get => _canScrollLeft;
            set => SetProperty(ref _canScrollLeft, value);
        }

        /// <summary>
        /// Чи можна прокручувати вправо
        /// </summary>
        public bool CanScrollRight
        {
            get => _canScrollRight;
            set => SetProperty(ref _canScrollRight, value);
        }

        #endregion

        #region Commands

        public ICommand ToggleSidebarCommand { get; }
        public ICommand OpenJournalsListCommand { get; }
        public ICommand OpenTemplatesListCommand { get; }
        public ICommand OpenUsersListCommand { get; }
        public ICommand CloseScreenCommand { get; }
        public ICommand ScrollLeftCommand { get; }
        public ICommand ScrollRightCommand { get; }

        #endregion

        #region Command Handlers

        private void ToggleSidebar()
        {
            IsSidebarExpanded = !IsSidebarExpanded;
        }

        private void OpenJournalsList()
        {
            OpenOrActivateScreen(() => new Screens.JournalsListScreen(_dbService, this));
        }

        private void OpenTemplatesList()
        {
            OpenOrActivateScreen(() => new Screens.TemplatesListScreen(_dbService, this));
        }

        private async void OpenUsersList()
        {
            await DialogService.ShowInformationAsync(
                "Екран користувачів буде доданий у наступній версії",
                "У розробці");
        }

        private void CloseScreen(object? parameter)
        {
            if (parameter is ScreenBase screen)
            {
                screen.OnClosing();
                OpenScreens.Remove(screen);

                if (CurrentScreen == screen)
                {
                    CurrentScreen = OpenScreens.LastOrDefault();
                }

                if (OpenScreens.Any())
                {
                    SelectedScreen = CurrentScreen;
                }
            }
        }

        private void ScrollLeft()
        {
            _screensPanelScrollViewer?.LineLeft();
            UpdateScrollButtons();
        }

        private void ScrollRight()
        {
            _screensPanelScrollViewer?.LineRight();
            UpdateScrollButtons();
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Імпортує шаблони з JSON файлів у БД (виконується при запуску)
        /// </summary>
        private void ImportTemplatesFromJsonFiles()
        {
            string presetsPath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Presets");

            if (!Directory.Exists(presetsPath))
            {
                Directory.CreateDirectory(presetsPath);
                return;
            }

            foreach (string filePath in Directory.GetFiles(presetsPath, "*.json"))
            {
                try
                {
                    string jsonContent = File.ReadAllText(filePath);
                    var template = JsonSerializer.Deserialize<Models.TableTemplate>(jsonContent);
                    string key = Path.GetFileNameWithoutExtension(filePath);

                    if (template != null)
                    {
                        // Встановлюємо ID якщо не вказано
                        if (string.IsNullOrEmpty(template.Id))
                        {
                            template.Id = key;
                        }

                        // Перевіряємо, чи вже є такий шаблон у БД
                        var existing = _dbService.GetTemplate(template.Id);
                        if (existing == null)
                        {
                            _dbService.SaveTemplate(template);
                            System.Diagnostics.Debug.WriteLine($"Імпортовано шаблон: {template.Id}");
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Помилка імпорту {Path.GetFileName(filePath)}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Відкриває або активує існуючий екран
        /// </summary>
        private void OpenOrActivateScreen(System.Func<ScreenBase> screenFactory)
        {
            // Спробуємо знайти вже відкритий екран
            var screen = screenFactory();
            var existingScreen = OpenScreens.FirstOrDefault(s => s.ScreenId == screen.ScreenId);

            if (existingScreen != null)
            {
                // Активуємо існуючий екран
                SelectedScreen = existingScreen;
            }
            else
            {
                // Відкриваємо новий екран
                OpenScreens.Add(screen);
                SelectedScreen = screen;
            }
        }

        /// <summary>
        /// Встановлює ScrollViewer панелі екранів (викликається з MainWindow.xaml.cs)
        /// </summary>
        public void SetScreensPanelScrollViewer(ScrollViewer scrollViewer)
        {
            _screensPanelScrollViewer = scrollViewer;
            if (_screensPanelScrollViewer != null)
            {
                _screensPanelScrollViewer.ScrollChanged += (s, e) => UpdateScrollButtons();
            }
            UpdateScrollButtons();
        }

        /// <summary>
        /// Оновлює стан кнопок прокрутки
        /// </summary>
        private void UpdateScrollButtons()
        {
            if (_screensPanelScrollViewer == null)
            {
                CanScrollLeft = false;
                CanScrollRight = false;
                return;
            }

            CanScrollLeft = _screensPanelScrollViewer.HorizontalOffset > 0;
            CanScrollRight = _screensPanelScrollViewer.HorizontalOffset < 
                (_screensPanelScrollViewer.ScrollableWidth - 1);
        }

        #endregion
    }
}
