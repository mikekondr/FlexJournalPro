using System.ComponentModel;
using System.Windows.Data;

namespace FlexJournalPro.Models
{
    public class BindableRow : Dictionary<string, object>, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private bool _isDirty = false;
        public virtual bool IsNewRowPlaceholder => false;
        public virtual bool IsPlaceholder => false;
        private bool _isInitialized = true;

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

        public new object this[string key]
        {
            get => this.ContainsKey(key) ? base[key] : null;
            set
            {
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

        public void MarkAsSaved()
        {
            IsDirty = false;
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Спеціальний тип заглушки на час завантаження даних
    public class PlaceholderRow : BindableRow
    {
        public override bool IsPlaceholder => true;

        public PlaceholderRow()
        {
            base["__isPlaceholder"] = true;
        }
    }

    // Спеціальний тип заглушки для введення нових даних.
    public class NewRowPlaceholder : BindableRow
    {
        public override bool IsNewRowPlaceholder => true;

        public NewRowPlaceholder()
        {
            base["__isNewRowPlaceholder"] = true;
            IsInitialized = false;
        }
    }
}