using DevExpress.Xpf.Editors;
using System.Windows;
using System.Windows.Controls;

namespace ZeroPlus.Oms.Ui.Controls
{
    /// <summary>
    /// Interaction logic for AdminConfigControl.xaml
    /// </summary>
    public partial class AdminConfigControl : UserControl
    {
        public static readonly RoutedEvent ConfigSelectionChangedEvent = EventManager.RegisterRoutedEvent(
            "ConfigSelectionChanged", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(AdminConfigControl));

        public static readonly RoutedEvent ReloadConfigsSelectedEvent = EventManager.RegisterRoutedEvent(
            "ReloadConfigsSelected", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(AdminConfigControl));

        public event RoutedEventHandler ConfigSelectionChanged
        {
            add { AddHandler(ConfigSelectionChangedEvent, value); }
            remove { RemoveHandler(ConfigSelectionChangedEvent, value); }
        }

        public event RoutedEventHandler ReloadConfigsSelected
        {
            add { AddHandler(ReloadConfigsSelectedEvent, value); }
            remove { RemoveHandler(ReloadConfigsSelectedEvent, value); }
        }

        public AdminConfigControl()
        {
            InitializeComponent();
        }

        private void SimpleButton_Click(object sender, RoutedEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(ReloadConfigsSelectedEvent));
        }

        private void ComboBoxEdit_PopupClosed(object sender, ClosePopupEventArgs e)
        {
            if (e.CloseMode == PopupCloseMode.Normal)
            {
                RaiseEvent(new RoutedEventArgs(ConfigSelectionChangedEvent));
            }
        }
    }
}
