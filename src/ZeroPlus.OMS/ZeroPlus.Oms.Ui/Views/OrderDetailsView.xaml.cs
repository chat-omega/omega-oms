using DevExpress.Xpf.Core;
using System.Windows.Input;


namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for OrderDetailsView.xaml
    /// </summary>
    public partial class OrderDetailsView : ThemedWindow
    {
        public OrderDetailsView()
        {
            InitializeComponent();
        }

        private void OnZoomMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) ||
                Keyboard.IsKeyDown(Key.RightCtrl))
            {
                GridScale.Value += (e.Delta > 0) ? 0.1 : -0.1;
            }
        }
    }
}
