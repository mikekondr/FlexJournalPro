using FlexJournalPro.Services;
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;

namespace FlexJournalPro.Models
{
    /// <summary>
    /// Віртуальна колекція для асинхронного завантаження даних сторінками.
    /// Імітує список, але завантажує дані порціями для зменшення навантаження на UI.
    /// </summary>
    public class AsyncVirtualizingCollection : IList, INotifyCollectionChanged, INotifyPropertyChanged
    {
        #region Fields

        private readonly IJournalDataProvider _itemsProvider;
        private readonly int _pageSize = 50;
        private readonly int _loadTimeout = 3000;

        private int _count = -1;

        // Кеш сторінок: номер сторінки -> (дані сторінки)
        private readonly Dictionary<int, IList<BindableRow>> _pages = new();
        private readonly Dictionary<int, DateTime> _pageTouchTimes = new();

        // Нові елементи, ще не збережені в БД
        private readonly List<BindableRow> _newItems = new();

        // Рядок-заглушка для введення нових даних
        private NewRowPlaceholder? _newRowPlaceholder;

        #endregion

        #region Constructor

        /// <summary>
        /// Ініціалізує нову віртуальну колекцію з вказаним провайдером даних.
        /// </summary>
        /// <param name="itemsProvider">Провайдер даних для завантаження елементів.</param>
        /// <param name="isReadOnly">Якщо <c>true</c>, колекція працює лише в режимі читання.</param>
        public AsyncVirtualizingCollection(IJournalDataProvider itemsProvider, bool isReadOnly = false)
        {
            _itemsProvider = itemsProvider;
            IsReadOnly = isReadOnly;

            if (!IsReadOnly)
            {
                _newRowPlaceholder = new NewRowPlaceholder();
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Отримує загальну кількість елементів у колекції.
        /// Під час першого звернення значення підвантажується від провайдера.
        /// </summary>
        public int Count
        {
            get
            {
                if (_count == -1)
                {
                    LoadCount();
                }

                int baseCount = _count == -1 ? 0 : _count;
                return baseCount + _newItems.Count + (_newRowPlaceholder != null ? 1 : 0);
            }
        }

        /// <summary>
        /// Отримує або встановлює елемент за вказаним індексом.
        /// Під час звернення автоматично ініціює завантаження відповідної сторінки.
        /// </summary>
        /// <param name="index">Індекс елемента.</param>
        /// <returns>Елемент колекції або заглушка під час завантаження.</returns>
        public object this[int index]
        {
            get
            {
                if (_count == -1)
                {
                    LoadCount();
                }

                int dbCount = _count == -1 ? 0 : _count;
                bool isPlaceholderAtTop = IsSortDescending;

                if (isPlaceholderAtTop && _newRowPlaceholder != null)
                {
                    if (index == 0)
                    {
                        return _newRowPlaceholder;
                    }

                    index -= 1;
                }

                if (index >= dbCount)
                {
                    int newItemIndex = index - dbCount;
                    if (newItemIndex < _newItems.Count)
                    {
                        return _newItems[newItemIndex];
                    }

                    if (!isPlaceholderAtTop && _newRowPlaceholder != null && index == dbCount + _newItems.Count)
                    {
                        return _newRowPlaceholder;
                    }
                }

                int pageIndex = index / _pageSize;
                int pageOffset = index % _pageSize;
                RequestPage(pageIndex);

                if (_pages.TryGetValue(pageIndex, out var page) && page != null && pageOffset < page.Count)
                {
                    return page[pageOffset];
                }

                var loadingRow = new BindableRow();
                loadingRow["__IsLoading"] = true;
                return loadingRow;
            }
            set => throw new NotSupportedException();
        }

        /// <summary>
        /// Отримує значення, що вказує, чи колекція доступна лише для читання.
        /// </summary>
        public bool IsReadOnly { get; set; }

        public bool IsFixedSize => false;

        public object SyncRoot => this;

        public bool IsSynchronized => false;

        public IJournalDataProvider Provider => _itemsProvider;

        /// <summary>
        /// Отримує значення сортування провайдера.
        /// </summary>
        public bool IsSortDescending => _itemsProvider.IsSortDescending;

        #endregion

        #region Events

        /// <summary>
        /// Виникає при зміні колекції.
        /// </summary>
        public event NotifyCollectionChangedEventHandler CollectionChanged;

        /// <summary>
        /// Виникає при зміні значення властивості.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region Private helpers

        private void OnCollectionChanged(NotifyCollectionChangedEventArgs e) => CollectionChanged?.Invoke(this, e);

        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        /// <summary>
        /// Асинхронно завантажує загальну кількість елементів з провайдера.
        /// </summary>
        private async void LoadCount()
        {
            int dbCount = await Task.Run(() => _itemsProvider.FetchCount());
            _count = dbCount;
            OnPropertyChanged(nameof(Count));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        /// <summary>
        /// Асинхронно завантажує сторінку даних з провайдера.
        /// </summary>
        /// <param name="pageIndex">Індекс сторінки для завантаження.</param>
        private async void RequestPage(int pageIndex)
        {
            if (_pages.ContainsKey(pageIndex)) return;

            var placeholderPage = new List<BindableRow>(_pageSize);
            for (int i = 0; i < _pageSize; i++)
            {
                placeholderPage.Add(new PlaceholderRow());
            }

            _pages[pageIndex] = placeholderPage;
            _pageTouchTimes[pageIndex] = DateTime.Now;

            try
            {
                int startIndex = pageIndex * _pageSize;

                var data = await Task.Run(() => _itemsProvider.FetchRange(startIndex, _pageSize));
#if DEBUG
                App.StopTimer("FirstRowLoaded");
#endif
                var oldPage = _pages[pageIndex];
                _pages[pageIndex] = data;
                _pageTouchTimes[pageIndex] = DateTime.Now;

                bool isPlaceholderAtTop = IsSortDescending && _newRowPlaceholder != null;
                int offset = isPlaceholderAtTop ? 1 : 0;

                int replaceCount = Math.Min(data.Count, oldPage.Count);
                for (int i = 0; i < replaceCount; i++)
                {
                    int globalIndex = startIndex + i + offset;
                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                        NotifyCollectionChangedAction.Replace, data[i], oldPage[i], globalIndex));
                }

                CleanUpCache();
            }
            catch
            {
                _pages.Remove(pageIndex);
                _pageTouchTimes.Remove(pageIndex);
            }
        }

        /// <summary>
        /// Очищає застарілі сторінки кешу.
        /// </summary>
        private void CleanUpCache()
        {
            var keysToRemove = new List<int>();
            foreach (var key in _pageTouchTimes.Keys)
            {
                if ((DateTime.Now - _pageTouchTimes[key]).TotalMilliseconds > _loadTimeout)
                {
                    keysToRemove.Add(key);
                }
            }

            foreach (var key in keysToRemove)
            {
                _pages.Remove(key);
                _pageTouchTimes.Remove(key);
            }
        }

        #endregion

        #region Public methods

        /// <summary>
        /// Додає елемент до колекції.
        /// </summary>
        /// <param name="value">Елемент для додавання.</param>
        /// <returns>Індекс доданого елемента.</returns>
        public int Add(object value)
        {
            if (value is BindableRow newRow)
            {
                int dbCount = _count == -1 ? 0 : _count;
                _newItems.Add(newRow);

                int newIndex = dbCount + _newItems.Count - 1;
                if (IsSortDescending && _newRowPlaceholder != null)
                {
                    newIndex += 1;
                }

                OnPropertyChanged(nameof(Count));
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Add, newRow, newIndex));

                return newIndex;
            }

