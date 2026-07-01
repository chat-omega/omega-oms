using NLog;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Oms.Data.Securities;

namespace ZeroPlus.Oms.Data.Trading
{
    public class PositionModel
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        private readonly ConcurrentDictionary<string, SymbolPosition> _symbolToPositionMap = new();
        private readonly List<SymbolPosition> _symbolPositions = new();
        public string Title { get; set; }
        public int NetQty { get; set; }
        public double UnrealizedPnl { get; set; }
        public double RealizedPnl { get; set; }
        public double AdjustedPnl { get; set; }
        public bool FirstEdgeAcquired { get; private set; }
        public double FirstEdge { get; private set; }
        public string PositionAcquiringSymbol { get; set; }
        public bool PositionAcquiringIsSell { get; set; }
        public double BestSellPrice { get; set; } = double.NaN;
        public double BestSellPriceMid { get; set; } = double.NaN;
        public double BestBuyPrice { get; set; } = double.NaN;
        public double BestBuyPriceMid { get; set; } = double.NaN;
        public double NetDelta { get; set; }

        public PositionModel(string title)
        {
            Title = title;
        }

        public void AddTradeWithPosition(IOmsOrder trade)
        {
            var side = trade.Side is ZeroPlus.Models.Data.Enums.Side.Sell or ZeroPlus.Models.Data.Enums.Side.SellShort
                ? "-"
                : "+";
            if (string.IsNullOrWhiteSpace(PositionAcquiringSymbol))
            {
                PositionAcquiringSymbol = side + trade.Symbol;
                bool isSellOrder = false;
                if (trade.OrderLegs.Count == 0)
                {
                    isSellOrder = trade.Side is ZeroPlus.Models.Data.Enums.Side.Sell or ZeroPlus.Models.Data.Enums.Side.SellShort;
                }
                else if (trade.OrderLegs.Count == 1)
                {
                    isSellOrder = trade.OrderLegs[0].Side == ZeroPlus.Models.Data.Enums.Side.Sell || trade.Side == ZeroPlus.Models.Data.Enums.Side.SellShort;
                }
                else if (trade.OrderLegs.Count > 1)
                {
                    isSellOrder = trade.SpreadType switch
                    {
                        "CALL 1X2" or "CALL 1X3" or "CALL 2X3" or "CALL VERTICAL" or "CALL 1X3X3X1" or "CALL CONDOR" or "PUT CONDOR" or "STRADDLE" or "STRANGLE" => trade.OrderLegs.OrderBy(x => x.Security.Strike).FirstOrDefault()?.Side == ZeroPlus.Models.Data.Enums.Side.Sell,
                        "IRON BUTTERFLY" or "IRON CONDOR" => trade.OrderLegs.OrderBy(x => x.Security.Strike).FirstOrDefault()?.Side == ZeroPlus.Models.Data.Enums.Side.Buy,
                        "PUT 1X2" or "PUT 1X3" or "PUT 2X3" or "PUT VERTICAL" or "PUT 1X3X3X1" => trade.OrderLegs.OrderByDescending(x => x.Security.Strike).FirstOrDefault()?.Side == ZeroPlus.Models.Data.Enums.Side.Sell,
                        "CALL BUTTERFLY" or "PUT BUTTERFLY" or "CALL SKEWED BUTTERFLY" or "PUT SKEWED BUTTERFLY" => trade.OrderLegs.OrderBy(x => x.Ratio).FirstOrDefault()?.Side == ZeroPlus.Models.Data.Enums.Side.Sell,
                        "CALL CALENDAR" or "PUT CALENDAR" or "CALL DIAGONAL" or "PUT DIAGONAL" or "CALL TRIAGONAL" or "PUT TRIAGONAL" => trade.OrderLegs.OrderBy(x => x.Security.Expiration).FirstOrDefault()?.Side == ZeroPlus.Models.Data.Enums.Side.Buy,
                        "CALL CALENDAR FLY" or "PUT CALENDAR FLY" => trade.OrderLegs.OrderBy(x => x.Security.Expiration).FirstOrDefault()?.Side == ZeroPlus.Models.Data.Enums.Side.Buy,
                        "REVERSAL" or "CONVERSION" => trade.OrderLegs.FirstOrDefault(x => x.Security.Type == OptionType.PUT)?.Side == ZeroPlus.Models.Data.Enums.Side.Sell,
                        "INVALID" or "CUSTOM" => false,
                        _ => trade.AveragePrice < 0 || trade.Price < 0 || trade.Side == ZeroPlus.Models.Data.Enums.Side.Sell || trade.Side == ZeroPlus.Models.Data.Enums.Side.SellShort,
                    };
                }
                PositionAcquiringIsSell = isSellOrder;
            }

            bool isSellTrade = false;
            if (side + trade.Symbol == PositionAcquiringSymbol)
            {
                NetQty += PositionAcquiringIsSell ? -1 * trade.LastQuantity : trade.LastQuantity;
                isSellTrade = PositionAcquiringIsSell;
            }
            else
            {
                NetQty += PositionAcquiringIsSell ? trade.LastQuantity : -1 * trade.LastQuantity;
                isSellTrade = !PositionAcquiringIsSell;
            }

            if (isSellTrade)
            {
                if (trade.AveragePrice < BestSellPrice || double.IsNaN(BestSellPrice))
                {
                    BestSellPrice = trade.AveragePrice;
                    BestSellPriceMid = trade.UnderMid;
                }
            }
            else
            {
                if (trade.AveragePrice < BestBuyPrice || double.IsNaN(BestBuyPrice))
                {
                    BestBuyPrice = trade.AveragePrice;
                    BestBuyPriceMid = trade.UnderMid;
                }
            }

            AddTradeToPnlCalculator(trade);
        }

