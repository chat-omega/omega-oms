using System;

namespace ZeroPlus.Models.Data.EdgeScanner
{
    public class ReferenceTradeModel
    {
        public double TradeBid { get; set; }
        public double TradeAsk { get; set; }
        public double TradePrice { get; set; }
        public double TradeUnderMid { get; set; }
        public DateTime TradeTime { get; set; }

        public bool TryGetAdjustedAsk(double underMid, double totalDelta, out double refTradeAsk)
        {
            refTradeAsk = double.NaN;
            if (double.IsNaN(TradeAsk))
            {
                return false;
            }
            refTradeAsk = ((underMid - TradeUnderMid) * totalDelta) + TradeAsk;
            return !double.IsNaN(refTradeAsk);
        }

        public bool TryGetAdjustedBid(double underMid, double totalDelta, out double refTradeBid)
        {
            refTradeBid = double.NaN;
            if (double.IsNaN(TradeBid))
            {
                return false;
            }
            refTradeBid = ((underMid - TradeUnderMid) * totalDelta) + TradeBid;
            return !double.IsNaN(refTradeBid);
        }

        public bool TryGetAdjustedTradePrice(double underMid, double totalDelta, out double refTradeTradePx)
        {
            refTradeTradePx = double.NaN;
            if (double.IsNaN(TradePrice))
            {
                return false;
            }
            refTradeTradePx = ((underMid - TradeUnderMid) * totalDelta) + TradePrice;
            return !double.IsNaN(refTradeTradePx);
        }
    }
}