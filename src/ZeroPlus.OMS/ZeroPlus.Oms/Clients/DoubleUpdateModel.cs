using System;

namespace ZeroPlus.Oms.Clients
{
    public class DoubleUpdateModel
    {
        public DateTime Timestamp { get; }
        public double Bid { get; }
        public double Ask { get; }
        public Generated.QuoteChangeType BidChange { get; }
        public Generated.QuoteChangeType AskChange { get; }
        public int BidSize { get; }
        public int AskSize { get; }
        public double LastPrice { get; }
        public double Mid => (Bid + Ask) / 2;

        public DoubleUpdateModel(DateTime timestamp, double bid, double ask, Generated.QuoteChangeType bidChange, Generated.QuoteChangeType askChange, int bidSize, int askSize, double lastPrice)
        {
            Timestamp = timestamp;
            Bid = bid;
            Ask = ask;
            BidChange = bidChange;
            AskChange = askChange;
            BidSize = bidSize;
            AskSize = askSize;
            LastPrice = lastPrice;
        }
    }
}
