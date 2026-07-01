using DevExpress.Xpf.Core;
using System.Windows;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for AddColumnView.xaml
    /// </summary>
    public partial class AddColumnView : ThemedWindow
    {
        public AddColumnView()
        {
            InitializeComponent();
        }

        private void SimpleButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
