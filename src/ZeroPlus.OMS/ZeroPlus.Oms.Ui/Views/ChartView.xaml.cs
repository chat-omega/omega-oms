using DevExpress.Xpf.Core;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Views.Interfaces;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for ChartView.xaml
    /// </summary>

    public partial class ChartView : ThemedWindow, IModuleView
    {
        public Module Module { get; set; }
        public ConfigSave ConfigSave { get; set; }

        public ChartView()
        {
            InitializeComponent();
        }
    }
}
