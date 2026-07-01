using DevExpress.Xpf.Core;
using DevExpress.Xpf.Editors;
using System.Windows.Input;


namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for LowLatencyManualAdjustmentRequestView.xaml
    /// </summary>
    public partial class LowLatencyManualAdjustmentRequestView : ThemedWindow
    {
        public LowLatencyManualAdjustmentRequestView()
        {
            InitializeComponent();
        }

        private void SelectAll(object sender, MouseButtonEventArgs e)
        {
            if (sender is BaseEdit baseEdit)
            {
                baseEdit.SelectAll();
            }
        }
    }
}
