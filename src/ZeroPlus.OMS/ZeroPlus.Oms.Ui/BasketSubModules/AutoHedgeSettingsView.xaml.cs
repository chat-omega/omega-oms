using System.Windows.Controls;
using System.Windows.Input;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for SweepTradeSettingsView.xaml
    /// </summary>
    public partial class AutoHedgeSettingsView : UserControl
    {
        public AutoHedgeSettingsView()
        {
            InitializeComponent();
        }
        private void SelectAll(object sender, MouseButtonEventArgs e) => this.SelectAllE(sender, e);
    }
}
