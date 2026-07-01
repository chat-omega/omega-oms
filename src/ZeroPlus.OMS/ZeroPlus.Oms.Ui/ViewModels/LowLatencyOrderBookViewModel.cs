using System.Linq;
using DevExpress.Mvvm.DataAnnotations;
using ZeroPlus.Oms.Ui.Collections;
using ZeroPlus.Oms.Ui.LowLatency;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class LowLatencyOrderBookViewModel : ModuleViewModelBase
    {
        private readonly LowLatencyTransactionsProcessor _latencyTransactionsProcessor;


        public override Module Module { get; protected set; } = Module.LowLatencyOrderBook;
        [Bindable]
        public partial bool ShowWorkingOrders { get; set; }
        [Bindable]
        public partial LowLatencyOrderModel LastOrder { get; set; }
        [Bindable]
        public partial LowLatencyOrderModel LastWorkingOrder { get; set; }
        [Bindable]
        public partial FastObservableCollection<LowLatencyOrderModel> WorkingOrders { get; set; }
        [Bindable]
        public partial FastObservableCollection<LowLatencyOrderModel> Orders { get; set; }
        [Bindable]
        public partial FastObservableCollection<LowLatencyInstanceModel> Instances { get; set; }
        [Bindable]
        public partial LowLatencyInstanceModel SelectedInstance { get; set; }

        public LowLatencyOrderBookViewModel(ConfigBrowserViewModel configBrowserViewModel, OmsCore omsCore, LowLatencyTransactionsProcessor latencyTransactionsProcessor) : base(configBrowserViewModel, omsCore)
        {
            _latencyTransactionsProcessor = latencyTransactionsProcessor;
            ShowWorkingOrders = true;
            Instances = _latencyTransactionsProcessor.LatencyInstanceModels;
            SelectedInstance = Instances.FirstOrDefault();
            SelectionChanged();
        }

        [Command]
        public void SelectionChanged()
        {
            WorkingOrders = SelectedInstance?.WorkingOrders;
            Orders = SelectedInstance?.Orders;
        }

        [Command]
        public void ClearCommand()
        {
            _latencyTransactionsProcessor.ClearTransactions();
        }

        public override System.Threading.Tasks.Task DeserializeAndLoadConfig(string configJson, bool withContent = true)
        {
            return null;
        }

        public override string GetConfigSerialized(bool withContent = false, bool layoutOnly = false)
        {
            return "";
        }
    }
}