        public void AddTradeToPnlCalculator(IOmsOrder trade)
        {
            if (trade.Side != null)
            {
                if (!_symbolToPositionMap.TryGetValue(trade.Symbol, out SymbolPosition symbolPosition))
                {
                    symbolPosition = new SymbolPosition(trade.Symbol);
                    _symbolToPositionMap[trade.Symbol] = symbolPosition;
                    _symbolPositions.Add(symbolPosition);
                }

                symbolPosition.AddTrade(trade.Side, trade.LastQuantity, trade.TotalDelta, trade.AveragePrice, trade.TotalCommissions);

                symbolPosition.Calculate();
            }
            else
            {
                foreach (IOmsOrderLeg leg in trade.TradedLegs)
                {
                    if (!_symbolToPositionMap.TryGetValue(leg.Symbol, out SymbolPosition symbolPosition))
                    {
                        symbolPosition = new SymbolPosition(leg.Symbol);
                        _symbolToPositionMap[leg.Symbol] = symbolPosition;
                        _symbolPositions.Add(symbolPosition);
                    }

                    symbolPosition.AddTrade(leg.Side, leg.LastQuantity, leg.Delta, leg.AveragePrice, leg.TotalCommissions);

                    symbolPosition.Calculate();
                }
            }

            UpdatePosition();
        }

        private void UpdatePosition()
        {
            double netDelta = 0, realizedPnl = 0, adjustedPnl = 0;
            for (int i = 0; i < _symbolPositions.Count; i++)
            {
                SymbolPosition pos = _symbolPositions[i];
                netDelta += pos.NetDelta;
                realizedPnl += pos.RealizedPnl;
                adjustedPnl += pos.AdjustedPnl;
            }
            NetDelta = netDelta;
            RealizedPnl = realizedPnl;
            double prevPnl = AdjustedPnl;
            AdjustedPnl = adjustedPnl;
            if (AdjustedPnl > prevPnl &&
                AdjustedPnl - prevPnl > 0 &&
                !FirstEdgeAcquired)
            {
                FirstEdgeAcquired = true;
                FirstEdge = AdjustedPnl - prevPnl;
            }
        }
    }
}
