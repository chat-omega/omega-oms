using System;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Data.Models
{
    public class PriceCache
    {
        public DateTime LowestBidTimestamp { get; set; }
        public double LowestBid { get; set; } = double.NaN;
        public double LowestBidUnderlying { get; set; } = double.NaN;
        public DateTime HighestBidTimestamp { get; set; }
        public double HighestBid { get; set; } = double.NaN;
        public double HighestBidUnderlying { get; set; } = double.NaN;
        public DateTime LowestAskTimestamp { get; set; }
        public double LowestAsk { get; set; } = double.NaN;
        public double LowestAskUnderlying { get; set; } = double.NaN;
        public DateTime HighestAskTimestamp { get; set; }
        public double HighestAsk { get; set; } = double.NaN;
        public double HighestAskUnderlying { get; set; } = double.NaN;
        public DateTime LastBuyAttempt { get; set; }
        public DateTime LastSellAttempt { get; set; }
        public DateTime LastLoserTime { get; set; }

        public double GetAdjustedLowestBid(double underlying, double delta)
        {
            return ((underlying - LowestBidUnderlying) * delta) + LowestBid;
        }

        public double GetAdjustedHighestBid(double underlying, double delta)
        {
            return ((underlying - HighestBidUnderlying) * delta) + HighestBid;
        }

        public double GetAdjustedLowestAsk(double underlying, double delta)
        {
            return ((underlying - LowestAskUnderlying) * delta) + LowestAsk;
        }

        public double GetAdjustedHighestAsk(double underlying, double delta)
        {
            return ((underlying - HighestAskUnderlying) * delta) + HighestAsk;
        }

        public void UpdateCache(Side side, double price, double underlyingPrice)
        {
            if (double.IsNaN(price))
            {
                return;
            }
            price = Math.Abs(price);
            switch (side)
            {
                case Side.Buy:
                    if (double.IsNaN(LowestBid) || price < LowestBid)
                    {
                        LowestBid = price;
                        LowestBidUnderlying = underlyingPrice;
                        LowestBidTimestamp = DateTime.Now;
                    }
                    if (double.IsNaN(HighestBid) || price > HighestBid)
                    {
                        HighestBid = price;
                        HighestBidUnderlying = underlyingPrice;
                        HighestBidTimestamp = DateTime.Now;
                    }
                    break;
                case Side.Sell:
                    if (double.IsNaN(LowestAsk) || price < LowestAsk)
                    {
                        LowestAsk = price;
                        LowestAskUnderlying = underlyingPrice;
                        LowestAskTimestamp = DateTime.Now;
                    }
                    if (double.IsNaN(HighestAsk) || price > HighestAsk)
                    {
                        HighestAsk = price;
                        HighestAskUnderlying = underlyingPrice;
                        HighestAskTimestamp = DateTime.Now;
                    }
                    break;
            }
        }

        public void UpdateLastAttempt(Side side, DateTime lastTimeUpdated)
        {
            switch (side)
            {
                case Side.Buy:
                    LastBuyAttempt = lastTimeUpdated;
                    break;
                case Side.Sell:
                    LastSellAttempt = lastTimeUpdated;
                    break;
            }
        }

        public void UpdateLastLoser(DateTime lastTimeUpdated)
        {
            LastLoserTime = lastTimeUpdated;
        }
    }
}
