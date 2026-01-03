using FlexJournalPro.ViewModels;
using System.Windows;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;

namespace FlexJournalPro
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
            Loaded += MainWindow_Loaded;
            StateChanged += MainWindow_StateChanged;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.SetScreensPanelScrollViewer(ScreensPanelScrollViewer);
            }
        }

        private void MainWindow_StateChanged(object sender, System.EventArgs e)
        {
            UpdateMaximizeRestoreButton();
        }

        private void UpdateMaximizeRestoreButton()
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowStyle = WindowStyle.SingleBorderWindow;
                WindowStyle = WindowStyle.None;

                MaximizeIcon.Kind = PackIconKind.WindowRestore;
                MaximizeButton.ToolTip = "Відновити";
            }
            else
            {
                MaximizeIcon.Kind = PackIconKind.WindowMaximize;
                MaximizeButton.ToolTip = "Розгорнути";
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized 
                ? WindowState.Normal 
                : WindowState.Maximized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}