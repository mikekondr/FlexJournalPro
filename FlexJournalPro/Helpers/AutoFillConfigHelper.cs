using FlexJournalPro.Models;
using FlexJournalPro.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace FlexJournalPro.Helpers
{
    /// <summary>
    /// Допоміжний клас для роботи з параметрами заповнення
    /// </summary>
    public static class AutoFillConfigHelper
    {
        private static readonly List<string> SystemUsers = new List<string>();

        static AutoFillConfigHelper()
        {
            var app = Application.Current as App;
            if (app?.ServiceProvider != null)
            {
                var _db = app.ServiceProvider.GetRequiredService<IDatabaseService>();
                _db.GetAllUsers().ForEach(u => SystemUsers.Add(u.FullName));
            }
        }

        /// <summary>
        /// Створює UI панель для редагування параметрів заповнення
        /// </summary>
        public static void BuildAutoFillPanel(
            Panel panel,
            List<AutoFillParameter> parameters,
            Dictionary<string, object> autoFillValues,
            Action? onValueChanged = null)
        {
            panel.Children.Clear();

            // Save current values to preserve loaded data
            var currentValues = new Dictionary<string, object>(autoFillValues);

            autoFillValues.Clear();

            if (parameters == null || parameters.Count == 0)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = "Немає параметрів заповнення",
                    Opacity = 0.5,
                    FontStyle = FontStyles.Italic
                });
                return;
            }

            foreach (var parameter in parameters)
            {
                // Determine initial value: priority to saved values
                object initialValue = parameter.DefaultValue;
                if (currentValues.TryGetValue(parameter.Key, out var savedVal))
                {
                    initialValue = savedVal;
                }

                var control = CreateParameterControl(parameter, autoFillValues, onValueChanged, initialValue);
                if (control != null)
                {
                    panel.Children.Add(control);
                }
            }
        }

        /// <summary>
        /// Створює контрол для одного параметра заповнення
        /// </summary>
        private static Control CreateParameterControl(
            AutoFillParameter parameter,
            Dictionary<string, object> autoFillValues,
            Action? onValueChanged,
            object initialValue)
        {
            Control inputControl;

            switch (parameter.Type)
            {
                case ColumnType.Boolean:
                    inputControl = CreateBooleanControl(parameter, autoFillValues, onValueChanged, initialValue);
                    break;

                case ColumnType.Date:
                    inputControl = CreateDateControl(parameter, autoFillValues, onValueChanged, initialValue);
                    break;

                case ColumnType.Dropdown:
                case ColumnType.DropdownEditable:
                    inputControl = CreateDropdownControl(parameter, autoFillValues, onValueChanged, initialValue);
                    break;

                default:
                    inputControl = CreateTextControl(parameter, autoFillValues, onValueChanged, initialValue);
                    break;
            }

            return inputControl;
        }

        private static CheckBox CreateBooleanControl(
            AutoFillParameter parameter,
            Dictionary<string, object> autoFillValues,
            Action? onValueChanged,
            object initialValue)
        {
            var checkBox = new CheckBox
            {
                Content = parameter.Label,
                Margin = new Thickness(0, 10, 0, 10)
            };

            bool initVal = false;

            if (initialValue is bool b) initVal = b;
            else if (initialValue is JsonElement je && (je.ValueKind == JsonValueKind.True || je.ValueKind == JsonValueKind.False))
                initVal = je.GetBoolean();
            else if (initialValue != null)
                bool.TryParse(initialValue.ToString(), out initVal);

            checkBox.IsChecked = initVal;
            autoFillValues[parameter.Key] = initVal;

            checkBox.Checked += (s, e) =>
            {
                autoFillValues[parameter.Key] = true;
                onValueChanged?.Invoke();
            };

            checkBox.Unchecked += (s, e) =>
            {
                autoFillValues[parameter.Key] = false;
                onValueChanged?.Invoke();
            };

            return checkBox;
        }

        private static DatePicker CreateDateControl(
            AutoFillParameter parameter,
            Dictionary<string, object> autoFillValues,
            Action? onValueChanged,
            object initialValue)
        {
            var datePicker = new DatePicker
            {
                Width = Double.NaN,
                Margin = new Thickness(0, 0, 0, 15)
            };

            MaterialDesignThemes.Wpf.HintAssist.SetHint(datePicker, parameter.Label);

            if (initialValue != null)
            {
                object val = ParseDefaultValue(initialValue, ColumnType.Date);
                if (val is DateTime d)
                {
                    datePicker.SelectedDate = d;
                    autoFillValues[parameter.Key] = d;
                }
            }

            datePicker.SelectedDateChanged += (s, e) =>
            {
                if (datePicker.SelectedDate.HasValue)
                    autoFillValues[parameter.Key] = datePicker.SelectedDate.Value;
                else
                    autoFillValues.Remove(parameter.Key);

                onValueChanged?.Invoke();
            };

            return datePicker;
        }

        private static ComboBox CreateDropdownControl(
            AutoFillParameter parameter,
            Dictionary<string, object> autoFillValues,
            Action? onValueChanged,
            object initialValue)
        {
            bool isEditable = (parameter.Type == ColumnType.DropdownEditable);

            // Клонуємо базові опції з шаблону
            var options = parameter.Options != null ? new List<string>(parameter.Options) : new List<string>();

            // Підміна макросу %%ALL_USERS%% на реальний список користувачів
            if (options.Contains("%%ALL_USERS%%"))
            {
                // Додаємо макрос для поточного користувача
                options.Add("%%CURRENT_USER%%");

                options.Remove("%%ALL_USERS%%");
                if (SystemUsers != null) 
                    options.AddRange(SystemUsers);
            }

            var comboBox = new ComboBox
            {
                Margin = new Thickness(0, 0, 0, 15),
                IsEditable = isEditable,
                ItemsSource = options.Distinct().ToList(),
                StaysOpenOnEdit = true
            };

            MaterialDesignThemes.Wpf.HintAssist.SetHint(comboBox, parameter.Label);

            if (initialValue != null)
            {
                string val = initialValue.ToString();
                comboBox.Text = val;
                autoFillValues[parameter.Key] = val;
            }

            if (isEditable)
            {
                comboBox.AddHandler(TextBoxBase.TextChangedEvent,
                    new TextChangedEventHandler((s, e) =>
                    {
                        autoFillValues[parameter.Key] = comboBox.Text;
                        onValueChanged?.Invoke();
                    }));
            }

            comboBox.SelectionChanged += (s, e) =>
            {
                if (comboBox.SelectedValue != null)
                {
                    autoFillValues[parameter.Key] = comboBox.SelectedValue.ToString();
                    onValueChanged?.Invoke();
                }
            };

            return comboBox;
        }

        private static TextBox CreateTextControl(
            AutoFillParameter parameter,
            Dictionary<string, object> autoFillValues,
            Action? onValueChanged,
            object initialValue)
        {
            var textBox = new TextBox { Margin = new Thickness(0, 0, 0, 15) };
            MaterialDesignThemes.Wpf.HintAssist.SetHint(textBox, parameter.Label);

            if (initialValue != null)
            {
                string val = initialValue.ToString();
                textBox.Text = val;
                autoFillValues[parameter.Key] = val;
            }

            textBox.TextChanged += (s, e) =>
            {
                autoFillValues[parameter.Key] = textBox.Text;
                onValueChanged?.Invoke();
            };

            return textBox;
        }

        private static object ParseDefaultValue(object rawValue, ColumnType targetType)
        {
            if (rawValue is JsonElement jsonElement)
            {
                switch (targetType)
                {
                    case ColumnType.Date:
                    case ColumnType.DateTime:
                    case ColumnType.Time:
                        string dateStr = jsonElement.ToString();
                        if (jsonElement.ValueKind == JsonValueKind.String)
                            dateStr = jsonElement.GetString();

                        if (dateStr?.ToUpper() == "NOW") return DateTime.Now;
                        if (DateTime.TryParse(dateStr, out var d)) return d;
                        return dateStr;
                    case ColumnType.Text:
                        string textStr = jsonElement.ToString();
                        if (jsonElement.ValueKind == JsonValueKind.String)
                            textStr = jsonElement.GetString();
                        if (textStr?.ToUpper() == "USERNAME") return App.CurrentUser?.FullName ?? App.CurrentUser?.Login ?? "Невідомий користувач";
                        return textStr;
                    default:
                        return jsonElement.ToString();
                }
            }

            if (targetType == ColumnType.Date || targetType == ColumnType.DateTime || targetType == ColumnType.Time)
            {
                if (rawValue.ToString().ToUpper() == "NOW")
                {
                    return DateTime.Now;
                }
            }

            return rawValue;
        }
    }
}
