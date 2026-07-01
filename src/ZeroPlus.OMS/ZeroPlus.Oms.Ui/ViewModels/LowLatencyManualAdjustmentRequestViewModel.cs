using DevExpress.Mvvm;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using DevExpress.Mvvm.DataAnnotations;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Ui.LowLatency;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class LowLatencyManualAdjustmentRequestViewModel : ViewModelBase
    {
        private readonly LowLatencyTransactionsProcessor _latencyTransactionsProcessor;

        public IEnumerable<Side> Sides { get; } = new List<Side> { ZeroPlus.Models.Data.Enums.Side.Buy, ZeroPlus.Models.Data.Enums.Side.Sell };

        [Bindable]
        public partial string Username { get; set; }
        [Bindable]
        public partial string Symbol { get; set; }
        [Bindable]
        public partial double FillPrice { get; set; }
        [Bindable]
        public partial int FillQty { get; set; }
        [Bindable(Default = ZeroPlus.Models.Data.Enums.Side.Buy)]
        public partial Side FillSide { get; set; }
        [Bindable]
        public partial string FillClOrderId { get; set; }
        [Bindable]
        public partial bool ByPassRisk { get; set; }
        [Bindable]
        public partial bool DoNothing { get; set; }
        [Bindable]
        public partial List<LowLatencyModel> Modules { get; set; }
        [Bindable]
        public partial ObservableCollection<string> Usernames { get; set; }
        [Bindable]
        public partial ObservableCollection<string> Symbols { get; set; }

        public LowLatencyManualAdjustmentRequestViewModel(LowLatencyTransactionsProcessor latencyTransactionsProcessor)
        {
            _latencyTransactionsProcessor = latencyTransactionsProcessor;
            Symbols = new ObservableCollection<string>();
        }

        [Command]
        public void UsernameChangedCommand()
        {
            Symbols.Clear();
            var instance = Modules.FirstOrDefault(x => x.Username == Username);
            if (instance != null)
            {
                var symbols = _latencyTransactionsProcessor.GetHangs(instance.LatencyInstance);
                if (symbols != null)
                {
                    foreach (var symbol in symbols)
                    {
                        Symbols.Add(symbol.Symbol);
                    }
                }
            }
        }

        [Command]
        public void SaveCommand()
        {
            var instance = Modules.FirstOrDefault(x => x.Username == Username);
            instance?.LatencyInstance?.RequestManualAdj(Username, Symbol, FillPrice, FillQty, FillSide, FillClOrderId, "", ByPassRisk, DoNothing);
        }
    }
}
