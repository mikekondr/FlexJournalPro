using FlexJournalPro.Services;
using System.Windows;
using System.Windows.Controls;

namespace FlexJournalPro.Views
{
    /// <summary>
    /// Interaction logic for DialogView.xaml
    /// </summary>
    public partial class DialogView : UserControl
    {
        public DialogView()
        {
            InitializeComponent();
            DataContextChanged += DialogView_DataContextChanged;
        }

        private void DialogView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is DialogViewModel viewModel)
            {
                ConfigureButtons(viewModel.Buttons);
            }
        }

        private void ConfigureButtons(DialogButtons buttons)
        {
            // Сховати всі кнопки за замовчуванням
            BtnOK.Visibility = Visibility.Collapsed;
            BtnCancel.Visibility = Visibility.Collapsed;
            BtnYes.Visibility = Visibility.Collapsed;
            BtnNo.Visibility = Visibility.Collapsed;

            // Показати потрібні кнопки
            switch (buttons)
            {
                case DialogButtons.OK:
                    BtnOK.Visibility = Visibility.Visible;
                    break;

                case DialogButtons.OKCancel:
                    BtnOK.Visibility = Visibility.Visible;
                    BtnCancel.Visibility = Visibility.Visible;
                    break;

                case DialogButtons.YesNo:
                    BtnYes.Visibility = Visibility.Visible;
                    BtnNo.Visibility = Visibility.Visible;
                    break;

                case DialogButtons.YesNoCancel:
                    BtnYes.Visibility = Visibility.Visible;
                    BtnNo.Visibility = Visibility.Visible;
                    BtnCancel.Visibility = Visibility.Visible;
                    break;
            }

            // Встановити фокус на першу видиму кнопку
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                if (BtnYes.Visibility == Visibility.Visible)
                    BtnYes.Focus();
                else if (BtnOK.Visibility == Visibility.Visible)
                    BtnOK.Focus();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }
}
