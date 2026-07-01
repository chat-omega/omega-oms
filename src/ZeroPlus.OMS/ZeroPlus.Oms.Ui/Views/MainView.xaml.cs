using NLog;
using System.Windows;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Views.Interfaces;
using Module = ZeroPlus.Oms.Ui.Models.Module;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainView : IModuleView
    {
        private const string MODULE_NAME = "ZeroPlus OMS";

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private bool _layoutRestored;
        private readonly OmsCore _omsCore;

        public Module Module { get; set; }
        public ConfigSave ConfigSave { get; set; }
        public WindowHelper WindowHelper { get; private set; }

        public MainView(OmsCore omsCore)
        {
            _omsCore = omsCore;
            Module = Module.MainWindowLayout;
            Uid = "d6abdaa9-fb6b-4c28-a14f-d20fdf8eb04a";
            InitializeComponent();
            OrderBookView.Uid = Uid;
            Name = nameof(MainView);
            Loaded += RestoreLayout;
            VersionLabel.Content = omsCore.AppUpdateManager.GetCurrentVersion();
        }

        private void RestoreLayout(object sender, RoutedEventArgs e)
        {
            Loaded -= RestoreLayout;
            WindowHelper = new WindowHelper(this, _omsCore);
            ConfigSave = new ConfigSave()
            {
                Title = MODULE_NAME,
                Module = (int)Module,
                Username = _omsCore.User.Username,
                Group = _omsCore.User.Username,
                OwnerId = _omsCore.User.ID,
            };
            if (!_layoutRestored)
            {
                _layoutRestored = true;
            }
        }
    }
}
