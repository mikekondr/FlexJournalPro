using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading.Tasks;
using FlexJournalPro.Services;

namespace FlexJournalPro.Models
{
    /// <summary>
    /// Віртуальна колекція для асинхронного завантаження даних сторінками.
    /// Імітує список, але завантажує дані порціями для оптимізації продуктивності.
    /// </summary>
    public class AsyncVirtualizingCollection : IList, INotifyCollectionChanged, INotifyPropertyChanged
    {
        private readonly IItemsProvider _itemsProvider;
        private readonly int _pageSize = 50; // Розмір порції
        private readonly int _loadTimeout = 3000; // Час життя сторінки в кеші (мс)

        private int _count = -1;

        // Кеш сторінок: Номер сторінки -> (Дані, Час останнього доступу)
        private readonly Dictionary<int, IList<BindableRow>> _pages = new Dictionary<int, IList<BindableRow>>();
        private readonly Dictionary<int, DateTime> _pageTouchTimes = new Dictionary<int, DateTime>();

        // Список нових елементів (не збережених у БД)
        private readonly List<BindableRow> _newItems = new List<BindableRow>();

        // Рядок-заглушка для введення нових даних
        private NewRowPlaceholder _newRowPlaceholder;

        /// <summary>
        /// Ініціалізує нову віртуальну колекцію з вказаним провайдером даних.
        /// </summary>
        /// <param name="itemsProvider">Провайдер даних для завантаження елементів.</param>
        public AsyncVirtualizingCollection(IItemsProvider itemsProvider)
        {
            _itemsProvider = itemsProvider;
            _newRowPlaceholder = new NewRowPlaceholder();
        }

        // --- Властивості інтерфейсів ---

        /// <summary>
        /// Отримує загальну кількість елементів у колекції.
        /// Автоматично завантажує значення з провайдера при першому зверненні.
        /// </summary>
        public int Count
        {
            get
            {
                if (_count == -1)
                {
                    _count = 0; // Тимчасово, поки вантажимо
                    LoadCount();
                }
                // Повертаємо кількість з БД + нові елементи + 1 рядок-заглушка
                return _count + _newItems.Count + 1;
            }
        }

        /// <summary>
        /// Отримує або встановлює елемент за вказаним індексом.
        /// При зверненні до елементу автоматично завантажує відповідну сторінку даних.
        /// </summary>
        /// <param name="index">Індекс елементу.</param>
        /// <returns>Елемент колекції або заглушка під час завантаження.</returns>
        public object this[int index]
        {
            get
            {
                int dbCount = _count == -1 ? 0 : _count;
                int newItemsStartIndex = dbCount;
                int placeholderIndex = dbCount + _newItems.Count;

                // Перевіряємо, чи це індекс рядка-заглушки
                if (index == placeholderIndex)
                {
                    return _newRowPlaceholder;
                }
                
                // Перевіряємо, чи це індекс нового елементу
                if (index >= newItemsStartIndex && index < placeholderIndex)
                {
                    // Це новий елемент
                    return _newItems[index - newItemsStartIndex];
                }
                
                // Визначаємо, яка сторінка нам треба (для елементів з БД)
                int pageIndex = index / _pageSize;
                int pageOffset = index % _pageSize;

                // Спробуємо отримати з кешу
                if (_pages.ContainsKey(pageIndex))
                {
                    var page = _pages[pageIndex];
                    if (page == null)
                    {
                        // Якщо сторінка помічена як null (старий режим), повертаємо заглушку
                        var placeholder = new PlaceholderRow();
                        return placeholder;
                    }

                    _pageTouchTimes[pageIndex] = DateTime.Now; // Оновлюємо час доступу

                    if (pageOffset < page.Count)
                        return page[pageOffset];
                }
                else
                {
                    // Сторінки немає - запускаємо завантаження
                    RequestPage(pageIndex);
                }

                // Поки вантажиться, повертаємо заглушку (унікальну для цього індексу)
                var ph = new PlaceholderRow();
                return ph;
            }
            set => throw new NotSupportedException("Редагування через індексатор не підтримується прямо");
        }

        // --- Асинхронне завантаження ---

