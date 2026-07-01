using DevExpress.Xpf.Core;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ViewModels;
using ZeroPlus.Oms.Ui.Views.Interfaces;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for ConfigBrowserView.xaml
    /// </summary>
    public partial class ConfigBrowserWindowView : ThemedWindow, IModuleView
    {
        public Module Module { get; set; }
        public ConfigSave ConfigSave { get; set; }

        public ConfigBrowserWindowView()
        {
            InitializeComponent();
            Loaded += ConfigBrowserView_Loaded;
        }

        private void ConfigBrowserView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            ConfigBrowserViewModel configBrowserViewModel = (ConfigBrowserViewModel)DataContext;
            configBrowserViewModel.SetDispatcher(Dispatcher);
        }
    }
}
