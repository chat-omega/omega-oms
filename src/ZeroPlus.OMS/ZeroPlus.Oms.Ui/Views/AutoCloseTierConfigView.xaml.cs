using DevExpress.Xpf.Core;
using DevExpress.Xpf.Editors;
using System;
using System.Windows;
using System.Windows.Input;


namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for AutoCloseTierConfigView.xaml
    /// </summary>
    public partial class AutoCloseTierConfigView : ThemedWindow
    {
        public AutoCloseTierConfigView()
        {
            InitializeComponent();
        }

        private void SelectAll(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (sender is BaseEdit baseEdit)
                {
                    baseEdit.SelectAll();
                }
            }
            catch (Exception) { }
        }

        private void CloseWindow(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