            throw new ArgumentException("Елемент повинен бути типу BindableRow");
        }

        /// <summary>
        /// Очищає всі елементи колекції та перезавантажує дані.
        /// </summary>
        public void Clear()
        {
            _count = -1;
            _pages.Clear();
            _newItems.Clear();
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        /// <summary>
        /// Визначає, чи містить колекція вказаний елемент.
        /// </summary>
        /// <param name="value">Елемент для пошуку.</param>
        /// <returns><c>true</c>, якщо елемент знайдено серед нових елементів.</returns>
        public bool Contains(object value) => value is BindableRow row && _newItems.Contains(row);

        /// <summary>
        /// Визначає індекс вказаного елемента в колекції.
        /// </summary>
        /// <param name="value">Елемент для пошуку.</param>
        /// <returns>Індекс елемента або -1, якщо не знайдено.</returns>
        public int IndexOf(object value)
        {
            if (value is BindableRow row)
            {
                int newItemIndex = _newItems.IndexOf(row);
                if (newItemIndex >= 0)
                {
                    int index = (_count == -1 ? 0 : _count) + newItemIndex;
                    if (IsSortDescending && _newRowPlaceholder != null)
                    {
                        index += 1;
                    }

                    return index;
                }
            }

            return -1;
        }

        /// <summary>
        /// Вставляє елемент у колекцію за вказаним індексом.
        /// Операція не підтримується для віртуальної колекції.
        /// </summary>
        /// <param name="index">Індекс вставки.</param>
        /// <param name="value">Елемент для вставки.</param>
        public void Insert(int index, object value) => throw new NotImplementedException();

        /// <summary>
        /// Видаляє перше входження вказаного елемента з колекції.
        /// </summary>
        /// <param name="value">Елемент для видалення.</param>
        public void Remove(object value)
        {
            if (value is BindableRow row && _newItems.Contains(row))
            {
                int index = _newItems.IndexOf(row);
                _newItems.Remove(row);

                int dbCount = _count == -1 ? 0 : _count;
                int globalIndex = dbCount + index;
                if (IsSortDescending && _newRowPlaceholder != null)
                {
                    globalIndex += 1;
                }

                OnPropertyChanged(nameof(Count));
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Remove, row, globalIndex));
            }
        }

        /// <summary>
        /// Видаляє елемент за вказаним індексом.
        /// Операція не підтримується для віртуальної колекції.
        /// </summary>
        /// <param name="index">Індекс елемента для видалення.</param>
        public void RemoveAt(int index) => throw new NotImplementedException();

        /// <summary>
        /// Очищає список нових елементів після їх збереження в БД та перезавантажує дані.
        /// </summary>
        public void RefreshAfterSave()
        {
            _newItems.Clear();
            _pages.Clear();
            _pageTouchTimes.Clear();
            _count = -1;

            _newRowPlaceholder = IsReadOnly ? null : new NewRowPlaceholder();
            LoadCount();
        }

        /// <summary>
        /// Конвертує рядок-заглушку в звичайний новий рядок.
        /// Викликається при початку редагування.
        /// </summary>
        public BindableRow ConvertPlaceholderToNewRow(NewRowPlaceholder placeholder)
        {
            if (IsReadOnly) return null;
            if (placeholder != _newRowPlaceholder) return null;

            int dbCount = _count == -1 ? 0 : _count;
            bool isTop = IsSortDescending;

            var newRow = new BindableRow();
            foreach (var key in placeholder.Keys)
            {
                if (!key.StartsWith("__"))
                {
                    newRow[key] = placeholder[key];
                }
            }

            _newItems.Add(newRow);
            _newRowPlaceholder = new NewRowPlaceholder();

            OnPropertyChanged(nameof(Count));

            if (isTop)
            {
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Replace, _newRowPlaceholder, placeholder, 0));
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Add, newRow, dbCount + _newItems.Count));
            }
            else
            {
                int oldPlaceholderIndex = dbCount + _newItems.Count - 1;
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Replace, newRow, placeholder, oldPlaceholderIndex));
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Add, _newRowPlaceholder, oldPlaceholderIndex + 1));
            }

            return newRow;
        }

        /// <summary>
        /// Повертає рядок-заглушку для прокручування до нього.
        /// </summary>
        public NewRowPlaceholder? GetPlaceholder() => _newRowPlaceholder;

        /// <summary>
        /// Копіює елементи колекції в масив. Операція не виконує дій для віртуальної колекції.
        /// </summary>
        public void CopyTo(Array array, int index) { }

        /// <summary>
        /// Повертає перерахувач для ітерації по колекції.
        /// Не використовується DataGrid при увімкненій віртуалізації.
        /// </summary>
        public IEnumerator GetEnumerator() { yield break; }

        #endregion
    }
}