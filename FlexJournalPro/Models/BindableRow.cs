using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Data;

namespace FlexJournalPro.Models
{
    public class BindableRow : Dictionary<string, object>, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private bool _isDirty = false;

        /// <summary>
        /// Визначає, чи є цей рядок рядком-заглушкою для нового запису
        /// </summary>
        public virtual bool IsNewRowPlaceholder => false;

        /// <summary>
        /// Визначає, чи є цей рядок тимчасовою заглушкою під час завантаження
        /// </summary>
        public virtual bool IsPlaceholder => false;

        private bool _isInitialized = true;

        /// <summary>
        /// Визначає, чи рядок ініціалізований даними (має значення за замовчуванням або введені дані)
        /// </summary>
        public virtual bool IsInitialized
        {
            get => _isInitialized;
            set
            {
                if (_isInitialized != value)
                {
                    _isInitialized = value;
                    OnPropertyChanged(nameof(IsInitialized));
                }
            }
        }

        /// <summary>
        /// Визначає, чи рядок був змінений але не збережений
        /// </summary>
        public bool IsDirty
        {
            get => _isDirty;
            set
            {
                if (_isDirty != value)
                {
                    _isDirty = value;
                    OnPropertyChanged(nameof(IsDirty));
                    
                    // Сповіщаємо про зміну самого об'єкта для оновлення Row Header
                    OnPropertyChanged(string.Empty);
                }
            }
        }

        // Перевизначаємо індексатор, щоб додати сповіщення
        public new object this[string key]
        {
            get
            {
                return this.ContainsKey(key) ? base[key] : null;
            }
            set
            {
                // Отримуємо попереднє значення (зведене до null якщо DBNull)
                object oldValue = this.ContainsKey(key) ? base[key] : null;
                if (oldValue == DBNull.Value) oldValue = null;

                object newValue = value;
                if (newValue == DBNull.Value) newValue = null;

                // Якщо значення не змінилося — не шлемо нотифікації
                if (EqualityComparer<object>.Default.Equals(oldValue, newValue))
                {
                    // Але якщо ключ відсутній, а присвоюється null — створюємо запис (щоб під час серіалізації/збереження він був присутній)
                    if (!this.ContainsKey(key) && value != null)
                    {
                        base[key] = value;
                    }
                    return;
                }

                base[key] = value;

                // Позначаємо рядок як змінений (крім системних полів)
                if (!key.StartsWith("__") && key != "Id")
                {
                    IsDirty = true;
                }

                // Сповіщаємо WPF про зміну індексатора та конкретної властивості
                OnPropertyChanged(Binding.IndexerName);
                OnPropertyChanged(key);
            }
        }

        /// <summary>
        /// Скидає прапорець "змінено" після збереження
        /// </summary>
        public void MarkAsSaved()
        {
            IsDirty = false;
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Specialized placeholder row used by virtualizing collection while loading
    public class PlaceholderRow : BindableRow
    {
        public override bool IsPlaceholder => true;

        public PlaceholderRow()
        {
            // Optionally set a marker value to make inspection easier
            base["__isPlaceholder"] = true;
        }
    }

    /// <summary>
    /// Спеціальний рядок-заглушка для введення нових даних.
    /// Завжди знаходиться в кінці таблиці і служить місцем для додавання нових рядків.
    /// </summary>
    public class NewRowPlaceholder : BindableRow
    {
        public override bool IsNewRowPlaceholder => true;

        public NewRowPlaceholder()
        {
            // Маркер для ідентифікації рядка-заглушки
            base["__isNewRowPlaceholder"] = true;
            IsInitialized = false;
        }
    }
}