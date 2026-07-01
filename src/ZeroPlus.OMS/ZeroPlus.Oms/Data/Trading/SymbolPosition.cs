using System.Collections.Concurrent;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Data.Securities;
using ZeroPlus.Oms.Helper;

namespace ZeroPlus.Oms.Data.Trading
{
    internal class SymbolPosition
    {
        private readonly ConcurrentQueue<TradeUnit> _sells = new();
        private readonly ConcurrentQueue<TradeUnit> _buys = new();

        public Option Option { get; set; }
        public double RealizedPnl { get; set; }
        public double AdjustedPnl { get; set; }
        public double NetDelta { get; set; }

        public SymbolPosition(string symbol)
        {
            Option = OptionsHelper.GetOptionFromSymbol(symbol);
        }

        internal void AddTrade(Side? side, int qty, double delta, double price, double commissions)
        {
            double totalPrice = price * Option.Multiplier;
            double unitCommissions = commissions / qty;
            bool isSell = side == Side.Sell;
            TradeUnit singleTrade = new()
            {
                Quantity = 1,
                Price = price,
                TotalPrice = totalPrice,
                NetPrice = isSell ? totalPrice - unitCommissions : totalPrice + unitCommissions,
            };

            for (int i = 0; i < qty; i++)
            {
                if (isSell)
                {
                    _sells.Enqueue(singleTrade);
                }
                else
                {
                    _buys.Enqueue(singleTrade);
                }
            }

            if (isSell)
            {
                NetDelta -= delta * qty;
            }
            else
            {
                NetDelta += delta * qty;
            }
        }

        internal void Calculate()
        {
            while (_buys.Count > 0 && _sells.Count > 0)
            {
                if (_sells.TryDequeue(out TradeUnit sell))
                {
                    if (_buys.TryDequeue(out TradeUnit buy))
                    {
                        double tradePnl = sell.TotalPrice - buy.TotalPrice;
                        double netPnl = sell.NetPrice - buy.NetPrice;
                        RealizedPnl += tradePnl;
                        AdjustedPnl += netPnl;
                    }
                    else
                    {
                        _sells.Enqueue(sell);
                    }
                }
            }
        }
    }
}
