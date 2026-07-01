using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Mvvm.Native;
using NLog;
using System;
using System.Linq;
using System.Windows.Threading;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class MlTradersControlViewModel : CustomizableTableViewModelBase
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        private DispatcherTimer _updateTimer = new();


        [Bindable]
        public partial string ModuleTitle { get; set; }

        [Bindable]
        public partial int TotalQty { get; set; }

        [Bindable]
        public partial double RealPnl { get; set; }

        [Bindable]
        public partial double UnrealPnl { get; set; }

        public ModelTradersManagerModel ManagerModel { get; }

        public MlTradersControlViewModel(ModelTradersManagerModel managerModel)
        {
            _updateTimer.Interval = TimeSpan.FromMilliseconds(750);
            _updateTimer.Tick += OnUpdateTimer_Tick;
            ModuleTitle = "Model Trader Control";
            ManagerModel = managerModel;
        }

        [Command]
        public void AddCommand()
        {
            ModelTraderView window = new();
            ModelTraderViewModel viewModel = (ModelTraderViewModel)window.DataContext;
            viewModel.SetDispatcher(window.Dispatcher);
        }

        [Command]
        public void ActivateWindowCommand(ModelTraderViewModel viewModel)
        {
            viewModel.Activate();
        }

        [Command]
        public void HideWindowCommand(ModelTraderViewModel viewModel)
        {
            viewModel.Hide();
        }

        [Command]
        public void CloseWindowCommand(ModelTraderViewModel viewModel)
        {
            viewModel.Close();
        }

        internal void Dispose()
        {
            _updateTimer.Stop();
        }

        private void OnUpdateTimer_Tick(object sender, EventArgs e)
        {
            TotalQty = ManagerModel.TraderModels.Sum(x => x.TotalQty);
            RealPnl = ManagerModel.TraderModels.Where(x => !double.IsNaN(x.RealPnl)).Sum(x => x.RealPnl);
            UnrealPnl = ManagerModel.TraderModels.Where(x => !double.IsNaN(x.UnrealPnl)).Sum(x => x.UnrealPnl);
        }
    }
}
