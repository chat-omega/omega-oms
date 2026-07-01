using DevExpress.Mvvm;
using System;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class TronTradeModel : BindableBase
    {
        public DateTime _SendTime;
        public int _ErrorCode;
        public string _ErrorMessage;
        public string _Symbol;
        public double _LastPrice;
        public int _LastQty;
        public string _LastExch;
        public string _TradeCond0;
        public string _TradeCond1;
        public string _TradeCond2;
        public string _TradeCond3;
        public double _High;
        public double _Low;
        public double _Open;
        public double _Close;
        public double _PrevClose;
        public int _Volume;
        public DateTime _TradeTime;
        public int _OpenInterest;
        public bool _HasAMatch;

        [Bindable]
        public partial DateTime SendTime { get; set; }

        [Bindable]
        public partial int ErrorCode { get; set; }

        [Bindable]
        public partial string ErrorMessage { get; set; }

        [Bindable]
        public partial string Symbol { get; set; }

        [Bindable]
        public partial double LastPrice { get; set; }

        [Bindable]
        public partial int LastQty { get; set; }

        [Bindable]
        public partial string LastExch { get; set; }

        [Bindable]
        public partial string TradeCond0 { get; set; }

        [Bindable]
        public partial string TradeCond1 { get; set; }

        [Bindable]
        public partial string TradeCond2 { get; set; }

        [Bindable]
        public partial string TradeCond3 { get; set; }

        [Bindable]
        public partial double High { get; set; }

        [Bindable]
        public partial double Low { get; set; }

        [Bindable]
        public partial double Open { get; set; }

        [Bindable]
        public partial double Close { get; set; }

        [Bindable]
        public partial double PrevClose { get; set; }

        [Bindable]
        public partial int Volume { get; set; }

        [Bindable]
        public partial DateTime TradeTime { get; set; }

        [Bindable]
        public partial int OpenInterest { get; set; }

        [Bindable]
        public partial bool HasAMatch { get; set; }

        public TronTradeModel()
        {
            HasAMatch = false;
        }

        public TronTradeModel(Comms.Models.Data.MarketData.MDSendDmitryTrade trade)
        {
            SendTime = trade.SendTime;
            ErrorCode = trade.ErrorCode;
            ErrorMessage = trade.ErrorMessage;
            Symbol = trade.Symbol;
            LastPrice = trade.LastPrice;
            LastQty = trade.LastQty;
            LastExch = trade.LastExch;
            TradeCond0 = trade.TradeCond0;
            TradeCond1 = trade.TradeCond1;
            TradeCond2 = trade.TradeCond2;
            TradeCond3 = trade.TradeCond3;
            High = trade.High;
            Low = trade.Low;
            Open = trade.Open;
            Close = trade.Close;
            PrevClose = trade.PrevClose;
            Volume = trade.Volume;
            TradeTime = trade.TradeTime;
            OpenInterest = trade.OpenInterest;
            HasAMatch = false;
        }
    }
}
