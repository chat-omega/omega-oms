using DevExpress.Xpf.Core;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Views.Interfaces;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for ReleaseNotesView.xaml
    /// </summary>
    public partial class ReleaseNotesView : ThemedWindow, IModuleView
    {
        Module IModuleView.Module { get; set; }
        ConfigSave IModuleView.ConfigSave { get; set; }

        public ReleaseNotesView()
        {
            InitializeComponent();
        }
    }
}
