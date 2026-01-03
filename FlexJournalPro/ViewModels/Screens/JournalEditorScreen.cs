using FlexJournalPro.Models;
using FlexJournalPro.Services;
using MaterialDesignThemes.Wpf;
using System.Collections.Generic;

namespace FlexJournalPro.ViewModels.Screens
{
    /// <summary>
    /// Screen для редагування журналу
    /// </summary>
    public class JournalEditorScreen : ScreenBase
    {
        private readonly JournalMetadata _journal;
        private readonly DatabaseService _dbService;
        private readonly MainViewModel _mainViewModel;
        private TableTemplate? _template;
        private Dictionary<string, object> _sessionValues = new Dictionary<string, object>();

        public JournalEditorScreen(JournalMetadata journal, DatabaseService dbService, MainViewModel mainViewModel)
        {
            _journal = journal;
            _dbService = dbService;
            _mainViewModel = mainViewModel;

            Title = journal.Title;
            Icon = PackIconKind.BookEdit;

            // Завантажуємо шаблон та дані
            LoadTemplate();
        }

        #region Properties

        /// <summary>
        /// Метадані журналу
        /// </summary>
        public JournalMetadata Journal => _journal;

        /// <summary>
        /// Шаблон журналу
        /// </summary>
        public TableTemplate? Template
        {
            get => _template;
            private set => SetProperty(ref _template, value);
        }

        /// <summary>
        /// Сеансові значення
        /// </summary>
        public Dictionary<string, object> SessionValues => _sessionValues;

        public override string ScreenId => $"JournalEditor_{_journal.Id}";

        #endregion

        #region Methods

        private void LoadTemplate()
        {
            Template = _dbService.GetTemplate(_journal.PresetId);
            
            // Відновлюємо сеансові константи
            if (!string.IsNullOrEmpty(_journal.SessionConstantsJson))
            {
                try
                {
                    var constants = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(_journal.SessionConstantsJson);
                    if (constants != null)
                    {
                        _sessionValues = new Dictionary<string, object>(constants);
                    }
                }
                catch (System.Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Помилка завантаження констант: {ex.Message}");
                }
            }
        }

        public override void OnClosing()
        {
            // Можна додати логіку збереження перед закриттям
            base.OnClosing();
        }

        #endregion
    }
}
