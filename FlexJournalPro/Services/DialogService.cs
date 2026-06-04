using FlexJournalPro.ViewModels;
using MaterialDesignThemes.Wpf;
using System.Windows;

namespace FlexJournalPro.Services
{
    /// <summary>
    /// Сервіс для відображення модальних діалогів і toast-повідомлень.
    /// Підтримує як Material Design діалоги, так і fallback на стандартні MessageBox.
    /// </summary>
    public class DialogService
    {
        #region Fields

        private static string _dialogHostIdentifier = "RootDialogHost";
        private static ISnackbarMessageQueue? _messageQueue;

        #endregion

        #region Configuration

        /// <summary>
        /// Встановлює чергу повідомлень для toast-нотифікацій.
        /// </summary>
        public static void SetMessageQueue(ISnackbarMessageQueue messageQueue)
        {
            _messageQueue = messageQueue;
        }

        /// <summary>
        /// Встановлює ідентифікатор DialogHost для відображення діалогів.
        /// </summary>
        public static void SetDialogHostIdentifier(string identifier)
        {
            _dialogHostIdentifier = identifier;
        }

        #endregion

        #region Public methods - Standard dialogs

        /// <summary>
        /// Показує інформаційне повідомлення.
        /// </summary>
        public static async Task<DialogResult> ShowInformationAsync(string message, string title = "Інформація")
        {
            return await ShowDialogAsync(title, message, DialogType.Information, DialogButtons.OK);
        }

        /// <summary>
        /// Показує повідомлення про помилку.
        /// </summary>
        public static async Task<DialogResult> ShowErrorAsync(string message, string title = "Помилка")
        {
            return await ShowDialogAsync(title, message, DialogType.Error, DialogButtons.OK);
        }

        /// <summary>
        /// Показує попередження.
        /// </summary>
        public static async Task<DialogResult> ShowWarningAsync(string message, string title = "Попередження")
        {
            return await ShowDialogAsync(title, message, DialogType.Warning, DialogButtons.OK);
        }

        /// <summary>
        /// Показує повідомлення про успіх.
        /// </summary>
        public static async Task<DialogResult> ShowSuccessAsync(string message, string title = "Успіх")
        {
            return await ShowDialogAsync(title, message, DialogType.Success, DialogButtons.OK);
        }

        #endregion

        #region Public methods - Confirmation dialogs

        /// <summary>
        /// Показує діалог підтвердження (Так/Ні).
        /// </summary>
        public static async Task<DialogResult> ShowConfirmationAsync(string message, string title = "Підтвердження")
        {
            return await ShowDialogAsync(title, message, DialogType.Question, DialogButtons.YesNo);
        }

        /// <summary>
        /// Показує діалог підтвердження з можливістю скасування.
        /// </summary>
        public static async Task<DialogResult> ShowConfirmationWithCancelAsync(string message, string title = "Підтвердження")
        {
            return await ShowDialogAsync(title, message, DialogType.Question, DialogButtons.YesNoCancel);
        }

        #endregion

        #region Public methods - Custom content

        /// <summary>
        /// Показує діалог з довільним вмістом (UI або ViewModel).
        /// </summary>
        public static async Task<object?> ShowCustomDialogAsync(object content)
        {
            try
            {
                return await DialogHost.Show(content, _dialogHostIdentifier);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Dialog error: {ex.Message}");
                MessageBox.Show(content?.ToString() ?? "Помилка відображення діалогу",
                    "Діалог", MessageBoxButton.OK);
                return null;
            }
        }

        #endregion

        #region Public methods - Toast notifications

        /// <summary>
        /// Показує toast-повідомлення на короткий час.
        /// </summary>
        /// <param name="message">Текст повідомлення.</param>
        /// <param name="promote">Якщо <c>true</c>, скине попереднє повідомлення та покаже негайно.</param>
        public static void ShowToast(string message, bool promote = true)
        {
            _messageQueue?.Enqueue(message, null, null, null, promote, true, TimeSpan.FromSeconds(3));
        }

        /// <summary>
        /// Показує toast-повідомлення з однією дією.
        /// </summary>
        /// <param name="message">Текст повідомлення.</param>
        /// <param name="actionContent">Текст кнопки дії.</param>
        /// <param name="actionHandler">Обробник кліку на кнопку дії.</param>
        public static void ShowToastWithAction(string message, string actionContent, Action actionHandler)
        {
            _messageQueue?.Enqueue(message, actionContent, actionHandler, promote: true);
        }

        #endregion

        #region Private helpers

        /// <summary>
        /// Основний метод показу діалогу через DialogHost.
        /// </summary>
        private static async Task<DialogResult> ShowDialogAsync(
            string title,
            string message,
            DialogType type,
            DialogButtons buttons)
        {
            var viewModel = new DialogViewModel
            {
                Title = title,
                Message = message,
                DialogType = type,
                Buttons = buttons
            };

            try
            {
                var result = await DialogHost.Show(viewModel, _dialogHostIdentifier);
                return result is DialogResult dialogResult ? dialogResult : DialogResult.None;
            }
            catch (InvalidOperationException)
            {
                // DialogHost не знайдено — fallback на MessageBox
                return ShowMessageBoxFallback(message, title, type, buttons);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Dialog error: {ex.Message}");
                return ShowMessageBoxFallback(message, title, type, buttons);
            }
        }

        /// <summary>
        /// Fallback на стандартний MessageBox при недоступності DialogHost.
        /// </summary>
        private static DialogResult ShowMessageBoxFallback(
            string message,
            string title,
            DialogType type,
            DialogButtons buttons)
        {
            var mbButtons = buttons switch
            {
                DialogButtons.OK => MessageBoxButton.OK,
                DialogButtons.OKCancel => MessageBoxButton.OKCancel,
                DialogButtons.YesNo => MessageBoxButton.YesNo,
                DialogButtons.YesNoCancel => MessageBoxButton.YesNoCancel,
                _ => MessageBoxButton.OK
            };

            var mbIcon = type switch
            {
                DialogType.Error => MessageBoxImage.Error,
                DialogType.Warning => MessageBoxImage.Warning,
                DialogType.Question => MessageBoxImage.Question,
                DialogType.Information or DialogType.Success => MessageBoxImage.Information,
                _ => MessageBoxImage.None
            };

            var mbResult = MessageBox.Show(message, title, mbButtons, mbIcon);

            return mbResult switch
            {
                MessageBoxResult.OK => DialogResult.OK,
                MessageBoxResult.Cancel => DialogResult.Cancel,
                MessageBoxResult.Yes => DialogResult.Yes,
                MessageBoxResult.No => DialogResult.No,
                _ => DialogResult.None
            };
        }

        #endregion
    }
}