        /// <summary>
        /// Асинхронно завантажує загальну кількість елементів з провайдера.
        /// </summary>
        private async void LoadCount()
        {
            int dbCount = await Task.Run(() => _itemsProvider.FetchCount());
            _count = dbCount;
            OnPropertyChanged(nameof(Count));
            // Кажемо WPF повністю перемалювати список, бо змінилася кількість
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        /// <summary>
        /// Асинхронно завантажує сторінку даних з провайдера.
        /// </summary>
        /// <param name="pageIndex">Індекс сторінки для завантаження.</param>
        private async void RequestPage(int pageIndex)
        {
            // Щоб не запитувати ту саму сторінку сто разів, поки вона вантажиться
            if (_pages.ContainsKey(pageIndex)) return;

            // Створюємо сторінку заглушок (унікальна заглушка на кожен індекс)
            var placeholderPage = new List<BindableRow>(_pageSize);
            for (int i = 0; i < _pageSize; i++)
            {
                var ph = new PlaceholderRow();
                placeholderPage.Add(ph);
            }

            _pages[pageIndex] = placeholderPage;
            _pageTouchTimes[pageIndex] = DateTime.Now;

            try
            {
                int startIndex = pageIndex * _pageSize;

                var data = await Task.Run(() => _itemsProvider.FetchRange(startIndex, _pageSize));

                // Зберігаємо в кеш фактичні дані
                var oldPage = _pages[pageIndex];
                _pages[pageIndex] = data;
                _pageTouchTimes[pageIndex] = DateTime.Now;

                // Сповіщаємо UI, що дані змінилися в діапазоні цієї сторінки
                // Замінюємо заглушки на реальні елементи без глобального Reset
                int replaceCount = Math.Min(data.Count, oldPage.Count);
                for (int i = 0; i < replaceCount; i++)
                {
                    int globalIndex = startIndex + i;
                    var oldItem = oldPage[i];
                    var newItem = data[i];
                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                        NotifyCollectionChangedAction.Replace, newItem, oldItem, globalIndex));
                }

                // Очищення старого кешу
                CleanUpCache();
            }
            catch
            {
                _pages.Remove(pageIndex); // Видалити маркер завантаження при помилці
                _pageTouchTimes.Remove(pageIndex);
            }
        }

        /// <summary>
        /// Очищає застарілі сторінки з кешу.
        /// Видаляє сторінки, до яких не зверталися протягом заданого таймауту.
        /// </summary>
        private void CleanUpCache()
        {
            // Видаляємо сторінки, які не чіпали більше N секунд
            var keysToRemove = new List<int>();
            foreach (var key in _pageTouchTimes.Keys)
            {
                if ((DateTime.Now - _pageTouchTimes[key]).TotalMilliseconds > _loadTimeout)
                    keysToRemove.Add(key);
            }
            foreach (var key in keysToRemove)
            {
                _pages.Remove(key);
                _pageTouchTimes.Remove(key);
            }
        }

        // --- Реалізація решти інтерфейсів (стандартна заглушка) ---
        
        /// <summary>
        /// Отримує значення, що вказує, чи колекція доступна тільки для читання.
        /// </summary>
        public bool IsReadOnly => false; // Дозволяємо додавання
        
        /// <summary>
        /// Отримує значення, що вказує, чи має колекція фіксований розмір.
        /// </summary>
        public bool IsFixedSize => false;
        
        /// <summary>
        /// Отримує об'єкт для синхронізації доступу до колекції.
        /// </summary>
        public object SyncRoot => this;
        
        /// <summary>
        /// Отримує значення, що вказує, чи синхронізований доступ до колекції.
        /// </summary>
        public bool IsSynchronized => false;

        /// <summary>
        /// Виникає при зміні колекції.
        /// </summary>
        public event NotifyCollectionChangedEventHandler CollectionChanged;
        
        /// <summary>
        /// Виникає при зміні значення властивості.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Викликає подію CollectionChanged.
        /// </summary>
        /// <param name="e">Аргументи події.</param>
        private void OnCollectionChanged(NotifyCollectionChangedEventArgs e) => CollectionChanged?.Invoke(this, e);
        
        /// <summary>
        /// Викликає подію PropertyChanged.
        /// </summary>
        /// <param name="name">Назва властивості.</param>
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        /// <summary>
        /// Додає елемент до колекції.
        /// </summary>
        /// <param name="value">Елемент для додавання.</param>
        /// <returns>Індекс доданого елементу.</returns>
        public int Add(object value)
        {
            if (value is BindableRow newRow)
            {
                int dbCount = _count == -1 ? 0 : _count;
                _newItems.Add(newRow);
                int newIndex = dbCount + _newItems.Count - 1;
                
                OnPropertyChanged(nameof(Count));
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Add, newRow, newIndex));
                
