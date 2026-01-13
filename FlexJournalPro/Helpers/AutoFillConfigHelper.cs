using FlexJournalPro.Models;
using System;
using System.Collections.Generic;
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
                var control = CreateParameterControl(parameter, autoFillValues, onValueChanged);
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
            Action onValueChanged)
        {
            Control inputControl;

            switch (parameter.Type)
            {
                case ColumnType.Boolean:
                    inputControl = CreateBooleanControl(parameter, autoFillValues, onValueChanged);
                    break;

                case ColumnType.Date:
                    inputControl = CreateDateControl(parameter, autoFillValues, onValueChanged);
                    break;

                case ColumnType.Dropdown:
                case ColumnType.DropdownEditable:
                    inputControl = CreateDropdownControl(parameter, autoFillValues, onValueChanged);
                    break;

                default:
                    inputControl = CreateTextControl(parameter, autoFillValues, onValueChanged);
                    break;
            }

            return inputControl;
        }

        private static CheckBox CreateBooleanControl(
            AutoFillParameter parameter, 
            Dictionary<string, object> autoFillValues,
            Action onValueChanged)
        {
            var checkBox = new CheckBox 
            { 
                Content = parameter.Label, 
                Margin = new Thickness(0, 10, 0, 10) 
            };

            bool initVal = parameter.DefaultValue is JsonElement je && je.GetBoolean();
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
            Action onValueChanged)
        {
            var datePicker = new DatePicker 
            { 
                Width = Double.NaN, 
                Margin = new Thickness(0, 0, 0, 15) 
            };

            MaterialDesignThemes.Wpf.HintAssist.SetHint(datePicker, parameter.Label);

            if (parameter.DefaultValue != null)
            {
                object val = ParseDefaultValue(parameter.DefaultValue, ColumnType.Date);
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
            Action onValueChanged)
        {
            bool isEditable = (parameter.Type == ColumnType.DropdownEditable);

            var comboBox = new ComboBox
            {
                Margin = new Thickness(0, 0, 0, 15),
                IsEditable = isEditable,
                ItemsSource = parameter.Options,
                StaysOpenOnEdit = true
            };

            MaterialDesignThemes.Wpf.HintAssist.SetHint(comboBox, parameter.Label);

            if (parameter.DefaultValue != null)
            {
                comboBox.Text = parameter.DefaultValue.ToString();
                autoFillValues[parameter.Key] = parameter.DefaultValue.ToString();
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
            Action onValueChanged)
        {
            var textBox = new TextBox { Margin = new Thickness(0, 0, 0, 15) };
            MaterialDesignThemes.Wpf.HintAssist.SetHint(textBox, parameter.Label);

            if (parameter.DefaultValue != null)
            {
                string val = parameter.DefaultValue.ToString();
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
                        string dateStr = jsonElement.GetString();
                        if (dateStr?.ToUpper() == "NOW") return DateTime.Now;
                        return DateTime.Parse(dateStr);
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
