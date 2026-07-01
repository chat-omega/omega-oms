using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Editors;


namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for SyntheticSpreadConfigView.xaml
    /// </summary>
    public partial class SyntheticSpreadConfigView : ThemedWindow
    {
        public SyntheticSpreadConfigView()
        {
            InitializeComponent();
        }

        private void EditBox_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is BaseEdit baseEdit)
            {
                baseEdit.SelectAll();
            }
        }
    }
}
