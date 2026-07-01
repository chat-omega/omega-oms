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
    public partial class PairTradersControllerViewModel : CustomizableTableViewModelBase
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        private DispatcherTimer _updateTimer = new();


        [Bindable]
        public partial string ModuleTitle { get; set; }

        [Bindable]
        public partial int TotalRunning { get; set; }

        [Bindable]
        public partial int TotalQty { get; set; }

        [Bindable]
        public partial int TotalBuyQty { get; set; }

        [Bindable]
        public partial int TotalSellQty { get; set; }

        [Bindable]
        public partial double RealPnl { get; set; }

        [Bindable]
        public partial double UnrealPnl { get; set; }

        public ModelTradersManagerModel ManagerModel { get; }

        public PairTradersControllerViewModel(ModelTradersManagerModel managerModel)
        {
            _updateTimer.Interval = TimeSpan.FromMilliseconds(750);
            _updateTimer.Tick += OnUpdateTimer_Tick;
            ModuleTitle = "Model Trader Control";
            ManagerModel = managerModel;
        }

        [Command]
        public void AddCommand()
        {
            PairTraderView window = new();
            PairTraderViewModel viewModel = (PairTraderViewModel)window.DataContext;
            viewModel.SetDispatcher(window.Dispatcher);
        }

        [Command]
        public void ActivateWindowCommand(PairTraderViewModel viewModel)
        {
            viewModel.Activate();
        }

        [Command]
        public void HideWindowCommand(PairTraderViewModel viewModel)
        {
            viewModel.Hide();
        }

        [Command]
        public void CloseWindowCommand(PairTraderViewModel viewModel)
        {
            viewModel.Close();
        }

        internal void Dispose()
        {
            _updateTimer.Stop();
        }

        private void OnUpdateTimer_Tick(object sender, EventArgs e)
        {
            TotalRunning = ManagerModel.PairTraderModels.Count(x => x.OrderEnabled);
            TotalQty = ManagerModel.PairTraderModels.Sum(x => x.TotalQty);
            TotalBuyQty = ManagerModel.PairTraderModels.Sum(x => x.TotalBuyQty);
            TotalSellQty = ManagerModel.PairTraderModels.Sum(x => x.TotalSellQty);
            RealPnl = ManagerModel.PairTraderModels.Where(x => !double.IsNaN(x.RealPnl)).Sum(x => x.RealPnl);
            UnrealPnl = ManagerModel.PairTraderModels.Where(x => !double.IsNaN(x.UnrealPnl)).Sum(x => x.UnrealPnl);
        }
    }
}
