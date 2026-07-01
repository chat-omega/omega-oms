using DevExpress.Xpf.Core;
using DevExpress.Xpf.Editors;
using System;
using System.Windows.Input;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for DynamicIntervalConfigView.xaml
    /// </summary>
    public partial class DynamicIntervalConfigView : ThemedWindow
    {
        public DynamicIntervalConfigView()
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
            catch (Exception)
            {
            }
        }
    }
}
