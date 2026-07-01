using System.Windows.Controls;
using System.Windows.Input;

namespace ZeroPlus.Oms.Ui.Views
{
    public partial class AutoCancelSettingsView : UserControl
    {
        public AutoCancelSettingsView()
        {
            InitializeComponent();
        }
        private void SelectAll(object sender, MouseButtonEventArgs e) => this.SelectAllE(sender, e);
    }
}