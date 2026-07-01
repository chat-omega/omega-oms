using DevExpress.Xpf.Core;
using DevExpress.Xpf.Editors;
using System.Windows.Input;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Views.Interfaces;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for ModifyStagedPxQtyView.xaml
    /// </summary>
    public partial class ModifyStagedPxQtyView : ThemedWindow, IModuleView
    {
        public Module Module { get; set; }
        public ConfigSave ConfigSave { get; set; }

        public ModifyStagedPxQtyView()
        {
            InitializeComponent();
        }

        private void PriceSpinEdit_MouseUp(object sender, MouseButtonEventArgs e)
        {
            SpinEdit spinEdit = sender as SpinEdit;
            spinEdit.SelectAll();
        }
    }
}
