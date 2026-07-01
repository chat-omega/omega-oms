using DevExpress.Xpf.Core;
using DevExpress.Xpf.Editors;
using System.Windows.Input;


namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for EditHeatmapAlertView.xaml
    /// </summary>
    public partial class EditHeatmapAlertView : ThemedWindow
    {
        public EditHeatmapAlertView()
        {
            InitializeComponent();
        }

        private void SpinEdit_MouseUp(object sender, MouseButtonEventArgs e)
        {
            SpinEdit spinEdit = sender as SpinEdit;
            spinEdit?.SelectAll();
        }
    }
}
