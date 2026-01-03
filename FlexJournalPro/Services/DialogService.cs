using MaterialDesignThemes.Wpf;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace FlexJournalPro.Services
{
    /// <summary>
    /// Тип діалогового вікна
    /// </summary>
    public enum DialogType
    {
        Information,
        Warning,
        Error,
        Question,
        Success
    }

    /// <summary>
    /// Результат діалогу
    /// </summary>
    public enum DialogResult
    {
        None,
        OK,
        Cancel,
        Yes,
        No
    }

    /// <summary>
    /// Кнопки діалогу
    /// </summary>
    public enum DialogButtons
    {
        OK,
        OKCancel,
        YesNo,
        YesNoCancel
    }

    /// <summary>
    /// Сервіс для відображення модальних повідомлень
    /// </summary>
    public class DialogService
    {
        private static string _dialogHostIdentifier = "RootDialogHost";

        /// <summary>
        /// Встановлює ідентифікатор DialogHost для відображення діалогів
        /// </summary>
        public static void SetDialogHostIdentifier(string identifier)
        {
            _dialogHostIdentifier = identifier;
        }

        /// <summary>
        /// Показує інформаційне повідомлення
        /// </summary>
        public static async Task<DialogResult> ShowInformationAsync(string message, string title = "Інформація")
        {
            return await ShowDialogAsync(title, message, DialogType.Information, DialogButtons.OK);
        }

        /// <summary>
        /// Показує повідомлення про помилку
        /// </summary>
        public static async Task<DialogResult> ShowErrorAsync(string message, string title = "Помилка")
        {
            return await ShowDialogAsync(title, message, DialogType.Error, DialogButtons.OK);
        }

        /// <summary>
        /// Показує попередження
        /// </summary>
        public static async Task<DialogResult> ShowWarningAsync(string message, string title = "Попередження")
        {
            return await ShowDialogAsync(title, message, DialogType.Warning, DialogButtons.OK);
        }

        /// <summary>
        /// Показує повідомлення про успіх
        /// </summary>
        public static async Task<DialogResult> ShowSuccessAsync(string message, string title = "Успіх")
        {
            return await ShowDialogAsync(title, message, DialogType.Success, DialogButtons.OK);
        }

        /// <summary>
        /// Показує діалог підтвердження (Так/Ні)
        /// </summary>
        public static async Task<DialogResult> ShowConfirmationAsync(string message, string title = "Підтвердження")
        {
            return await ShowDialogAsync(title, message, DialogType.Question, DialogButtons.YesNo);
        }

        /// <summary>
        /// Показує діалог підтвердження з можливістю скасування
        /// </summary>
        public static async Task<DialogResult> ShowConfirmationWithCancelAsync(string message, string title = "Підтвердження")
        {
            return await ShowDialogAsync(title, message, DialogType.Question, DialogButtons.YesNoCancel);
        }

        /// <summary>
        /// Показує діалог з кастомним вмістом
        /// </summary>
        public static async Task<object> ShowCustomDialogAsync(object content)
        {
            try
            {
                return await DialogHost.Show(content, _dialogHostIdentifier);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Dialog error: {ex.Message}");
                // Fallback до MessageBox при помилці
                MessageBox.Show(content?.ToString() ?? "Помилка відображення діалогу", 
                    "Діалог", MessageBoxButton.OK);
                return null;
            }
        }

        /// <summary>
        /// Основний метод показу діалогу
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
                
                if (result is DialogResult dialogResult)
                {
                    return dialogResult;
                }
                
                return DialogResult.None;
            }
            catch (InvalidOperationException)
            {
                // DialogHost не знайдено - використовуємо MessageBox як fallback
                return ShowMessageBoxFallback(message, title, type, buttons);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Dialog error: {ex.Message}");
                return ShowMessageBoxFallback(message, title, type, buttons);
            }
        }

        /// <summary>
        /// Fallback до стандартного MessageBox
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
    }

    /// <summary>
    /// ViewModel для діалогового вікна
    /// </summary>
    public class DialogViewModel
    {
        public string Title { get; set; }
        public string Message { get; set; }
        public DialogType DialogType { get; set; }
        public DialogButtons Buttons { get; set; }

        public PackIconKind Icon => DialogType switch
        {
            DialogType.Information => PackIconKind.Information,
            DialogType.Warning => PackIconKind.Alert,
            DialogType.Error => PackIconKind.AlertCircle,
            DialogType.Question => PackIconKind.HelpCircle,
            DialogType.Success => PackIconKind.CheckCircle,
            _ => PackIconKind.Information
        };

        public string IconColor => DialogType switch
        {
            DialogType.Information => "#2196F3",
            DialogType.Warning => "#FF9800",
            DialogType.Error => "#F44336",
            DialogType.Question => "#9C27B0",
            DialogType.Success => "#4CAF50",
            _ => "#757575"
        };
    }
}
