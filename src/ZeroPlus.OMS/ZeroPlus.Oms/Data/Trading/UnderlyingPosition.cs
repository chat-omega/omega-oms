using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace ZeroPlus.Oms.Data.Trading
{
    public class UnderlyingPosition
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        private readonly ConcurrentDictionary<string, SymbolPosition> _symbolToPositionMap = new();
        private readonly List<SymbolPosition> _symbolPositions = new();
        private readonly ConcurrentDictionary<DateTime, HashSet<SymbolPosition>> _expirationToSymbolsMap = new();
        public string Underlying { get; set; }
        public int NetQty { get; set; }
        public double RealizedPnl { get; set; }
        public double AdjustedPnl { get; set; }
        public string PositionAcquiringSymbol { get; set; }
        public int PositionAcquiringSymbolMultiplier { get; set; }

        public UnderlyingPosition(string spreadId)
        {
            Underlying = spreadId;
        }

        public void AddOrder(OmsOrder order)
        {
            if (string.IsNullOrWhiteSpace(PositionAcquiringSymbol))
            {
                PositionAcquiringSymbol = order.Symbol;
                PositionAcquiringSymbolMultiplier = order.AveragePrice < 0 || order.Price < 0 ? -1 : 1;
            }

            if (order.Symbol == PositionAcquiringSymbol)
            {
                NetQty += PositionAcquiringSymbolMultiplier * order.LastQuantity;
            }
            else
            {
                NetQty += -1 * PositionAcquiringSymbolMultiplier * order.LastQuantity;
            }

            foreach (IOmsOrderLeg leg in order.TradedLegs)
            {
                if (!_symbolToPositionMap.TryGetValue(leg.Symbol, out SymbolPosition symbolPosition))
                {
                    symbolPosition = new SymbolPosition(leg.Symbol);
                    _symbolToPositionMap[leg.Symbol] = symbolPosition;
                    _symbolPositions.Add(symbolPosition);
                }

                if (!_expirationToSymbolsMap.TryGetValue(symbolPosition.Option.Expiration, out HashSet<SymbolPosition> symbolsList))
                {
                    symbolsList = new HashSet<SymbolPosition>();
                    _expirationToSymbolsMap[symbolPosition.Option.Expiration] = symbolsList;
                }

                symbolsList.Add(symbolPosition);

                symbolPosition.AddTrade(leg.Side, leg.LastQuantity, leg.Delta, leg.AveragePrice, leg.TotalCommissions);

                symbolPosition.Calculate();
            }

            UpdatePosition();
        }

        internal double GetExpirationPnlFor(DateTime expiration)
        {
            if (!_expirationToSymbolsMap.TryGetValue(expiration, out HashSet<SymbolPosition> symbolsList))
            {
                symbolsList = new HashSet<SymbolPosition>();
                _expirationToSymbolsMap[expiration] = symbolsList;
            }

            double total = 0;
            foreach (SymbolPosition pos in symbolsList)
            {
                total += pos.AdjustedPnl;
            }
            return total;
        }

        internal List<SymbolPosition> GetSymbolPositions()
        {
            return _symbolPositions;
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
            AdjustedPnl = adjustedPnl;
        }
    }
}
