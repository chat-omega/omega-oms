using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class PositionManagerViewModel : ViewModelBase
    {
        private double _UnrealizedPL;

        public ICurrentWindowService CurrentWindowService => GetService<ICurrentWindowService>();
        public OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();
        public Dispatcher Dispatcher { get; set; }
        [Bindable]
        public partial string Symbol { get; set; }
        [Bindable]
        public partial string AccountAcronym { get; set; }
        [Bindable]
        public partial int AccountID { get; set; }
        [Bindable]
        public partial double AvgCost { get; set; }
        [Bindable]
        public partial int Id { get; set; }
        [Bindable]
        public partial DateTime LastUpdateTime { get; set; }
        [Bindable]
        public partial double MarkedCost { get; set; }
        [Bindable]
        public partial double OpeningCost { get; set; }
        [Bindable]
        public partial int OpeningQty { get; set; }
        [Bindable]
        public partial double RealizedPL { get; set; }
        [Bindable]
        public partial double TradingBuyAvePrice { get; set; }
        [Bindable]
        public partial int TradingBuyQty { get; set; }
        [Bindable]
        public partial double TradingSellAvePrice { get; set; }
        [Bindable]
        public partial int TradingSellQty { get; set; }
        [Bindable]
        public partial double DayPL { get; set; }
        [Bindable]
        public partial double MarketValue { get; set; }
        [Bindable]
        public partial double NetPL { get; set; }
        [Bindable]
        public partial int NetQty { get; set; }
        [Bindable]
        public partial int NewNetQty { get; set; }
        [Bindable]
        public partial double NotionalValue { get; set; }
        [Bindable]
        public partial double TradingAveCost { get; set; }
        [Bindable]
        public partial int TradingNetQty { get; set; }
        [Bindable]
        public partial double TradingPL { get; set; }
        public double UnrealizedPL

        {
            get => _UnrealizedPL;
            set => SetValue(ref _UnrealizedPL, value);
        }

        public void SetDispatcher(Dispatcher dispatcher)
        {
            Dispatcher = dispatcher;
        }

        [Command]
        public async Task UpdateCommand()
        {
            await Task.Run(() => OmsCore.OrderClient.SendPositionAdjustmentRequest(symbol: Symbol,
                                                                                   account: AccountAcronym,
                                                                                   netQtyDelta: NewNetQty - NetQty,
                                                                                   openingPrice: OpeningCost));
        }

        [Command]
        public void CancelCommand()
        {
            CurrentWindowService?.Close();
        }

        internal void LoadFromModel(PositionModel position)
        {
            Symbol = position.Symbol;
            UnrealizedPL = position.UnrealizedPL;
            TradingPL = position.TradingPL;
            TradingNetQty = position.TradingNetQty;
            TradingAveCost = position.TradingAveCost;
            NetQty = position.NetQty;
            NewNetQty = position.NetQty;
            NetPL = position.NetPL;
            MarketValue = position.MarketValue;
            NotionalValue = position.NotionalValue;
            OpeningQty = position.OpeningQty;
            DayPL = position.DayPL;
            TradingSellQty = position.TradingSellQty;
            TradingSellAvePrice = position.TradingSellAvePrice;
            TradingBuyQty = position.TradingBuyQty;
            RealizedPL = position.RealizedPL;
            OpeningCost = position.OpeningCost;
            MarkedCost = position.MarkedCost;
            AvgCost = position.AveCost;
            TradingBuyAvePrice = position.TradingBuyAvePrice;
            AccountAcronym = position.Account;
            AccountID = position.AccountID;
            Id = position.ID;
        }
    }
}
