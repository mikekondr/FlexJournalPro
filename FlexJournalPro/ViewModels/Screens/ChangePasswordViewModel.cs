using FlexJournalPro.Services;
using System.Windows.Input;

namespace FlexJournalPro.ViewModels
{
    public class ChangePasswordViewModel : ViewModelBase
    {
        private string _oldPassword = string.Empty;
        private string _newPassword = string.Empty;
        private string _confirmNewPassword = string.Empty;

        public string OldPassword
        {
            get => _oldPassword;
            set => SetProperty(ref _oldPassword, value);
        }

        public string NewPassword
        {
            get => _newPassword;
            set => SetProperty(ref _newPassword, value);
        }

        public string ConfirmNewPassword
        {
            get => _confirmNewPassword;
            set => SetProperty(ref _confirmNewPassword, value);
        }

        public ICommand ChangeCommand { get; }
        public ICommand CancelCommand { get; }

        public ChangePasswordViewModel()
        {
            ChangeCommand = new RelayCommand(ChangePassword, CanChangePassword);
            CancelCommand = new RelayCommand(Cancel);
        }

        private bool CanChangePassword()
        {
            return !string.IsNullOrEmpty(OldPassword) &&
                   !string.IsNullOrEmpty(NewPassword) &&
                   !string.IsNullOrEmpty(ConfirmNewPassword) &&
                   NewPassword == ConfirmNewPassword;
        }

        private void ChangePassword()
        {
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