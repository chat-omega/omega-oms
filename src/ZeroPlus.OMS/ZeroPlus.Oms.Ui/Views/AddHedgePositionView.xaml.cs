using DevExpress.Xpf.Core;
using System.Windows.Input;


namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for AddHedgePositionView.xaml
    /// </summary>
    public partial class AddHedgePositionView : ThemedWindow
    {
        public AddHedgePositionView()
        {
            InitializeComponent();
        }

        private void OnPositionZoomMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) ||
                Keyboard.IsKeyDown(Key.RightCtrl))
            {
                PositionsGridScale.Value += (e.Delta > 0) ? 0.1 : -0.1;
            }
        }
    }
}
