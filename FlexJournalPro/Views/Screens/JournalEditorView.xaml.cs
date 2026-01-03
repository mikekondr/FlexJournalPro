using FlexJournalPro.Controls;
using FlexJournalPro.Helpers;
using FlexJournalPro.ViewModels.Screens;
using System;
using System.Text.Json;
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
            Loaded += JournalEditorView_Loaded;
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

            // Будуємо панель констант
            BuildConstantsPanel(viewModel);

            // Застосовуємо сеансові значення
            SmartTable.ApplySessionValues(viewModel.SessionValues);

            // Завантажуємо дані
            SmartTable.SetVirtualDataSource(
                new Services.DatabaseService(), 
                viewModel.Journal.TableName);
        }

        private void BuildConstantsPanel(JournalEditorScreen viewModel)
        {
            if (viewModel.Template?.Constants == null || viewModel.Template.Constants.Count == 0)
            {
                ConstantsColumn.Width = new GridLength(0);
                ToggleConstantsButton.Visibility = Visibility.Collapsed;
                return;
            }

            SessionConstantsHelper.BuildConstantsPanel(
                ConstantsPanel,
                viewModel.Template.Constants,
                viewModel.SessionValues,
                () => SmartTable.ApplySessionValues(viewModel.SessionValues));
        }

        private void ToggleConstantsButton_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (ToggleConstantsButton.IsChecked == true)
            {
                ConstantsColumn.Width = new GridLength(300);
                ConstantsCard.Visibility = Visibility.Visible;
            }
            else
            {
                ConstantsColumn.Width = new GridLength(0);
                ConstantsCard.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnSaveConstants_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not JournalEditorScreen viewModel) return;

            try
            {
                string json = JsonSerializer.Serialize(viewModel.SessionValues);
                var dbService = new Services.DatabaseService();
                dbService.UpdateJournalConstants(viewModel.Journal.Id, json);

                MessageBox.Show("Налаштування збережено!", "Успіх",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка збереження налаштувань: {ex.Message}",
                    "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SmartTable_RowSaved(object sender, RowSavedEventArgs e)
        {
            if (DataContext is not JournalEditorScreen viewModel) return;

            try
            {
                var dbService = new Services.DatabaseService();
                dbService.UpsertDictionaryRow(
                    viewModel.Journal.TableName,
                    e.RowData,
                    viewModel.Template!.Columns);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка збереження: {ex.Message}",
                    "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
