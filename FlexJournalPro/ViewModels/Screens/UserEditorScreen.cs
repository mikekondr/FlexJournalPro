using FlexJournalPro.Models;
using FlexJournalPro.Services;
using MaterialDesignThemes.Wpf;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace FlexJournalPro.ViewModels.Screens
{
    public class JournalAccessItem
    {
        public long JournalId { get; set; }
        public string Title { get; set; } = string.Empty;
    }

    public class UserEditorScreen : ScreenBase
    {
        private readonly MainViewModel _mainViewModel;
        private readonly IDatabaseService _dbService;
        private readonly IKeyManagementService _keyManagementService;
        private readonly IAuthService _authService;

        public AppUser? UserToEdit { get; }

        private bool _isEditing;
        public bool IsEditing
        {
            get => _isEditing;
            private set => SetProperty(ref _isEditing, value);
        }

        private string _login = string.Empty;
        private string _password = string.Empty;
        private string _fullName = string.Empty;
        private UserRole _role = UserRole.Viewer;

        // Dual ListBox Collections
        public ObservableCollection<JournalAccessItem> AvailableJournals { get; } = new ObservableCollection<JournalAccessItem>();
        public ObservableCollection<JournalAccessItem> SelectedJournals { get; } = new ObservableCollection<JournalAccessItem>();

        private JournalAccessItem? _selectedAvailableJournal;
        public JournalAccessItem? SelectedAvailableJournal
        {
            get => _selectedAvailableJournal;
            set
            {
                if (SetProperty(ref _selectedAvailableJournal, value))
                    (AddJournalCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        private JournalAccessItem? _selectedAssignedJournal;
        public JournalAccessItem? SelectedAssignedJournal
        {
            get => _selectedAssignedJournal;
            set
            {
                if (SetProperty(ref _selectedAssignedJournal, value))
                    (RemoveJournalCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public UserEditorScreen(AppUser userToEdit, MainViewModel mainViewModel, 
            IDatabaseService dbService, IKeyManagementService keyManagementService, IAuthService authService)
        {
            UserToEdit = userToEdit;
            _mainViewModel = mainViewModel;
            _dbService = dbService;
            _keyManagementService = keyManagementService;
            _authService = authService;

            IsEditing = userToEdit.Id != 0;

            Title = IsEditing ? $"Редагування користувача: {userToEdit!.Login}" : "Новий користувач";
            Icon = IsEditing ? PackIconKind.AccountEdit : PackIconKind.AccountPlus;

            LoadJournalAccessList();

            if (IsEditing)
            {
                Login = userToEdit!.Login;
                FullName = userToEdit.FullName;
                Role = userToEdit.Role;
            }

            SaveCommand = new RelayCommand(SaveUser, CanSaveUser);
            CancelCommand = new RelayCommand(Cancel);

            AddJournalCommand = new RelayCommand(AddJournal, () => SelectedAvailableJournal != null);
            RemoveJournalCommand = new RelayCommand(RemoveJournal, () => SelectedAssignedJournal != null);
            AddAllJournalsCommand = new RelayCommand(AddAllJournals, () => AvailableJournals.Any());
            RemoveAllJournalsCommand = new RelayCommand(RemoveAllJournals, () => SelectedJournals.Any());
        }

        public override string ScreenId => IsEditing ? $"UserEditor_{UserToEdit!.Id}" : "UserEditor_New";

        #region Properties
        public string Login
        {
            get => _login;
            set
            {
                if (SetProperty(ref _login, value))
                    (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public string Password
        {
            get => _password;
            set
            {
                if (SetProperty(ref _password, value))
                    (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public string FullName
        {
            get => _fullName;
            set
            {
                if (SetProperty(ref _fullName, value))
                    (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public UserRole Role
        {
            get => _role;
            set
            {
                if (SetProperty(ref _role, value))
                {
                    OnPropertyChanged(nameof(IsJournalSelectionVisible));
                }
            }
        }

        public bool IsJournalSelectionVisible => Role != UserRole.Admin;
        #endregion

        #region Commands
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public ICommand AddJournalCommand { get; }
        public ICommand RemoveJournalCommand { get; }
        public ICommand AddAllJournalsCommand { get; }
        public ICommand RemoveAllJournalsCommand { get; }
        #endregion

        private void LoadJournalAccessList()
        {
            AvailableJournals.Clear();
            SelectedJournals.Clear();

            var allJournals = _dbService.GetAllJournals();
            var userJournalIds = IsEditing ? UserToEdit!.AllowedJournalIds : new List<int>();

            foreach (var journal in allJournals)
            {
                var item = new JournalAccessItem
                {
                    JournalId = journal.Id,
                    Title = journal.Title
                };

                if (userJournalIds.Contains((int)journal.Id))
                {
                    SelectedJournals.Add(item);
                }
                else
                {
                    AvailableJournals.Add(item);
                }
            }

            UpdateCommandStates();
        }

        #region DualListBox Logic
        private void AddJournal()
        {
            if (SelectedAvailableJournal != null)
            {
                var item = SelectedAvailableJournal;
                AvailableJournals.Remove(item);
                SelectedJournals.Add(item);

                // Пересортування для красивого вигляду, якщо потрібно
                var sorted = SelectedJournals.OrderBy(x => x.Title).ToList();
                SelectedJournals.Clear();
                foreach (var i in sorted) SelectedJournals.Add(i);

                UpdateCommandStates();
            }
        }

        private void RemoveJournal()
        {
            if (SelectedAssignedJournal != null)
            {
                var item = SelectedAssignedJournal;
                SelectedJournals.Remove(item);
                AvailableJournals.Add(item);

                var sorted = AvailableJournals.OrderBy(x => x.Title).ToList();
                AvailableJournals.Clear();
                foreach (var i in sorted) AvailableJournals.Add(i);

                UpdateCommandStates();
            }
        }

        private void AddAllJournals()
        {
            foreach (var item in AvailableJournals.ToList())
            {
                AvailableJournals.Remove(item);
                SelectedJournals.Add(item);
            }
            UpdateCommandStates();
        }

        private void RemoveAllJournals()
        {
            foreach (var item in SelectedJournals.ToList())
            {
                SelectedJournals.Remove(item);
                AvailableJournals.Add(item);
            }
            UpdateCommandStates();
        }

        private void UpdateCommandStates()
        {
            (AddAllJournalsCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (RemoveAllJournalsCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
        #endregion

        private bool CanSaveUser()
        {
            if (string.IsNullOrWhiteSpace(Login) || string.IsNullOrWhiteSpace(FullName))
                return false;

            if (!IsEditing && string.IsNullOrWhiteSpace(Password))
                return false;

            return true;
        }

        private async void SaveUser()
        {
            try
            {
                var selectedIds = SelectedJournals.Select(j => (int)j.JournalId).ToList();

                if (IsEditing)
                {
                    UserToEdit!.Login = Login;
                    UserToEdit.FullName = FullName;
                    UserToEdit.Role = Role;
                    UserToEdit.AllowedJournalIds = selectedIds;

                    string? newPasswordHash = null;
                    if (!string.IsNullOrWhiteSpace(Password))
                    {
                        newPasswordHash = _authService.HashPassword(Password);
                    }

                    _dbService.UpdateUser(UserToEdit, newPasswordHash);
                    if (!string.IsNullOrWhiteSpace(Password))
                    {
                        _keyManagementService.SetOrUpdateUserKey(UserToEdit.Login, Password);
                    }
                }
                else
                {
                    var newUser = new AppUser
                    {
                        Login = Login,
                        FullName = FullName,
                        Role = Role,
                        AllowedJournalIds = selectedIds
                    };

                    string passwordHash = _authService.HashPassword(Password);

                    _dbService.CreateUser(newUser, passwordHash);
                    _keyManagementService.SetOrUpdateUserKey(newUser.Login, Password);
                }

                _mainViewModel.CloseScreenCommand.Execute(this);
            }
            catch (Exception ex)
            {
                await DialogService.ShowErrorAsync($"Помилка збереження користувача: {ex.Message}");
            }
        }

        private void Cancel()
        {
            _mainViewModel.CloseScreenCommand.Execute(this);
        }
    }
}