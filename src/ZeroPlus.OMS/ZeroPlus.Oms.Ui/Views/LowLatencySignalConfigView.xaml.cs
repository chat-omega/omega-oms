using DevExpress.Mvvm;
using DevExpress.Xpf.Editors;
using System.Windows.Controls;
using System.Windows.Input;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for LowLatencySignalConfigView.xaml
    /// </summary>
    public partial class LowLatencySignalConfigView : UserControl
    {
        public LowLatencySignalConfigView()
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

        private void InitialOrderQtyChanging(object sender, EditValueChangingEventArgs eventArgs)
        {
            if (DataContext is SignalModel viewModel && eventArgs.OldValue is int oldVal && eventArgs.NewValue is int newVal && oldVal < newVal)
            {
                if (viewModel.MessageBoxService.ShowMessage("Are you sure you want to raise the Initial Order Quantity?", viewModel.Title, MessageButton.YesNo, MessageIcon.Warning, MessageResult.No) !=
                    MessageResult.Yes)
                {
                    if (sender is BaseEdit baseEdit)
                    {
                        baseEdit.EditValue = oldVal;
                    }
                    eventArgs.IsCancel = true;
                }
            }
        }
    }
}
