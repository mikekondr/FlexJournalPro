using FlexJournalPro.Models;
using FlexJournalPro.Services;
using MaterialDesignThemes.Wpf;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace FlexJournalPro.ViewModels.Screens
{
    public class UsersListScreen : ScreenBase
    {
        private readonly DatabaseService _databaseService;
        private readonly MainViewModel _mainViewModel;

        public override string ScreenId => "UsersList";

        public ObservableCollection<AppUser> Users { get; }

        private AppUser? _selectedUser;
        public AppUser? SelectedUser
        {
            get => _selectedUser;
            set
            {
                if (SetProperty(ref _selectedUser, value))
                {
                    (EditUserCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (DeleteUserCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public ICommand AddUserCommand { get; }
        public ICommand EditUserCommand { get; }
        public ICommand DeleteUserCommand { get; }
        public ICommand RefreshCommand { get; }

        public UsersListScreen(MainViewModel mainViewModel)
        {
            _databaseService = App.Database;
            _mainViewModel = mainViewModel;

            Users = new ObservableCollection<AppUser>();

            Title = "Користувачі";
            Icon = PackIconKind.AccountGroupOutline;

            AddUserCommand = new RelayCommand(CreateNewUser);
            EditUserCommand = new RelayCommand(OpenUser, () => SelectedUser != null);
            DeleteUserCommand = new RelayCommand(DeleteUser, () => SelectedUser != null);
            RefreshCommand = new RelayCommand(LoadUsers);

            LoadUsers();
        }

        private async void LoadUsers()
        {
            try
            {
                var usrs = _databaseService.GetAllUsers();
                Users.Clear();
                foreach (var u in usrs)
                {
                    Users.Add(u);
                }
            }
            catch (System.Exception ex)
            {
                await DialogService.ShowErrorAsync(
                    $"Помилка завантаження користувачів: {ex.Message}");
            }
        }

        private void CreateNewUser()
        {
            // Відкриваємо екран створення нового користувача (передаємо null замість наявного юзера)
            var userEditorScreen = new UserEditorScreen(null, _databaseService, _mainViewModel);
            
            _mainViewModel.OpenScreens.Add(userEditorScreen);
            _mainViewModel.SelectedScreen = userEditorScreen;
        }

        private void OpenUser()
        {
            if (SelectedUser == null) return;

            // Відкриваємо екран редагування існуючого користувача
            var editorScreen = new UserEditorScreen(SelectedUser, _databaseService, _mainViewModel);
            
            // Перевіряємо, чи не відкритий вже цей екран (за допомогою ScreenId)
            var existingScreen = _mainViewModel.OpenScreens
                .OfType<UserEditorScreen>()
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

        private async void DeleteUser()
        {
            if (SelectedUser == null) return;

            var result = await DialogService.ShowConfirmationAsync(
                $"Ви впевнені, що хочете видалити користувача '{SelectedUser.Login}'?",
                "Підтвердження видалення");

            if (result == DialogResult.Yes)
            {
                try
                {
                    // Закриваємо екран редагування користувача, якщо він відкритий
                    var openEditorScreen = _mainViewModel.OpenScreens
                        .OfType<UserEditorScreen>()
                        .FirstOrDefault(s => s.UserToEdit?.Id == SelectedUser.Id);

                    if (openEditorScreen != null)
                    {
                        _mainViewModel.OpenScreens.Remove(openEditorScreen);
                    }

                    // Видаляємо ключ шифрування
                    App.KeyManager.RemoveUserKey(SelectedUser.Login);
                    // Видаляємо користувача з бази даних
                    _databaseService.DeleteUser(SelectedUser.Id);

                    // Оновлюємо локальний список
                    Users.Remove(SelectedUser);
                    SelectedUser = null;
                }
                catch (System.Exception ex)
                {
                    await DialogService.ShowErrorAsync(
                        $"Помилка видалення користувача: {ex.Message}");
                }
            }
        }
    }
}
