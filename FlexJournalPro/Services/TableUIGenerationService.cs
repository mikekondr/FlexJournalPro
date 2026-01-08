using FlexJournalPro.Helpers;
using FlexJournalPro.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace FlexJournalPro.Services
{
    /// <summary>
    /// Сервіс для генерації UI елементів таблиці
    /// </summary>
    public class TableUIGenerationService
    {
        private static readonly Dictionary<string, DataTemplate> _templateCache = new Dictionary<string, DataTemplate>();

        #region Public Methods

        /// <summary>
        /// Очищує кеш шаблонів
        /// </summary>
        public static void ClearTemplateCache(string templateId = null)
        {
            if (templateId != null)
            {
                var keysToRemove = _templateCache.Keys.Where(k => k.StartsWith(templateId + "_")).ToList();
                foreach (var key in keysToRemove)
                {
                    _templateCache.Remove(key);
                }
            }
            else
            {
                _templateCache.Clear();
            }
        }

        /// <summary>
        /// Групує колонки для візуального представлення
        /// </summary>
        public List<VisualColumnGroup> GroupColumns(List<ColumnConfig> config)
        {
            var groups = new List<VisualColumnGroup>();
            VisualColumnGroup currentGroup = null;
            VisualRow currentRow = null;

            foreach (var col in config)
            {
                if (col.FieldName?.Equals("Id", StringComparison.OrdinalIgnoreCase) == true)
                    continue;

                if (col.Position == ColumnPosition.NewColumn || currentGroup == null)
                {
                    currentGroup = new VisualColumnGroup { MainConfig = col };
                    groups.Add(currentGroup);

                    if (col.Type != ColumnType.SectionHeader)
                    {
                        currentRow = new VisualRow();
                        currentRow.Items.Add(col);
                        currentGroup.Rows.Add(currentRow);
                    }
                    else
                    {
                        currentRow = null;
                    }
                }
                else if (col.Position == ColumnPosition.NextRow)
                {
                    currentRow = new VisualRow();
                    currentRow.Items.Add(col);
                    currentGroup.Rows.Add(currentRow);
                }
                else if (col.Position == ColumnPosition.SameColumn)
                {
                    if (currentRow == null)
                    {
                        currentRow = new VisualRow();
                        currentGroup.Rows.Add(currentRow);
                    }
                    currentRow.Items.Add(col);
                }
            }
            return groups;
        }

        /// <summary>
        /// Розраховує ширину групи колонок
        /// </summary>
        public double CalculateGroupWidth(VisualColumnGroup group)
        {
            double calculatedMinWidth = 0;

            foreach (var row in group.Rows)
            {
                double rowFixedSum = row.Items.Where(i => i.Width > 0).Sum(i => i.Width);
                if (rowFixedSum > calculatedMinWidth) calculatedMinWidth = rowFixedSum;
            }

            if (group.MainConfig.Width > 0)
            {
                return Math.Max(group.MainConfig.Width, calculatedMinWidth);
            }

            return calculatedMinWidth > 0 ? calculatedMinWidth : DataGridLength.Auto.Value;
        }

        /// <summary>
        /// Перевіряє, чи можна використати compiled template
        /// </summary>
        public bool CanUseCompiledTemplate(VisualColumnGroup group)
        {
            if (group.Rows.Count != 1 || group.Rows[0].Items.Count != 1)
                return false;

            var item = group.Rows[0].Items[0];
            return DataTemplateBuilder.CanUseCompiledTemplate(item);
        }

        /// <summary>
        /// Створює compiled template для перегляду
        /// </summary>
        public DataTemplate CreateCompiledViewTemplate(ColumnConfig config)
        {
            DataTemplate innerTemplate = config.Type switch
            {
                ColumnType.Boolean => DataTemplateBuilder.CreateBooleanViewTemplate(config.FieldName),
                _ => DataTemplateBuilder.CreateTextViewTemplate(
                    config.FieldName,
                    GetDisplayFormat(config),
                    !string.IsNullOrEmpty(config.Expression))
            };

            return innerTemplate;
        }

        /// <summary>
        /// Створює compiled template для редагування
        /// </summary>
        public DataTemplate CreateCompiledEditTemplate(ColumnConfig config)
        {
            DataTemplate innerTemplate = config.Type switch
            {
                ColumnType.Boolean => DataTemplateBuilder.CreateBooleanEditTemplate(config.FieldName),
                _ => DataTemplateBuilder.CreateTextEditTemplate(
                    config.FieldName,
                    config.Format,
                    !string.IsNullOrEmpty(config.Expression))
            };
            return innerTemplate;
        }

        /// <summary>
        /// Отримує або створює DataTemplate з кешу
        /// </summary>
        public DataTemplate GetOrCreateTemplate(string key, Func<string> xamlGenerator)
        {
            if (!_templateCache.ContainsKey(key))
            {
                string xaml = xamlGenerator();
                _templateCache[key] = (DataTemplate)XamlReader.Parse(xaml);
            }
            return _templateCache[key];
        }

        /// <summary>
        /// Генерує XAML для заголовка
        /// </summary>
        public string GenerateHeaderXaml(VisualColumnGroup group)
        {
            var sb = new StringBuilder();
            sb.Append(@"<DataTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"">");

            if (group.MainConfig.Type == ColumnType.SectionHeader)
            {
                sb.Append(@"<Grid>");
                sb.Append(@"<Grid.RowDefinitions><RowDefinition Height=""Auto""/><RowDefinition Height=""*""/></Grid.RowDefinitions>");
                sb.Append(@"<Border BorderThickness=""0,0,0,1"" BorderBrush=""{DynamicResource MaterialDesignDivider}"" Focusable=""False"">");
                sb.Append($@"<TextBlock Text=""{group.MainConfig.HeaderText}"" HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>");
                sb.Append(@"</Border>");

                sb.Append(@"<Grid Grid.Row=""1"">");
                GenerateSubHeadersGrid(sb, group.Rows);
                sb.Append(@"</Grid>");
                sb.Append(@"</Grid>");
            }
            else
            {
                GenerateSubHeadersGrid(sb, group.Rows);
            }

            sb.Append(@"</DataTemplate>");
            return sb.ToString();
        }

        /// <summary>
        /// Генерує XAML для клітинки
        /// </summary>
        public string GenerateCellXaml(VisualColumnGroup group, bool isEditing, ResourceDictionary resources)
        {
            var sb = new StringBuilder();
            sb.Append(@"<DataTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                              xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
                              xmlns:materialDesign=""http://materialdesigninxaml.net/winfx/xaml/themes"">");

            if (group.Rows.Count == 1 && group.Rows[0].Items.Count == 1)
            {
                var item = group.Rows[0].Items[0];
                string content = isEditing ? GenerateEditControlXaml(item, resources) : GenerateViewControlXaml(item, resources);
                sb.Append(content);
            }
            else
            {
                sb.Append(@"<Grid>");
                sb.Append(@"<Grid.RowDefinitions>");
                foreach (var _ in group.Rows) sb.Append(@"<RowDefinition Height=""*""/>");
                sb.Append(@"</Grid.RowDefinitions>");

                for (int r = 0; r < group.Rows.Count; r++)
                {
                    GenerateRowContent(sb, group.Rows[r], r, isHeader: false, isEditing: isEditing, resources: resources);
                }

                sb.Append(@"</Grid>");
            }

            sb.Append(@"</DataTemplate>");
            return sb.ToString();
        }

        #endregion

        #region Private Methods

        private string GetDisplayFormat(ColumnConfig config)
        {
            if (!string.IsNullOrEmpty(config.Format))
                return config.Format;

            return config.Type switch
            {
                ColumnType.Date => "dd.MM.yyyy",
                ColumnType.DateTime => "dd.MM.yyyy HH:mm",
                ColumnType.Time => "HH\\:mm",
                ColumnType.Currency => "C2",
                _ => ""
            };
        }

        private void GenerateSubHeadersGrid(StringBuilder sb, List<VisualRow> rows)
        {
            if (rows.Count == 1)
            {
                GenerateRowContent(sb, rows[0], 0, isHeader: true);
                return;
            }

            sb.Append(@"<Grid>");
            sb.Append(@"<Grid.RowDefinitions>");
            foreach (var _ in rows) sb.Append(@"<RowDefinition Height=""*""/>");
            sb.Append(@"</Grid.RowDefinitions>");

            for (int r = 0; r < rows.Count; r++)
            {
                GenerateRowContent(sb, rows[r], r, isHeader: true);
            }
            sb.Append(@"</Grid>");
        }

        private void GenerateRowContent(StringBuilder sb, VisualRow row, int rowIndex, bool isHeader, bool isEditing = false, ResourceDictionary resources = null)
        {
            if (row.Items.Count == 1)
            {
                var item = row.Items[0];
                string topBorder = (isHeader && rowIndex > 0) ? "0,1,0,0" : "0";
                string padding = isHeader ? "5" : "0";

                if (isHeader)
                {
                    sb.Append($@"<Border Grid.Row=""{rowIndex}"" BorderThickness=""{topBorder}"" 
                         BorderBrush=""{{DynamicResource MaterialDesignDivider}}"" Padding=""{padding}"" Focusable=""False"">");
                    sb.Append(GenerateHeaderControl(item));
                    sb.Append(@"</Border>");
                }
                else
                {
                    string gridRow = rowIndex > 0 ? $@" Grid.Row=""{rowIndex}""" : "";

                    if (isEditing)
                    {
                        string xaml = GenerateEditControlXaml(item, resources);
                        sb.Append(xaml.Replace("<TextBox ", $"<TextBox{gridRow} ")
                                     .Replace("<ComboBox ", $"<ComboBox{gridRow} ")
                                     .Replace("<DatePicker ", $"<DatePicker{gridRow} ")
                                     .Replace("<CheckBox ", $"<CheckBox{gridRow} ")
                                     .Replace("<materialDesign:TimePicker ", $"<materialDesign:TimePicker{gridRow} "));
                    }
                    else
                    {
                        string xaml = GenerateViewControlXaml(item, resources);
                        sb.Append(xaml.Replace("<TextBlock ", $"<TextBlock{gridRow} ")
                                     .Replace("<CheckBox ", $"<CheckBox{gridRow} "));
                    }
                }
            }
            else
            {
                string topBorder = (isHeader && rowIndex > 0) ? "0,1,0,0" : "0";

                sb.Append($@"<Grid Grid.Row=""{rowIndex}"">");
                sb.Append(GenerateColumnDefinitionsXaml(row.Items));

                for (int c = 0; c < row.Items.Count; c++)
                {
                    var item = row.Items[c];
                    string leftBorder = (c > 0) ? "1,0,0,0" : "0";
                    string padding = isHeader ? "5" : "0";
                    string tabIndex = isEditing ? $@" TabIndex=""{c}""" : "";

                    if (isHeader)
                    {
                        sb.Append($@"<Border Grid.Column=""{c}"" BorderThickness=""{leftBorder}"" 
                             BorderBrush=""{{DynamicResource MaterialDesignDivider}}"" Padding=""{padding}"" Focusable=""False"">");
                        sb.Append(GenerateHeaderControl(item));
                        sb.Append(@"</Border>");
                    }
                    else
                    {
                        if (isEditing)
                        {
                            string xaml = GenerateEditControlXaml(item, resources);
                            sb.Append(xaml.Replace("<TextBox ", $"<TextBox Grid.Column=\"{c}\"{tabIndex} ")
                                         .Replace("<ComboBox ", $"<ComboBox Grid.Column=\"{c}\"{tabIndex} ")
                                         .Replace("<DatePicker ", $"<DatePicker Grid.Column=\"{c}\"{tabIndex} ")
                                         .Replace("<CheckBox ", $"<CheckBox Grid.Column=\"{c}\"{tabIndex} ")
                                         .Replace("<materialDesign:TimePicker ", $"<materialDesign:TimePicker Grid.Column=\"{c}\"{tabIndex} "));
                        }
                        else
                        {
                            string xaml = GenerateViewControlXaml(item, resources);
                            sb.Append(xaml.Replace("<TextBlock ", $"<TextBlock Grid.Column=\"{c}\" ")
                                         .Replace("<CheckBox ", $"<CheckBox Grid.Column=\"{c}\" "));
                        }
                    }
                }

                sb.Append(@"</Grid>");
            }
        }

        private string GenerateHeaderControl(ColumnConfig item)
        {
            string result = string.Empty;
            string label = "(no label)";

            if (item.Header != null) label = item.IsRequired ? $"{item.Header.Text} *" : item.Header.Text;
            else label = item.IsRequired ? $"{item.HeaderText} *" : item.HeaderText;

            result = $@"<TextBlock Text=""{label}"" VerticalAlignment=""Center"" HorizontalAlignment=""Center"" TextAlignment=""Center""";

            if (item.Header != null)
            {
                if (item.Header.Size > 0)
                    result += $@" FontSize=""{item.Header.Size}"">";
                else result += ">";

                if (item.Header.Direction == ColumnHeaderDirection.Vertical)
                {
                    result += $@"<TextBlock.LayoutTransform><RotateTransform Angle=""-90""/></TextBlock.LayoutTransform>";
                }
            }
            else result += " TextWrapping=\"Wrap\" >";

            result += @"</TextBlock>";

            return result;
        }

        private string GenerateEditControlXaml(ColumnConfig col, ResourceDictionary resources)
        {
            return col.Type switch
            {
                ColumnType.Dropdown or ColumnType.DropdownEditable => GenerateDropdownXaml(col, resources),
                ColumnType.Date => GenerateDatePickerXaml(col),
                ColumnType.Time => GenerateTimePickerXaml(col, resources),
                ColumnType.DateTime => GenerateDateTimePickerXaml(col),
                ColumnType.Boolean => GenerateCheckBoxXaml(col),
                _ => GenerateTextBoxXaml(col)
            };
        }

        private string GenerateViewControlXaml(ColumnConfig col, ResourceDictionary resources)
        {
            if (col.Type == ColumnType.SectionHeader) return "";

            if (col.Type == ColumnType.Boolean)
                return GenerateBooleanViewXaml(col);

            return GenerateTextBlockViewXaml(col, resources);
        }

        private string GenerateDropdownXaml(ColumnConfig col, ResourceDictionary resources)
        {
            string resKey = $"Options_{col.FieldName}";
            if (resources != null && !resources.Contains(resKey))
                resources.Add(resKey, col.Options ?? new List<string>());

            bool isEditable = col.Type == ColumnType.DropdownEditable;

            return $@"<ComboBox ItemsSource=""{{DynamicResource {resKey}}}""
                        Tag=""{col.FieldName}""
                        Text=""{{Binding [{col.FieldName}], Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}}""
                        IsEditable=""{isEditable}"" StaysOpenOnEdit=""True""
                        Focusable=""True"" IsTabStop=""True""
                        materialDesign:TextFieldAssist.HasClearButton=""True"" 
                        BorderThickness=""0"" Background=""Transparent""
                        Padding=""5,2"" VerticalAlignment=""Center"" HorizontalAlignment=""Stretch""/>";
        }

        private string GenerateDatePickerXaml(ColumnConfig col)
        {
            return $@"<DatePicker xmlns:helpers=""clr-namespace:FlexJournalPro.Helpers;assembly=FlexJournalPro""
                        SelectedDate=""{{Binding [{col.FieldName}], Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}}"" 
                        Tag=""{col.FieldName}""
                        Focusable=""True"" IsTabStop=""True""
                        materialDesign:TextFieldAssist.HasClearButton=""False""
                        BorderThickness=""0"" Background=""Transparent""
                        VerticalAlignment=""Center"" HorizontalAlignment=""Stretch""
                        helpers:DatePickerHelper.EnableFastInput=""True""/>";
        }

        private string GenerateTimePickerXaml(ColumnConfig col, ResourceDictionary resources)
        {
            if (resources != null && !resources.Contains("TimeSpanToNullableDateTimeConverter"))
            {
                resources.Add("TimeSpanToNullableDateTimeConverter",
                    new FlexJournalPro.Converters.TimeSpanToNullableDateTimeConverter());
            }

            return $@"<materialDesign:TimePicker xmlns:helpers=""clr-namespace:FlexJournalPro.Helpers;assembly=FlexJournalPro""
                          SelectedTime=""{{Binding [{col.FieldName}], Mode=TwoWay, UpdateSourceTrigger=PropertyChanged, Converter={{StaticResource TimeSpanToNullableDateTimeConverter}}}}""
                          Tag=""{col.FieldName}""
                          Focusable=""True"" IsTabStop=""True""
                          Is24Hours=""True""
                          BorderThickness=""0"" Background=""Transparent""
                          VerticalAlignment=""Center"" HorizontalAlignment=""Stretch""
                          helpers:TimePickerHelper.EnableFastInput=""True""/>";
        }

        private string GenerateDateTimePickerXaml(ColumnConfig col)
        {
            return $@"<Grid>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width=""*""/>
                            <ColumnDefinition Width=""80""/>
                        </Grid.ColumnDefinitions>
                        
                        <DatePicker xmlns:helpers=""clr-namespace:FlexJournalPro.Helpers;assembly=FlexJournalPro""
                            Grid.Column=""0"" 
                            SelectedDate=""{{Binding [{col.FieldName}], Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}}"" 
                            materialDesign:TextFieldAssist.HasClearButton=""False""
                            Tag=""{col.FieldName}""
                            Focusable=""True"" IsTabStop=""True""
                            BorderThickness=""0"" Background=""Transparent""
                            VerticalAlignment=""Center"" HorizontalAlignment=""Stretch""
                            helpers:DatePickerHelper.EnableFastInput=""True""/>

                        <materialDesign:TimePicker xmlns:helpers=""clr-namespace:FlexJournalPro.Helpers;assembly=FlexJournalPro""
                            Grid.Column=""1"" 
                            SelectedTime=""{{Binding [{col.FieldName}], Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}}"" 
                            Is24Hours=""True""
                            Margin=""5,0,0,0""
                            Tag=""{col.FieldName}""
                            Focusable=""True"" IsTabStop=""True""
                            BorderThickness=""0"" Background=""Transparent""
                            VerticalAlignment=""Center"" HorizontalAlignment=""Stretch""
                            helpers:TimePickerHelper.EnableFastInput=""True""/>
                      </Grid>";
        }

        private string GenerateCheckBoxXaml(ColumnConfig col)
        {
            string bindingDef = $"Binding [{col.FieldName}], Mode=TwoWay, UpdateSourceTrigger=PropertyChanged, Converter={{StaticResource SafeBoolConverter}}";
            return $@"<CheckBox IsChecked=""{{{bindingDef}}}""
                        Tag=""{col.FieldName}""
                        Focusable=""True"" IsTabStop=""True""
                        HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>";
        }

        private string GenerateTextBoxXaml(ColumnConfig col)
        {
            bool isCalculated = !string.IsNullOrEmpty(col.Expression);
            string mode = isCalculated ? "OneWay" : "TwoWay";

            string readOnlyProps = isCalculated ? @"IsReadOnly=""True"" Focusable=""False"" IsTabStop=""False"" Foreground=""Gray"" FontStyle=""Italic""" : "";

            string updateTrigger = isCalculated ? "PropertyChanged" : "LostFocus";

            string bindingDef = $"Binding [{col.FieldName}], Mode={mode}, UpdateSourceTrigger={updateTrigger}, ValidatesOnExceptions=True, TargetNullValue={{}}, FallbackValue={{}}";

            return $@"<TextBox Text=""{{{bindingDef}}}""
                       Tag=""{col.FieldName}""
                       BorderThickness=""0"" Background=""Transparent""
                       Padding=""4,2"" VerticalAlignment=""Stretch"" VerticalContentAlignment=""Center""
                       {readOnlyProps} />";
        }

        private string GenerateBooleanViewXaml(ColumnConfig col)
        {
            string bindingDef = BuildViewBindingDefinition(col);

            return $@"<CheckBox IsChecked=""{{{bindingDef}}}""
                        IsEnabled=""True""
                        HorizontalAlignment=""Center"" 
                        VerticalAlignment=""Center""
                        IsHitTestVisible=""False""/>";
        }

        private string GenerateTextBlockViewXaml(ColumnConfig col, ResourceDictionary resources)
        {
            string bindingDef = BuildViewBindingDefinition(col, resources);
            string readOnlyProps = !string.IsNullOrEmpty(col.Expression)
                ? @"Focusable=""False"" Foreground=""Gray"" FontStyle=""Italic"""
                : "";

            return $@"<TextBlock Text=""{{{bindingDef}}}"" 
                         VerticalAlignment=""Center"" 
                         HorizontalAlignment=""Stretch""
                         Padding=""4,2""
                         TextTrimming=""CharacterEllipsis""
                         {readOnlyProps}/>";
        }

        private string BuildViewBindingDefinition(ColumnConfig col, ResourceDictionary resources = null)
        {
            var binding = $"Binding [{col.FieldName}], Mode=OneWay, TargetNullValue={{}}, FallbackValue={{}}";

            if (col.Type == ColumnType.Time)
            {
                if (resources != null && !resources.Contains("TimeSpanToNullableDateTimeConverter"))
                {
                    resources.Add("TimeSpanToNullableDateTimeConverter",
                        new FlexJournalPro.Converters.TimeSpanToNullableDateTimeConverter());
                }
                binding += ", Converter={StaticResource TimeSpanToNullableDateTimeConverter}";
            }

            if (col.Type is ColumnType.Date or ColumnType.DateTime or ColumnType.Time)
            {
                string format = GetDisplayFormat(col);
                binding += $", StringFormat={format}";
            }
            else if (col.Type == ColumnType.Boolean)
            {
                binding += ", Converter={{StaticResource SafeBoolConverter}}";
            }
            else if (!string.IsNullOrEmpty(col.Format))
            {
                binding += $", StringFormat={col.Format}";
            }

            return binding;
        }

        private string GenerateColumnDefinitionsXaml(List<ColumnConfig> items)
        {
            var sb = new StringBuilder();
            sb.Append(@"<Grid.ColumnDefinitions>");

            foreach (var item in items)
            {
                string width = item.Width > 0
                    ? item.Width.ToString(CultureInfo.InvariantCulture)
                    : "*";
                sb.Append($@"<ColumnDefinition Width=""{width}""/>");
            }

            sb.Append(@"</Grid.ColumnDefinitions>");
            return sb.ToString();
        }

        #endregion
    }

    #region Helper Classes

    /// <summary>
    /// Візуальна група колонок
    /// </summary>
    public class VisualColumnGroup
    {
        public ColumnConfig MainConfig { get; set; }
        public List<VisualRow> Rows { get; set; } = new List<VisualRow>();
    }

    /// <summary>
    /// Візуальний рядок у групі
    /// </summary>
    public class VisualRow
    {
        public List<ColumnConfig> Items { get; set; } = new List<ColumnConfig>();
    }

    #endregion
}
