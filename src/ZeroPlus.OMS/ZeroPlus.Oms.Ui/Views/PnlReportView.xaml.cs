using DevExpress.Xpf.Core;
using NLog;
using System;
using System.ComponentModel;
using System.Windows;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ViewModels;
using ZeroPlus.Oms.Ui.Views.Interfaces;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for PnlReportView.xaml
    /// </summary>
    public partial class PnlReportView : ThemedWindow, IModuleView
    {
        private const string MODULE_NAME = "PnL Report";

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private bool _layoutRestored;

        public Module Module { get; set; }
        public ConfigSave ConfigSave { get; set; }
        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        public PnlReportView() : this(Guid.NewGuid().ToString())
        {
        }

        public PnlReportView(string uid)
        {
            Uid = uid;
            InitializeComponent();
            Name = nameof(PnlReportView);
            Closing += Window_Closing;
            Loaded += RestoreLayout;
            Module = Module.PnlReportLayout;
            ConfigSave = new ConfigSave()
            {
                Title = MODULE_NAME,
                Module = (int)Module,
                Username = OmsCore.User.Username,
                Group = OmsCore.User.Username,
                OwnerId = OmsCore.User.ID,
            };
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            PnlReportViewModel dataContext = (PnlReportViewModel)DataContext;
            dataContext.Dispose();
        }

        private void RestoreLayout(object sender, RoutedEventArgs e)
        {
            StartupWindowViewModel.MainWindow.WindowHelper.AddWindow(this);
            PnlReportViewModel dataContext = (PnlReportViewModel)DataContext;
            dataContext.Uid = Uid;
            if (!_layoutRestored)
            {
                _layoutRestored = true;
                _ = dataContext.LoadViewModelConfigAsync(Uid);
            }
        }
    }
}