                return newIndex;
            }
            throw new ArgumentException("Елемент повинен бути типу BindableRow");
        }
        
        /// <summary>
        /// Очищає всі елементи з колекції та перезавантажує дані.
        /// </summary>
        public void Clear() 
        { 
            _count = 0; 
            _pages.Clear(); 
            _newItems.Clear();
            LoadCount(); 
        }
        
        /// <summary>
        /// Визначає, чи містить колекція вказаний елемент.
        /// </summary>
        /// <param name="value">Елемент для пошуку.</param>
        /// <returns>True, якщо елемент знайдено в нових елементах.</returns>
        public bool Contains(object value) => value is BindableRow row && _newItems.Contains(row);
        
        /// <summary>
        /// Визначає індекс вказаного елементу в колекції.
        /// </summary>
        /// <param name="value">Елемент для пошуку.</param>
        /// <returns>Індекс елементу або -1, якщо не знайдено.</returns>
        public int IndexOf(object value)
        {
            if (value is BindableRow row)
            {
                int newItemIndex = _newItems.IndexOf(row);
                if (newItemIndex >= 0)
                {
                    return (_count == -1 ? 0 : _count) + newItemIndex;
                }
            }
            return -1;
        }
        
        /// <summary>
        /// Вставляє елемент у колекцію за вказаним індексом. Операція не підтримується.
        /// </summary>
        /// <param name="index">Індекс вставки.</param>
        /// <param name="value">Елемент для вставки.</param>
        /// <exception cref="NotImplementedException">Операція не підтримується для віртуальної колекції.</exception>
        public void Insert(int index, object value) => throw new NotImplementedException();
        
        /// <summary>
        /// Видаляє перше входження вказаного елементу з колекції.
        /// </summary>
        /// <param name="value">Елемент для видалення.</param>
        public void Remove(object value)
        {
            if (value is BindableRow row && _newItems.Contains(row))
            {
                int index = _newItems.IndexOf(row);
                _newItems.Remove(row);
                
                int dbCount = _count == -1 ? 0 : _count;
                OnPropertyChanged(nameof(Count));
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Remove, row, dbCount + index));
            }
        }
        
        /// <summary>
        /// Видаляє елемент за вказаним індексом. Операція не підтримується.
        /// </summary>
        /// <param name="index">Індекс елементу для видалення.</param>
        /// <exception cref="NotImplementedException">Операція не підтримується для віртуальної колекції.</exception>
        public void RemoveAt(int index) => throw new NotImplementedException();
        
        /// <summary>
        /// Очищає список нових елементів після їх збереження в БД та перезавантажує дані
        /// </summary>
        public void RefreshAfterSave()
        {
            _newItems.Clear();
            _pages.Clear();
            _pageTouchTimes.Clear();
            _count = -1;
            
            // Створюємо новий рядок-заглушку
            _newRowPlaceholder = new NewRowPlaceholder();
            
            LoadCount();
        }

        /// <summary>
        /// Конвертує рядок-заглушку в звичайний новий рядок (викликається при початку редагування)
        /// </summary>
        public BindableRow ConvertPlaceholderToNewRow(NewRowPlaceholder placeholder)
        {
            if (placeholder != _newRowPlaceholder) return null;

            // Додаємо поточний рядок-заглушку до списку нових елементів
            int dbCount = _count == -1 ? 0 : _count;
            int oldPlaceholderIndex = dbCount + _newItems.Count;
            
            // Конвертуємо в звичайний BindableRow
            var newRow = new BindableRow();
            foreach (var key in placeholder.Keys)
            {
                if (!key.StartsWith("__")) // Пропускаємо системні маркери
                {
                    newRow[key] = placeholder[key];
                }
            }
            
            _newItems.Add(newRow);

            // Створюємо новий рядок-заглушку
            _newRowPlaceholder = new NewRowPlaceholder();

            // Сповіщаємо про зміни
            OnPropertyChanged(nameof(Count));
            // Заміна старого placeholder на newRow
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Replace, newRow, placeholder, oldPlaceholderIndex));
            // Додавання нового placeholder
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Add, _newRowPlaceholder, oldPlaceholderIndex + 1));

            return newRow;
        }

        /// <summary>
        /// Повертає рядок-заглушку для прокручування до нього
        /// </summary>
        public NewRowPlaceholder GetPlaceholder()
        {
            return _newRowPlaceholder;
        }
        
        /// <summary>
        /// Копіює елементи колекції в масив. Операція не виконує дій для віртуальної колекції.
        /// </summary>
        /// <param name="array">Масив призначення.</param>
        /// <param name="index">Індекс початку копіювання.</param>
        public void CopyTo(Array array, int index) { }
        
        /// <summary>
        /// Повертає перечислювач для ітерації по колекції.
        /// Не використовується DataGrid при увімкненій віртуалізації.
        /// </summary>
        /// <returns>Порожній перечислювач.</returns>
        public IEnumerator GetEnumerator() { yield break; } // Не використовується DataGrid при віртуалізації
    }
}