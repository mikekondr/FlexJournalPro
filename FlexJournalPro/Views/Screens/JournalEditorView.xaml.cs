using FlexJournalPro.Helpers;
using FlexJournalPro.Services;
using FlexJournalPro.ViewModels.Screens;
using System.Windows;
using System.Windows.Controls;

namespace FlexJournalPro.Views.Screens
{
    /// <summary>
    /// Interaction logic for JournalEditorView.xaml
    /// </summary>
    public partial class JournalEditorView : UserControl
    {
        public JournalEditorView()
        {
            InitializeComponent();
            
            SmartTable.Loaded += JournalEditorView_Loaded;
            SmartTable.RowSaved += SmartTable_RowSaved;
        }

        private void JournalEditorView_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is JournalEditorScreen viewModel)
            {
                LoadData(viewModel);
            }
        }

        private void LoadData(JournalEditorScreen viewModel)
        {
            if (viewModel.Template == null) return;

            // Завантажуємо шаблон
            SmartTable.LoadTemplate(viewModel.Template);

            // Будуємо панель параметрів заповнення
            BuildAutoFillPanel(viewModel);

            // Застосовуємо значення параметрів
            SmartTable.ApplyAutoFillValues(viewModel.AutoFillValues);

            // Завантажуємо дані
            SmartTable.SetVirtualDataSource(
                viewModel.DatabaseService,
                viewModel.Journal.TableName,
                viewModel.Journal.NumberStart,
                viewModel.IsReadOnly);
        }

        private void BuildAutoFillPanel(JournalEditorScreen viewModel)
        {
            if (viewModel.Template?.AutoFillConfig == null || viewModel.Template.AutoFillConfig.Count == 0)
            {
                // Якщо немає параметрів заповнення - вимикаємо виїзну панель та ховаємо кнопку
                MainDrawerHost.RightDrawerContent = null;
                
                // УВАГА: вам потрібно додати x:Name="BtnOpenConstants" до кнопки в Toolbar у файлі XAML
                BtnOpenConstants.Visibility = Visibility.Collapsed; 
                return;
            }

            // Якщо параметри є, гарантуємо що кнопка видима
            BtnOpenConstants.Visibility = Visibility.Visible;

            AutoFillConfigHelper.BuildAutoFillPanel(
                ConstantsPanel,
                viewModel.Template.AutoFillConfig,
                viewModel.AutoFillValues,
                () => SmartTable.ApplyAutoFillValues(viewModel.AutoFillValues));
        }

        private void BtnAddRow_Click(object sender, RoutedEventArgs e)
        {
            SmartTable.AddNewRow();
        }

        private void SmartTable_RowSaved(object sender, RowSavedEventArgs e)
        {
            if (DataContext is JournalEditorScreen viewModel)
            {
                if (viewModel.SaveRowCommand.CanExecute(e.RowData))
                {
                    viewModel.SaveRowCommand.Execute(e.RowData);
                }
            }
        }
    }
}
