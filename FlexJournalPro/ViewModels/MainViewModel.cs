using FlexJournalPro.Models;
using FlexJournalPro.Services;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Input;

namespace FlexJournalPro.ViewModels
{
    /// <summary>
    /// ViewModel головного вікна з sidebar та керуванням екранами
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        private readonly IAuthService _authService;
        private readonly IKeyManagementService _keyManagementService;
        private readonly IScreenFactory _screenFactory;
        private readonly IAppLifecycleService _appLifecycleService;

        private bool _isSidebarExpanded = true;
        private ScreenBase? _currentScreen;
        private ScreenBase? _selectedScreen;
        private bool _canScrollLeft;
        private bool _canScrollRight;
        private ScrollViewer? _screensPanelScrollViewer;

        public MainViewModel(IAuthService authService,
            IKeyManagementService keyManagementService,
            IScreenFactory screenFactory,
            IAppLifecycleService appLifecycleService
            )
        {
            _authService = authService;
            _keyManagementService = keyManagementService;
            _screenFactory = screenFactory;
            _appLifecycleService = appLifecycleService;

            OpenScreens = new ObservableCollection<ScreenBase>();

            // Команди
            ToggleSidebarCommand = new RelayCommand(ToggleSidebar);
            OpenJournalsListCommand = new RelayCommand(OpenJournalsList, () => _authService.UserCan("ViewJournalsList"));
            OpenTemplatesListCommand = new RelayCommand(OpenTemplatesList, () => _authService.UserCan("ViewTemplates"));
            OpenUsersListCommand = new RelayCommand(OpenUsersList, () => _authService.UserCan("ManageUsers"));
            OpenLogsCommand = new RelayCommand(OpenLogs, () => _authService.UserCan("ViewLogs"));
            CloseScreenCommand = new RelayCommand(CloseScreen);
            ScrollLeftCommand = new RelayCommand(ScrollLeft, () => CanScrollLeft);
            ScrollRightCommand = new RelayCommand(ScrollRight, () => CanScrollRight);
            ChangePasswordCommand = new RelayCommand(ChangePassword);
            LogoutCommand = new RelayCommand(Logout);

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
        public ICommand OpenLogsCommand { get; }
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
            OpenOrActivateScreen(() => _screenFactory.CreateJournalsListScreen(this));
        }

        private void OpenTemplatesList()
        {
            OpenOrActivateScreen(() => _screenFactory.CreateTemplatesListScreen(this));
        }

        private async void OpenUsersList()
        {
            OpenOrActivateScreen(() => _screenFactory.CreateUsersListScreen(this));
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

        private async void ChangePassword()
        {
            var vm = new ChangePasswordViewModel();
            var dialog = new Views.ChangePasswordDialog { DataContext = vm };
            
            var result = await Services.DialogService.ShowCustomDialogAsync(dialog);

            if (result is bool success && success)
            {
                var user = App.CurrentUser;

                if (user != null)
                {
                    // Перевірка старого пароля
                    if (_authService.Authenticate(user.Login, vm.OldPassword) == null)
                    {
                        AppLogger.LogSystemWarning(LogAction.UserLoginFailed, $"Користувач {user.Login} спробував змінити пароль, але вказав невірний старий пароль.");
                        await DialogService.ShowErrorAsync("Старий пароль невірний");
                        return;
                    }

                    if (!string.IsNullOrWhiteSpace(vm.NewPassword))
                    {
                        _authService.UpdateUserPassword(user, vm.NewPassword);
                        _keyManagementService.SetOrUpdateUserKey(user.Login, vm.NewPassword);
                    }

                    AppLogger.LogSystemInfo(LogAction.PasswordChanged, $"Користувач {user.Login} змінив пароль.");

                    await DialogService.ShowSuccessAsync("Пароль успішно змінено.");
                }
            }
        }

        private void Logout()
        {
            _appLifecycleService.LogoutAndRestart();
        }

        private void OpenLogs()
        {
            OpenOrActivateScreen(() => _screenFactory.CreateLogsScreen(this));
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
