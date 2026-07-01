using NLog;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace ZeroPlus.Oms.Data.Trading
{
    public class PositionModel
    {

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        private ConcurrentDictionary<string, SymbolPosition> _symbolToPositionMap = new ConcurrentDictionary<string, SymbolPosition>();
        private readonly List<SymbolPosition> _symbolPositions = new();
        public string Title { get; set; }
        public int NetQty { get; set; }
        public double RealizedPnl { get; set; }
        public double AdjustedPnl { get; set; }
        public bool FirstEdgeAcquired { get; private set; }

        public PositionModel(string spreadId)
        {
            Title = spreadId;
        }

        public void AddTrade(ITrade trade)
        {
            if (trade.AveragePrice < 0 || trade.Price < 0)
            {
                NetQty -= trade.LastQuantity;
            }
            else
            {
                NetQty += trade.LastQuantity;
            }

            foreach (ILegTrade leg in trade.TradedLegs)
            {
                if (!_symbolToPositionMap.TryGetValue(leg.Symbol, out var symbolPosition))
                {
                    symbolPosition = new SymbolPosition(leg.Symbol);
                    _symbolToPositionMap[leg.Symbol] = symbolPosition;
                    _symbolPositions.Add(symbolPosition);
                }

                symbolPosition.AddTrade(leg.Side, leg.LastQuantity, leg.AveragePrice, leg.TotalCommissions);

                symbolPosition.Calculate();
            }

            UpdatePosition();
        }

        private void UpdatePosition()
        {
            double realizedPnl = 0, adjustedPnl = 0;
            for (int i = 0; i < _symbolPositions.Count; i++)
            {
                SymbolPosition pos = _symbolPositions[i];
                realizedPnl += pos.RealizedPnl;
                adjustedPnl += pos.AdjustedPnl;
            }
            RealizedPnl = realizedPnl;
            var prevPnl = AdjustedPnl;
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
