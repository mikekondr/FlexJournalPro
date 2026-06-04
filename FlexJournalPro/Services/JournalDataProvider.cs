using FlexJournalPro.Models;

namespace FlexJournalPro.Services
{
    /// <summary>
    /// Реалізація провайдера даних журналу для віртуалізованого завантаження записів.
    /// Керує пагінацією, сортуванням та отриманням даних з БД.
    /// </summary>
    public class JournalDataProvider : IJournalDataProvider
    {
        #region Fields

        private readonly IDatabaseService _dbService;
        private readonly string _tableName;
        private readonly List<ColumnConfig> _columns;

        #endregion

        #region Properties

        /// <summary>
        /// Отримує або встановлює, чи сортувати записи за спаданням.
        /// </summary>
        public bool IsSortDescending { get; set; } = true;

        /// <summary>
        /// Отримує або встановлює назву колони для сортування.
        /// </summary>
        public string SortColumn { get; set; } = "Id";

        #endregion

        #region Constructor

        /// <summary>
        /// Ініціалізує новий екземпляр класу <see cref="JournalDataProvider"/>.
        /// </summary>
        /// <param name="dbService">Сервіс для роботи з базою даних.</param>
        /// <param name="tableName">Назва таблиці журналу.</param>
        /// <param name="columns">Список конфігурацій колон журналу.</param>
        public JournalDataProvider(IDatabaseService dbService, string tableName, List<ColumnConfig> columns)
        {
            _dbService = dbService;
            _tableName = tableName;
            _columns = columns;

            // Якщо є поле RegNumber, будемо сортувати за ним за замовчуванням
            if (columns.Any(c => c.FieldName == "RegNumber"))
            {
                SortColumn = "RegNumber";
            }
        }

        #endregion

        #region Public methods

        /// <summary>
        /// Отримує загальну кількість записів у журналі.
        /// </summary>
        /// <returns>Кількість записів у журналі.</returns>
        public int FetchCount()
        {
            return _dbService.GetJournalCount(_tableName);
        }

        /// <summary>
        /// Отримує діапазон записів журналу з пагінацією та сортуванням.
        /// Дані отримуються з сервісу БД і конвертуються у привязані рядки.
        /// </summary>
        /// <param name="startIndex">Індекс першого запису для отримання.</param>
        /// <param name="count">Кількість записів для отримання.</param>
        /// <returns>Список привязаних рядків журналу.</returns>
        public IList<BindableRow> FetchRange(int startIndex, int count)
        {
            return _dbService.FetchRange(_tableName, startIndex, count, _columns, SortColumn, IsSortDescending);
        }

        #endregion
    }
}