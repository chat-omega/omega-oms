using System;

namespace ZeroPlus.Models.Data.Models
{
    public class BarModel
    {
        public int SymbolId { get; set; }
        public string? Symbol { get; set; }
        public DateTime Timestamp { get; set; }
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }

        public BarModel()
        {

        }

        public BarModel(int symbolId, string symbol, DateTime timestamp, float open, float high, float low, float close)
        {
            SymbolId = symbolId;
            Symbol = symbol;
            Timestamp = timestamp;
            Open = open;
            High = high;
            Low = low;
            Close = close;
        }

        public BarModel(int symbolId, string symbol, DateTime timestamp, double open, double high, double low, double close)
        {
            SymbolId = symbolId;
            Symbol = symbol;
            Timestamp = timestamp;
            Open = open;
            High = high;
            Low = low;
            Close = close;
        }
    }
}
