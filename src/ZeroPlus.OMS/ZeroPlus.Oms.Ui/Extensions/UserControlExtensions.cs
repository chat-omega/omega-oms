using System.Windows.Controls;
using System.Windows.Input;
using DevExpress.Xpf.Editors;

namespace ZeroPlus.Oms.Ui.Views
{
    public static class UserControlExtensions
    {
        public static void SelectAllE(this UserControl userControl, object sender, MouseButtonEventArgs e)
        {
            if (sender is BaseEdit baseEdit) baseEdit.SelectAll();
        }
    }
}