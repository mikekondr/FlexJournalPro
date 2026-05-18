using System.Collections.ObjectModel;
using System.Linq;
using FlexJournalPro.Models;
using FlexJournalPro.Services;

namespace FlexJournalPro.ViewModels.Screens
{
    public enum LogViewMode
    {
        AllEvents,
        SystemOnly,
        JournalOnly
    }

    public class LogsScreen : ScreenBase
    {
        private readonly ILogService _logService;
        private readonly IDatabaseService _dbService;

        public LogsScreen(ILogService logService, IDatabaseService dbService, MainViewModel mainViewModel)
        {
            Title = "Журнал подій";
            _logService = logService;
            _dbService = dbService;

            Logs = new ObservableCollection<LogEntry>();
            AvailableJournals = new ObservableCollection<JournalMetadata>();

            RefreshCommand = new RelayCommand(LoadLogs);

            LoadJournals();
            SelectedMode = LogViewMode.AllEvents; // Завантажить логи автоматично через setter
        }

        public ObservableCollection<LogEntry> Logs { get; }
        public ObservableCollection<JournalMetadata> AvailableJournals { get; }
        public RelayCommand RefreshCommand { get; }

        private LogViewMode _selectedMode;
        public LogViewMode SelectedMode
        {
            get => _selectedMode;
            set
            {
                _selectedMode = value;
                OnPropertyChanged(nameof(SelectedMode));
                OnPropertyChanged(nameof(IsJournalSelectionVisible));
                LoadLogs();
            }
        }

        private JournalMetadata? _selectedJournal;
        public JournalMetadata? SelectedJournal
        {
            get => _selectedJournal;
            set
            {
                _selectedJournal = value;
                OnPropertyChanged(nameof(SelectedJournal));
                if (SelectedMode == LogViewMode.JournalOnly)
                {
                    LoadLogs();
                }
            }
        }

        // Властивість для керування видимістю випадаючого списку журналів у View
        public bool IsJournalSelectionVisible => SelectedMode == LogViewMode.JournalOnly;

        public override string ScreenId => "Logging";

        private void LoadJournals()
        {
            AvailableJournals.Clear();
            // Припускаємо, що у SqliteDatabaseService є метод отримання всіх журналів
            var journals = _dbService.GetAllJournals();
            foreach (var j in journals)
            {
                AvailableJournals.Add(j);
            }
        }

        private void LoadLogs()
        {
            Logs.Clear();
            IEnumerable<LogEntry> result = Enumerable.Empty<LogEntry>();

            switch (SelectedMode)
            {
                case LogViewMode.AllEvents:
                    result = _logService.GetAllLogs();
                    break;
                case LogViewMode.SystemOnly:
                    result = _logService.GetSystemLogs();
                    break;
                case LogViewMode.JournalOnly:
                    if (SelectedJournal != null)
                    {
                        result = _logService.GetJournalLogs(SelectedJournal.TableName);
                    }
                    break;
            }

            foreach (var log in result)
            {
                Logs.Add(log);
            }
        }
    }
}