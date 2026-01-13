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
        private Dictionary<string, object> _autoFillValues = new Dictionary<string, object>();

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
        /// Значення параметрів заповнення
        /// </summary>
        public Dictionary<string, object> AutoFillValues => _autoFillValues;

        public override string ScreenId => $"JournalEditor_{_journal.Id}";

        #endregion

        #region Methods

        private void LoadTemplate()
        {
            // Спроба завантажити конфігурацію зі зліпка (snapshot)
            if (!string.IsNullOrEmpty(_journal.TemplateConfigJson))
            {
                try
                {
                    Template = System.Text.Json.JsonSerializer.Deserialize<TableTemplate>(_journal.TemplateConfigJson);
                }
                catch (System.Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Помилка десеріалізації зліпка шаблону: {ex.Message}");
                }
            }

            // Відновлюємо параметри заповнення
            if (!string.IsNullOrEmpty(_journal.AutoFillConfigJson))
            {
                try
                {
                    var autoFillParams = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(_journal.AutoFillConfigJson);
                    if (autoFillParams != null)
                    {
                        _autoFillValues = new Dictionary<string, object>(autoFillParams);
                    }
                }
                catch (System.Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Помилка завантаження параметрів заповнення: {ex.Message}");
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
