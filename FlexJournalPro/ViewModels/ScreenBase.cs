using MaterialDesignThemes.Wpf;

namespace FlexJournalPro.ViewModels
{
    /// <summary>
    /// Базовий клас для всіх екранів (Screens)
    /// </summary>
    public abstract class ScreenBase : ViewModelBase
    {
        private string _title = string.Empty;
        private PackIconKind _icon = PackIconKind.FileDocument;

        /// <summary>
        /// Заголовок екрану (відображається у вкладці)
        /// </summary>
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        /// <summary>
        /// Іконка екрану
        /// </summary>
        public PackIconKind Icon
        {
            get => _icon;
            set => SetProperty(ref _icon, value);
        }

        /// <summary>
        /// Унікальний ідентифікатор екрану для запобігання дублюванню
        /// </summary>
        public abstract string ScreenId { get; }

        /// <summary>
        /// Викликається при закритті екрану
        /// </summary>
        public virtual void OnClosing()
        {
            // Можна додати логіку збереження даних перед закриттям
        }
    }
}
