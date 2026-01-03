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
    /// Допоміжний клас для роботи з сеансовими константами
    /// </summary>
    public static class SessionConstantsHelper
    {
        /// <summary>
        /// Створює UI панель для редагування сеансових констант
        /// </summary>
        public static void BuildConstantsPanel(
            Panel panel, 
            List<SessionConstant> constants, 
            Dictionary<string, object> sessionValues,
            Action onValueChanged = null)
        {
            panel.Children.Clear();
            sessionValues.Clear();

            if (constants == null || constants.Count == 0)
            {
                panel.Children.Add(new TextBlock 
                { 
                    Text = "Немає налаштувань", 
                    Opacity = 0.5, 
                    FontStyle = FontStyles.Italic 
                });
                return;
            }

            foreach (var constant in constants)
            {
                var control = CreateConstantControl(constant, sessionValues, onValueChanged);
                if (control != null)
                {
                    panel.Children.Add(control);
                }
            }
        }

        /// <summary>
        /// Створює контрол для одної сеансової константи
        /// </summary>
        private static Control CreateConstantControl(
            SessionConstant constant, 
            Dictionary<string, object> sessionValues,
            Action onValueChanged)
        {
            Control inputControl;

            switch (constant.Type)
            {
                case ColumnType.Boolean:
                    inputControl = CreateBooleanControl(constant, sessionValues, onValueChanged);
                    break;

                case ColumnType.Date:
                    inputControl = CreateDateControl(constant, sessionValues, onValueChanged);
                    break;

                case ColumnType.Dropdown:
                case ColumnType.DropdownEditable:
                    inputControl = CreateDropdownControl(constant, sessionValues, onValueChanged);
                    break;

                default:
                    inputControl = CreateTextControl(constant, sessionValues, onValueChanged);
                    break;
            }

            return inputControl;
        }

        private static CheckBox CreateBooleanControl(
            SessionConstant constant, 
            Dictionary<string, object> sessionValues,
            Action onValueChanged)
        {
            var checkBox = new CheckBox 
            { 
                Content = constant.Label, 
                Margin = new Thickness(0, 10, 0, 10) 
            };

            bool initVal = constant.DefaultValue is JsonElement je && je.GetBoolean();
            checkBox.IsChecked = initVal;
            sessionValues[constant.Key] = initVal;

            checkBox.Checked += (s, e) =>
            {
                sessionValues[constant.Key] = true;
                onValueChanged?.Invoke();
            };

            checkBox.Unchecked += (s, e) =>
            {
                sessionValues[constant.Key] = false;
                onValueChanged?.Invoke();
            };

            return checkBox;
        }

        private static DatePicker CreateDateControl(
            SessionConstant constant, 
            Dictionary<string, object> sessionValues,
            Action onValueChanged)
        {
            var datePicker = new DatePicker 
            { 
                Width = Double.NaN, 
                Margin = new Thickness(0, 0, 0, 15) 
            };

            MaterialDesignThemes.Wpf.HintAssist.SetHint(datePicker, constant.Label);

            if (constant.DefaultValue != null)
            {
                object val = ParseDefaultValue(constant.DefaultValue, ColumnType.Date);
                if (val is DateTime d)
                {
                    datePicker.SelectedDate = d;
                    sessionValues[constant.Key] = d;
                }
            }

            datePicker.SelectedDateChanged += (s, e) =>
            {
                if (datePicker.SelectedDate.HasValue)
                    sessionValues[constant.Key] = datePicker.SelectedDate.Value;
                else
                    sessionValues.Remove(constant.Key);

                onValueChanged?.Invoke();
            };

            return datePicker;
        }

        private static ComboBox CreateDropdownControl(
            SessionConstant constant, 
            Dictionary<string, object> sessionValues,
            Action onValueChanged)
        {
            bool isEditable = (constant.Type == ColumnType.DropdownEditable);

            var comboBox = new ComboBox
            {
                Margin = new Thickness(0, 0, 0, 15),
                IsEditable = isEditable,
                ItemsSource = constant.Options,
                StaysOpenOnEdit = true
            };

            MaterialDesignThemes.Wpf.HintAssist.SetHint(comboBox, constant.Label);

            if (constant.DefaultValue != null)
            {
                comboBox.Text = constant.DefaultValue.ToString();
                sessionValues[constant.Key] = constant.DefaultValue.ToString();
            }

            if (isEditable)
            {
                comboBox.AddHandler(TextBoxBase.TextChangedEvent,
                    new TextChangedEventHandler((s, e) =>
                    {
                        sessionValues[constant.Key] = comboBox.Text;
                        onValueChanged?.Invoke();
                    }));
            }

            comboBox.SelectionChanged += (s, e) =>
            {
                if (comboBox.SelectedValue != null)
                {
                    sessionValues[constant.Key] = comboBox.SelectedValue.ToString();
                    onValueChanged?.Invoke();
                }
            };

            return comboBox;
        }

        private static TextBox CreateTextControl(
            SessionConstant constant, 
            Dictionary<string, object> sessionValues,
            Action onValueChanged)
        {
            var textBox = new TextBox { Margin = new Thickness(0, 0, 0, 15) };
            MaterialDesignThemes.Wpf.HintAssist.SetHint(textBox, constant.Label);

            if (constant.DefaultValue != null)
            {
                string val = constant.DefaultValue.ToString();
                textBox.Text = val;
                sessionValues[constant.Key] = val;
            }

            textBox.TextChanged += (s, e) =>
            {
                sessionValues[constant.Key] = textBox.Text;
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
