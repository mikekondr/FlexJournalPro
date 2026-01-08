using FlexJournalPro.Helpers;
using FlexJournalPro.Models;
using FlexJournalPro.Services;
using FlexJournalPro.ViewModels;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace FlexJournalPro.Views
{
    /// <summary>
    /// Аргументи події збереження рядка даних.
    /// </summary>
    public class RowSavedEventArgs : EventArgs
    {
        /// <summary>
        /// Отримує або встановлює дані збереженого рядка.
        /// </summary>
        public BindableRow RowData { get; set; }
    }

    /// <summary>
    /// Користувацький контрол для динамічної таблиці з підтримкою віртуалізації та складного лейауту.
    /// Використовує MVVM pattern для розділення UI та бізнес-логіки.
    /// </summary>
    public partial class DynamicTableView : UserControl
    {
        #region Fields

        private readonly DynamicTableViewModel _viewModel;
        private readonly TableUIGenerationService _uiService;
        private Point? lastClickPosition;

        #endregion

        #region Events

        /// <summary>
        /// Виникає при збереженні рядка даних.
        /// </summary>
        public event EventHandler<RowSavedEventArgs> RowSaved;

        #endregion

        #region Constructor

        /// <summary>
        /// Ініціалізує новий екземпляр класу DynamicTableView.
        /// </summary>
        public DynamicTableView()
        {
            InitializeComponent();

            _viewModel = new DynamicTableViewModel();
            _uiService = new TableUIGenerationService();

            // Прив'язка до ViewModel
            DataContext = _viewModel;

            // Підписка на події ViewModel
            _viewModel.RowSaved += ViewModel_RowSaved;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            // Підписка на події DataGrid
            DynamicGrid.PreparingCellForEdit += DynamicGrid_PreparingCellForEdit;
            DynamicGrid.PreviewMouseLeftButtonDown += DynamicGrid_PreviewMouseLeftButtonDown;
            DynamicGrid.PreviewKeyDown += DynamicGrid_PreviewKeyDown;
            DynamicGrid.PreviewTextInput += DynamicGrid_PreviewTextInput;
            DynamicGrid.RowEditEnding += DynamicGrid_RowEditEnding;
            DynamicGrid.PreviewMouseWheel += DynamicGrid_PreviewMouseWheel;

            this.Loaded += DynamicTableView_Loaded;
            this.Unloaded += DynamicTableView_Unloaded;

            EventManager.RegisterClassHandler(typeof(TextBox),
                UIElement.GotFocusEvent,
                new RoutedEventHandler(TextBox_GotFocus));
        }

        #endregion

        #region Public API

        /// <summary>
        /// Завантажує шаблон таблиці, будує інтерфейс та структуру даних.
        /// </summary>
        public void LoadTemplate(TableTemplate template)
        {
            if (template == null) return;

            _viewModel.LoadTemplate(template);
            BuildGridStructure(template.Columns);
        }

        /// <summary>
        /// Завантажує шаблон з бази даних (з кешуванням).
        /// </summary>
        public void LoadTemplateFromDatabase(DatabaseService dbService, string templateId)
        {
            _viewModel.LoadTemplateFromDatabase(dbService, templateId);
            BuildGridStructure(_viewModel.CurrentTemplate.Columns);
        }

        /// <summary>
        /// Очищує кеш шаблонів.
        /// </summary>
        public static void ClearTemplateCache(string templateId = null)
        {
            DynamicTableViewModel.ClearTemplateCache(templateId);
            TableUIGenerationService.ClearTemplateCache(templateId);
        }

        /// <summary>
        /// Встановлює віртуальне джерело даних для таблиці.
        /// </summary>
        public void SetVirtualDataSource(DatabaseService dbService, string tableName)
        {
            _viewModel.SetVirtualDataSource(dbService, tableName);
        }

        /// <summary>
        /// Застосовує сеансові значення.
        /// </summary>
        public void ApplySessionValues(Dictionary<string, object> values)
        {
            _viewModel.ApplySessionValues(values);
        }

        /// <summary>
        /// Отримує поточні сеансові значення.
        /// </summary>
        public Dictionary<string, object> GetSessionValues()
        {
            return _viewModel.GetSessionValues();
        }

        /// <summary>
        /// Отримує поточний шаблон.
        /// </summary>
        public TableTemplate GetCurrentTemplate()
        {
            return _viewModel.CurrentTemplate;
        }

        /// <summary>
        /// Додає новий порожній рядок до таблиці та починає його редагування.
        /// </summary>
        public void AddNewRow()
        {
            try
            {
                if (_viewModel?.VirtualData == null)
                {
                    System.Diagnostics.Debug.WriteLine("VirtualData is null, cannot add new row");
                    return;
                }

                // Отримуємо рядок-заглушку з віртуальної колекції
                var placeholder = _viewModel.VirtualData.GetPlaceholder();

                // Прокручуємо до рядка-заглушки
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    ScrollToNewRow(placeholder);
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error scrolling to placeholder row: {ex.Message}");
            }
        }

        #endregion

        #region ViewModel Event Handlers

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DynamicTableViewModel.VirtualData))
            {
                // Оновлюємо ItemsSource при зміні віртуальних даних
                DynamicGrid.ItemsSource = _viewModel.VirtualData;
            }
        }

        private void ViewModel_RowSaved(object sender, RowSavedEventArgs e)
        {
            // Пробрасуємо подію далі
            RowSaved?.Invoke(this, e);
        }

        #endregion

        #region UI Generation

        private void BuildGridStructure(List<ColumnConfig> config)
        {
            DynamicGrid.Columns.Clear();
            ClearResources();

            var groups = _uiService.GroupColumns(config);
            ConfigureGridSorting(config);

            var columns = new List<DataGridColumn>(groups.Count);

            foreach (var group in groups)
            {
                var templateCol = new DataGridTemplateColumn
                {
                    HeaderStyle = CreateHeaderStyle(),
                    CellStyle = CreateCellStyle(),
                    Width = _uiService.CalculateGroupWidth(group)
                };

                ConfigureSorting(templateCol, group);

                // Header template
                try
                {
                    string fieldName = group?.MainConfig?.FieldName ?? "Unknown";
                    string headerKey = $"{_viewModel.CurrentTemplate.Id}_Header_{fieldName}";
                    templateCol.HeaderTemplate = _uiService.GetOrCreateTemplate(headerKey, 
                        () => _uiService.GenerateHeaderXaml(group));
                }
                catch (Exception ex)
                {
                    templateCol.Header = group?.MainConfig?.HeaderText ?? "Column";
                    System.Diagnostics.Debug.WriteLine($"Header XAML Error: {ex.Message}");
                }

                // Cell templates
                bool useCompiledTemplates = _uiService.CanUseCompiledTemplate(group);

                if (useCompiledTemplates)
                {
                    var item = group.Rows[0].Items[0];
                    templateCol.CellTemplate = _uiService.CreateCompiledViewTemplate(item);
                    templateCol.CellEditingTemplate = _uiService.CreateCompiledEditTemplate(item);
                }
                else
                {
                    try
                    {
                        string fieldName = group?.MainConfig?.FieldName ?? "Unknown";
                        string viewKey = $"{_viewModel.CurrentTemplate.Id}_View_{fieldName}";
                        templateCol.CellTemplate = _uiService.GetOrCreateTemplate(viewKey,
                            () => _uiService.GenerateCellXaml(group, isEditing: false, DynamicGrid.Resources));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"View XAML Error: {ex.Message}");
                    }

                    try
                    {
                        string fieldName = group?.MainConfig?.FieldName ?? "Unknown";
                        string editKey = $"{_viewModel.CurrentTemplate.Id}_Edit_{fieldName}";
                        templateCol.CellEditingTemplate = _uiService.GetOrCreateTemplate(editKey,
                            () => _uiService.GenerateCellXaml(group, isEditing: true, DynamicGrid.Resources));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"EditControl XAML Error: {ex.Message}");
                    }
                }

                columns.Add(templateCol);
            }

            // Оптимізація: відключаємо ItemsSource при додаванні колонок
            var itemsSource = DynamicGrid.ItemsSource;
            DynamicGrid.ItemsSource = null;

            foreach (var col in columns)
            {
                DynamicGrid.Columns.Add(col);
            }

            DynamicGrid.ItemsSource = itemsSource;
        }

        private Style CreateHeaderStyle()
        {
            return (Style)this.Resources["DynamicTableColumnHeaderStyle"];
        }

        private Style CreateCellStyle()
        {
            return (Style)this.Resources["DynamicTableCellStyle"];
        }

        private void ConfigureSorting(DataGridTemplateColumn col, VisualColumnGroup group)
        {
            string sortField = group?.MainConfig?.FieldName;

            if (group?.MainConfig?.Type == ColumnType.SectionHeader &&
                group.Rows != null && group.Rows.Count > 0 &&
                group.Rows[0].Items != null && group.Rows[0].Items.Count > 0)
            {
                sortField = group.Rows[0].Items[0]?.FieldName;
            }

            col.CanUserSort = !string.IsNullOrEmpty(sortField);
        }

        private void ConfigureGridSorting(List<ColumnConfig> config)
        {
            if (config == null)
            {
                DynamicGrid.CanUserSortColumns = false;
                return;
            }

            bool hasIdColumn = config.Any(c =>
                !string.IsNullOrEmpty(c?.FieldName) &&
                c.FieldName.Equals("Id", StringComparison.OrdinalIgnoreCase));

            if (hasIdColumn)
            {
                System.Diagnostics.Debug.WriteLine("Warning: Template contains Id field configuration - system Id field is always available");
            }

            DynamicGrid.CanUserSortColumns = false;
        }

        private void ClearResources()
        {
            var keysToRemove = DynamicGrid.Resources.Keys.Cast<object>().ToList();
            foreach (var k in keysToRemove) DynamicGrid.Resources.Remove(k);
        }

        #endregion

        #region DataGrid Event Handlers

        private void DynamicGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit) return;

            var rowData = e.Row.Item as BindableRow;
            if (rowData == null) return;

            // Якщо це порожній рядок-заглушка - дозволяємо вийти з редагування без збереження
            // Використовуємо IsRowEmpty, щоб визначити, чи ввів користувач якісь дані
            if (rowData is NewRowPlaceholder placeholder && _viewModel.IsRowEmpty(placeholder))
            {
                // Порожній placeholder - просто виходимо без збереження
                return;
            }

            var errors = _viewModel.ValidateRow(rowData);

            if (errors.Any())
            {
                CancelRowEdit(e, errors);
            }
            else
            {
                // Викликаємо збереження через ViewModel
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    _viewModel.SaveRow(rowData);
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private async void CancelRowEdit(DataGridRowEditEndingEventArgs e, List<string> errors)
        {
            e.Cancel = true;
            string message = "Неможливо зберегти рядок:\n" + string.Join("\n", errors);
            await DialogService.ShowWarningAsync(message, "Помилка валідації");
            Dispatcher.BeginInvoke(new Action(() => e.Row.Focus()));
        }

        private void DynamicGrid_PreparingCellForEdit(object? sender, DataGridPreparingCellForEditEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var editingElement = GetActualEditingElement(e.EditingElement);
                FocusEditingElement(editingElement);
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void DynamicGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            lastClickPosition = e.GetPosition(DynamicGrid);
        }

        private void DynamicGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                HandleEnterKey(e);
            }
            else if (e.Key == Key.Add || (e.Key == Key.OemPlus && Keyboard.Modifiers == ModifierKeys.Shift))
            {
                // Обробка клавіші "+" (як на NumPad, так і Shift + "=")
                AddNewRow();
                e.Handled = true;
            }
        }

        private void DynamicGrid_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var currentCell = DynamicGrid.CurrentCell;

            if (currentCell.Column == null || currentCell.Item == null)
                return;

            if (IsCellInViewMode(currentCell))
            {
                StartEditingWithNewText(e.Text);
                e.Handled = true;
            }
        }

        #region Horizontal Scroll Support

        private void DynamicTableView_Loaded(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window != null)
            {
                var source = PresentationSource.FromVisual(window) as HwndSource;
                source?.AddHook(WndProc);
            }
        }

        private void DynamicTableView_Unloaded(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window != null)
            {
                var source = PresentationSource.FromVisual(window) as HwndSource;
                source?.RemoveHook(WndProc);
            }
        }

        private void DynamicGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Shift && e.Delta != 0)
            {
                var scrollViewer = FindVisualChild<ScrollViewer>(DynamicGrid);
                if (scrollViewer != null)
                {
                    if (e.Delta < 0)
                        scrollViewer.LineRight();
                    else
                        scrollViewer.LineLeft();

                    e.Handled = true;
                }
            }
        }

        private const int WM_MOUSEHWHEEL = 0x020E;

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_MOUSEHWHEEL)
            {
                if (DynamicGrid.IsMouseOver)
                {
                    var scrollViewer = FindVisualChild<ScrollViewer>(DynamicGrid);
                    if (scrollViewer != null)
                    {
                        int tilt = (short)((wParam.ToInt64() >> 16) & 0xFFFF);

                        if (tilt > 0)
                        {
                            scrollViewer.LineRight();
                            scrollViewer.LineRight();
                            scrollViewer.LineRight();
                        }
                        else
                        {
                            scrollViewer.LineLeft();
                            scrollViewer.LineLeft();
                            scrollViewer.LineLeft();
                        }
                        handled = true;
                    }
                }
            }
            return IntPtr.Zero;
        }

        #endregion

        #endregion

        #region TextBox Event Handlers

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && IsDescendantOf(textBox, DynamicGrid))
            {
                SelectAllText(textBox);
            }
        }

        #endregion

        #region Keyboard Navigation

        private void HandleEnterKey(KeyEventArgs e)
        {
            var currentCell = DynamicGrid.CurrentCell;

            if (currentCell.Column == null || currentCell.Item == null)
                return;

            if (IsCellInEditMode(currentCell))
            {
                SimulateTabKey();
                e.Handled = true;
            }
            else
            {
                DynamicGrid.BeginEdit();
                e.Handled = true;
            }
        }

        private void SimulateTabKey()
        {
            var tabEvent = new KeyEventArgs(
                Keyboard.PrimaryDevice,
                PresentationSource.FromVisual(DynamicGrid),
                0,
                Key.Tab)
            {
                RoutedEvent = Keyboard.KeyDownEvent
            };

            InputManager.Current.ProcessInput(tabEvent);
        }

        #endregion

        #region Editing Helpers

        private FrameworkElement? GetActualEditingElement(FrameworkElement editingElement)
        {
            return editingElement is ContentPresenter contentPresenter
                ? FindVisualChild<FrameworkElement>(contentPresenter)
                : editingElement;
        }

        private void FocusEditingElement(FrameworkElement? editingElement)
        {
            switch (editingElement)
            {
                case TextBox textBox:
                    FocusAndSelectTextBox(textBox);
                    break;

                case DatePicker datePicker:
                    datePicker.Focus();
                    break;

                case TimePicker timePicker:
                    FocusTimePicker(timePicker);
                    break;

                case Grid grid:
                    FocusGridElement(grid);
                    break;

                case Control control when control.Focusable:
                    control.Focus();
                    break;
            }
        }

        private void FocusGridElement(Grid grid)
        {
            var targetControl = DetermineTargetControlInGrid(grid, lastClickPosition);

            if (targetControl != null)
            {
                FocusControl(targetControl);
                return;
            }

            var focusableControl = FindVisualChild<Control>(grid);
            focusableControl?.Focus();
        }

        private void FocusControl(Control control)
        {
            control.Focus();

            switch (control)
            {
                case TextBox textBox:
                    textBox.SelectAll();
                    break;

                case ComboBox comboBox:
                    FocusComboBox(comboBox);
                    break;

                case TimePicker timePicker:
                    FocusTimePicker(timePicker);
                    break;
            }
        }

        private void FocusComboBox(ComboBox comboBox)
        {
            if (comboBox.IsEditable)
            {
                var textBox = FindVisualChild<TextBox>(comboBox);
                if (textBox != null)
                {
                    textBox.Focus();
                    textBox.SelectAll();
                }
            }
            else
            {
                comboBox.IsDropDownOpen = true;
            }
        }

        private void FocusTimePicker(TimePicker timePicker)
        {
            timePicker.Focus();
            timePicker.RaiseEvent(new RoutedEventArgs(UIElement.GotFocusEvent));
        }

        private void FocusAndSelectTextBox(TextBox textBox)
        {
            textBox.Focus();
            textBox.SelectAll();
        }

        private void StartEditingWithNewText(string text)
        {
            DynamicGrid.BeginEdit();

            Dispatcher.BeginInvoke(new Action(() =>
            {
                var textBox = GetFocusedTextBox();
                if (textBox != null)
                {
                    textBox.Clear();
                    textBox.Text = text;
                    textBox.SelectionStart = text.Length;
                }
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private TextBox? GetFocusedTextBox()
        {
            var focusedElement = Keyboard.FocusedElement;

            return focusedElement switch
            {
                TextBox textBox => textBox,
                DependencyObject dependencyObject => FindVisualChild<TextBox>(dependencyObject),
                _ => null
            };
        }

        private void SelectAllText(TextBox textBox)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                textBox.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void ScrollToNewRow(BindableRow newRow)
        {
            try
            {
                DynamicGrid.ScrollIntoView(newRow);
                DynamicGrid.SelectedItem = newRow;
                DynamicGrid.CurrentItem = newRow;

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var row = DynamicGrid.ItemContainerGenerator.ContainerFromItem(newRow) as DataGridRow;
                    if (row != null)
                    {
                        row.Focus();

                        if (DynamicGrid.Columns.Count > 0)
                        {
                            DynamicGrid.CurrentCell = new DataGridCellInfo(newRow, DynamicGrid.Columns[0]);
                            DynamicGrid.BeginEdit();
                        }
                    }
                }), System.Windows.Threading.DispatcherPriority.ContextIdle);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error focusing new row: {ex.Message}");
            }
        }

        #endregion

        #region Cell State Checking

        private bool IsCellInEditMode(DataGridCellInfo currentCell)
        {
            var currentRow = DynamicGrid.ItemContainerGenerator.ContainerFromItem(currentCell.Item) as DataGridRow;
            if (currentRow == null) return false;

            var cell = GetCell(DynamicGrid, currentRow, currentCell.Column);
            return cell?.IsEditing == true;
        }

        private bool IsCellInViewMode(DataGridCellInfo currentCell)
        {
            return !IsCellInEditMode(currentCell);
        }

        private DataGridCell? GetCell(DataGrid dataGrid, DataGridRow? row, DataGridColumn column)
        {
            if (row == null) return null;

            int columnIndex = dataGrid.Columns.IndexOf(column);
            if (columnIndex < 0) return null;

            var presenter = FindVisualChild<DataGridCellsPresenter>(row);
            if (presenter == null) return null;

            var cell = presenter.ItemContainerGenerator.ContainerFromIndex(columnIndex) as DataGridCell;
            if (cell == null)
            {
                dataGrid.ScrollIntoView(row, column);
                cell = presenter.ItemContainerGenerator.ContainerFromIndex(columnIndex) as DataGridCell;
            }

            return cell;
        }

        #endregion

        #region Grid Column Helpers

        private Control? DetermineTargetControlInGrid(Grid grid, Point? clickPosition)
        {
            var controls = GetEditableControlsInGrid(grid);

            if (controls.Count == 0) return null;
            if (controls.Count == 1) return controls[0];
            if (clickPosition == null) return controls[0];

            return FindControlAtPosition(controls, clickPosition.Value, grid);
        }

        private List<Control> GetEditableControlsInGrid(Grid grid)
        {
            var controls = new List<Control>();

            controls.AddRange(GetVisualChildren<TextBox>(grid).Where(tb => !tb.IsReadOnly && tb.IsTabStop));
            controls.AddRange(GetVisualChildren<ComboBox>(grid).Where(cb => cb.IsTabStop));
            controls.AddRange(GetVisualChildren<DatePicker>(grid).Where(dp => dp.IsTabStop));
            controls.AddRange(GetVisualChildren<TimePicker>(grid).Where(tp => tp.IsTabStop));
            controls.AddRange(GetVisualChildren<CheckBox>(grid).Where(cb => cb.IsTabStop));

            return controls;
        }

        private Control? FindControlAtPosition(List<Control> controls, Point clickPosition, Grid grid)
        {
            var exactMatch = controls.FirstOrDefault(control =>
                IsPointInControl(clickPosition, control));

            if (exactMatch != null) return exactMatch;

            return FindNearestControl(controls, clickPosition, grid);
        }

        private bool IsPointInControl(Point clickPosition, Control control)
        {
            try
            {
                var relativePoint = DynamicGrid.TranslatePoint(clickPosition, control);
                var bounds = new Rect(0, 0, control.ActualWidth, control.ActualHeight);
                return bounds.Contains(relativePoint);
            }
            catch
            {
                return false;
            }
        }

        private Control? FindNearestControl(List<Control> controls, Point clickPosition, Grid grid)
        {
            var sortedControls = controls
                .Select(c => new { Control = c, Left = GetControlLeft(c, grid) })
                .OrderBy(x => x.Left)
                .ToList();

            if (sortedControls.Count == 0) return null;

            var gridPosition = DynamicGrid.TranslatePoint(clickPosition, grid);

            for (int i = 0; i < sortedControls.Count - 1; i++)
            {
                var current = sortedControls[i];
                var next = sortedControls[i + 1];
                var midPoint = (current.Left + next.Left) / 2;

                if (gridPosition.X < midPoint)
                    return current.Control;
            }

            return sortedControls[^1].Control;
        }

        private double GetControlLeft(Control control, Grid grid)
        {
            try
            {
                var position = control.TranslatePoint(new Point(0, 0), grid);
                return position.X;
            }
            catch
            {
                return 0;
            }
        }

        #endregion

        #region Visual Tree Helpers

        private T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T typedChild)
                    return typedChild;

                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }

            return null;
        }

        private void FindAllVisualChildren<T>(DependencyObject parent, List<T> children) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T typedChild)
                    children.Add(typedChild);

                FindAllVisualChildren(child, children);
            }
        }

        private IEnumerable<T> GetVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            var result = new List<T>();
            FindAllVisualChildren(parent, result);
            return result;
        }

        private bool IsDescendantOf(DependencyObject child, DependencyObject parent)
        {
            var current = child;
            while (current != null)
            {
                if (current == parent) return true;
                current = VisualTreeHelper.GetParent(current);
            }
            return false;
        }

        #endregion
    }
}

