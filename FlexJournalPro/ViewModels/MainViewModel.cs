using FlexJournalPro.Services;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Input;
using FlexJournalPro.Windows;

namespace FlexJournalPro.ViewModels
{
    /// <summary>
    /// ViewModel головного вікна з sidebar та керуванням екранами
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        private readonly DatabaseService _dbService = App.Database;
        private readonly TemplateService _templateService;
        private bool _isSidebarExpanded = true;
        private ScreenBase? _currentScreen;
        private ScreenBase? _selectedScreen;
        private bool _canScrollLeft;
        private bool _canScrollRight;
        private ScrollViewer? _screensPanelScrollViewer;

        public MainViewModel()
        {
            _templateService = new TemplateService(_dbService);
            OpenScreens = new ObservableCollection<ScreenBase>();

            // Імпортуємо шаблони з JSON файлів
            _templateService.ImportDefaultTemplates();

            // Команди
            ToggleSidebarCommand = new RelayCommand(ToggleSidebar);
            OpenJournalsListCommand = new RelayCommand(OpenJournalsList);
            OpenTemplatesListCommand = new RelayCommand(OpenTemplatesList);
            OpenUsersListCommand = new RelayCommand(OpenUsersList);
            CloseScreenCommand = new RelayCommand(CloseScreen);
            ScrollLeftCommand = new RelayCommand(ScrollLeft, () => CanScrollLeft);
            ScrollRightCommand = new RelayCommand(ScrollRight, () => CanScrollRight);
            ChangePasswordCommand = new RelayCommand(ChangePassword);
            LogoutCommand = new RelayCommand(Logout);

            // Підписка на зміну колекції екранів
            OpenScreens.CollectionChanged += (s, e) => UpdateScrollButtons();

            // Встановимо ім'я користувача, якщо він залогінений
            //if (App.CurrentUser != null)
            //{
            //    CurrentUserFullName = App.CurrentUser.FullName;
            //}
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

        // Властивості для користувача
        private string _currentUserFullName = App.CurrentUser?.FullName ?? "Користувач";

        public string CurrentUserFullName => _currentUserFullName;

        #endregion

        #region Commands

        public ICommand ToggleSidebarCommand { get; }
        public ICommand OpenJournalsListCommand { get; }
        public ICommand OpenTemplatesListCommand { get; }
        public ICommand OpenUsersListCommand { get; }
        public ICommand CloseScreenCommand { get; }
        public ICommand ScrollLeftCommand { get; }
        public ICommand ScrollRightCommand { get; }

        // Команди
        public ICommand ChangePasswordCommand { get; private set; }
        public ICommand LogoutCommand { get; private set; }

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
            OpenOrActivateScreen(() => new Screens.TemplatesListScreen(_templateService, this));
        }

        private async void OpenUsersList()
        {
            OpenOrActivateScreen(() => new Screens.UsersListScreen(this));
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

        private void ChangePassword()
        {
            // TODO: Показати вікно зміни пароля
        }

        private void Logout()
        {
            // Скидаємо користувача
            App.CurrentUser = null;
            
             // Змінюємо режим зупинки, щоб програма не завершилася під час закриття головного вікна
            System.Windows.Application.Current.ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;

            // Отримуємо поточне головне вікно та закриваємо його
            if (System.Windows.Application.Current.MainWindow != null)
            {
                System.Windows.Application.Current.MainWindow.Close();
            }

            // Створюємо та відкриваємо вікно авторизації наново
            var loginWindow = new LoginWindow(App.KeyManager);
            
            if (loginWindow.ShowDialog() == true)
            {
                // Повертаємо режим закриття програми за замовчуванням
                System.Windows.Application.Current.ShutdownMode = System.Windows.ShutdownMode.OnMainWindowClose;
                
                // Якщо користувач знову залогінився, відкриваємо його вікно
                var mainWindow = new MainWindow();
                System.Windows.Application.Current.MainWindow = mainWindow;
                mainWindow.Show();
            }
            else
            {
                // Якщо закрили вікно авторизації
                System.Windows.Application.Current.Shutdown();
            }
        }

        #endregion

        #region Helper Methods

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
