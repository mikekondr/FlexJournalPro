using FlexJournalPro.Services;
using System.Windows.Input;

namespace FlexJournalPro.ViewModels
{
    public class ChangePasswordViewModel : ViewModelBase
    {
        private readonly IAuthService _authService;

        private string _oldPassword = string.Empty;
        private string _newPassword = string.Empty;
        private string _confirmNewPassword = string.Empty;

        private string? _errorMessage;

        public string OldPassword
        {
            get => _oldPassword;
            set
            {
                if (SetProperty(ref _oldPassword, value))
                    ErrorMessage = null;
            }
        }

        public string NewPassword
        {
            get => _newPassword;
            set
            {
                if (SetProperty(ref _newPassword, value))
                    ErrorMessage = null;
            }
        }

        public string? ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public string ConfirmNewPassword
        {
            get => _confirmNewPassword;
            set
            {
                if (SetProperty(ref _confirmNewPassword, value))
                    ErrorMessage = null;
            }
        }

        public ICommand ChangeCommand { get; }
        public ICommand CancelCommand { get; }

        public ChangePasswordViewModel(IAuthService authService)
        {
            _authService = authService;

            ChangeCommand = new RelayCommand(ChangePassword);
            CancelCommand = new RelayCommand(Cancel);
        }

        private void ChangePassword()
        {
            if (string.IsNullOrEmpty(OldPassword))
            {
                ErrorMessage = "Будь ласка, введіть старий пароль.";
                return;
            }
            else if (_authService.Authenticate(App.CurrentUser?.Login, OldPassword) == null)
            {
                ErrorMessage = "Невірний старий пароль.";
                return;
            }

            if (string.IsNullOrEmpty(NewPassword))
            {
                ErrorMessage = "Будь ласка, введіть новий пароль.";
                return;
            }
            else
            {
                var res = _authService.ValidatePasswordComplexity(NewPassword);
                if (!res.IsValid)
                {
                    ErrorMessage = res.ErrorMessage;
                    return;
                }
            }

            if (NewPassword != ConfirmNewPassword)
            {
                ErrorMessage = "Новий пароль та його підтвердження не збігаються.";
                return;
            }

            // DialogHost returns true
            MaterialDesignThemes.Wpf.DialogHost.CloseDialogCommand.Execute(true, null);
        }

        private void Cancel()
        {
            // DialogHost returns false
            MaterialDesignThemes.Wpf.DialogHost.CloseDialogCommand.Execute(false, null);
        }
    }
}