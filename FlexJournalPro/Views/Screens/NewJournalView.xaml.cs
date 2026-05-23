using FlexJournalPro.Helpers;
using FlexJournalPro.ViewModels.Screens;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace FlexJournalPro.Views.Screens
{
    public partial class NewJournalView : UserControl
    {
        public NewJournalView()
        {
            InitializeComponent();

            // Підписуємося на зміну DataContext, щоб зловити момент, коли прив'язується ViewModel
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Відписуємося від старої ViewModel (якщо була)
            if (e.OldValue is INotifyPropertyChanged oldVm)
            {
                oldVm.PropertyChanged -= ViewModel_PropertyChanged;
            }

            // Підписуємося на нову ViewModel
            if (e.NewValue is NewJournalScreen newVm)
            {
                newVm.PropertyChanged += ViewModel_PropertyChanged;
            }
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(NewJournalScreen.CurrentConfiguredTemplate) &&
                DataContext is NewJournalScreen vm)
            {
                BuildAutoFillUI(vm);
            }
        }

        private void BuildAutoFillUI(NewJournalScreen vm)
        {
            if (vm.CurrentConfiguredTemplate == null)
            {
                DynamicAutoFillPanel.Children.Clear();
                return;
            }

            // Викликаємо єдиний хелпер генерації
            AutoFillConfigHelper.BuildAutoFillPanel(
                DynamicAutoFillPanel,
                vm.CurrentConfiguredTemplate.AutoFillConfig,
                vm.AutoFillValues,
                onValueChanged: null
            );
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Здесь указываем порог ширины для перехода в вертикальный режим (например, 600)
            if (e.NewSize.Width < 600)
            {
                // Вертикальное расположение (в одну колонку)
                LeftCol.Width = new GridLength(1, GridUnitType.Star);
                SplitterCol.Width = new GridLength(0);
                RightCol.Width = new GridLength(0);

                // Перемещаем правый контент вниз
                Grid.SetRow(RightContent, 1);
                Grid.SetColumn(RightContent, 0);

                VerticalSplitter.Visibility = Visibility.Collapsed;
                AdaptiveSeparator.Visibility = Visibility.Visible;

                LeftContent.Margin = new Thickness(0);
                RightContent.Margin = new Thickness(0, 16, 0, 0);
            }
            else
            {
                // Горизонтальное расположение (две колонки)
                LeftCol.Width = new GridLength(1, GridUnitType.Star);
                SplitterCol.Width = GridLength.Auto;
                RightCol.Width = new GridLength(1, GridUnitType.Star);

                // Возвращаем контент во вторую колонку
                Grid.SetRow(RightContent, 0);
                Grid.SetColumn(RightContent, 2);

                VerticalSplitter.Visibility = Visibility.Visible;
                AdaptiveSeparator.Visibility = Visibility.Collapsed;

                LeftContent.Margin = new Thickness(0, 0, 16, 0);
                RightContent.Margin = new Thickness(16, 0, 0, 0);
            }
        }
    }
}