using DevExpress.Mvvm;
using DevExpress.Xpf.Core;
using System.Windows;

namespace ZeroPlus.Oms.Ui.Services
{

    public class OmsMessageBoxService : DXMessageBoxService, IOmsMessageBoxService
    {
        MessageResult IMessageBoxService.Show(string messageBoxText, string caption, MessageButton button, MessageIcon icon, MessageResult defaultResult)
        {
            return DXMessageBox.Show(AssociatedObject, messageBoxText, caption, button.ToMessageBoxButton(), icon.ToMessageBoxImage(), defaultResult.ToMessageBoxResult(), MessageBoxOptions.None, FloatingMode.Popup).ToMessageResult();
        }
    }
}
