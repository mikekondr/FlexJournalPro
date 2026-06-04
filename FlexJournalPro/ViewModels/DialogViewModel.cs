using MaterialDesignThemes.Wpf;

namespace FlexJournalPro.ViewModels
{
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
}
