using FlexJournalPro.Models;

namespace FlexJournalPro.Services
{
    /// <summary>
    /// Провайдер даних журналу для віртуалізованого завантаження.
    /// Підтримує пагінацію, сортування та підрахунок записів.
    /// </summary>
    public interface IJournalDataProvider
    {
        #region Data fetching

        /// <summary>
        /// Отримує загальну кількість записів у журналі.
        /// </summary>
        /// <returns>Кількість записів.</returns>
        int FetchCount();

        /// <summary>
        /// Отримує діапазон записів журналу з пагінацією та сортуванням.
        /// </summary>
        /// <param name="startIndex">Індекс першого запису для отримання.</param>
        /// <param name="count">Кількість записів для отримання.</param>
        /// <returns>Список привязаних рядків журналу.</returns>
        IList<BindableRow> FetchRange(int startIndex, int count);

        #endregion

        #region Sorting

        /// <summary>
        /// Отримує або встановлює, чи сортувати записи за спаданням.
        /// </summary>
        bool IsSortDescending { get; set; }

        /// <summary>
        /// Отримує або встановлює назву колони для сортування.
        /// </summary>
        string SortColumn { get; set; }

        #endregion
    }
}