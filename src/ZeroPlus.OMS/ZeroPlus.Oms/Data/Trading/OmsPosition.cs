using SymbolLib;
using System;
using ZeroPlus.Comms.Models.Data.Trading;

namespace ZeroPlus.Oms.Data.Trading
{
    public class OmsPosition
    {
        public double UnrealizedPL { get; set; }
        public double TradingPL { get; set; }
        public int TradingNetQty { get; set; }
        public double TradingAveCost { get; set; }
        public double NotionalValue { get; set; }
        public int NetQty { get; set; }
        public double NetPL { get; set; }
        public double MarketValue { get; set; }
        public double DayPL { get; set; }
        public int TradingSellQty { get; set; }
        public double TradingSellAvePrice { get; set; }
        public DateTime Timestamp { get; set; }
        public int TradingBuyQty { get; set; }
        public string Symbol { get; set; }
        public double RealizedPL { get; set; }
        public int OpeningQty { get; set; }
        public double OpeningCost { get; set; }
        public double MarkedCost { get; set; }
        public DateTime LastUpdateTime { get; set; }
        public int ID { get; set; }
        public double AveCost { get; set; }
        public int AccountID { get; set; }
        public string AccountAcronym { get; set; }
        public double TradingBuyAvePrice { get; set; }
        public Instrument Instrument { get; set; }

        public OmsPosition(string symbol)
        {
            Instrument = new Instrument(symbol);
        }

        internal void Update(OMSSendPosition positionMessage)
        {
            UnrealizedPL = positionMessage.UnrealizedPL;
            TradingPL = positionMessage.TradingPL;
            TradingNetQty = positionMessage.TradingNetQty;
            TradingAveCost = positionMessage.TradingAveCost;
            NotionalValue = positionMessage.NotionalValue;
            NetQty = positionMessage.NetQty;
            NetPL = positionMessage.NetPL;
            MarketValue = positionMessage.MarketValue;
            DayPL = positionMessage.DayPL;
            TradingSellQty = positionMessage.TradingSellQty;
            TradingSellAvePrice = positionMessage.TradingSellAvePrice;
            Timestamp = positionMessage.Timestamp;
            TradingBuyQty = positionMessage.TradingBuyQty;
            Symbol = positionMessage.Symbol;
            RealizedPL = positionMessage.RealizedPL;
            OpeningQty = positionMessage.OpeningQty;
            OpeningCost = positionMessage.OpeningCost;
            MarkedCost = positionMessage.MarkedCost;
            LastUpdateTime = positionMessage.LastUpdateTime;
            ID = positionMessage.ID;
            AveCost = positionMessage.AveCost;
            AccountID = positionMessage.AccountID;
            AccountAcronym = positionMessage.AccountAcronym;
            TradingBuyAvePrice = positionMessage.TradingBuyAvePrice;
        }
    }
}
