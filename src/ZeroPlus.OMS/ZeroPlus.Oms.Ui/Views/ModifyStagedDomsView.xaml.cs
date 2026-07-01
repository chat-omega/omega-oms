using DevExpress.Xpf.Core;
using DevExpress.Xpf.Editors;
using System.Windows.Input;


namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for ModifyStagedDomsView.xaml
    /// </summary>
    public partial class ModifyStagedDomsView : ThemedWindow
    {
        public ModifyStagedDomsView()
        {
            InitializeComponent();
        }

        private void PriceSpinEdit_MouseUp(object sender, MouseButtonEventArgs e)
        {
            SpinEdit spinEdit = sender as SpinEdit;
            spinEdit?.SelectAll();
        }
    }
}
