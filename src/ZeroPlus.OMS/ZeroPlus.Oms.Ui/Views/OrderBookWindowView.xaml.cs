using DevExpress.Xpf.Core;
using NLog;
using System;
using System.Windows;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ViewModels;
using ZeroPlus.Oms.Ui.Views.Interfaces;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for OrderBookWindowView.xaml
    /// </summary>
    public partial class OrderBookWindowView : ThemedWindow, IModuleView
    {
        private const string MODULE_NAME = "Orderbook";

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private bool _layoutRestored;

        public Module Module { get; set; }
        public ConfigSave ConfigSave { get; set; }
        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        public OrderBookWindowView() : this(Guid.NewGuid().ToString())
        {
        }

        public OrderBookWindowView(string uid)
        {
            Uid = uid;
            InitializeComponent();
            Name = nameof(OrderBookWindowView);
            OrderBookView.Uid = Uid;
            Loaded += RestoreLayout;
            Closing += OrderBookWindowView_Closing;
            Module = Module.OrderBookLayout;
            ConfigSave = new ConfigSave()
            {
                Title = MODULE_NAME,
                Module = (int)Module,
                Username = OmsCore.User.Username,
                Group = OmsCore.User.Username,
                OwnerId = OmsCore.User.ID,
            };
        }

        private void OrderBookWindowView_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            OrderBookViewModel dataContext = (OrderBookViewModel)DataContext;
            dataContext.IsDisposed = true;
        }

        private void RestoreLayout(object sender, RoutedEventArgs e)
        {
            StartupWindowViewModel.MainWindow.WindowHelper.AddWindow(this);
            if (!_layoutRestored)
            {
                _layoutRestored = true;
            }
        }

        internal void CloneFrom(string uid)
        {
            OrderBookViewModel dataContext = (OrderBookViewModel)DataContext;
            dataContext.ParentUid = uid;

            OrderBookView.RestoreLayout(uid);

            StartupWindowViewModel.MainWindow.WindowHelper.RestoreLayout(this, uid);
        }
    }
}
